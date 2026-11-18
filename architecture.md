# Skriptec – Architektúra

> Verzia: 1.1 | Máj 2026 | Fáza: Pre-development

---

## Prehľad aplikácie

Skriptec je **Windows desktop aplikácia** (WPF / .NET 10) na centralizovanú správu, organizáciu a spúšťanie skriptov. Rieši problém skriptov rozhádzaných po rôznych priečinkoch — poskytuje virtuálnu hierarchickú štruktúru bez zmeny fyzického umiestnenia súborov.

---

## Technologický stack

| Vrstva | Technológia | Poznámka |
|--------|-------------|----------|
| UI Framework | **WPF (.NET 10)** | Natívna podpora AvalonEdit |
| Editor | **AvalonEditB** (NuGet) | Fork kompatibilný s .NET 6+ |
| Dáta | **JSON súbory** (System.Text.Json) | workspace.json, schedules.json, runs/ |
| Plánovanie | **TaskScheduler** (NuGet) | Wrapper nad Win32 Task Scheduler API |
| File watcher | **.NET FileSystemWatcher** | Built-in, pre file trigger |
| HTTP | **HttpClient** | Built-in, sťahovanie šablón |
| Export/Import | **System.IO.Compression.ZipArchive** | Built-in |
| JSON | **System.Text.Json** | Built-in — hlavný formát úložiska |
| WPF štýly | **WPF-UI** (ModernWPF) | Fluent-like dizajn |
| IDE | **Visual Studio Code** + C# Dev Kit | Zadarmo |

**Odstránené závislosti:** ~~SQLite~~, ~~Microsoft.Data.Sqlite~~, ~~Dapper~~

---

## Štruktúra uloženia dát

### Súborová štruktúra workspace

```
%AppData%\Skriptec\
├── config.json                     ← globálne nastavenia app, zoznam workspaces
└── scheduled\                      ← nasadené verzie skriptov pre TaskScheduler
    ├── script_abc123_v3.ps1
    └── script_def456_v1.bat

C:\Users\Jan\Scripts\               ← používateľom zvolený priečinok (workspace)
├── .skriptec\                      ← skrytý priečinok metadát
│   ├── workspace.json              ← hierarchia + metadáta skriptov
│   ├── schedules.json              ← plány spustení
│   └── runs\                       ← história behov (per-mesiac)
│       ├── 2026-05.json
│       └── 2026-04.json
│
├── DevOps\
│   └── Deployment\
│       ├── deploy-prod.ps1         ← working copy (editovateľná)
│       └── .versions\              ← lokálne verzie
│           ├── deploy-prod_v1.ps1
│           ├── deploy-prod_v2.ps1
│           └── deploy-prod_v3.ps1  ← aktuálne nasadená verzia
│
└── Domacnost\
    └── Zalohy\
        └── backup.ps1
```

---

## Formát súborov

### `%AppData%\Skriptec\config.json`

```json
{
  "version": "1",
  "workspaces": [
    {
      "id": "ws_abc123",
      "name": "Hlavný workspace",
      "scriptsRootPath": "C:\\Users\\Jan\\Scripts",
      "isActive": true
    },
    {
      "id": "ws_def456",
      "name": "Práca",
      "scriptsRootPath": "D:\\WorkScripts",
      "isActive": false
    }
  ],
  "editor": {
    "font": "Cascadia Code",
    "fontSize": 13,
    "tabSize": 4,
    "autoSave": false
  },
  "runner": {
    "defaultShell": "powershell",
    "bashPath": "C:\\Program Files\\Git\\bin\\bash.exe"
  },
  "ui": {
    "theme": "light",
    "sidebarWidth": 220
  }
}
```

### `<workspace>/.skriptec/workspace.json`

```json
{
  "version": "1",
  "name": "Hlavný workspace",
  "notebooks": [
    {
      "id": "nb_abc123",
      "name": "DevOps",
      "description": "Deployment a monitoring skripty",
      "sortOrder": 0,
      "sections": [
        {
          "id": "sc_def456",
          "name": "Deployment",
          "sortOrder": 0,
          "scripts": [
            {
              "id": "s_ghi789",
              "name": "deploy-prod",
              "language": "powershell",
              "relativePath": "DevOps/Deployment/deploy-prod.ps1",
              "currentVersion": 4,
              "deployedVersion": 3,
              "tags": ["deploy", "prod"],
              "description": "Produkčný deploy na server",
              "createdAt": "2026-03-15T10:00:00",
              "updatedAt": "2026-05-26T14:30:00",
              "syncId": "550e8400-e29b-41d4-a716-446655440000"
            }
          ]
        }
      ]
    }
  ]
}
```

