# Skriptec – TODO & otvorené otázky

> Aktualizované: Máj 2026 | Fáza: Pre-development
> Poradie implementácie MVP riadi `MVP-PLAN.md` (etapy M0–M7).

---

## ✅ Dokončené (z analýzy)

- [x] Definovaná vízia produktu a odlíšenie od konkurencie
- [x] Navrhnutá trojúrovňová hierarchia (OneNote model) + Workspaces
- [x] Rozhodnutý dátový model (JSON súbory, bez SQLite)
- [x] Vybraný UI framework (WPF)
- [x] Vybraný editor komponent (AvalonEdit)
- [x] Navrhnutý model monetizácie (Freemium + Pro)
- [x] Definované MVP jazyky (PS, Batch, Bash)
- [x] Navrhnutý formát .scriptpack
- [x] Navrhnutá architektúra databázy šablón (CDN)
- [x] Navrhnutý systém verzionovania skriptov (working copy vs. deployed)
- [x] Navrhnutá Solution štruktúra (Core + Runner + WPF)
- [x] Vytvorené WPF XAML mockupy (MainWindow, Scheduler, Wizard)
- [x] Vytvorená projektová dokumentácia

---

## 🔴 Kritické — pred začatím kódovania

- [x] **Setup projektu (M0)** — Solution `Skriptec.sln` so 4 projektmi (`Skriptec`, `Skriptec.Core`, `Skriptec.Runner`, `Skriptec.Core.Tests`), všetky `net10.0-windows`, referencie Skriptec→Core, Runner→Core, Tests→Core
- [x] **NuGet balíky (M0):** Skriptec: AvalonEditB, WPF-UI, CommunityToolkit.Mvvm · Core: TaskScheduler, CommunityToolkit.Mvvm
- [x] **Runner** — `AssemblyName=skriptec-runner`, výstup sa po builde kopíruje vedľa `Skriptec.exe`
- [x] **Directory.Build.props** (Nullable, ImplicitUsings, LangVersion latest; TreatWarningsAsErrors pre Core) + `.gitignore`
- [x] **Git repository** — inicializovaný, `.gitignore` pre .NET
- [x] **Štýly** — `Resources/Styles/Colors.xaml`, `Buttons.xaml`, `Typography.xaml` zlúčené v `App.xaml`
- [x] **MainWindow layout** — 3 panely, GridSplittery, top bar, tab bar, toolbar, status bar, `ae:TextEditor` (bez dát)
- [ ] **Wireframy** — 3 hlavné obrazovky (hlavné okno, editor s výstupom, dialóg plánovania)

---

## 🟡 Fáza 2 — Správa skriptov (prvý funkčný kód)

### Dátové modely (`Skriptec.Core/Models/`)
- [x] `WorkspaceData` / `Notebook` / `Section` / `Script` (`WorkspaceData.cs`) — M1
- [x] `Schedule`, `ScheduleTrigger`, `ScheduleFile` (`ScheduleFile.cs`) — M1
- [x] `Run`, `RunFile` (`RunFile.cs`) — M1
- [x] `AppConfig`, `WorkspaceRef` (`AppConfig.cs`), `ScriptLanguage`, `EntityBase`/`Ids` — M1

### WorkspaceService (`Skriptec.Core/Services/`)
- [x] `JsonStore` (`Storage/`) — atomický zápis, `.bak`, kontrola `version`, zámky + retry — M1
- [x] `WorkspaceService.cs` — M1:
  - [x] `OpenOrCreateAsync()` — vytvorí `.skriptec/`, `runs/`, `workspace.json`, `schedules.json`
  - [x] `LoadAsync()` / `SaveAsync()`
  - [x] `AddRunAsync(Run)` / `GetRunsAsync(scriptId, take)`
  - [x] `LoadSchedulesAsync()` / `SaveSchedulesAsync()`
  - [x] CRUD Notebook / Section / Script, `FindScript`, `GetAbsolutePath`
- [x] `ConfigService.cs` — čítanie/zápis `%AppData%\Skriptec\config.json`, add/remove/activate workspace — M1
- [x] Xunit testy (`Skriptec.Core.Tests`, 16 testov zelených) — M1

