# FileCompare

A Blazor Server single-page reconciliation tool that compares a "Convertor output file" against a "Client expected file" (delimited text or Excel workbooks, first row = headers), matches rows on a user-selected composite key, compares one numeric column, and shows the results as an expandable tree table grouped by key.

## Solution layout

```
FileCompare.sln
src/FileCompare/            Blazor Server app (net8.0)
  Models/                   ParsedFile, RowResult, ComparisonResult, ...
  Services/                 FileParserService, ComparisonService, GroupingService, ExcelExportService
  Components/Pages/Home.razor(.cs)   Single-page workflow
  Components/FileUpload/    FileDropUpload, FileTypeSelector, DualListKeySelector, CompareColumnSelector, GroupingSettings
  Components/ResultTree/    ResultTreeTable, CategoryNode, GroupNode
tests/FileCompare.Tests/    xUnit tests for the Services layer
SampleData/                 Sample Convertor/Client files for manual testing
```

## Running the app

Requires the .NET 8 SDK (or later).

```bash
dotnet run --project src/FileCompare
```

Then open the printed `https://localhost:xxxx` URL. Upload `SampleData/Convertor.csv` as the "Convertor output file" and `SampleData/Client.csv` as the "Client expected file" to try the full workflow — the sample data covers Matching, Different (across all default buckets), Added, and Deleted rows.

To see the header-mismatch error banner, try uploading `SampleData/Convertor_HeaderMismatch.csv` (its last column is named `Amount` instead of `Betrag`) alongside `SampleData/Client.csv`.

## Running the tests

```bash
dotnet test
```

## Workflow

1. **Select the input file type**: "CSV" (comma-delimited, `.csv`), "Delimited text" (default: semicolon, with a Semicolon/Tab/Pipe delimiter picker), or "Excel workbook (.xlsx)". Both files must use the same type/delimiter. Changing this after files are uploaded automatically re-parses and re-validates them.
2. **Upload** both files (browse or drag-and-drop).
3. Headers are validated — if the column sets don't match exactly, comparison is blocked with an inline error. Header names are compared case-sensitively, except that Ä/ä, Ö/ö, and Ü/ü are treated as equivalent case pairs.
4. **Select key column(s)** in the dual-list selector (double-click also moves an item between lists); `PersonalNr`, `Lohnart`, and `TextLohnart` are pre-selected by default if present. Reorder with the up/down arrows to set key precedence.
5. **Select the compare column** — only numeric, non-key columns are offered; `Betrag` is pre-selected by default if present and numeric.
6. **Configure difference-magnitude grouping** (default: a single group, boundary 0.5) — these buckets can be changed and re-applied after a comparison without re-uploading files.
7. Click **Compare**. Results appear as an expandable tree: Matching / Added / Deleted / Different at the top level (Different further split by magnitude bucket), then drilled down by each key column's values down to the individual row(s).
8. Optionally **Download report** to export the result as a multi-sheet Excel workbook (`comparison-report.xlsx`): a **Summary** sheet (counts + grouping boundaries used + any warnings), then **Matching**, **Added**, **Deleted** sheets, then one sheet per difference-magnitude bucket (e.g. "Diff 0-0.5", "Diff gt 0.5"), each with an `AbsoluteDifference` column.

Every result sheet (and the on-page tree table) shows columns in this order: key columns, `{compare column} (Convertor)`, `{compare column} (Client)`, an `AbsoluteDifference` column (bucket sheets only), then a single combined **Other columns** column — all remaining non-key, non-compare columns joined as `Name: Value; Name: Value`.

### Delimited-text encoding

Delimited-text files are read as UTF-8 (BOM-aware) by default, with an automatic fallback to Windows-1252/ISO-8859-1 if the bytes aren't valid UTF-8 — so German umlaut characters (ä, ö, ü, Ä, Ö, Ü, ß) round-trip correctly whichever encoding the file was saved in. Excel-workbook input/output is natively Unicode and needs no special handling.

## Notes

- No database — everything is in-memory, driven by plain C# services that are independently unit-tested (no Blazor dependency).
- Row matching uses dictionary lookups (O(n)), and parsing/comparison run off the UI thread via `Task.Run` so the page stays responsive on large files.
- Max upload size is set to 50 MB per file (`FileDropUpload`'s `InputFile` read limit).
- Excel workbook input/export uses [ClosedXML](https://github.com/ClosedXML/ClosedXML) (MIT).