### `<workspace>/.skriptec/schedules.json`

```json
{
  "version": "1",
  "schedules": [
    {
      "id": "sch_xyz",
      "scriptId": "s_ghi789",
      "triggerType": "cron",
      "cronExpr": "0 9 * * 1-5",
      "deployedFile": "%AppData%\\Skriptec\\scheduled\\script_ghi789_v3.ps1",
      "deployedVersion": 3,
      "parameters": { "Environment": "prod" },
      "isEnabled": true,
      "createdAt": "2026-05-01T09:00:00"
    }
  ]
}
```

### `<workspace>/.skriptec/runs/2026-05.json`

```json
{
  "version": "1",
  "runs": [
    {
      "id": "run_001",
      "scriptId": "s_ghi789",
      "startedAt": "2026-05-26T09:00:01",
      "duration": 4230,
      "exitCode": 0,
      "output": "Deploy successful. Version 3.2.1 deployed."
    }
  ]
}
```

---

## Verzionovanie skriptov

### Working copy vs. Deployed version

```
Workflow:

1. VYTVORENIE
   → fyzický .ps1 súbor vznikne na disku
   → v workspace.json: currentVersion=1, deployedVersion=null

2. NAPLÁNUJE SA
   → snapshot sa skopíruje do %AppData%\Skriptec\scheduled\script_id_v1.ps1
   → TaskScheduler dostane túto cestu
   → deployedVersion=1

3. ÚPRAVA (working copy)
   → používateľ edituje .ps1 (v app alebo externe vo VS Code)
   → deployed verzia v scheduled\ beží ďalej — nedotknutá
   → app zobrazí: "Verzia v4 sa líši od nasadenej v3"

4. ULOŽENIE NOVEJ VERZIE
   → snapshot sa uloží do .versions\deploy-prod_v4.ps1
   → currentVersion=4, deployedVersion zostáva 3

5. NASADENIE
   → scheduled\script_id_v4.ps1 sa skopíruje
   → TaskScheduler sa aktualizuje
   → deployedVersion=4
```

### UI indikátory stavu

```
Sidebar:
  DevOps
    Deployment
      ► deploy-prod.ps1   [v4] ●       ← nasadená v3, existuje novšia v4
      ► rollback.sh       [v1]         ← nasadená = aktuálna
      ► check-disk.ps1        ✎        ← nie je naplánovaná
```

---

## Hierarchia dát — OneNote model

Trojúrovňová štruktúra s voliteľnou úrovňou Workspace:

```
Workspace (config.json)
  └── Zošit (Notebook)
        └── Sekcia (Section)
              └── Skript (Script)
```

**Viacero workspaces** — každý má vlastný `workspace.json` a vlastný koreňový priečinok skriptov. Prepínanie = načítanie iného workspace.

---

## Štruktúra Solution (.NET)

