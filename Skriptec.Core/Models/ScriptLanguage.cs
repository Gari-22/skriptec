namespace Skriptec.Core.Models;

public enum ScriptLanguage
{
    PowerShell,  // .ps1 — MVP povinný
    Batch,       // .bat / .cmd — MVP povinný
    Bash,        // .sh — MVP voliteľný
    Python,      // .py — V2
    NodeJs       // .js — V2
}

public static class LanguageInfo
{
    public static string DefaultExtension(ScriptLanguage language) => language switch
    {
        ScriptLanguage.PowerShell => ".ps1",
        ScriptLanguage.Batch => ".bat",
        ScriptLanguage.Bash => ".sh",
        ScriptLanguage.Python => ".py",
        ScriptLanguage.NodeJs => ".js",
        _ => throw new ArgumentOutOfRangeException(nameof(language)),
    };

    /// <summary>Rozpozná jazyk podľa prípony (vrátane bodky). Neznáma prípona → null.</summary>
    public static ScriptLanguage? FromExtension(string extension) =>
        extension.ToLowerInvariant() switch
        {
            ".ps1" => ScriptLanguage.PowerShell,
            ".bat" or ".cmd" => ScriptLanguage.Batch,
            ".sh" => ScriptLanguage.Bash,
            ".py" => ScriptLanguage.Python,
            ".js" => ScriptLanguage.NodeJs,
            _ => null,
        };
}
