using Skriptec.Core.Services;

namespace Skriptec.Core.Tests;

public class ConfigServiceTests
{
    [Fact]
    public async Task Load_MissingFile_ReturnsDefaults()
    {
        using var tmp = new TempDirectory();
        var cfg = new ConfigService(tmp.Combine("config.json"));

        var config = await cfg.LoadAsync();

        Assert.Empty(config.Workspaces);
        Assert.False(config.IsPremium);
        Assert.Equal(13, config.Editor.FontSize);
        Assert.Null(cfg.ActiveWorkspace);
    }

    [Fact]
    public async Task AddActivateRemove_Workspaces_PersistAcrossInstances()
    {
        using var tmp = new TempDirectory();
        var path = tmp.Combine("cfg", "config.json");
        var cfg = new ConfigService(path);
        await cfg.LoadAsync();

        var a = await cfg.AddWorkspaceAsync(tmp.Combine("A"), "Prvý");
        Assert.StartsWith("ws_", a.Id);
        Assert.True(a.IsActive);

        var b = await cfg.AddWorkspaceAsync(tmp.Combine("B")); // názov z priečinka
        Assert.Equal("B", b.Name);
        Assert.Equal(b.Id, cfg.ActiveWorkspace!.Id);
        Assert.False(a.IsActive);

        // rovnaká cesta → žiadny duplikát, len aktivácia
        var again = await cfg.AddWorkspaceAsync(tmp.Combine("A"));
        Assert.Equal(a.Id, again.Id);
        Assert.Equal(2, cfg.Config.Workspaces.Count);

        var reloaded = new ConfigService(path);
        await reloaded.LoadAsync();
        Assert.Equal(2, reloaded.Config.Workspaces.Count);
        Assert.Equal(a.Id, reloaded.ActiveWorkspace!.Id);
        Assert.Single(reloaded.Config.Workspaces, w => w.IsActive);

        await reloaded.RemoveWorkspaceAsync(a.Id); // aktívny sa odstráni → aktivuje sa zvyšný
        Assert.Single(reloaded.Config.Workspaces);
        Assert.Equal(b.Id, reloaded.ActiveWorkspace!.Id);

        await reloaded.ActivateWorkspaceAsync(b.Id);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => reloaded.ActivateWorkspaceAsync("ws_nope"));
    }
}