```
Skriptec.sln
│
├── Skriptec/                       ← WPF hlavná aplikácia
│   ├── App.xaml
│   ├── MainWindow.xaml
│   ├── Views/
│   │   ├── SchedulerWindow.xaml
│   │   ├── WizardWindow.xaml
│   │   └── TemplateStoreWindow.xaml
│   ├── ViewModels/
│   │   ├── MainViewModel.cs
│   │   ├── ScriptEditorViewModel.cs
│   │   ├── SchedulerViewModel.cs
│   │   └── WizardViewModel.cs
│   └── Resources/
│       ├── Styles/
│       │   ├── Colors.xaml
│       │   ├── Buttons.xaml
│       │   └── Typography.xaml
│       └── Highlighting/
│           ├── PowerShell.xshd
│           ├── Bash.xshd
│           └── Batch.xshd
│
├── Skriptec.Core/                  ← Zdieľaná logika (Models + Services)
│   ├── Models/
│   │   ├── Workspace.cs
│   │   ├── Notebook.cs
│   │   ├── Section.cs
│   │   ├── Script.cs
│   │   ├── ScriptVersion.cs
│   │   ├── Schedule.cs
│   │   └── Run.cs
│   ├── Services/
│   │   ├── WorkspaceService.cs     ← namiesto DatabaseService (JSON I/O)
│   │   ├── ScriptRunnerService.cs
│   │   ├── SchedulerService.cs
│   │   ├── ExportService.cs
│   │   └── TemplateService.cs
│   └── Wizard/
│       ├── WizardModel.cs
│       └── Templates/
│           ├── BackupScriptTemplate.cs
│           ├── RunProgramTemplate.cs
│           ├── HttpRequestTemplate.cs
│           ├── CleanupTemplate.cs
│           └── ShellCommandTemplate.cs
│
└── Skriptec.Runner/                ← CLI runner pre TaskScheduler (bez UI)
    └── Program.cs                  ← skriptec-runner.exe --script-id s_ghi789
```

### Prečo tri projekty

`Skriptec.Core` obsahuje všetku logiku zdieľanú medzi WPF aplikáciou a Runner CLI. TaskScheduler nevolá priamo `.ps1` súbor, ale `skriptec-runner.exe` — ten načíta workspace, spustí skript a zapíše Run do `runs/YYYY-MM.json`.

---

## WorkspaceService — kľúčová trieda

```csharp
public class WorkspaceService
{
    private readonly string _workspacePath;
    private WorkspaceData _cache;

    public async Task<WorkspaceData> LoadAsync()
    {
        var path = MetaPath("workspace.json");
        var json = await File.ReadAllTextAsync(path);
        _cache = JsonSerializer.Deserialize<WorkspaceData>(json);
        return _cache;
    }

    public async Task SaveAsync()
    {
        var path = MetaPath("workspace.json");
        var json = JsonSerializer.Serialize(_cache, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        // Atomický zápis — ochrana pred korupciou pri páde app
        var tmp = path + ".tmp";
        await File.WriteAllTextAsync(tmp, json);
        File.Replace(tmp, path, path + ".bak");
    }

    public async Task AddRunAsync(Run run)
    {
        var month = run.StartedAt.ToString("yyyy-MM");
        var runsPath = MetaPath($"runs/{month}.json");
        // append do mesačného súboru
    }

    private string MetaPath(string file) =>
        Path.Combine(_workspacePath, ".skriptec", file);
}
```

---

## Runner — spúšťanie bez otvorenej app

```csharp
// Skriptec.Runner/Program.cs
// TaskScheduler volá: skriptec-runner.exe --script-id s_ghi789 --workspace "C:\Users\Jan\Scripts"

var scriptId = args["--script-id"];
var workspacePath = args["--workspace"];

var ws = new WorkspaceService(workspacePath);
var data = await ws.LoadAsync();
var script = data.FindScript(scriptId);

// Spusti skript
var runner = new ScriptRunnerService();
var result = await runner.RunAsync(script);

// Ulož výsledok
await ws.AddRunAsync(new Run {
    ScriptId = scriptId,
    StartedAt = DateTime.Now,
    Duration = result.DurationMs,
    ExitCode = result.ExitCode,
    Output = result.Output
});
```

---

## Hlavné obrazovky

### MainWindow — trojpanelové rozloženie

```
┌─────────────────────────────────────────────────────────────┐
│  [Logo]  [Hľadaj…]              [Workspace ▼] [Nastavenia]  │
├──────────────┬──────────────────────────────┬───────────────┤
│ Sidebar      │ Editor                        │ Meta panel    │
│ (220px)      │ (flex)                        │ (190px)       │
│              │ ┌ Tabs ─────────────────────┐ │               │
│ DevOps       │ │ deploy-prod.ps1 · rollback│ │ METADÁTA      │
│  Deployment  │ └───────────────────────────┘ │ Zošit: DevOps │
│   ► deploy.. │ ┌ Toolbar ──────────────────┐ │ Sekcia: Dep.  │
│   [v4]● roll │ │▶ Spustiť │ Naplánovať ... │ │               │
│  Monitoring  │ └───────────────────────────┘ │ VERZIE        │
│              │ ┌ Line nums │ Code area ─────┐ │ ● v4 (teraz)  │
│ Domácnosť    │ │    1      │ # deploy...    │ │ ○ v3 (nasad.) │
│  Zálohy      │ │    2      │ param(...)     │ │ ○ v2          │
│              │ └───────────────────────────┘ │               │
│ [+ Nový]     │ ┌ Status ───────────────────┐ │ HISTÓRIA      │
│              │ │ [PS1] [v4≠v3] Riadok 14   │ │ ✓ dnes 09:14  │
│              │ └───────────────────────────┘ │ ✗ 23.5 18:02  │
└──────────────┴──────────────────────────────┴───────────────┘
```

