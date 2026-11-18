# CLAUDE.md — Skriptec

> Tento súbor číta Claude Code pri každom spustení v tomto projekte.
> Obsahuje kontext projektu, konvencie a pravidlá pre AI asistenta.

---

## Čo je Skriptec

Windows desktop aplikácia (WPF / .NET 10 / C#) na centralizovanú správu, organizáciu a spúšťanie skriptov. Virtuálna hierarchia Notebook → Section → Script bez zmeny fyzického umiestnenia súborov.

**Hlavný problém ktorý rieši:** Programátori majú skripty rozhádzané po priečinkoch — po mesiacoch ich nevedia nájsť, nevedia čo robia, nevedia ich bezpečne spustiť.

---

## Aktuálny stav projektu

**Fáza:** Vývoj MVP — etapa M0 a M1 dokončené, ďalej M2 (Strom hierarchie + CRUD v UI) podľa `MVP-PLAN.md`. Vývoj prebieha vo vetve `dev`.

Dokončené:
- M1: modely, `JsonStore`, `ConfigService`, `WorkspaceService`, 16 xunit testov
- M0: solution, 4 projekty, NuGet balíky, štýly v `Resources/Styles/`, layout MainWindow
- Architektonické rozhodnutia (viď `decisions.md`)
- Dátový model (JSON súbory — bez SQLite)
- WPF XAML mockupy (MainWindow, Scheduler, Wizard)
- Projektová dokumentácia

Ďalší krok: **M2** — `MainViewModel`, TreeView s CRUD, workspaces, import, drag & drop

---

## Technologický stack

```
UI:          WPF (.NET 10)
Editor:      AvalonEditB (NuGet)
Dáta:        JSON súbory (System.Text.Json built-in) — BEZ SQLite
Plánovanie:  TaskScheduler (NuGet)
HTTP:        HttpClient (built-in)
Export:      ZipArchive (built-in)
JSON:        System.Text.Json (built-in)
Štýly:       WPF-UI (Fluent-like)
IDE:         Visual Studio Code + C# Dev Kit
```

---

## Uloženie dát — súborová štruktúra

```
%AppData%\Skriptec\
├── config.json                  ← globálne nastavenia, zoznam workspaces
└── scheduled\                   ← nasadené verzie skriptov pre TaskScheduler
    └── script_id_vN.ps1

<workspace priečinok>\           ← používateľom zvolený adresár
├── .skriptec\
│   ├── workspace.json           ← hierarchia Notebooks/Sections/Scripts
│   ├── schedules.json           ← plány spustení
│   └── runs\
│       └── YYYY-MM.json         ← história behov per-mesiac
├── DevOps\Deployment\
│   ├── deploy-prod.ps1          ← working copy (editovateľná)
│   └── .versions\
│       ├── deploy-prod_v1.ps1
│       └── deploy-prod_v3.ps1   ← deployed verzia
└── ...
```

**Kľúčové princípy:**
- Žiadna SQLite databáza — všetko v JSON súboroch
- Skripty sú fyzicky na disku — editovateľné externe (VS Code, Notepad++)
- Záloha = skopíruj workspace priečinok
- Sync = OneDrive/Dropbox/Git bez špeciálnej logiky

---

## Solution štruktúra

```
Skriptec.sln
├── Skriptec/           # WPF aplikácia (Views, ViewModels, Resources)
├── Skriptec.Core/      # Zdieľaná logika (Models, Services, Wizard)
└── Skriptec.Runner/    # CLI runner pre TaskScheduler (bez UI)
```

**Skriptec.Runner** — TaskScheduler nevolá `.ps1` priamo, ale:
```
skriptec-runner.exe --script-id s_abc123 --workspace "C:\Users\Jan\Scripts"
```
Runner načíta workspace.json, spustí skript, zapíše Run do `runs/YYYY-MM.json`.

---

## Dátový model (JSON)

### workspace.json (skrátená schéma)
```json
{
  "version": "1",
  "notebooks": [{
    "id": "nb_abc",
    "name": "DevOps",
    "sections": [{
      "id": "sc_def",
      "name": "Deployment",
      "scripts": [{
        "id": "s_ghi",
        "name": "deploy-prod",
        "language": "powershell",
        "relativePath": "DevOps/Deployment/deploy-prod.ps1",
        "currentVersion": 4,
        "deployedVersion": 3,
        "tags": ["deploy","prod"],
        "description": "...",
        "syncId": "uuid-here"
      }]
    }]
  }]
}
```

### schedules.json
```json
{
  "version": "1",
  "schedules": [{
    "id": "sch_xyz",
    "scriptId": "s_ghi",
    "triggerType": "cron",
    "cronExpr": "0 9 * * 1-5",
    "deployedFile": "%AppData%\\Skriptec\\scheduled\\script_ghi_v3.ps1",
    "deployedVersion": 3,
    "parameters": {},
    "isEnabled": true
  }]
}
```

### runs/YYYY-MM.json
```json
{
  "version": "1",
  "runs": [{
    "id": "run_001",
    "scriptId": "s_ghi",
    "startedAt": "2026-05-26T09:00:01",
    "duration": 4230,
    "exitCode": 0,
    "output": "..."
  }]
}
```

---

## Verzionovanie skriptov

```
working copy  = súbor na disku (vždy editovateľný)
deployed      = snapshot v %AppData%\Skriptec\scheduled\ (beží v TaskScheduler)

UI indikátor:
  [v4] ●  = currentVersion=4, deployedVersion=3 → máš novšiu verziu
  [v1]    = currentVersion=deployedVersion → všetko OK
```

Postup nasadenia novej verzie:
1. Skopíruj working copy → `scheduled\script_id_v4.ps1`
2. Aktualizuj TaskScheduler task na novú cestu
3. Nastav `deployedVersion=4` v `workspace.json`

---

## Konvencie kódu

### Naming
```csharp
public class WorkspaceService { }          // triedy: PascalCase
public async Task<Workspace> LoadAsync() { } // metódy: PascalCase
public string Name { get; set; }           // properties: PascalCase
private readonly WorkspaceService _ws;     // fields: _camelCase
x:Name="ScriptTree"                        // XAML: PascalCase
```

### Async/Await
```csharp
// Všetky file I/O operácie sú async
public async Task SaveAsync()
{
    var json = JsonSerializer.Serialize(_cache, new JsonSerializerOptions { WriteIndented = true });
    var tmp = _path + ".tmp";
    await File.WriteAllTextAsync(tmp, json);
    File.Replace(tmp, _path, _path + ".bak");  // atomický zápis
}
```

### Error handling
```csharp
try { await _ws.SaveAsync(); }
catch (Exception ex)
{
    _logger.LogError(ex, "Save failed");
    // Zobraz chybu v UI — nikdy neskrývaj
}
```

### MVVM pattern
```csharp
// ViewModel dedí z ObservableObject alebo implementuje INotifyPropertyChanged
// Commands sú RelayCommand alebo AsyncRelayCommand
// View neobsahuje biznis logiku — len data binding
```

---

## Farby a dizajn

```xml
Purple:       #534AB7   <!-- primárna akčná farba -->
PurpleLight:  #EEEDFE
Success:      #3B6D11
SuccessLight: #EAF3DE
Danger:       #A32D2D
DangerLight:  #FCEBEB
Gray:         #5F5E5A
GrayLight:    #F1EFE8
Border:       #E0DED8
Background:   #FFFFFF
Surface:      #F7F6F2
```

```xml
UI text:    Segoe UI
Mono/code:  Cascadia Code, Consolas (fallback)
Ikony:      Segoe MDL2 Assets
```

---

## Podporované jazyky skriptov

```csharp
public enum ScriptLanguage
{
    PowerShell,  // .ps1 — MVP povinný
    Batch,       // .bat / .cmd — MVP povinný
    Bash,        // .sh — MVP voliteľný (detekuj WSL/GitBash)
    Python,      // .py — V2
    NodeJs       // .js — V2
}
```

---

## Spúšťanie skriptov

```csharp
var psi = new ProcessStartInfo
{
    FileName = "powershell.exe",
    Arguments = $"-ExecutionPolicy Bypass -File \"{scriptPath}\"",
    RedirectStandardOutput = true,
    RedirectStandardError = true,
    UseShellExecute = false,
    CreateNoWindow = true
};

// Asynchrónne čítanie výstupu — NIKDY neblokuj UI thread
process.OutputDataReceived += (s, e) =>
    Application.Current.Dispatcher.Invoke(() => OutputPanel.AppendText(e.Data + "\n"));
```

---

## Čo NEUROBIŤ

```
❌ Nepresúvaj fyzické súbory na disku pri reorganizácii hierarchie (len JSON metadáta)
❌ Neblokuj UI thread pri spúšťaní skriptov
❌ Nepoužívaj SQLite — rozhodnutie je JSON súbory
❌ Nepridávaj Python/Node do MVP — len V2
❌ Nekóduj paywall mechanizmus v MVP — len IsPremium = false flag
❌ Neskladuj citlivé dáta (heslá, API kľúče) v JSON bez šifrovania
❌ Nespúšťaj skripty bez potvrdenia ak majú destruktívne parametre
❌ Nezapisuj priamo do workspace.json — vždy cez atomický zápis (.tmp → replace)
```

---

## Čo VŽDY robiť

```
✅ Všetky file I/O operácie async
✅ Atomický zápis JSON súborov (cez .tmp súbor)
✅ Výstup skriptu zobrazuj v reálnom čase (nie po skončení)
✅ Ukladaj každý beh do runs/YYYY-MM.json
✅ Dirty flag v záložke ak skript má neuložené zmeny
✅ Potvrď pred zmazaním skriptu/sekcie/zošita
✅ Zobrazuj ľudsky čitateľný popis plánovaného spustenia (nie len cron)
✅ Detekuj dostupnosť Bash runtime pred spustením .sh skriptu
✅ syncId (UUID) na každej entite v workspace.json
```

---

## Príkazy pre bežné úlohy

```bash
# Spustenie
dotnet run --project Skriptec

# Build celej solution
dotnet build

# Nový NuGet package
dotnet add <projekt> package <nazov>

# Lokácia dát v dev mode
# %AppData%\Skriptec\config.json
# <workspace>\.skriptec\workspace.json
```

---

## Súvisiace súbory

| Súbor | Popis |
|-------|-------|
| `MVP-PLAN.md` | **Krokový plán implementácie MVP (etapy M0–M7) — riaď sa ním** |
| `architecture.md` | Kompletná architektúra, štruktúra súborov, JSON schémy |
| `decisions.md` | ADR — prečo sa čo rozhodlo (vrátane prechodu na JSON) |
| `tasks.md` | TODO zoznam podľa fáz |
| `design/xaml/` | XAML mockupy (MainWindow, Scheduler, Wizard) — vizuálne zadanie |

---

*Aktualizované: Máj 2026 — prechod z SQLite na JSON súborový model*
