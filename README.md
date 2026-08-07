# FileCompare

A Blazor Server single-page reconciliation tool that compares a "Convertor output file" against a "Client expected file" (both semicolon-delimited, first line = headers), matches rows on a user-selected composite key, compares one numeric column, and shows the results as an expandable tree table grouped by key.

## Solution layout

```
FileCompare.sln
src/FileCompare/            Blazor Server app (net8.0)
  Models/                   ParsedFile, RowResult, ComparisonResult, ...
  Services/                 FileParserService, ComparisonService, GroupingService, ExcelExportService
  Components/Pages/Home.razor(.cs)   Single-page workflow
  Components/FileUpload/    FileDropUpload, DualListKeySelector, CompareColumnSelector, GroupingSettings
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

1. **Upload** both files (browse or drag-and-drop).
2. Headers are validated — if the column sets don't match exactly, comparison is blocked with an inline error.
3. **Select key column(s)** in the dual-list selector; `PersonalNr` and `Lohnart` are pre-selected by default if present. Reorder with the up/down arrows to set key precedence.
4. **Select the compare column** — only numeric, non-key columns are offered; `Betrag` is pre-selected by default if present and numeric.
5. **Configure difference-magnitude grouping** (defaults: 0.5, 1, 1.5) — these buckets can be changed and re-applied after a comparison without re-uploading files.
6. Click **Compare**. Results appear as an expandable tree: Matching / Added / Deleted / Different at the top level (Different further split by magnitude bucket), then drilled down by each key column's values down to the individual row(s).
7. Optionally **Download report** to export the result as a multi-sheet Excel workbook (`comparison-report.xlsx`): a **Summary** sheet (counts + grouping boundaries used + any warnings), then **Matching**, **Added**, **Deleted** sheets, then one sheet per difference-magnitude bucket (e.g. "Diff 0-0.5", "Diff 0.5-1", "Diff gt 1.5"), each with an `AbsoluteDifference` column.

## Notes

- No database — everything is in-memory, driven by plain C# services that are independently unit-tested (no Blazor dependency).
- Row matching uses dictionary lookups (O(n)), and parsing/comparison run off the UI thread via `Task.Run` so the page stays responsive on large files.
- Max upload size is set to 50 MB per file (`FileDropUpload`'s `InputFile` read limit).