---

## Podporované jazyky skriptov

| Jazyk | MVP | Syntax highlighting | Runtime |
|-------|-----|---------------------|---------|
| PowerShell (.ps1) | ✅ Povinný | AvalonEdit .xshd | Built-in Windows |
| Batch (.bat/.cmd) | ✅ Povinný | AvalonEdit .xshd | Built-in Windows |
| Bash (.sh) | ⚠️ Voliteľný | AvalonEdit .xshd | WSL / Git Bash |
| Python (.py) | ❌ V2 | AvalonEdit .xshd | Vyžaduje runtime |
| Node.js (.js) | ❌ V2 | AvalonEdit .xshd | Vyžaduje runtime |

---

## Typy spúšťacích triggerov

| Trigger | Implementácia |
|---------|---------------|
| Plán (cron) | TaskScheduler NuGet — vizuálny konfigurátor |
| Spustenie PC | TaskScheduler OnBoot trigger |
| Prihlásenie | TaskScheduler OnLogon trigger |
| Zmena súboru | .NET FileSystemWatcher |
| Manuálne | Priame volanie ScriptRunnerService |

---

## Sync a portabilita

| Scenár | Riešenie |
|--------|----------|
| **Záloha** | Skopíruj celý workspace priečinok (skripty + `.skriptec/`) |
| **Presun na nový PC** | Skopíruj workspace + `config.json` z `%AppData%` |
| **OneDrive / Dropbox** | Nastav sync na workspace priečinok — funguje automaticky |
| **Git sync** | `git init` v workspace priečinku — JSON súbory sú diff-friendly |
| **Zdieľanie s kolegom** | Pošli workspace priečinok — obsahuje všetko |

**Poznámka ku konfliktu:** Ak je workspace otvorený na dvoch PC naraz cez OneDrive, `WorkspaceService.SaveAsync()` používa atomický zápis cez `.tmp` súbor. Pre aktívny multi-PC sync odporúčame Git (ručný commit) alebo vlastný server (V3).

---

## Formát .scriptpack

```
nazov-balika.scriptpack   (ZIP archív)
├── manifest.json
└── scripts/
    ├── deploy-prod.ps1
    └── rollback.sh
```

```json
{
  "name": "Docker Deploy Pack",
  "version": "1.2.0",
  "author": "Jan Novak",
  "description": "Nasadenie a rollback pre Docker produkciu",
  "schemaVersion": "1",
  "scripts": [
    { "name": "deploy-prod.ps1", "lang": "powershell", "tags": ["deploy","prod"] },
    { "name": "rollback.sh",     "lang": "bash",        "tags": ["rollback"] }
  ],
  "requires": { "os": "windows" }
}
```

---

## MVP fázy

| Fáza | Obsah | Výstup |
|------|-------|--------|
| 1 – Analýza | Dátový model, štruktúra súborov, wireframy | Tento dokument |
| 2 – Správa skriptov | WorkspaceService, JSON I/O, strom hierarchie, CRUD | Fungujúci strom |
| 3 – Editor + Runner | AvalonEdit, syntax, Process runner, verzionovanie | Editor + výstup |
| 4 – Wizard + Export | Wizard 3 typy, ZIP, .scriptpack, plánovač | Kompletné MVP |
| 5 – Šablóny | CDN, JSON index, 10 free šablón | Online databáza |
| 6 – Pro licencia | Licenčný kľúč, paywall, platby | Monetizácia |
