using Skriptec.Core.Models;
using Skriptec.Core.Services;
using Skriptec.Core.Storage;

namespace Skriptec.Core.Tests;

public class WorkspaceServiceTests
{
    [Fact]
    public async Task OpenOrCreate_CreatesMetaStructure()
    {
        using var tmp = new TempDirectory();
        var root = tmp.Combine("ws");

        var ws = await WorkspaceService.OpenOrCreateAsync(root, "Môj workspace");

        Assert.True(Directory.Exists(Path.Combine(root, ".skriptec", "runs")));
        Assert.True(File.Exists(ws.WorkspaceFilePath));
        Assert.True(File.Exists(ws.SchedulesFilePath));
        Assert.Equal("Môj workspace", ws.Data.Name);
        Assert.Empty(ws.Data.Notebooks);
        Assert.Empty(ws.Schedules.Schedules);
    }

    [Fact]
    public async Task OpenOrCreate_ExistingWorkspace_IsNotOverwritten()
    {
        using var tmp = new TempDirectory();
        var first = await WorkspaceService.OpenOrCreateAsync(tmp.Path, "A");
        await first.AddNotebookAsync("DevOps");

        var second = await WorkspaceService.OpenOrCreateAsync(tmp.Path, "B");

        Assert.Equal("A", second.Data.Name);
        Assert.Single(second.Data.Notebooks);
    }

    [Fact]
    public async Task RoundTrip_PreservesHierarchyAndMetadata()
    {
        using var tmp = new TempDirectory();
        var ws = await WorkspaceService.OpenOrCreateAsync(tmp.Path);
        var nb = await ws.AddNotebookAsync("DevOps", "popis");
        var sec = await ws.AddSectionAsync(nb.Id, "Deployment");
        var script = await ws.AddScriptAsync(sec.Id, "deploy-prod", ScriptLanguage.PowerShell, initialContent: "Write-Host hi");
        await ws.UpdateScriptAsync(script.Id, s =>
        {
            s.Tags.AddRange(["deploy", "prod"]);
            s.Description = "Produkčný deploy";
            s.DeployedVersion = 1;
        });

        var reopened = await WorkspaceService.OpenOrCreateAsync(tmp.Path);

        var loadedNb = Assert.Single(reopened.Data.Notebooks);
        Assert.Equal(nb.Id, loadedNb.Id);
        Assert.Equal(nb.SyncId, loadedNb.SyncId);
        Assert.Equal("popis", loadedNb.Description);
        var loaded = Assert.Single(Assert.Single(loadedNb.Sections).Scripts);
        Assert.Equal(script.Id, loaded.Id);
        Assert.StartsWith("s_", loaded.Id);
        Assert.Equal(ScriptLanguage.PowerShell, loaded.Language);
        Assert.Equal("DevOps/Deployment/deploy-prod.ps1", loaded.RelativePath);
        Assert.Equal(["deploy", "prod"], loaded.Tags);
        Assert.Equal(1, loaded.CurrentVersion);
        Assert.Equal(1, loaded.DeployedVersion);
        Assert.Equal(script.CreatedAt, loaded.CreatedAt);
    }

    [Fact]
    public async Task Json_UsesCamelCaseAndLowercaseEnums()
    {
        using var tmp = new TempDirectory();
        var ws = await WorkspaceService.OpenOrCreateAsync(tmp.Path);
        var nb = await ws.AddNotebookAsync("A");
        var sec = await ws.AddSectionAsync(nb.Id, "B");
        await ws.AddScriptAsync(sec.Id, "x", ScriptLanguage.PowerShell);

        var json = await File.ReadAllTextAsync(ws.WorkspaceFilePath);

        Assert.Contains("\"relativePath\"", json);
        Assert.Contains("\"language\": \"powershell\"", json);
        Assert.Contains("\"syncId\"", json);
        Assert.Contains("\"version\": \"1\"", json);
    }

    [Fact]
    public async Task AtomicWrite_SecondWriteCreatesBak_AndLeavesNoTmp()
    {
        using var tmp = new TempDirectory();
        var ws = await WorkspaceService.OpenOrCreateAsync(tmp.Path);

        await ws.AddNotebookAsync("Prvý");
        await ws.AddNotebookAsync("Druhý");

        Assert.True(File.Exists(ws.WorkspaceFilePath + ".bak"));
        Assert.False(File.Exists(ws.WorkspaceFilePath + ".tmp"));
        // .bak obsahuje predchádzajúci stav (jeden zošit)
        var bak = await JsonStore.ReadAsync<WorkspaceData>(ws.WorkspaceFilePath + ".bak");
        Assert.Single(bak!.Notebooks);
    }

