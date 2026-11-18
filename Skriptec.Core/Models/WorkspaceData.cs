namespace Skriptec.Core.Models;

/// <summary>&lt;workspace&gt;\.skriptec\workspace.json</summary>
public class WorkspaceData : IVersioned
{
    public string Version { get; set; } = "1";
    public string Name { get; set; } = "";
    public List<Notebook> Notebooks { get; set; } = new();
}

public class Notebook : EntityBase
{
    public Notebook() : base(Ids.Notebook) { }

    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public List<Section> Sections { get; set; } = new();
}

public class Section : EntityBase
{
    public Section() : base(Ids.Section) { }

    public string Name { get; set; } = "";
    public List<Script> Scripts { get; set; } = new();
}

public class Script : EntityBase
{
    public Script() : base(Ids.Script) { }

    public string Name { get; set; } = "";
    public ScriptLanguage Language { get; set; }

    /// <summary>Cesta k súboru relatívna ku koreňu workspace, oddeľovač "/".</summary>
    public string RelativePath { get; set; } = "";

    public int CurrentVersion { get; set; } = 1;
    public int? DeployedVersion { get; set; }
    public List<string> Tags { get; set; } = new();
    public string Description { get; set; } = "";
}
