using System.Globalization;
using Skriptec.Core.Models;
using Skriptec.Core.Storage;

namespace Skriptec.Core.Services;

/// <summary>
/// Správa jedného workspace: &lt;root&gt;\.skriptec\{workspace.json, schedules.json, runs\YYYY-MM.json}.
/// Hierarchia (Notebook → Section → Script) je len v JSON – fyzické súbory sa pri reorganizácii nepresúvajú.
/// </summary>
public sealed class WorkspaceService
{
    public const string MetaDirectoryName = ".skriptec";

    private readonly SemaphoreSlim _gate = new(1, 1);

    private WorkspaceService(string rootPath)
    {
        RootPath = Path.GetFullPath(rootPath);
    }

    public string RootPath { get; }
    public string MetaPath => Path.Combine(RootPath, MetaDirectoryName);
    public string WorkspaceFilePath => Path.Combine(MetaPath, "workspace.json");
    public string SchedulesFilePath => Path.Combine(MetaPath, "schedules.json");
    public string RunsPath => Path.Combine(MetaPath, "runs");

    public WorkspaceData Data { get; private set; } = new();
    public ScheduleFile Schedules { get; private set; } = new();

    // ───────────────────────── otvorenie / načítanie / uloženie ─────────────────────────

    /// <summary>
    /// Otvorí workspace; ak neexistuje, vytvorí .skriptec\, runs\, prázdny workspace.json a schedules.json.
    /// </summary>
    public static async Task<WorkspaceService> OpenOrCreateAsync(string path, string? name = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Cesta k workspace je povinná.", nameof(path));

        var service = new WorkspaceService(path);
        Directory.CreateDirectory(service.RunsPath);

        if (File.Exists(service.WorkspaceFilePath))
        {
            await service.LoadAsync(ct);
        }
        else
        {
            service.Data = new WorkspaceData
            {
                Name = string.IsNullOrWhiteSpace(name) ? DirectoryName(service.RootPath) : name.Trim(),
            };
            await JsonStore.WriteAtomicAsync(service.WorkspaceFilePath, service.Data, ct);
        }

        if (File.Exists(service.SchedulesFilePath))
        {
            await service.LoadSchedulesAsync(ct);
        }
        else
        {
            service.Schedules = new ScheduleFile();
            await JsonStore.WriteAtomicAsync(service.SchedulesFilePath, service.Schedules, ct);
        }

        return service;
    }

