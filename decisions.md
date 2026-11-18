# Skriptec – Rozhodnutia a ich kontext

> ADR (Architecture Decision Records) — prečo sa čo rozhodlo

---

## ADR-001: WPF namiesto WinUI 3 alebo .NET MAUI

**Rozhodnutie:** WPF (.NET 10)

**Kontext:**
Aplikácia potrebuje embedded editor so syntax highlightingom. Najlepšia dostupná knižnica je AvalonEdit, ktorá má natívnu WPF podporu.

**Dôvody:**
- AvalonEdit funguje v WPF priamo — žiadne workaroundy
- WinUI 3 by vyžadoval WebView2 pre editor (~150 MB Chromium overhead)
- Obrovská komunita, Stack Overflow plný riešení pre WPF
- Moderný dizajn riešiteľný cez WPF-UI / ModernWPF knižnicu
- Aplikácia je Windows-only — MAUI overhead (cross-platform) nepotrebujeme

**Zamietnuté alternatívy:**
- WinUI 3 — menšia komunita, problém s AvalonEdit
- .NET MAUI — cross-platform overhead, WebView2 pre editor
- Monaco Editor (VS Code) — 150 MB Chromium runtime, nepohodlná C# ↔ JS komunikácia

---

## ADR-002: JSON súbory namiesto SQLite

**Rozhodnutie:** Dáta uložené v JSON súboroch v rámci workspace priečinka. Žiadna SQLite databáza.

**Kontext:**
Pôvodne bol navrhnutý SQLite s interným uložením obsahu skriptu. Po analýze požiadaviek na sync, editovateľnosť a portabilitu sa rozhodlo pre JSON súbory.

**Štruktúra:**
```
<workspace>/.skriptec/
├── workspace.json      ← hierarchia, metadáta skriptov
├── schedules.json      ← plány spustení
└── runs/
    └── YYYY-MM.json    ← história behov per-mesiac
```

**Dôvody pre JSON:**
- Skripty sú fyzicky na disku — používateľ ich vie otvoriť vo VS Code, Notepad++ bez exportu
- Workspace = priečinok ktorý si používateľ sám zvolí
- Záloha = skopíruj priečinok (vrátane `.skriptec/`)
- Git sync — JSON súbory sú text, `git diff` funguje krásne
- OneDrive/Dropbox sync bez špeciálnej logiky v app
- Žiadna externá závislosť (SQLite NuGet, Dapper)
- Čitateľné bez špeciálnych nástrojov

**Kompromisy:**
- Súbežný prístup (dvoch PC naraz) — riešené atomickým zápisom cez `.tmp`
- Výkon pri tisíckach Runs — riešené mesačnými súbormi (`runs/YYYY-MM.json`)
- Migrácia schémy — riešená `"version"` poľom v každom súbore

**Zamietnuté:**
- SQLite — binárny formát, ťažko syncovateľný, vyžaduje export na externé editovanie
- YAML — citlivý na odsadenie, horšia .NET podpora
- XML — príliš verbose

---

## ADR-003: AvalonEdit ako editor komponent

**Rozhodnutie:** AvalonEditB NuGet package (fork pre .NET 6+)

**Kontext:**
Editor so syntax highlightingom je kľúčová funkcia. Možnosti: AvalonEdit, Monaco, Scintilla, RichTextBox.

**Dôvody:**
- Open source (MIT), aktívne udržiavaný (release 2024)
- Používaný v SharpDevelop, ILSpy, RoslynPad — produkčne overený
- Syntax highlighting cez `.xshd` XML súbory — hotové definície na GitHub
- SearchPanel (Ctrl+F/H), FoldingManager, UndoStack — všetko built-in
- Integrácia za ~20 minút: NuGet → XAML tag → beží

**Zamietnuté:**
- Monaco Editor — Chromium overhead, JS bridge
- Scintilla.NET — slabšia WPF dokumentácia
- RichTextBox — žiadny syntax highlighting

---

## ADR-004: Freemium + jednorazová Pro licencia

**Rozhodnutie:** Free tier + Pro jednorazová platba (15–25 EUR)

**Kontext:**
Monetizácia ovplyvňuje čo je v MVP a čo za paywall.

**Dôvody:**
- Freemium = nízka bariéra vstupu, organický rast
- Jednorazová platba = používatelia desktop apps neradi platia predplatné
- Žiadny cloud account pre basic tier = jednoduchosť
- Licenčný kľúč = jednoduchá implementácia

**Free tier obsahuje:**
- Hierarchia + editor + manuálne spúšťanie
- Max 3 jazyky (PS, Batch, Bash)
- Max 20 free šablón

**Pro tier obsahuje:**
- Plánovanie/cron všetky triggery
- Pokročilé šablóny (DevOps, DB, monitoring)
- Export .scriptpack
- História spustení (neobmedzená)
- AI asistent (Claude API)

**DÔLEŽITÉ:** V MVP sa paywall vôbec nekóduje. `IsPremium = false` flag stačí. Najprv validuj produkt.

---

## ADR-005: PowerShell + Batch pre MVP, Bash voliteľný

**Rozhodnutie:** PS1 + BAT/CMD povinné, .sh voliteľné s detekciou WSL/Git Bash

**Kontext:**
Cieľová platforma je Windows. Každý pridaný jazyk = viac práce na editore, runneri a wizard templatech.

**Dôvody:**
- PowerShell: primárny skriptovací jazyk Windows, žiadna závislosť
- Batch: najjednoduchší, stovky starých firemných skriptov, nulová závislosť
- Bash: relevantný aj na Windows (WSL, Git Bash) — ale závisí od runtime
- Python/Node: vyžadujú externý runtime — komplikuje distribúciu → V2