### UI — Strom hierarchie
- [ ] `MainViewModel.cs` — ObservableCollection pre strom, aktívny workspace
- [ ] `MainWindow.xaml` — TreeView s hierarchiou Notebook → Section → Script
- [ ] CRUD pre Notebook (pridaj, premenuj, zmaž)
- [ ] CRUD pre Section
- [ ] CRUD pre Script (bez editora)
- [ ] Drag & drop reorganizácia v strome (len metadáta v JSON — súbory sa nepresúvajú)
- [ ] Vyhľadávanie v strome (SearchBox → filter)
- [ ] Prepínač workspaces (dropdown v toolbar)
- [ ] Import existujúceho skriptu zo súboru (drag & drop .ps1/.sh/.bat)

---

## 🟡 Fáza 3 — Editor + Runner + Verzionovanie

### Editor
- [ ] Integrácia AvalonEdit do `MainWindow.xaml`
  - [ ] Načítať `.xshd` súbory pre PS, Batch, Bash
- [ ] Dirty flag v záložke (● pri neuložených zmenách)
- [ ] Auto-save pri prepnutí záložky
- [ ] Tab bar — viac otvorených skriptov naraz

### Verzionovanie
- [ ] Uložiť ako novú verziu → snapshot do `.versions/nazov_vN.ps1`
- [ ] Aktualizovať `currentVersion` v `workspace.json`
- [ ] UI panel verzií v Meta paneli (zoznam verzií, diff, revert)
- [ ] Indikátor stavu v strome: `[v4] ●` ak currentVersion > deployedVersion

### Runner (`Skriptec.Core/Services/`)
- [ ] `ScriptRunnerService.cs` — spúšťanie cez `Process`
  - [ ] Presmerovanie `StandardOutput` + `StandardError`
  - [ ] Asynchrónne čítanie (nie blokovanie UI)
  - [ ] Detekcia Bash runtime pred spustením `.sh` skriptu
- [ ] Výstupný panel pod editorom (stdout/stderr v reálnom čase)
- [ ] Uloženie výsledku do `runs/YYYY-MM.json`
- [ ] Dialóg parametrov pred spustením (ak skript má `param()` blok)

### Skriptec.Runner (CLI)
- [ ] `Program.cs` — CLI argument parsing (`--script-id`, `--workspace`)
- [ ] Načítanie workspace → nájdenie skriptu → spustenie → zápis Run
- [ ] Výstup do log súboru + `runs/YYYY-MM.json`

---

## 🟡 Fáza 4 — Wizard + Export + Plánovanie

### Wizard
- [ ] `WizardWindow.xaml` — navigácia medzi krokmi (krok 1–5)
- [ ] `WizardViewModel.cs` — state management
- [ ] Krok 1: Type selector (6 typov ako karty)
- [ ] Krok 2: Formuláre pre každý typ:
  - [ ] `BackupScriptTemplate.cs`
  - [ ] `RunProgramTemplate.cs`
  - [ ] `HttpRequestTemplate.cs`
  - [ ] `CleanupTemplate.cs`
  - [ ] `ShellCommandTemplate.cs`
- [ ] Krok 3: Parametre (auto-generovaný `param()` blok)
- [ ] Krok 4: Umiestnenie v hierarchii (výber Notebook/Section)
- [ ] Krok 5: Náhľad kódu → otvoriť v editore

### Export
- [ ] `ExportService.cs`:
  - [ ] Export surového skriptu (`File.Copy` — je fyzicky na disku)
  - [ ] Export zošita ako ZIP (priečinkový snapshot)
  - [ ] Export ako `.scriptpack` (ZIP + `manifest.json`)
  - [ ] Export histórie spustení (CSV / JSON z `runs/`)
- [ ] Import `.scriptpack` (drag & drop + file dialog)
- [ ] Import existujúceho priečinka skriptov (skenuj .ps1/.sh/.bat, vytvor hierarchiu)

### Plánovanie
- [ ] `SchedulerWindow.xaml` — kompletná navigácia triggerov
- [ ] `SchedulerService.cs` — TaskScheduler wrapper:
  - [ ] Pri naplánovaní: skopíruj working copy do `%AppData%\Skriptec\scheduled\script_id_vN.ext`
  - [ ] Registruj TaskScheduler task: `skriptec-runner.exe --script-id X --workspace Y`
  - [ ] Aktualizuj `deployedVersion` v `workspace.json`
  - [ ] Trigger: Plán (cron) — vizuálny konfigurátor → cron string
  - [ ] Trigger: OnBoot
  - [ ] Trigger: OnLogon
  - [ ] Trigger: FileSystemWatcher
