using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Skriptec.Core.Models;

namespace Skriptec.Core.Storage;

/// <summary>JSON súbor má inú verziu schémy, než aplikácia pozná.</summary>
public sealed class UnsupportedFormatVersionException : Exception
{
    public UnsupportedFormatVersionException(string path, string? found)
        : base($"Súbor '{path}' má verziu formátu '{found}', ktorú táto verzia Skriptec nepodporuje (podporovaná: '{JsonStore.CurrentVersion}'). Aktualizuj aplikáciu.")
    {
        Path = path;
        FoundVersion = found;
    }

    public string Path { get; }
    public string? FoundVersion { get; }
}

/// <summary>JSON súbor sa nepodarilo prečítať (poškodený obsah).</summary>
public sealed class CorruptJsonException : Exception
{
    public CorruptJsonException(string path, Exception inner)
        : base($"Súbor '{path}' je poškodený alebo má neplatný JSON: {inner.Message}", inner) { }
}

/// <summary>
/// Čítanie/zápis JSON súborov: camelCase, WriteIndented, enumy ako lowercase string.
/// Zápis je atomický (.tmp → File.Replace so zálohou .bak, pri neexistujúcom cieli File.Move).
/// Zápisy sú serializované v rámci procesu (SemaphoreSlim) aj medzi procesmi (lock súbor),
/// pretože Runner a aplikácia môžu zapisovať do runs/ súčasne.
/// </summary>
public static class JsonStore
{
    public const string CurrentVersion = "1";

    private const int IoRetries = 3;
    private const int LockAttempts = 100;
    private static readonly TimeSpan LockDelay = TimeSpan.FromMilliseconds(50);

    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates =
        new(StringComparer.OrdinalIgnoreCase);

    public static JsonSerializerOptions Options { get; } = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        };
        options.Converters.Add(new JsonStringEnumConverter(new LowerCaseNamingPolicy()));
        return options;
    }

    private sealed class LowerCaseNamingPolicy : JsonNamingPolicy
    {
        public override string ConvertName(string name) => name.ToLowerInvariant();
    }

    /// <summary>Načíta súbor; ak neexistuje, vráti null. Skontroluje pole "version".</summary>
    public static async Task<T?> ReadAsync<T>(string path, CancellationToken ct = default)
        where T : class, IVersioned
    {
        if (!File.Exists(path))
            return null;

        var json = await RetryIoAsync(() => File.ReadAllTextAsync(path, Encoding.UTF8, ct), ct);

        T? value;
        try
        {
            value = JsonSerializer.Deserialize<T>(json, Options);
        }
        catch (JsonException ex)
        {
            throw new CorruptJsonException(path, ex);
        }

        if (value is null)
            throw new CorruptJsonException(path, new JsonException("Súbor je prázdny."));
        if (value.Version != CurrentVersion)
            throw new UnsupportedFormatVersionException(path, value.Version);

        return value;
    }

    /// <summary>Atomický zápis.</summary>
    public static async Task WriteAtomicAsync<T>(string path, T value, CancellationToken ct = default)
    {
        await using var _ = await AcquireAsync(path, ct);
        await WriteCoreAsync(path, value, ct);
    }

    /// <summary>
    /// Read-modify-write pod zámkom (bezpečné pre súbežné zápisy, napr. append do runs/).
    /// Ak súbor neexistuje, začne sa z <paramref name="create"/>.
    /// </summary>
    public static async Task UpdateAsync<T>(string path, Func<T> create, Action<T> mutate,
        CancellationToken ct = default) where T : class, IVersioned
    {
        await using var _ = await AcquireAsync(path, ct);
        var value = await ReadAsync<T>(path, ct) ?? create();
        mutate(value);
        await WriteCoreAsync(path, value, ct);
    }

    private static async Task WriteCoreAsync<T>(string path, T value, CancellationToken ct)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(value, Options);
        var tmp = path + ".tmp";

        await RetryIoAsync(async () =>
        {
            await File.WriteAllTextAsync(tmp, json, new UTF8Encoding(false), ct);
            if (File.Exists(path))
                File.Replace(tmp, path, path + ".bak");
            else
                File.Move(tmp, path);
            return true;
        }, ct);
    }

    private static async Task<T> RetryIoAsync<T>(Func<Task<T>> operation, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException && attempt < IoRetries)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50 * attempt), ct);
            }
        }
    }

    private static async Task<IAsyncDisposable> AcquireAsync(string path, CancellationToken ct)
    {
        var full = Path.GetFullPath(path);
        var gate = Gates.GetOrAdd(full, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            var lockPath = full + ".lock";

            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    var stream = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite,
                        FileShare.None, 1, FileOptions.DeleteOnClose | FileOptions.Asynchronous);
                    return new Releaser(gate, stream);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException && attempt < LockAttempts)
                {
                    await Task.Delay(LockDelay, ct);
                }
            }
        }
        catch
        {
            gate.Release();
            throw;
        }
    }

    private sealed class Releaser(SemaphoreSlim gate, FileStream lockFile) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                await lockFile.DisposeAsync();
            }
            finally
            {
                gate.Release();
            }
        }
    }
}