    [Fact]
    public async Task AddRun_GoesToFileOfItsMonth_AndGetRunsFiltersAndOrders()
    {
        using var tmp = new TempDirectory();
        var ws = await WorkspaceService.OpenOrCreateAsync(tmp.Path);

        await ws.AddRunAsync(new Run { ScriptId = "s_a", StartedAt = new DateTime(2026, 5, 31, 23, 59, 0), ExitCode = 0 });
        await ws.AddRunAsync(new Run { ScriptId = "s_a", StartedAt = new DateTime(2026, 6, 1, 0, 1, 0), ExitCode = 1 });
        await ws.AddRunAsync(new Run { ScriptId = "s_b", StartedAt = new DateTime(2026, 6, 2, 8, 0, 0), ExitCode = 0 });
        await ws.AddRunAsync(new Run { ScriptId = "s_a", StartedAt = new DateTime(2026, 6, 3, 8, 0, 0), ExitCode = 0 });

        Assert.True(File.Exists(Path.Combine(ws.RunsPath, "2026-05.json")));
        Assert.True(File.Exists(Path.Combine(ws.RunsPath, "2026-06.json")));
        var may = await JsonStore.ReadAsync<RunFile>(Path.Combine(ws.RunsPath, "2026-05.json"));
        Assert.Single(may!.Runs);

        var runs = await ws.GetRunsAsync("s_a", take: 10);
        Assert.Equal(3, runs.Count);
        Assert.Equal(new DateTime(2026, 6, 3, 8, 0, 0), runs[0].StartedAt);
        Assert.Equal(new DateTime(2026, 5, 31, 23, 59, 0), runs[2].StartedAt);

        var latestTwo = await ws.GetRunsAsync("s_a", take: 2);
        Assert.Equal(2, latestTwo.Count);
        Assert.All(latestTwo, r => Assert.Equal("s_a", r.ScriptId));
    }

    [Fact]
    public async Task AddRun_ConcurrentWriters_LoseNothing()
    {
        using var tmp = new TempDirectory();
        var ws = await WorkspaceService.OpenOrCreateAsync(tmp.Path);
        var started = new DateTime(2026, 7, 10, 12, 0, 0);

        await Task.WhenAll(Enumerable.Range(0, 25).Select(i =>
            ws.AddRunAsync(new Run { ScriptId = "s_a", StartedAt = started.AddSeconds(i) })));

        var runs = await ws.GetRunsAsync("s_a", take: 100);
        Assert.Equal(25, runs.Count);
        Assert.Equal(25, runs.Select(r => r.Id).Distinct().Count());
    }

    [Fact]
    public async Task Crud_SortOrderAndRenameAndDelete()
    {
        using var tmp = new TempDirectory();
        var ws = await WorkspaceService.OpenOrCreateAsync(tmp.Path);

        var a = await ws.AddNotebookAsync("A");
        var b = await ws.AddNotebookAsync("B");
        var c = await ws.AddNotebookAsync("C");
        Assert.Equal([0, 1, 2], new[] { a, b, c }.Select(n => n.SortOrder));

        await ws.DeleteNotebookAsync(b.Id);
        var d = await ws.AddNotebookAsync("D");
        Assert.Equal(3, d.SortOrder);

        await ws.RenameNotebookAsync(a.Id, "Alfa");
        Assert.Equal("Alfa", ws.Data.Notebooks.First(n => n.Id == a.Id).Name);

        var s1 = await ws.AddSectionAsync(a.Id, "S1");
        var s2 = await ws.AddSectionAsync(a.Id, "S2");
        Assert.Equal(0, s1.SortOrder);
        Assert.Equal(1, s2.SortOrder);
        await ws.RenameSectionAsync(s1.Id, "Prvá");
        await ws.DeleteSectionAsync(s2.Id);
        Assert.Single(ws.Data.Notebooks.First(n => n.Id == a.Id).Sections);

        var reopened = await WorkspaceService.OpenOrCreateAsync(tmp.Path);
        Assert.Equal(["Alfa", "C", "D"], reopened.Data.Notebooks.Select(n => n.Name));
    }

