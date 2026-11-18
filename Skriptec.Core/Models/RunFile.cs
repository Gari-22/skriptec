namespace Skriptec.Core.Models;

/// <summary>&lt;workspace&gt;\.skriptec\runs\YYYY-MM.json</summary>
public class RunFile : IVersioned
{
    public string Version { get; set; } = "1";
    public List<Run> Runs { get; set; } = new();
}

public class Run : EntityBase
{
    public Run() : base(Ids.Run) { }

    public string ScriptId { get; set; } = "";
    public DateTime StartedAt { get; set; } = DateTime.Now;

    /// <summary>Trvanie v milisekundách.</summary>
    public long Duration { get; set; }

    public int ExitCode { get; set; }
    public string Output { get; set; } = "";

    /// <summary>"manual" alebo "scheduled".</summary>
    public string Trigger { get; set; } = "manual";
}