**Implementácia Bash detekcie:**
```csharp
var hasBash = File.Exists(@"C:\Windows\System32\bash.exe") ||    // WSL
              Directory.Exists(@"C:\Program Files\Git\bin");     // Git Bash
```

---

## ADR-006: Statický CDN pre šablóny (MVP)

**Rozhodnutie:** GitHub Releases ako CDN, statický JSON index

**Kontext:**
Databáza šablón potrebuje backend. Možnosti: vlastný REST API, statický CDN, GitHub.

**Dôvody pre statický CDN:**
- GitHub Releases = zadarmo, spoľahlivé, bez servera
- Nulové náklady na infraštruktúru pre MVP
- JSON index aktualizovateľný manuálne (push nového release)
- HttpClient + ZipArchive = menej ako 200 riadkov kódu

**Postup migrácie na V2:**
- Keď je >500 šablón alebo komunitné nahrávanie → REST API (Azure/Cloudflare Workers)
- Hodnotenia + komentáre → databáza na serveri

---

## ADR-007: MVVM pattern pre WPF

**Rozhodnutie:** MVVM (Model-View-ViewModel)

**Kontext:**
WPF podporuje priamy code-behind aj MVVM. Pre aplikáciu tejto komplexnosti je vzor dôležitý.

**Dôvody:**
- Testovateľnosť — ViewModely sa dajú unit testovať bez UI
- Separation of concerns — UI logika oddelená od biznis logiky
- Data binding je silná stránka WPF — MVVM ju plne využíva
- Štandard pre WPF aplikácie — dokumentácia + komunita

**Implementácia:**
- Bez extra frameworku (CommunityToolkit.Mvvm voliteľne)
- `INotifyPropertyChanged` priamo alebo cez `ObservableObject`
- Commands cez `RelayCommand` / `ICommand`

---

## ADR-008: Trojúrovňová fixná hierarchia (bez neobmedzenej hĺbky)

**Rozhodnutie:** Presne 3 úrovne — Notebook → Section → Script, s voliteľnou nadúrovňou Workspace

**Kontext:**
Alternatíva: self-referenčná štruktúra s neobmedzenou hĺbkou.

**Dôvody:**
- OneNote model je používateľom intuitívne zrozumiteľný
- Jednoduchšia JSON štruktúra — vnorené objekty namiesto relácií
- Prevažná väčšina use-cases nepotrebuje hlbšiu hierarchiu
- Viacero workspaces nahrádza potrebu ďalšej úrovne

**Workspaces:**
- Každý workspace = samostatný priečinok + vlastný `workspace.json`
- Prepínanie workspaces = načítanie iného súboru
- Umožňuje úplnú izoláciu (napr. Práca vs. Domácnosť)

---

## ADR-009: Tri-projektová Solution štruktúra

**Rozhodnutie:** `Skriptec` (WPF) + `Skriptec.Core` (shared) + `Skriptec.Runner` (CLI)

**Kontext:**
TaskScheduler musí spúšťať skripty aj keď je hlavná app zatvorená. Skript potrebuje zapísať výsledok do `runs/` JSON súborov.

**Dôvody:**
- `Skriptec.Runner.exe` je malý CLI bez UI — volá ho TaskScheduler
- Zdieľa `WorkspaceService` a `ScriptRunnerService` z `Skriptec.Core`
- Hlavná app nemusí bežať na pozadí
- TaskScheduler volá: `skriptec-runner.exe --script-id s_abc --workspace "C:\Scripts"`

**Alternatíva zamietnutá:**
- Runtime `.ps1` súbory zapisujúce výsledok priamo — ťažko udržiavateľné, PowerShell-only riešenie

---

## ADR-010: Lokálne verzionovanie skriptov

**Rozhodnutie:** Snapshoty verzií v `.versions/` priečinku vedľa skriptu, metadáta v `workspace.json`

**Kontext:**
Používateľ chce upravovať skript kým naplánovaná verzia beží. Potrebuje vedieť ktorá verzia je "nasadená".

**Mechanizmus:**
- `working copy` = súbor na disku (editovateľný kedykoľvek)
- `deployed version` = snapshot v `%AppData%\Skriptec\scheduled\` — túto volá TaskScheduler
- Pri "Nasadiť" = skopíruj working copy do `scheduled\`, aktualizuj `deployedVersion` v JSON

**Dôvody:**
- Používateľ môže bezpečne experimentovať — produkčná verzia beží ďalej
- Jasný stav v UI: `[v4] ●` = máš novšiu verziu ako je nasadená
- Jednoduché rollback: klikni na staršiu verziu → nasaď

---

## Súhrn všetkých rozhodnutí

| # | Rozhodnutie | Zvratiteľnosť |
|---|-------------|---------------|
| ADR-001 | WPF namiesto WinUI 3 | Stredná (veľká práca) |
| ADR-002 | JSON súbory namiesto SQLite | Stredná |
| ADR-003 | AvalonEdit editor | Stredná |
| ADR-004 | Freemium + jednorazová licencia | Vysoká |
| ADR-005 | PS + Batch MVP, Bash voliteľný | Vysoká |
| ADR-006 | Statický CDN pre šablóny | Vysoká |
| ADR-007 | MVVM pattern | Stredná |
| ADR-008 | 3-úrovňová hierarchia + Workspaces | Stredná |
| ADR-009 | Tri-projektová Solution | Stredná |
| ADR-010 | Lokálne verzionovanie skriptov | Vysoká |
