using Skriptec.Core.Models;
using Skriptec.Core.Storage;

namespace Skriptec.Core.Services;

/// <summary>Správa %AppData%\Skriptec\config.json (globálne nastavenia + zoznam workspaces).</summary>
public sealed class ConfigService
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public ConfigService(string? configPath = null)
    {
        ConfigPath = configPath ?? DefaultConfigPath;
    }

    public static string AppDataDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Skriptec");

    public static string DefaultConfigPath => Path.Combine(AppDataDirectory, "config.json");

    public string ConfigPath { get; }

    public AppConfig Config { get; private set; } = new();

    /// <summary>Aktívny workspace (alebo null, ak žiadny nie je).</summary>
    public WorkspaceRef? ActiveWorkspace => Config.Workspaces.FirstOrDefault(w => w.IsActive);

    /// <summary>Načíta konfiguráciu; ak súbor neexistuje, použije predvolenú (nezapisuje ju).</summary>
    public async Task<AppConfig> LoadAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            Config = await JsonStore.ReadAsync<AppConfig>(ConfigPath, ct) ?? new AppConfig();
            return Config;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            await JsonStore.WriteAtomicAsync(ConfigPath, Config, ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Pridá workspace a nastaví ho ako aktívny. Ak už existuje workspace s rovnakou cestou,
    /// len ho aktivuje.
    /// </summary>
    public Task<WorkspaceRef> AddWorkspaceAsync(string scriptsRootPath, string? name = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(scriptsRootPath))
            throw new ArgumentException("Cesta k workspace je povinná.", nameof(scriptsRootPath));

        var full = Path.GetFullPath(scriptsRootPath);

        return MutateAsync(() =>
        {
            var existing = Config.Workspaces.FirstOrDefault(w =>
                string.Equals(Path.GetFullPath(w.ScriptsRootPath), full, StringComparison.OrdinalIgnoreCase));

            var workspace = existing ?? new WorkspaceRef
            {
                Name = string.IsNullOrWhiteSpace(name) ? DefaultName(full) : name.Trim(),
                ScriptsRootPath = full,
            };
            if (existing is null)
                Config.Workspaces.Add(workspace);

            SetActive(workspace);
            return workspace;
        }, ct);
    }

    /// <summary>Odstráni workspace zo zoznamu (súbory na disku ostanú). Ak bol aktívny, aktivuje prvý zostávajúci.</summary>
    public Task RemoveWorkspaceAsync(string workspaceId, CancellationToken ct = default) =>
        MutateAsync(() =>
        {
            var workspace = Find(workspaceId);
            Config.Workspaces.Remove(workspace);
            if (workspace.IsActive && Config.Workspaces.Count > 0)
                SetActive(Config.Workspaces[0]);
            return true;
        }, ct);

    public Task ActivateWorkspaceAsync(string workspaceId, CancellationToken ct = default) =>
        MutateAsync(() =>
        {
            SetActive(Find(workspaceId));
            return true;
        }, ct);

    private WorkspaceRef Find(string workspaceId) =>
        Config.Workspaces.FirstOrDefault(w => w.Id == workspaceId)
        ?? throw new KeyNotFoundException($"Workspace '{workspaceId}' neexistuje.");

    private void SetActive(WorkspaceRef active)
    {
        foreach (var w in Config.Workspaces)
            w.IsActive = ReferenceEquals(w, active);
    }

    private static string DefaultName(string fullPath)
    {
        var name = Path.GetFileName(fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return string.IsNullOrEmpty(name) ? fullPath : name;
    }

    private async Task<T> MutateAsync<T>(Func<T> mutation, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var result = mutation();
            await JsonStore.WriteAtomicAsync(ConfigPath, Config, ct);
            return result;
        }
        finally
        {
            _gate.Release();
        }
    }
}
