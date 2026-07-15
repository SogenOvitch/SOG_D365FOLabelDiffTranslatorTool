# LabelDiffTool

A Windows desktop tool for comparing **D365 F&O label files** side by side, spotting the labels
missing from each side, and filling the gaps with free machine translation.

## Features

- **Open any number of files** (`*.label.txt`). A file is identified by its full path, so the same
  file can't be opened twice.
- Pick a **Source** and **Target** from dropdowns to diff any pair side by side; a **⇄** button
  swaps them, and clicking a file in the list sets it as the target.
- Compare by label id (case-insensitive ordering) and **highlight labels missing** from either side.
  Filter with **Gaps only** and/or **Needs saving only**.
- Language per file, auto-detected from the file name (`Foo.fr-FR.label.txt` → `fr-FR`) with a
  manual override.
- Translate using a **free** engine (GTranslate — Google/Bing/Yandex/Microsoft web endpoints, no API
  key). Four scopes:
  - selected label → the side that lacks it,
  - selected label → every other open file,
  - whole file → the target,
  - whole file → every other open file.
- **Reload** a file from disk, **Save** per file or **Save all** — always confirming each path.
- **Unsaved-changes protection**: reloading, closing a file, or quitting the app prompts to
  save / discard / cancel when there are pending translations. The status bar shows which files
  still need saving.
- A built-in **dark theme**.

### Copy rule (important)

When a label is copied from one file into another, the **label id is preserved exactly**, only the
**text is translated**, and the **description (`;` line) is carried over verbatim** — it is never
translated. Machine-translated labels are flagged until saved.

Files are always **saved sorted by label id** (the same order shown in the grid), so filled-in
labels are never just appended at the end.

## Structure

```
LabelDiffTool/
├─ src/
│  ├─ LabelDiffTool.Core/     Parsing, comparison, translation (no UI; N-file capable)
│  └─ LabelDiffTool.App/      WPF UI (MVVM) + dark theme
├─ tests/
│  └─ LabelDiffTool.Tests/    xUnit tests (parser, comparer, writer, batcher, translation service)
└─ samples/                   Example EN/FR label files
```

Both the comparison core and the UI are **N-file capable**.

## Translation batching

"Translate all missing" would otherwise fire one request per label and hit the free endpoint's rate
limits. Instead `TranslationBatcher`:

1. Packs labels into chunks whose joined length stays **≤ 3000 characters**.
2. Sends each chunk as **one** newline-joined request.
3. **Verifies** the response has the same number of lines; if not, it safely **falls back** to
   translating that chunk one label at a time — so a mangled delimiter can never write a translation
   onto the wrong label id.

Requests run with bounded concurrency and per-request retry/back-off.

## Build & run

```powershell
dotnet build
dotnet run --project src/LabelDiffTool.App
dotnet test
```

Requires the **.NET 8 SDK** with the Windows desktop workload (WPF).

## Swapping the translation engine

`ITranslationService` is the seam. `App.xaml.cs` wires up `GTranslateService`; replace it with a
LibreTranslate-backed implementation (self-hosted, more robust) without touching the UI or the
comparison logic.

## License

Released under the [MIT License](LICENSE).
