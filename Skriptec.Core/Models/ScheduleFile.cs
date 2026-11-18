namespace Skriptec.Core.Models;

/// <summary>&lt;workspace&gt;\.skriptec\schedules.json</summary>
public class ScheduleFile : IVersioned
{
    public string Version { get; set; } = "1";
    public List<Schedule> Schedules { get; set; } = new();
}

public enum TriggerType
{
    Daily,
    Weekly,
    Interval,
    OnBoot,
    OnLogon
}

/// <summary>Štruktúrovaný trigger (Windows Task Scheduler nepodporuje cron natívne).</summary>
public class ScheduleTrigger
{
    public TriggerType Type { get; set; }

    /// <summary>Čas dňa pre Daily/Weekly.</summary>
    public TimeSpan? TimeOfDay { get; set; }

    /// <summary>Dni pre Weekly.</summary>
    public List<DayOfWeek> DaysOfWeek { get; set; } = new();

    /// <summary>Interval v minútach pre Interval.</summary>
    public int? IntervalMinutes { get; set; }
}

public class Schedule : EntityBase
{
    public Schedule() : base(Ids.Schedule) { }

    public string ScriptId { get; set; } = "";
    public ScheduleTrigger Trigger { get; set; } = new();

    /// <summary>Voliteľný popis (cron v MVP nie je podporovaný).</summary>
    public string? CronExpr { get; set; }

    public string? DeployedFile { get; set; }
    public int? DeployedVersion { get; set; }
    public Dictionary<string, string> Parameters { get; set; } = new();
    public bool IsEnabled { get; set; } = true;
}