- [ ] Live preview cron výrazu (generovanie z UI)
- [ ] Výpočet "ďalší beh" dátumu
- [ ] Aktualizácia nasadenej verzie — flow "Nasadiť v4"

---

## 🟢 Fáza 5 — Databáza šablón

- [ ] Vytvoriť GitHub repository pre šablóny
- [ ] `templates-index.json` — statický index šablón
- [ ] Nahrať prvých 10 free `.scriptpack` šablón
- [ ] `TemplateService.cs` — HttpClient sťahovanie
- [ ] `TemplateStoreWindow.xaml` — UI databázy šablón
- [ ] Import stiahnutej šablóny do hierarchie

---

## 🟢 Fáza 6 — Pro licencia

- [ ] `IsPremium` flag v `AppConfig`
- [ ] Licenčný kľúč validácia (offline hash check)
- [ ] UI pre zadanie licenčného kľúča
- [ ] Paywall dialóg pre Pro funkcie
- [ ] Integrácia platobného systému (Gumroad / Paddle / LemonSqueezy)

---

## ⚪ V2 — po MVP

- [ ] Git sync — auto-commit pri uložení verzie
- [ ] Python runtime detekcia a spúšťanie
- [ ] Node.js podpora
- [ ] Autokompletácia kľúčových slov v editore
- [ ] Code folding `{ }` blokov
- [ ] Inline chyby (červená vlnovka) — PSParser
- [ ] AI asistent — Claude API integrácia (Pro tier)
- [ ] REST API pre šablóny (namiesto statického CDN)
- [ ] Vlastný server + používateľské kontá (V3)
- [ ] Komunitné šablóny — nahrávanie používateľmi
- [ ] Dark mode

---

## ❓ Otvorené otázky

| Otázka | Priorita | Poznámka |
|--------|----------|----------|
| Súbežný zápis do `runs/` (Runner + app) | Vyriešené (M1) | `JsonStore` serializuje zápisy cez `SemaphoreSlim` (v procese) + lock súbor `*.lock` (medzi procesmi), retry 3× pri `IOException` |
| `AddWorkspaceAsync` aktivuje pridaný workspace | Rozhodnutie (M1) | Predpoklad: po „Pridať workspace…" sa naň rovno prepne; rovnaká cesta = len aktivácia |
| `WorkspaceRef.Id` prefix | Rozhodnutie (M1) | Plán prefix neuvádza; použité `ws_` podľa `architecture.md` |
| AvalonEditB XML namespace | Vyriešené (M0) | Plán uvádza `.../texteditor`, balík 2.5.0 používa `http://icsharpcode.net/sharpdevelop/avalonedit` – použitý ten |
| `TextTransform` v štýle `SectionLabel` | Vyriešené (M0) | Vlastnosť vo WPF neexistuje; odstránená, popisky sú písané VERZALKAMI priamo v XAML |
| `dotnet` nie je v PATH | Nízka | SDK 10.0.404 je v `C:\Program Files\dotnet`; treba pridať do PATH, resp. reštartovať terminál |
| `ui:FluentWindow` (WPF-UI) | Nízka | M0 ponecháva `Window` kvôli vizuálnej zhode s mockupom; WPF-UI je zatiaľ len nainštalované |
| Migrácia formátu JSON pri zmene schémy? | Stredná | `"version"` pole v každom súbore, migračný kód pri načítaní |
| Čo ak používateľ ručne zmaže `.skriptec/`? | Stredná | Ponúknuť re-scan priečinka a obnoviť metadáta |
| Automatická záloha `workspace.json`? | Nízka | `.bak` súbor pri každom atomickom zápise — stačí |
| Tray icon pre plánované skripty? | Stredná | Potrebné ak app beží na pozadí |
| MSIX packaging pre Windows Store? | Nízka | Distribučný kanál |
| Signing certifikát pre .exe? | Stredná | SmartScreen warning bez neho |
| Ako riešiť `.versions/` pri Git sync? | Stredná | Pridať do `.gitignore` alebo syncovať? |
| Maximálny počet skriptov? | Nízka | JSON zvládne tisíce bez problémov |