    public async Task<WorkspaceData> LoadAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            Data = await JsonStore.ReadAsync<WorkspaceData>(WorkspaceFilePath, ct)
                   ?? throw new FileNotFoundException($"Súbor workspace.json neexistuje.", WorkspaceFilePath);
            return Data;
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
            await JsonStore.WriteAtomicAsync(WorkspaceFilePath, Data, ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ScheduleFile> LoadSchedulesAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            Schedules = await JsonStore.ReadAsync<ScheduleFile>(SchedulesFilePath, ct) ?? new ScheduleFile();
            return Schedules;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveSchedulesAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            await JsonStore.WriteAtomicAsync(SchedulesFilePath, Schedules, ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    // ───────────────────────── histórie behov ─────────────────────────

    /// <summary>Pripojí beh do runs\YYYY-MM.json podľa mesiaca <see cref="Run.StartedAt"/>.</summary>
    public async Task AddRunAsync(Run run, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(run);
        if (string.IsNullOrWhiteSpace(run.ScriptId))
            throw new ArgumentException("Run musí mať ScriptId.", nameof(run));

        Directory.CreateDirectory(RunsPath);
        await JsonStore.UpdateAsync(RunFilePath(run.StartedAt), () => new RunFile(), file => file.Runs.Add(run), ct);
    }

    /// <summary>Posledných <paramref name="take"/> behov skriptu, od najnovšieho.</summary>
    public async Task<IReadOnlyList<Run>> GetRunsAsync(string scriptId, int take = 10,
        CancellationToken ct = default)
    {
        var result = new List<Run>();
        if (take <= 0 || !Directory.Exists(RunsPath))
            return result;

        var files = Directory.EnumerateFiles(RunsPath, "????-??.json")
            .OrderByDescending(f => Path.GetFileName(f), StringComparer.Ordinal);

        foreach (var file in files)
        {
            var runFile = await JsonStore.ReadAsync<RunFile>(file, ct);
            if (runFile is null)
                continue;

            result.AddRange(runFile.Runs
                .Where(r => r.ScriptId == scriptId)
                .OrderByDescending(r => r.StartedAt));

            // Mesiace idú od najnovšieho – ďalšie (staršie) súbory už nemôžu priniesť novšie behy.
            if (result.Count >= take)
                break;
        }

        return result.Take(take).ToList();
    }

    private string RunFilePath(DateTime startedAt) =>
        Path.Combine(RunsPath, startedAt.ToString("yyyy-MM", CultureInfo.InvariantCulture) + ".json");

    // ───────────────────────── Notebook ─────────────────────────

    public Task<Notebook> AddNotebookAsync(string name, string? description = null, CancellationToken ct = default)
    {
        RequireName(name);
        return MutateAsync(() =>
        {
            var notebook = new Notebook
            {
                Name = name.Trim(),
                Description = description ?? "",
                SortOrder = NextSortOrder(Data.Notebooks.Select(n => n.SortOrder)),
            };
            Data.Notebooks.Add(notebook);
            return Task.FromResult(notebook);
        }, ct);
    }

    public Task RenameNotebookAsync(string notebookId, string newName, CancellationToken ct = default)
    {
        RequireName(newName);
        return MutateAsync(() =>
        {
            var notebook = GetNotebook(notebookId);
            notebook.Name = newName.Trim();
            notebook.UpdatedAt = DateTime.Now;
            return Task.FromResult(true);
        }, ct);
    }

    /// <summary>Zmaže zošit z hierarchie (fyzické súbory skriptov ostávajú na disku).</summary>
    public Task DeleteNotebookAsync(string notebookId, CancellationToken ct = default) =>
        MutateAsync(() =>
        {
            Data.Notebooks.Remove(GetNotebook(notebookId));
            return Task.FromResult(true);
        }, ct);

    // ───────────────────────── Section ─────────────────────────

    public Task<Section> AddSectionAsync(string notebookId, string name, CancellationToken ct = default)
    {
        RequireName(name);
        return MutateAsync(() =>
        {
            var notebook = GetNotebook(notebookId);
            var section = new Section
            {
                Name = name.Trim(),
                SortOrder = NextSortOrder(notebook.Sections.Select(s => s.SortOrder)),
            };
            notebook.Sections.Add(section);
            notebook.UpdatedAt = DateTime.Now;
            return Task.FromResult(section);
        }, ct);
    }

    public Task RenameSectionAsync(string sectionId, string newName, CancellationToken ct = default)
    {
        RequireName(newName);
        return MutateAsync(() =>
        {
            var (_, section) = GetSection(sectionId);
            section.Name = newName.Trim();
            section.UpdatedAt = DateTime.Now;
            return Task.FromResult(true);
        }, ct);
    }

    /// <summary>Zmaže sekciu z hierarchie (fyzické súbory skriptov ostávajú na disku).</summary>
    public Task DeleteSectionAsync(string sectionId, CancellationToken ct = default) =>
        MutateAsync(() =>
        {
            var (notebook, section) = GetSection(sectionId);
            notebook.Sections.Remove(section);
            notebook.UpdatedAt = DateTime.Now;
            return Task.FromResult(true);
        }, ct);

    // ───────────────────────── Script ─────────────────────────

    /// <summary>
    /// Pridá skript do sekcie. Ak súbor na <paramref name="relativePath"/> neexistuje, vytvorí ho
    /// (s <paramref name="initialContent"/>); existujúci súbor sa nikdy neprepíše.
    /// Bez <paramref name="relativePath"/> sa použije "Zošit/Sekcia/názov.prípona".
    /// </summary>
    public Task<Script> AddScriptAsync(string sectionId, string name, ScriptLanguage language,
        string? relativePath = null, string? initialContent = null, CancellationToken ct = default)
    {
        RequireName(name);
        return MutateAsync(async () =>
        {
            var (notebook, section) = GetSection(sectionId);

            var relative = NormalizeRelativePath(relativePath ??
                $"{SanitizeSegment(notebook.Name)}/{SanitizeSegment(section.Name)}/" +
                $"{SanitizeSegment(name.Trim())}{LanguageInfo.DefaultExtension(language)}");

            if (EnumerateScripts().Any(s => string.Equals(s.RelativePath, relative, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"Skript so súborom '{relative}' už vo workspace existuje.");

            var absolute = ResolvePath(relative);
            if (!File.Exists(absolute))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
                await File.WriteAllTextAsync(absolute, initialContent ?? "", ct);
            }

            var script = new Script
            {
                Name = name.Trim(),
                Language = language,
                RelativePath = relative,
                SortOrder = NextSortOrder(section.Scripts.Select(s => s.SortOrder)),
            };
            section.Scripts.Add(script);
            section.UpdatedAt = DateTime.Now;
            return script;
        }, ct);
    }

    /// <summary>Zmení metadáta skriptu (názov, tagy, popis, jazyk…); <c>UpdatedAt</c> sa nastaví automaticky.</summary>
    public Task UpdateScriptAsync(string scriptId, Action<Script> update, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(update);
        return MutateAsync(() =>
        {
            var script = FindScript(scriptId) ?? throw ScriptNotFound(scriptId);
            update(script);
            script.UpdatedAt = DateTime.Now;
            return Task.FromResult(true);
        }, ct);
    }

    /// <summary>Zmaže skript z hierarchie; fyzický súbor len ak <paramref name="deleteFile"/>.</summary>
    public Task DeleteScriptAsync(string scriptId, bool deleteFile = false, CancellationToken ct = default) =>
        MutateAsync(() =>
        {
            foreach (var section in Data.Notebooks.SelectMany(n => n.Sections))
            {
                var script = section.Scripts.FirstOrDefault(s => s.Id == scriptId);
                if (script is null)
                    continue;

                section.Scripts.Remove(script);
                section.UpdatedAt = DateTime.Now;
                if (deleteFile)
                {
                    var path = GetAbsolutePath(script);
                    if (File.Exists(path))
                        File.Delete(path);
                }
                return Task.FromResult(true);
            }
            throw ScriptNotFound(scriptId);
        }, ct);

    public Script? FindScript(string scriptId) => EnumerateScripts().FirstOrDefault(s => s.Id == scriptId);

    public string GetAbsolutePath(Script script)
    {
        ArgumentNullException.ThrowIfNull(script);
        return ResolvePath(script.RelativePath);
    }

    // ───────────────────────── interné pomocníčky ─────────────────────────

    private async Task<T> MutateAsync<T>(Func<Task<T>> mutation, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var result = await mutation();
            await JsonStore.WriteAtomicAsync(WorkspaceFilePath, Data, ct);
            return result;
        }
        finally
        {
            _gate.Release();
        }
    }

    private IEnumerable<Script> EnumerateScripts() =>
        Data.Notebooks.SelectMany(n => n.Sections).SelectMany(s => s.Scripts);

    private Notebook GetNotebook(string id) =>
        Data.Notebooks.FirstOrDefault(n => n.Id == id)
        ?? throw new KeyNotFoundException($"Zošit '{id}' neexistuje.");

    private (Notebook Notebook, Section Section) GetSection(string id)
    {
        foreach (var notebook in Data.Notebooks)
        {
            var section = notebook.Sections.FirstOrDefault(s => s.Id == id);
            if (section is not null)
                return (notebook, section);
        }
        throw new KeyNotFoundException($"Sekcia '{id}' neexistuje.");
    }

    private static KeyNotFoundException ScriptNotFound(string id) => new($"Skript '{id}' neexistuje.");

    private static int NextSortOrder(IEnumerable<int> existing) =>
        existing.DefaultIfEmpty(-1).Max() + 1;

    private static void RequireName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Názov je povinný.", nameof(name));
    }

    private static string DirectoryName(string fullPath)
    {
        var name = Path.GetFileName(fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return string.IsNullOrEmpty(name) ? fullPath : name;
    }

    private static string SanitizeSegment(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(value.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim().TrimEnd('.');
        return cleaned.Length == 0 ? "_" : cleaned;
    }

    private static string NormalizeRelativePath(string path) => path.Replace('\\', '/').TrimStart('/');

    /// <summary>Relatívna cesta → absolútna; cesta nesmie vyjsť mimo koreňa workspace.</summary>
    private string ResolvePath(string relativePath)
    {
        var full = Path.GetFullPath(Path.Combine(RootPath, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var rootWithSeparator = RootPath.EndsWith(Path.DirectorySeparatorChar) ? RootPath : RootPath + Path.DirectorySeparatorChar;
        if (!full.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Cesta '{relativePath}' smeruje mimo workspace.", nameof(relativePath));
        return full;
    }
}
