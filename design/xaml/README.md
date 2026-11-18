# ScriptMaster – WPF XAML súbory

## Súbory

| Súbor | Popis |
|-------|-------|
| `MainWindow.xaml` | Hlavné okno — sidebar strom, editor, meta panel |
| `SchedulerWindow.xaml` | Dialóg plánovania spúšťania skriptov |
| `WizardWindow.xaml` | Sprievodca tvorbou nového skriptu (5 krokov) |

## Ako použiť

1. Skopíruj súbory do WPF projektu (`dotnet new wpf -n ScriptMaster`)
2. V `MainWindow.xaml` nahraď `TextBox` s AvalonEdit editorom:
   - NuGet: `Install-Package AvalonEditB`
   - Pridaj namespace: `xmlns:ae="http://icsharpcode.net/sharpdevelop/texteditor"`
   - Nahraď TextBox za: `<ae:TextEditor x:Name="CodeEditor" .../>`

3. Ikony: súbory používajú **Segoe MDL2 Assets** — táto fonts je štandardná súčasť Windows 10/11

4. Pre moderný Fluent dizajn nainštaluj: `Install-Package WPF-UI`
   - Potom môžeš nahradiť `Window` za `ui:FluentWindow`

## Závislossti (NuGet)

```
Install-Package AvalonEditB          # Editor so syntax highlighting
Install-Package Microsoft.Data.Sqlite # SQLite databáza
Install-Package TaskScheduler        # Windows Task Scheduler wrapper
Install-Package WPF-UI               # Moderný Fluent dizajn (voliteľné)
```

## Poznámky

- Všetky farby sú definované ako Resources v `Window.Resources`
- GridSplitter umožňuje resize sidebar a meta panelu
- `x:Name` atribúty slúžia ako referencia z code-behind (.cs)
- Štýly sú lokálne v každom okne — pre produkciu ich presuň do `App.xaml`