    [Fact]
    public async Task AddScript_CreatesPhysicalFile_ButNeverOverwritesExisting()
    {
        using var tmp = new TempDirectory();
        var ws = await WorkspaceService.OpenOrCreateAsync(tmp.Path);
        var nb = await ws.AddNotebookAsync("DevOps");
        var sec = await ws.AddSectionAsync(nb.Id, "Deploy");

        var created = await ws.AddScriptAsync(sec.Id, "new", ScriptLanguage.Batch, initialContent: "@echo off");
        Assert.Equal("DevOps/Deploy/new.bat", created.RelativePath);
        Assert.Equal("@echo off", await File.ReadAllTextAsync(ws.GetAbsolutePath(created)));

        var existingPath = Path.Combine(tmp.Path, "legacy", "old.ps1");
        Directory.CreateDirectory(Path.GetDirectoryName(existingPath)!);
        await File.WriteAllTextAsync(existingPath, "ORIGINAL");
        var imported = await ws.AddScriptAsync(sec.Id, "old", ScriptLanguage.PowerShell,
            relativePath: "legacy/old.ps1", initialContent: "NEW");
        Assert.Equal("ORIGINAL", await File.ReadAllTextAsync(ws.GetAbsolutePath(imported)));
        Assert.Equal(1, imported.SortOrder);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ws.AddScriptAsync(sec.Id, "dup", ScriptLanguage.PowerShell, relativePath: "legacy/old.ps1"));
    }

    [Fact]
    public async Task AddScript_PathOutsideWorkspace_IsRejected()
    {
        using var tmp = new TempDirectory();
        var ws = await WorkspaceService.OpenOrCreateAsync(tmp.Combine("ws"));
        var nb = await ws.AddNotebookAsync("A");
        var sec = await ws.AddSectionAsync(nb.Id, "B");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            ws.AddScriptAsync(sec.Id, "evil", ScriptLanguage.PowerShell, relativePath: "../evil.ps1"));
    }

    [Fact]
    public async Task FindScript_GetAbsolutePath_AndDelete()
    {
        using var tmp = new TempDirectory();
        var ws = await WorkspaceService.OpenOrCreateAsync(tmp.Path);
        var nb = await ws.AddNotebookAsync("A");
        var sec = await ws.AddSectionAsync(nb.Id, "B");
        var s1 = await ws.AddScriptAsync(sec.Id, "one", ScriptLanguage.Bash);
        var s2 = await ws.AddScriptAsync(sec.Id, "two", ScriptLanguage.Bash);

        Assert.Same(s1, ws.FindScript(s1.Id));
        Assert.Null(ws.FindScript("s_nope"));
        Assert.Equal(Path.Combine(tmp.Path, "A", "B", "one.sh"), ws.GetAbsolutePath(s1));

        await ws.DeleteScriptAsync(s1.Id); // len metadáta
        Assert.Null(ws.FindScript(s1.Id));
        Assert.True(File.Exists(ws.GetAbsolutePath(s1)));

        await ws.DeleteScriptAsync(s2.Id, deleteFile: true);
        Assert.False(File.Exists(ws.GetAbsolutePath(s2)));

        await Assert.ThrowsAsync<KeyNotFoundException>(() => ws.DeleteScriptAsync("s_nope"));
    }

    [Fact]
    public async Task UnknownVersion_ThrowsUnderstandableException()
    {
        using var tmp = new TempDirectory();
        var meta = Path.Combine(tmp.Path, ".skriptec");
        Directory.CreateDirectory(meta);
        await File.WriteAllTextAsync(Path.Combine(meta, "workspace.json"),
            """{ "version": "99", "name": "x", "notebooks": [] }""");

        var ex = await Assert.ThrowsAsync<UnsupportedFormatVersionException>(() =>
            WorkspaceService.OpenOrCreateAsync(tmp.Path));
        Assert.Equal("99", ex.FoundVersion);
        Assert.Contains("99", ex.Message);
    }

    [Fact]
    public async Task CorruptJson_ThrowsCorruptJsonException()
    {
        using var tmp = new TempDirectory();
        var meta = Path.Combine(tmp.Path, ".skriptec");
        Directory.CreateDirectory(meta);
        await File.WriteAllTextAsync(Path.Combine(meta, "workspace.json"), "{ not json");

        await Assert.ThrowsAsync<CorruptJsonException>(() => WorkspaceService.OpenOrCreateAsync(tmp.Path));
    }

    [Fact]
    public async Task Schedules_RoundTrip()
    {
        using var tmp = new TempDirectory();
        var ws = await WorkspaceService.OpenOrCreateAsync(tmp.Path);
        ws.Schedules.Schedules.Add(new Schedule
        {
            ScriptId = "s_a",
            Trigger = new ScheduleTrigger
            {
                Type = TriggerType.Weekly,
                TimeOfDay = new TimeSpan(9, 0, 0),
                DaysOfWeek = [DayOfWeek.Monday, DayOfWeek.Friday],
            },
            Parameters = { ["Environment"] = "prod" },
        });
        await ws.SaveSchedulesAsync();

        var reopened = await WorkspaceService.OpenOrCreateAsync(tmp.Path);
        var schedule = Assert.Single(reopened.Schedules.Schedules);
        Assert.StartsWith("sch_", schedule.Id);
        Assert.Equal(TriggerType.Weekly, schedule.Trigger.Type);
        Assert.Equal(new TimeSpan(9, 0, 0), schedule.Trigger.TimeOfDay);
        Assert.Equal([DayOfWeek.Monday, DayOfWeek.Friday], schedule.Trigger.DaysOfWeek);
        Assert.Equal("prod", schedule.Parameters["Environment"]);
        Assert.True(schedule.IsEnabled);
    }
}
