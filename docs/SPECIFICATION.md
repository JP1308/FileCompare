# FileCompare — Functional & Technical Specification

*Source: [filecompare-requirements.xml](filecompare-requirements.xml)*

## 1. Overview

| | |
|---|---|
| **Name** | FileCompare |
| **Type** | ASP.NET Core Blazor Server, single interactive page |
| **Target framework** | .NET 8.0 (C#) |
| **Database** | None — fully in-memory |

FileCompare is a reconciliation tool that compares a **Converter output file** against a **Client expected file**. Both files are parsed under a shared input format, matched on a user-chosen composite key, and compared on one numeric column. Differences are grouped into configurable magnitude buckets and the whole result set is displayed as an expandable tree table grouped by key, with an optional multi-sheet Excel export.

## 2. Tech Stack

- **UI**: Blazor Server, single `.razor` page, server-side interactive rendering. Tree table is a custom recursive Razor component (no required external UI library; Radzen/MudBlazor optional).
- **Persistence**: none.
- **Libraries**: [ClosedXML](https://github.com/ClosedXML/ClosedXML) (MIT) — writes the `.xlsx` export (F8). No other third-party libraries beyond the built-in Blazor `InputFile` component.
- **Testing**: xUnit, covering the parsing/comparison/grouping services (see [§8 Test Plan](#8-test-plan)).

## 3. Architecture

**Pattern**: layered.

- **Models** — plain C# data types, no Blazor dependency.
- **Services** — parsing, key-matching, comparison, difference-grouping; plain C#, independently unit-testable.
- **UI** — a single Razor page (`Home.razor`) composed of sub-components:
  - `FileDropUpload`
  - `DualListKeySelector`
  - `CompareColumnSelector`
  - `GroupingSettings`
  - `ResultTreeTable`

**Project structure**: one Blazor Server project (`FileCompare.csproj`) plus a `FileCompare.Tests` project for the service-layer unit tests.

## 4. Data Model

| Entity | Fields |
|---|---|
| `FileTypeSelection` | `Format`: `Csv \| DelimitedText` — applies to both files for the run. `Delimiter`: `char`, used only when `Format = DelimitedText` (`;` default, tab, or `\|`); fixed to `,` for CSV. |
| `ParsedFile` | `Headers: List<string>` (file order). `Rows: List<Dictionary<string,string>>` (keyed by header name). |
| `KeySelection` | `OrderedKeyColumns: List<string>` — composite key in user-arranged order. |
| `CompareColumnSelection` | `ColumnName: string` — the single numeric column being compared. |
| `DifferenceGroupingConfig` | `Groups: List<GroupBoundary>` — each an absolute-difference upper bound; default is one group with boundary `0.5`. |
| `RowResult` | `KeyValues: List<string>`. `OtherColumnValues: Dictionary<string,string>` (non-key, non-compare, display-only; rendered as one combined column whose header is the joined column names and whose value is the joined values, both separated by `"; "`, with no `"ColumnName:"` prefix). `ConverterCompareValue: decimal?` (null if row absent in Converter file). `ClientCompareValue: decimal?` (null if row absent in Client file). `Status: Matching \| Different \| Added \| Deleted` (`Added` = key only in Converter file, `Deleted` = key only in Client file). `AbsoluteDifference: decimal?` (set only when `Status = Different`). `DifferenceGroup: string` (set only when `Status = Different`). |
| `ComparisonResult` | `Rows: List<RowResult>`, `MatchingCount`, `AddedCount`, `DeletedCount`, `DifferentCount` (all `int`). |

## 5. Features

### F1 — File upload: browse and drag & drop *(must-have)*

Two upload zones, "Converter output file" and "Client expected file", each supporting click-to-browse and drag-and-drop.

- Each zone shows its label and accepts a file via either input method.
- Visual highlight when a file is dragged over the zone.
- After upload, the zone shows the file name and a way to replace it.
- Upload errors (wrong format, unreadable file) are shown inline near the zone, never as a crash.

### F2 — Parse and validate headers *(must-have)*

Parse both files per the selected input type (F9): delimited text, first line = headers. Column name sets must match exactly between files.

- Whitespace is trimmed around every header and field value.
- A header mismatch (missing, extra, or renamed/differently-cased column) is a **hard failure**: comparison is blocked and a clear error lists the mismatched column names.
- Header comparison is case-sensitive **except** that Ä/ä, Ö/ö, Ü/ü are treated as equivalent pairs, so umlaut-casing differences alone never cause a false mismatch.
- Delimited text is read as UTF-8 (BOM-aware) by default, falling back automatically to Windows-1252/ISO-8859-1 if the content isn't valid UTF-8, so ä/ö/ü/Ä/Ö/Ü/ß in headers and values are read correctly.
- A data row with a field count different from the header row is reported as an error, not a crash.
- Empty lines are skipped.

### F3 — Key column(s) selection (dual list, ordered) *(must-have)*

After header validation, all columns appear in an "Available" list; the user moves one or more into "Selected Keys" to form a composite key and reorders them to set precedence.

- Two-list ("dual listbox") UI with controls to move items between lists (e.g. arrows or drag).
- Double-clicking a column in Available moves it to the end of Selected Keys; double-clicking in Selected Keys moves it back — an alternative to the arrow buttons in both directions.
- Up/down controls reorder items within Selected Keys.
- If present in the headers, `PersonalNr`, `Lohnart`, and/or `TextLohnart` are auto-pre-populated into Selected Keys, in that order.
- At least one key column is required before proceeding to compare-column selection.

### F4 — Compare column selection *(must-have)*

Exactly one numeric column is chosen as "the column to compare" — the only column checked for equality; all other non-key columns are display-only.

- The dropdown/list excludes columns already used as keys.
- Only columns whose values all parse as `decimal` are selectable; non-numeric columns are excluded or disabled with an explanation.
- `Betrag`, if present and numeric, is pre-selected by default.
- The "Compare" action stays disabled until a valid compare column is chosen.

### F5 — Row matching and status computation *(must-have)*

Match rows between files using the ordered composite key; classify each row's status.

- Same key, equal compare values → `Matching`.
- Same key, differing compare values → `Different`; `AbsoluteDifference = |ConverterValue - ClientValue|`.
- Key only in Converter file → `Added`.
- Key only in Client file → `Deleted`.
- Duplicate composite-key values within one file are detected and reported as a warning, not a crash.
- Matching uses in-memory dictionary lookups (O(n)), not nested-loop comparison — must scale to large files.

### F6 — Difference-magnitude grouping (configurable) *(must-have)*

`Different` rows are grouped by `AbsoluteDifference` magnitude, using user-configurable bucket boundaries.

- A settings panel lets the user set the number of groups and each group's boundary.
- Bucketing: group 1 = `0 < diff ≤ boundary1`, group 2 = `boundary1 < diff ≤ boundary2`, …, with a final "greater than last boundary" overflow group.
- Changing settings and re-running updates the grouping without re-uploading files.
- Default (unless customized): a single group with boundary `0.5`.

### F7 — Tree-table results view *(must-have)*

Comparison results are shown as an expandable tree table.

- Column headers, left to right: each key column (in selected order) → compare column suffixed `(Converter)` (bold) → compare column suffixed `(Client)` (bold) → one combined column for all remaining non-key, non-compare columns. That column's **header** is the names of those remaining columns joined with `"; "` (e.g. `"Name; Department"`), and each row's **value** is just the corresponding values joined with `"; "`, in the same order, with no column-name prefix (e.g. `"Alice; Sales"`).
- Top level: four nodes with label + count — `Matching entries (N)`, `Added entries (N)`, `Deleted entries (N)`, `Different entries (N)`.
- Under `Different entries`, rows are further grouped by difference-magnitude bucket (F6), each labeled with its range and count.
- Within any node/bucket, rows are grouped hierarchically by key column values in key order: expanding shows the next key column's distinct values (with counts), down to the last key level, which expands to the actual matched row(s) with all column values.
- Expand/collapse is interactive, no full page reload.
- Large result sets stay usable via virtualization or lazy expansion rather than rendering everything at once.
- Umlaut characters render correctly throughout — in key values, other-column values, and headers.

### F8 — Export results *(nice-to-have)*

A button downloads the full result as a multi-sheet `.xlsx` workbook, split by status group and difference bucket.

- "Download report" appears once results are shown; triggers a browser file download.
- `Summary` sheet: Matching/Added/Deleted/Different counts, the grouping boundaries used, a row count for each configured difference-magnitude bucket, and any warnings (e.g. duplicate keys).
- On the Summary sheet, the Added count is labeled **"Added (Rows found only in the Converter output file)"** and the Deleted count is labeled **"Deleted (Rows found only in the Client expected file)"**. This longer wording is for the Summary sheet's labels only — the `Added`/`Deleted` sheet tab names below stay short to satisfy Excel's naming constraints.
- One sheet each for `Matching`, `Added`, `Deleted`: key values, both compare values, and the combined other-columns column (same joined-header/joined-values format as F7, no column-name prefix) — same column order as the tree table.
- One additional sheet per difference-magnitude bucket, named after its range (e.g. `Diff 0-0.5`, `Diff gt 0.5`), each including an `AbsoluteDifference` column between the compare values and the combined other-columns column.
- Sheet names are sanitized and de-duplicated to satisfy Excel's constraints (≤31 chars, no `\ / ? * [ ] :`).
- Umlaut characters are preserved exactly, matching the tree table.

### F9 — Input file type selection (CSV / delimited text) *(must-have)*

Before/alongside the upload zones, the user picks the input type, applied to **both** files (mixed formats between the two files are not supported).

- Selector offers `CSV`, `Delimited text`, defaulting to `Delimited text`.
- `CSV`: comma-delimited, no delimiter picker; zones accept `.csv`.
- `Delimited text`: delimiter dropdown appears — Semicolon (default), Tab, Pipe (comma is covered by the dedicated CSV option).
- No per-file override — both files always share the selected format and delimiter.
- Changing type or delimiter after both files are uploaded re-parses and re-validates both (F2) without a full page reload.
- An unparsable file under the selected type (delimiter not found, etc.) shows a clear inline error near the relevant zone instead of crashing.

## 6. User Interface

**Screen: Home (single page)** — the entire workflow (upload, key/compare selection, grouping config, compare, results) happens on one page without navigation.

Elements, top to bottom:

1. File-type selector: CSV / Delimited text (+ delimiter dropdown) (F9)
2. Two upload zones — browse + drag-and-drop: "Converter output file", "Client expected file" (F1)
3. Header-mismatch error banner (shown only on F2 validation failure)
4. Dual-list key column selector with up/down reorder (F3)
5. Compare column dropdown, numeric columns only (F4)
6. Difference-grouping settings panel: number of groups + boundary per group (F6)
7. "Compare" button
8. Tree-table results area (F7): Matching/Added/Deleted/Different top-level nodes with counts, expandable through key levels to individual rows
9. "Download report" button *(nice-to-have)* — multi-sheet `.xlsx` export (F8)

## 7. Non-Functional Requirements

| Aspect | Requirement |
|---|---|
| **Performance** | Comfortably handle tens of thousands of rows via in-memory dictionary lookups for key matching (O(n), not O(n²)). Parsing/comparison run off the Blazor Server UI thread for large files so the page stays responsive. Tree rendering avoids force-rendering the entire result set at once. |
| **Error handling** | Header mismatches, malformed rows, unreadable/mismatched-type uploads, non-numeric compare values, and invalid grouping settings all surface as clear inline UI messages — never unhandled exceptions or stack traces. |
| **Logging** | Not required beyond on-page error messages. |
| **Configuration** | Max upload file size set reasonably (e.g. up to 50 MB) via Blazor `InputFile` settings. |
| **Encoding** | Delimited-text input: UTF-8 (BOM-aware) with automatic Windows-1252/ISO-8859-1 fallback, so ä/ö/ü/Ä/Ö/Ü/ß round-trip correctly end-to-end — file read → header validation → key/compare matching → tree display → Excel export. The exported `.xlsx` (F8) is natively Unicode and needs no special handling on output. |

## 8. Constraints

- No database — pure C#/.NET plus built-in/optional Blazor UI components.
- Supported input types: CSV (comma, header row 1), delimited text (semicolon/tab/pipe, header row 1) — see F9. Both files in a run must share type and delimiter.
- Single page only — no multi-page navigation/routing.
- Compare column must be numeric (`decimal`); non-numeric value comparison is out of scope for this version.

## 9. Test Plan

xUnit tests target the parsing/comparison/grouping services. Required scenarios:

**Parsing**
- Headers and data values are trimmed of surrounding whitespace.
- Empty lines are skipped, not treated as data rows.
- Identical header sets in both files pass validation.
- A missing column in one file's headers is detected and reported.
- An extra column in one file's headers is detected and reported.
- A renamed/differently-cased column name (case-sensitive) is detected as a mismatch.
- A data row with a different field count than the header row is reported as an error, not a crash.

**Matching**
- Equal compare values on a shared key → `Matching`.
- Differing compare values on a shared key → `Different`, with correct `AbsoluteDifference`.
- Key only in Converter file → `Added`.
- Key only in Client file → `Deleted`.
- Duplicate composite-key values within one file are detected and reported as a warning, without crashing.
- Composite key ordering is respected — matching is correct regardless of column order in the source files.
- A non-numeric value in the compare column produces a clear error, not an unhandled exception.

**Grouping**
- A difference exactly at a bucket boundary falls into the lower bucket (`boundaryN-1 < diff ≤ boundaryN`).
- A difference greater than the last configured boundary falls into the final overflow group.
- Default configuration is a single group with boundary `0.5`, applied when unconfigured.
- Changing boundaries and re-grouping the same `Different` rows updates bucket assignments without re-parsing.

**Export**
- Workbook contains a Summary sheet plus Matching/Added/Deleted sheets plus one sheet per difference bucket.
- Summary sheet shows correct Matching/Added/Deleted/Different counts.
- Summary sheet shows a row count for each configured difference-magnitude grouping boundary/bucket, not just the overall Different count.
- Summary sheet labels the Added and Deleted counts as "Added (Rows found only in the Converter output file)" and "Deleted (Rows found only in the Client expected file)" respectively.
- Each status/bucket sheet contains only that group's rows, with key values, both compare values, and one combined other-columns value per row.
- Column order everywhere: key columns → compare(Converter) → compare(Client) → [`AbsoluteDifference` on bucket sheets] → combined other-columns column, whose header is the joined names of the remaining columns and whose values are the joined values only (no column-name prefix).
- Difference-bucket sheets additionally include an `AbsoluteDifference` column.
- Warnings (e.g. duplicate keys) are listed on the Summary sheet.

**Input type**
- CSV parsing (fixed comma) produces the same headers/rows as semicolon-delimited parsing of an equivalent file with delimiters swapped.
- Tab- or pipe-delimited parsing likewise matches semicolon parsing of an equivalent swapped-delimiter file.
- Switching file type or delimiter after both files are uploaded re-parses/re-validates headers without a full page reload.

**Encoding**
- A UTF-8 (with and without BOM) file containing ä, ö, ü, Ä, Ö, Ü, ß in headers and values parses with those characters intact.
- A Windows-1252/ISO-8859-1 file with umlauts is auto-detected and parsed correctly, not as mojibake.
- Header comparison treats Ü/ü (and other umlaut case pairs) as equivalent, so umlaut casing alone doesn't trigger a false header mismatch.
- A row with umlauts in its key or other-column values matches correctly across both files and round-trips unchanged into the Excel export.

## 10. Deliverable

A runnable .NET 8 Blazor Server solution (`.sln` + `.csproj` + source), with a README explaining build/run (`dotnet run`), sample input files for manual testing, and a unit test project (`FileCompare.Tests`) covering the service layer per [§9](#9-test-plan).

## 11. Resolved Open Items

- Default key columns: `PersonalNr`, `Lohnart` (in that order).
- Default compare column: `Betrag`.
- `Added`/`Deleted` convention confirmed: `Added` = key exists only in the Converter file, `Deleted` = key exists only in the Client file.
- Excel workbook removed as an input option (F9 is now CSV / Delimited text only); ClosedXML is retained solely for the F8 `.xlsx` export.
- The combined other-columns column (F7/F8) now uses a header of the joined remaining column names, with values-only content (no `"ColumnName:"` prefix).
- F8 Summary sheet now also shows a row count per difference-magnitude bucket, and labels Added/Deleted with the fuller "Rows found only in the Converter/Client file" wording.
