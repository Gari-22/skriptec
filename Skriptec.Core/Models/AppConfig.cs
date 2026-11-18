namespace Skriptec.Core.Models;

/// <summary>%AppData%\Skriptec\config.json</summary>
public class AppConfig : IVersioned
{
    public string Version { get; set; } = "1";
    public List<WorkspaceRef> Workspaces { get; set; } = new();
    public EditorConfig Editor { get; set; } = new();
    public RunnerConfig Runner { get; set; } = new();
    public UiConfig Ui { get; set; } = new();

    /// <summary>MVP: len flag, žiadny paywall.</summary>
    public bool IsPremium { get; set; }
}

public class WorkspaceRef
{
    public string Id { get; set; } = Ids.New(Ids.Workspace);
    public string Name { get; set; } = "";
    public string ScriptsRootPath { get; set; } = "";
    public bool IsActive { get; set; }
}

public class EditorConfig
{
    public string Font { get; set; } = "Cascadia Code";
    public int FontSize { get; set; } = 13;
    public int TabSize { get; set; } = 4;
    public bool AutoSave { get; set; }
}

public class RunnerConfig
{
    public string DefaultShell { get; set; } = "powershell";
    public string? BashPath { get; set; }
    public string? PwshPath { get; set; }
}

public class UiConfig
{
    public string Theme { get; set; } = "light";
    public int SidebarWidth { get; set; } = 220;
}
