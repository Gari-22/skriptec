namespace Skriptec.Core.Models;

/// <summary>Súbor s poľom "version" – slúži na kontrolu/migráciu formátu pri načítaní.</summary>
public interface IVersioned
{
    string Version { get; set; }
}

/// <summary>Generátor ID s prefixom (nb_, sc_, s_, sch_, run_, ws_).</summary>
public static class Ids
{
    public const string Notebook = "nb_";
    public const string Section = "sc_";
    public const string Script = "s_";
    public const string Schedule = "sch_";
    public const string Run = "run_";
    public const string Workspace = "ws_";

    public static string New(string prefix) => prefix + Guid.NewGuid().ToString("N")[..12];
}

/// <summary>Spoločné polia každej entity: Id (s prefixom), SyncId, SortOrder, CreatedAt, UpdatedAt.</summary>
public abstract class EntityBase
{
    protected EntityBase(string idPrefix)
    {
        Id = Ids.New(idPrefix);
        var now = DateTime.Now;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public string Id { get; set; }
    public Guid SyncId { get; set; } = Guid.NewGuid();
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
