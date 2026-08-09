# FileCompare

A reconciliation tool that compares a "Converter output file" against a "Client expected file" (CSV or delimited text, first row = headers), matches rows on a user-selected composite key, compares one numeric column, and shows the results as an expandable tree grouped by key. Ships as two front ends over the same core logic: a Blazor Server web app, and a Windows Forms desktop app ("M&S Converter Output Compare").

## Solution layout

```
FileCompare.sln
src/FileCompare.Core/                     Shared library (net8.0) — no UI dependency
  Models/                                 ParsedFile, RowResult, ComparisonResult, ...
  Services/                               FileParserService, ComparisonService, GroupingService, ExcelExportService
src/FileCompare/                          Blazor Server app (net8.0)
  Components/Pages/Home.razor(.cs)        Single-page workflow
  Components/FileUpload/                  FileDropUpload, FileTypeSelector, DualListKeySelector, CompareColumnSelector, GroupingSettings
  Components/ResultTree/                  ResultTreeTable, CategoryNode, GroupNode
src/M&S Converter Output Compare/         Windows Forms desktop app (net8.0-windows)
  Controls/                               FileUploadPanel, DualListKeySelector, GroupingSettingsControl
  MainForm.cs                             Single-window workflow (mirrors Home.razor.cs)
tests/FileCompare.Tests/                  xUnit tests for the Services layer (FileCompare.Core)
SampleData/                               Sample Converter/Client files for manual testing
```

Both front ends reference `FileCompare.Core` and share 100% of the parsing/matching/grouping/export logic — only the UI layer differs.

## Running the web app

Requires the .NET 8 SDK (or later).

```bash
dotnet run --project src/FileCompare
```

Then open the printed `https://localhost:xxxx` URL. Upload `SampleData/Converter.csv` as the "Converter output file" and `SampleData/Client.csv` as the "Client expected file" to try the full workflow — the sample data covers Matching, Different (across all default buckets), Added, and Deleted rows.

To see the header-mismatch error banner, try uploading `SampleData/Converter_HeaderMismatch.csv` (its last column is named `Amount` instead of `Betrag`) alongside `SampleData/Client.csv`.

## Running the desktop app (Windows only)

```bash
dotnet run --project "src/M&S Converter Output Compare"
```

The workflow is the same as the web app (see below), laid out top-to-bottom in a single resizable window instead of a web page: a scrollable configuration panel on top, and a tree (left) + grid (right) results view on the bottom — select any tree node to see its rows in the grid on the right.

## Running the tests

```bash
dotnet test
```

## Workflow

1. **Select the input file type**: "CSV" (comma-delimited, `.csv`) or "Delimited text" (default: semicolon, with a Semicolon/Tab/Pipe delimiter picker). Both files must use the same type/delimiter. Changing this after files are uploaded automatically re-parses and re-validates them.
2. **Upload** both files (browse or drag-and-drop).
3. Headers are validated — if the column sets don't match exactly, comparison is blocked with an inline error. Header names are compared case-sensitively, except that Ä/ä, Ö/ö, and Ü/ü are treated as equivalent case pairs.
4. **Select key column(s)** in the dual-list selector (double-click also moves an item between lists); `PersonalNr`, `Lohnart`, and `TextLohnart` are pre-selected by default if present. Reorder with the up/down arrows to set key precedence.
5. **Select the compare column** — only numeric, non-key columns are offered; `Betrag` is pre-selected by default if present and numeric.
6. **Configure difference-magnitude grouping** (default: a single group, boundary 0.5) — these buckets can be changed and re-applied after a comparison without re-uploading files.
7. Click **Compare**. Results appear as an expandable tree: Matching / Added / Deleted / Different at the top level (Different further split by magnitude bucket), then drilled down by each key column's values down to the individual row(s).
8. Optionally **Download report** to export the result as a multi-sheet Excel workbook (`comparison-report.xlsx`): a **Summary** sheet (counts, with Added/Deleted labeled "Added (Rows found only in the Converter output file)" / "Deleted (Rows found only in the Client expected file)"; the grouping boundaries used, each with its own row count; and any warnings), then **Matching**, **Added**, **Deleted** sheets, then one sheet per difference-magnitude bucket (e.g. "Diff 0-0.5", "Diff gt 0.5"), each with an `AbsoluteDifference` column.

Every result sheet (and the on-page tree table) shows columns in this order: key columns, `{compare column} (Converter)`, `{compare column} (Client)`, an `AbsoluteDifference` column (bucket sheets only), then a single combined column for all remaining non-key, non-compare columns — its header is the joined column names (e.g. `Name; Department`) and each row's value is just the joined values in that order (e.g. `Alice; Sales`), with no column-name prefix.

### Delimited-text encoding

Delimited-text files are read as UTF-8 (BOM-aware) by default, with an automatic fallback to Windows-1252/ISO-8859-1 if the bytes aren't valid UTF-8 — so German umlaut characters (ä, ö, ü, Ä, Ö, Ü, ß) round-trip correctly whichever encoding the file was saved in. The exported `.xlsx` report is natively Unicode and needs no special handling on output.

## Notes

- No database — everything is in-memory, driven by plain C# services in `FileCompare.Core` that are independently unit-tested (no UI dependency at all).
- Row matching uses dictionary lookups (O(n)), and parsing/comparison run off the UI thread (`Task.Run`) in both apps so the UI stays responsive on large files.
- Web app: max upload size is set to 50 MB per file (`FileDropUpload`'s `InputFile` read limit). Desktop app: no explicit size cap (limited only by available memory).
- The `.xlsx` export uses [ClosedXML](https://github.com/ClosedXML/ClosedXML) (MIT).
