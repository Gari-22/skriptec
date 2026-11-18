# Skriptec – MVP plán pre Claude Code

> Verzia: 1.0 | November 2026 | Nadväzuje na `CLAUDE.md`, `architecture.md`, `decisions.md`, `tasks.md`
> Účel: krokový plán, ktorý Claude Code vykoná po etapách vo VS Code. Každá etapa končí buildom, testom a commitom.

---

## 1. Súhrn projektu (1 minúta čítania)

**Čo:** Windows desktop app (WPF / .NET 10 / C#) na správu, organizáciu a spúšťanie skriptov. Virtuálna hierarchia **Workspace → Notebook → Section → Script** — fyzické súbory sa nepresúvajú.

**Kľúčové rozhodnutia (ADR):**

| ADR | Rozhodnutie |
|-----|-------------|
| 001 | WPF (nie WinUI 3 / MAUI) – kvôli AvalonEdit |
| 002 | JSON súbory, **žiadne SQLite** – `.skriptec/workspace.json`, `schedules.json`, `runs/YYYY-MM.json` |
| 003 | AvalonEditB ako editor |
| 004 | Freemium – v MVP **len** `IsPremium = false`, žiadny paywall |
| 005 | MVP jazyky: PowerShell + Batch povinné, Bash voliteľný (detekcia WSL/Git Bash) |
| 007 | MVVM (CommunityToolkit.Mvvm) |
| 008 | Fixná 3-úrovňová hierarchia + viac workspaces |
| 009 | 3 projekty: `Skriptec` (WPF), `Skriptec.Core` (logika), `Skriptec.Runner` (CLI pre Task Scheduler) |
| 010 | Verzionovanie: working copy na disku, snapshoty v `.versions/`, nasadená kópia v `%AppData%\Skriptec\scheduled\` |

**Stav:** analýza hotová, kód neexistuje.

---

## 2. Definícia MVP

MVP = používateľ vie:
1. Vytvoriť/otvoriť workspace (priečinok) a spravovať v ňom Notebooky, Sekcie, Skripty.
2. Importovať existujúce skripty (súbor alebo celý priečinok).
3. Editovať skript so syntax highlightingom (PS1, BAT/CMD, SH), viac záložiek, dirty flag.
4. Spustiť skript ručne s výstupom v reálnom čase; beh sa uloží do histórie.
5. Uložiť verziu skriptu a vidieť stav `[v4] ●` vs. nasadená verzia.
6. Naplánovať skript cez Windows Task Scheduler (denne / týždenne / interval / pri štarte / pri prihlásení) – beží cez `skriptec-runner.exe` aj pri zatvorenej app.
7. Vidieť históriu behov v Meta paneli.

**Mimo MVP (neskôr):** Wizard, `.scriptpack` export/import, šablóny/CDN, licencie, File-change trigger, Python/Node, dark mode, AI asistent.

> **Pozor – cron:** Windows Task Scheduler nepodporuje cron výrazy natívne. V MVP sa v UI ponúknu presety (denne o HH:mm, vybrané dni v týždni, každých N minút/hodín), uložia sa do `schedules.json` ako štruktúrovaný trigger a `cronExpr` ostane len ako voliteľný popis. Plný cron → V2.

---

## 2a. Grafické návrhy (XAML mockupy)

Z analýzy existujú tri XAML súbory (ZIP `ScriptMaster-WPF-XAML.zip`). Sú **záväzným vizuálnym zadaním**: rozloženie, farby, rozostupy, štýly tlačidiel, pill toggle dní, progress bar wizardu.

**Umiestnenie v repozitári:**
```
design/xaml/
├── MainWindow.xaml       ← 3 panely + GridSplitter, strom, toolbar, tab bar, meta panel s históriou
├── SchedulerWindow.xaml  ← RadioButton navigácia triggerov, pill ToggleButton dni, toggle switch, preview box
├── WizardWindow.xaml     ← krokový progress bar, krok 2 (záloha) s formulárom
└── README.md
```

**Známe rozdiely mockupov oproti aktuálnej architektúre (Claude Code ich má opraviť pri prenose):**
- Názov `ScriptMaster` → `Skriptec` (namespace, `x:Class`, titulky).
- Editor je `TextBox` → nahradiť `ae:TextEditor` (AvalonEditB, `xmlns:ae="http://icsharpcode.net/sharpdevelop/texteditor"`).
- Štýly a farby sú lokálne vo `Window.Resources` → presunúť do `Resources/Styles/Colors.xaml`, `Buttons.xaml`, `Typography.xaml` a zlúčiť v `App.xaml`.
- Statický obsah (napevno vpísané položky stromu, história, tagy) → nahradiť data bindingom na ViewModely; code-behind s `x:Name` referenciami → MVVM (commands, binding).
- Preview box s cron výrazom v Scheduleri → zobrazovať slovenský popis triggera + „ďalší beh“ (cron v MVP nie je, viď sekcia 2).
- Indikátory verzií `[v4] ●`, `✎` a Workspace prepínač v hornej lište v mockupe chýbajú → doplniť v štýle mockupu.
- `Window` → zvážiť `ui:FluentWindow` (WPF-UI) len ak nenaruší vzhľad mockupu.

**Pravidlo:** vizuál sa preberá z mockupu, štruktúra (bindingy, VM, služby) z tohto plánu. Ak je konflikt, vizuál podľa mockupu, správanie podľa plánu.

---

## 3. Pravidlá pre Claude Code (platia v každej etape)

- Najprv si prečítaj `CLAUDE.md` a túto etapu; pracuj **iba** na aktuálnej etape.
- Na konci etapy: `dotnet build` bez chýb a varovaní typu error, `dotnet test` zelené, commit s popisom `Mx: <názov>`.
- Všetky file I/O async, JSON zápis atomicky (`.tmp` → `File.Replace`, pri neexistujúcom cieli `File.Move`).
- UI thread sa nikdy neblokuje; výstup procesov cez `Dispatcher`.
- Biznis logika patrí do `Skriptec.Core` (testovateľná bez UI); `Skriptec` obsahuje len Views/ViewModels.
- Po etape aktualizuj zaškrtnutia v `tasks.md` a stav v `CLAUDE.md`.
- Ak narazíš na nejasnosť v zadaní, zapíš ju do sekcie „Otvorené otázky“ v `tasks.md` a pokračuj rozumným predpokladom.

---

## 4. Etapy

### M0 – Setup solution (≈ 0,5 dňa)

**Úlohy**
- Solution `Skriptec.sln` s projektmi `Skriptec` (wpf), `Skriptec.Core` (classlib), `Skriptec.Runner` (console), `Skriptec.Core.Tests` (xunit). Všetky `net10.0-windows`.
- Referencie: `Skriptec → Core`, `Runner → Core`, `Core.Tests → Core`.
- NuGet: `AvalonEditB`, `WPF-UI`, `CommunityToolkit.Mvvm` (Skriptec); `TaskScheduler`, `CommunityToolkit.Mvvm` (Core).
- `Skriptec.Runner.csproj`: `<AssemblyName>skriptec-runner</AssemblyName>`; výstup kopírovať vedľa `Skriptec.exe` (post-build alebo ProjectReference s `ReferenceOutputAssembly=false`).
- `.gitignore` (.NET), `Directory.Build.props` (Nullable enable, ImplicitUsings, LangVersion latest, TreatWarningsAsErrors pre Core).
- Z `design/xaml/MainWindow.xaml` vytiahni štýly do `Resources/Styles/` (Colors, Buttons, Typography) a prenes layout MainWindow (3 panely, GridSplittery, toolbar, tab bar, status bar) – zatiaľ bez dát, s premenovaním na Skriptec.

**Akceptácia:** `dotnet build` OK, `dotnet run --project Skriptec` zobrazí hlavné okno vizuálne zhodné s mockupom (bez obsahu).

---

### M1 – Modely + WorkspaceService + ConfigService (≈ 1–2 dni)

**Úlohy**
- `Skriptec.Core/Models/`: `AppConfig`, `WorkspaceRef`, `WorkspaceData`, `Notebook`, `Section`, `Script`, `ScriptLanguage` (enum), `ScheduleFile`, `Schedule`, `ScheduleTrigger`, `RunFile`, `Run`. Každá entita má `Id` (prefix `nb_`, `sc_`, `s_`, `sch_`, `run_`) a `SyncId` (Guid), `SortOrder`, `CreatedAt`, `UpdatedAt`.
- `JsonStore` helper: `ReadAsync<T>`, `WriteAtomicAsync<T>` (camelCase, WriteIndented, enum ako string).
- `ConfigService`: `%AppData%\Skriptec\config.json` – load/save, add/remove/activate workspace.
- `WorkspaceService`:
  - `OpenOrCreateAsync(path)` – vytvorí `.skriptec/`, `runs/`, prázdny `workspace.json` a `schedules.json`.
  - `LoadAsync` / `SaveAsync`, `LoadSchedulesAsync` / `SaveSchedulesAsync`.
  - `AddRunAsync(Run)` – append do `runs/YYYY-MM.json`; `GetRunsAsync(scriptId, take)`.
  - CRUD: Notebook, Section, Script (Script = metadáta + vytvorenie fyzického súboru, ak neexistuje).
  - `FindScript(id)`, `GetAbsolutePath(script)`.
  - Migrácia: kontrola poľa `"version"`; neznáma verzia → zrozumiteľná výnimka.
- Thread-safety: `SemaphoreSlim` okolo zápisov (Runner a app môžu zapisovať do `runs/` súčasne – pri `IOException` 3× retry s krátkym oneskorením).

**Testy (xUnit, dočasný priečinok):** create workspace, round-trip JSON, atomický zápis vytvorí `.bak`, AddRun do správneho mesiaca, CRUD + `SortOrder`.

**Akceptácia:** všetky testy zelené.

---

### M2 – Strom hierarchie + CRUD v UI (≈ 2–3 dni)

**Úlohy**
- `MainViewModel` (CommunityToolkit.Mvvm): aktívny workspace, `ObservableCollection` pre strom (wrapper VM pre Notebook/Section/Script s `IsExpanded`, `IsSelected`).
- `TreeView` s `HierarchicalDataTemplate` v štýle sidebaru z mockupu `MainWindow.xaml`, ikony Segoe MDL2 Assets, kontextové menu: Nový / Premenovať / Zmazať (s potvrdením).
- Prvé spustenie: dialóg „Vyber priečinok workspace“ → `ConfigService` + `WorkspaceService.OpenOrCreateAsync`.
- Prepínač workspaces (ComboBox v hornej lište) + „Pridať workspace…“.
- Vyhľadávanie (TextBox → filter podľa názvu, tagov, popisu; rozbalí zhody).
- Drag & drop v strome (presun skriptu medzi sekciami, sekcie medzi notebookmi) – mení **len JSON**.
- Import: drag & drop `.ps1/.bat/.cmd/.sh` súborov do sekcie; „Importovať priečinok…“ → sken, podpriečinky 1. úrovne = Notebooky, 2. úrovne = Sekcie, ostatné do sekcie „Nezaradené“. Súbory mimo workspace: spýtať sa, či skopírovať do workspace, alebo odkazovať absolútnou cestou (MVP: kopírovať).
- Meta panel: Notebook, Sekcia, jazyk, tagy, popis (editovateľné, uloží sa do JSON).

**Akceptácia:** vytvorím workspace, 2 notebooky, 3 sekcie, importujem priečinok skriptov; po reštarte app je všetko na mieste; `workspace.json` je čitateľný a validný.

---

### M3 – Editor (≈ 2 dni)

**Úlohy**
- AvalonEdit v strednom paneli, `TabControl` pre viac otvorených skriptov (`ScriptEditorViewModel` per záložka).
- `.xshd` pre PowerShell, Batch, Bash v `Resources/Highlighting/` (embedded resource), registrácia podľa `ScriptLanguage`.
- Čísla riadkov, Ctrl+F/H (SearchPanel), Ctrl+S uloží working copy, font Cascadia Code / Consolas.
- Dirty flag `●` v záložke; pri zatváraní záložky/app s neuloženými zmenami potvrdenie.
- `FileSystemWatcher` na otvorené súbory: ak sa zmenia externe (VS Code) a v editore nie sú zmeny → reload; inak ponúknuť výber.
- Status bar: jazyk, verzia `[v4≠v3]`, riadok/stĺpec, kódovanie.

**Akceptácia:** otvorím 3 skripty, editujem, uložím; externá zmena vo VS Code sa prejaví v app.

---

### M4 – Runner (manuálne spustenie) + história (≈ 2 dni)

**Úlohy**
- `Skriptec.Core/Services/ScriptRunnerService`:
  - `RunAsync(script, parameters, IProgress<OutputLine>, CancellationToken)` → `RunResult` (exitCode, durationMs, output).
  - PS1: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File "…"` (voliteľne `pwsh.exe`, ak je v configu).
  - BAT/CMD: `cmd.exe /c "…"`.
  - SH: detekcia Git Bash / WSL (`RuntimeDetector`), cesta z `config.runner.bashPath`; ak chýba → zrozumiteľná chyba.
  - Pracovný adresár = priečinok skriptu; UTF-8 výstup; stdout/stderr rozlíšené; Stop = kill procesového stromu.
  - Limit uloženého výstupu do histórie (napr. posledných 64 kB).
- UI: tlačidlo ▶ Spustiť / ■ Zastaviť v toolbare, výstupný panel pod editorom (GridSplitter), stderr červenou.
- Detekcia `param()` bloku v PS1 (System.Management.Automation nepoužívať – regex/jednoduchý parser) → dialóg parametrov pred spustením.
- Každý beh → `AddRunAsync`; Meta panel zobrazí posledných 10 behov (✓/✗, čas, trvanie), klik = zobrazí výstup.

**Testy:** spustenie testovacieho `.ps1` a `.bat` (exit 0 aj ≠ 0), zápis Run.

**Akceptácia:** spustím PS1 s dlhým výstupom – UI nezamrzne, výstup tečie priebežne, beh je v histórii.

---

### M5 – Verzionovanie (≈ 1–2 dni)

**Úlohy**
- `VersioningService` (Core):
  - `SaveVersionAsync(script)` → kópia do `<dir>/.versions/<name>_v{N}.<ext>`, `currentVersion = N`.
  - `ListVersions(script)`, `RevertToAsync(script, version)` (prepíše working copy, pred tým uloží aktuálnu ako novú verziu, ak je zmenená).
  - `IsWorkingCopyModified(script)` – porovnanie hashu s poslednou verziou.
- UI: „Uložiť verziu“ v toolbare; Meta panel – zoznam verzií (aktuálna ●, nasadená ○ „nasad.“); jednoduchý diff (dvojpanelový, riadkový – bez externých knižníc, alebo DiffPlex ak treba).
- Indikátory v strome: `[vN] ●` (current > deployed), `[vN]` (rovnaké), `✎` (nenaplánovaný).

**Akceptácia:** uložím v1, v2, v3; revert na v1 funguje; indikátory sedia s `workspace.json`.

---

### M6 – Plánovanie + Skriptec.Runner CLI (≈ 3 dni)

**Úlohy – Runner CLI**
- `skriptec-runner.exe --script-id <id> --workspace "<path>" [--schedule-id <id>]`.
- Načíta workspace + schedule, spustí **nasadený súbor** (`deployedFile`) cez `ScriptRunnerService`, zapíše Run (s `trigger = "scheduled"`), log chýb do `%AppData%\Skriptec\logs\runner-YYYY-MM.log`.
- Exit code = exit code skriptu; žiadne UI okno (`OutputType Exe`, ale spúšťať s `CreateNoWindow`/skrytým režimom v úlohe).

**Úlohy – SchedulerService (Core)**
- `DeployAsync(script)` → kópia working copy do `%AppData%\Skriptec\scheduled\script_<id>_v{N}.<ext>`, `deployedVersion = N`.
- `CreateOrUpdateTaskAsync(schedule)` cez NuGet `TaskScheduler`: priečinok úloh `\Skriptec\`, názov `Skriptec_<scheduleId>`, akcia = `skriptec-runner.exe` s argumentmi, triggery: Daily, Weekly (dni), Interval (každých N min/h), OnBoot, OnLogon. Spúšťať pod aktuálnym používateľom („Run only when user is logged on“ v MVP – bez ukladania hesla).
- `Enable/Disable/Delete`, `GetNextRunTime`, synchronizácia: pri štarte app porovnať `schedules.json` s úlohami v `\Skriptec\` a upozorniť na nezhody.
- Zrozumiteľný popis triggera v slovenčine („Každý pracovný deň o 9:00“).

**Úlohy – UI**
- `SchedulerWindow` (dialóg) prenesený z `design/xaml/SchedulerWindow.xaml` a napojený na `SchedulerViewModel`: výber typu triggera, čas/dni/interval, parametre, náhľad popisu + ďalší beh.
- Tlačidlá „Naplánovať“ a „Nasadiť vN“ (ak current > deployed).
- Meta panel: sekcia Plán (popis, ďalší beh, zapnuté/vypnuté).

**Akceptácia:** naplánujem skript na „každé 2 minúty“, zavriem app, po 2 min je v `runs/` nový záznam; po úprave a „Nasadiť v2“ beží nová verzia; úloha je viditeľná v Plánovači úloh Windows v priečinku `Skriptec`.

---

### M7 – Stabilizácia a release MVP (≈ 2 dni)

**Úlohy**
- Globálny handler výnimiek (`DispatcherUnhandledException`, `TaskScheduler.UnobservedTaskException`) → dialóg + log.
- Obnova po zmazanom `.skriptec/`: ponúknuť re-scan priečinka.
- Potvrdenie pred zmazaním; zmazanie skriptu z hierarchie sa spýta, či zmazať aj súbor.
- Publikovanie: `dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true` pre app aj Runner do jedného priečinka; README s návodom.
- Smoke test checklist (sekcia 6) prejdený ručne.

**Akceptácia:** zip s release buildom funguje na čistom Windows 11 s nainštalovaným .NET 10 Desktop Runtime.

---

### Po MVP (podľa `tasks.md`)
M8 Wizard (5 šablón, UI z `design/xaml/WizardWindow.xaml`) → M9 Export/Import `.scriptpack` → M10 File-change trigger → M11 Šablóny (GitHub Releases CDN) → M12 Licencie.

---

## 5. Odhad

| Etapa | Dni (čistá práca s Claude Code) |
|-------|------|
| M0 | 0,5 |
| M1 | 1–2 |
| M2 | 2–3 |
| M3 | 2 |
| M4 | 2 |
| M5 | 1–2 |
| M6 | 3 |
| M7 | 2 |
| **Spolu** | **≈ 14–17 dní** |

---

## 6. Smoke test MVP

- [ ] Prvé spustenie → výber priečinka → vznikne `.skriptec/`
- [ ] Import priečinka s 10+ skriptami → správna hierarchia
- [ ] Presun skriptu drag & drop → súbor na disku ostal na mieste
- [ ] Editácia + Ctrl+S + externá zmena vo VS Code
- [ ] Spustenie PS1, BAT, SH (ak je Git Bash) s výstupom naživo
- [ ] Skript s `param()` → dialóg parametrov
- [ ] Uloženie verzií v1–v3, revert, indikátor `[v3] ●`
- [ ] Naplánovanie (interval 2 min) pri zatvorenej app → záznam v `runs/`
- [ ] Nasadenie novej verzie → beží nová verzia
- [ ] Druhý workspace + prepnutie
- [ ] Reštart app → všetko obnovené

---

## 7. Ako to spustiť v Claude Code (VS Code)

1. Vytvor prázdny priečinok repozitára, nakopíruj doň `CLAUDE.md`, `architecture.md`, `decisions.md`, `tasks.md`, `MVP-PLAN.md` a rozbaľ `ScriptMaster-WPF-XAML.zip` do `design/xaml/`.
2. Otvor priečinok vo VS Code, spusti Claude Code (panel alebo terminál `claude`).
3. Prvý prompt:

```
Prečítaj CLAUDE.md, MVP-PLAN.md a súbory v design/xaml/. Vykonaj etapu M0 presne podľa plánu.
Na konci spusti dotnet build, ukáž výsledok, aktualizuj tasks.md a sprav commit "M0: Solution setup".
Potom sa zastav a zhrň, čo je hotové.
```

4. Ďalšie etapy rovnako: `Vykonaj etapu M1 podľa MVP-PLAN.md …` – vždy jedna etapa, po nej kontrola a commit.
5. Pri väčších etapách (M2, M6) odporúčam najprv: `Navrhni plán implementácie etapy M2 (súbory, triedy, poradie) a počkaj na schválenie.` (Plan mode – Shift+Tab.)

**Predpoklady na PC:** Windows 10/11, .NET 10 SDK, VS Code + C# Dev Kit, Git, voliteľne Git Bash, Node.js (pre Claude Code).
