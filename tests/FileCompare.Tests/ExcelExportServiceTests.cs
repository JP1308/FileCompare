using ClosedXML.Excel;
using FileCompare.Models;
using FileCompare.Services;

namespace FileCompare.Tests;

public class ExcelExportServiceTests
{
    private readonly ExcelExportService _sut = new();

    private static ComparisonResult BuildSampleResult() => new()
    {
        Rows = new List<RowResult>
        {
            new()
            {
                KeyValues = new List<string> { "1", "A1" },
                OtherColumnValues = new Dictionary<string, string> { ["Name"] = "Alice" },
                ConverterCompareValue = 100m,
                ClientCompareValue = 100m,
                Status = RowStatus.Matching,
            },
            new()
            {
                KeyValues = new List<string> { "2", "A2" },
                OtherColumnValues = new Dictionary<string, string> { ["Name"] = "Bob" },
                ConverterCompareValue = 100m,
                ClientCompareValue = 100.3m,
                Status = RowStatus.Different,
                AbsoluteDifference = 0.3m,
                DifferenceGroup = "0 < diff <= 0.5",
            },
            new()
            {
                KeyValues = new List<string> { "3", "A3" },
                OtherColumnValues = new Dictionary<string, string> { ["Name"] = "Carol" },
                ConverterCompareValue = 100m,
                Status = RowStatus.Added,
            },
            new()
            {
                KeyValues = new List<string> { "4", "A4" },
                OtherColumnValues = new Dictionary<string, string> { ["Name"] = "Dave" },
                ClientCompareValue = 100m,
                Status = RowStatus.Deleted,
            },
        },
        Warnings = new List<string> { "Duplicate key (5, A5) found in Converter output file; only the first occurrence is used." },
    };

    private static List<string> KeyColumns => new() { "PersonalNr", "Lohnart" };
    private static List<string> OtherColumns => new() { "Name" };

    [Fact]
    public void ExportToExcel_CreatesOneSheetPerStatusPlusSummaryAndBucketSheets()
    {
        var bytes = _sut.ExportToExcel(BuildSampleResult(), KeyColumns, OtherColumns, "Betrag", DifferenceGroupingConfig.Default());

        using var stream = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(stream);

        var sheetNames = workbook.Worksheets.Select(w => w.Name).ToList();
        Assert.Contains("Summary", sheetNames);
        Assert.Contains("Matching", sheetNames);
        Assert.Contains("Added", sheetNames);
        Assert.Contains("Deleted", sheetNames);
        Assert.Contains(sheetNames, n => n.StartsWith("Diff"));
    }

    [Fact]
    public void ExportToExcel_SummarySheet_HasCorrectCounts()
    {
        var bytes = _sut.ExportToExcel(BuildSampleResult(), KeyColumns, OtherColumns, "Betrag", DifferenceGroupingConfig.Default());

        using var stream = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(stream);
        var summary = workbook.Worksheet("Summary");

        Assert.Equal("Matching", summary.Cell(2, 1).GetString());
        Assert.Equal(1, summary.Cell(2, 2).GetValue<int>());
        Assert.Equal("Added (Rows found only in the Converter output file)", summary.Cell(3, 1).GetString());
        Assert.Equal(1, summary.Cell(3, 2).GetValue<int>());
        Assert.Equal("Deleted (Rows found only in the Client expected file)", summary.Cell(4, 1).GetString());
        Assert.Equal(1, summary.Cell(4, 2).GetValue<int>());
        Assert.Equal("Different", summary.Cell(5, 1).GetString());
        Assert.Equal(1, summary.Cell(5, 2).GetValue<int>());
    }

    [Fact]
    public void ExportToExcel_SummarySheet_ShowsRowCountPerDifferenceMagnitudeBucket()
    {
        var bytes = _sut.ExportToExcel(BuildSampleResult(), KeyColumns, OtherColumns, "Betrag", DifferenceGroupingConfig.Default());

        using var stream = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(stream);
        var summary = workbook.Worksheet("Summary");

        Assert.Equal("0 < diff <= 0.5", summary.Cell(8, 1).GetString());
        Assert.Equal(1, summary.Cell(8, 2).GetValue<int>());
        Assert.Equal("greater than last boundary (0.5)", summary.Cell(9, 1).GetString());
        Assert.Equal(0, summary.Cell(9, 2).GetValue<int>());
    }

    [Fact]
    public void ExportToExcel_MatchingSheet_ContainsOnlyMatchingRows()
    {
        var bytes = _sut.ExportToExcel(BuildSampleResult(), KeyColumns, OtherColumns, "Betrag", DifferenceGroupingConfig.Default());

        using var stream = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheet("Matching");

        Assert.Equal("PersonalNr", sheet.Cell(1, 1).GetString());
        Assert.Equal("1", sheet.Cell(2, 1).GetString());
        Assert.True(sheet.Cell(3, 1).IsEmpty());
    }

    [Fact]
    public void ExportToExcel_ColumnOrder_IsKeysThenCompareThenCombinedOtherColumns()
    {
        var bytes = _sut.ExportToExcel(BuildSampleResult(), KeyColumns, OtherColumns, "Betrag", DifferenceGroupingConfig.Default());

        using var stream = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheet("Matching");

        Assert.Equal("PersonalNr", sheet.Cell(1, 1).GetString());
        Assert.Equal("Lohnart", sheet.Cell(1, 2).GetString());
        Assert.Equal("Betrag (Converter)", sheet.Cell(1, 3).GetString());
        Assert.Equal("Betrag (Client)", sheet.Cell(1, 4).GetString());
        Assert.Equal("Name", sheet.Cell(1, 5).GetString());
        Assert.Equal("Alice", sheet.Cell(2, 5).GetString());
    }

    [Fact]
    public void ExportToExcel_DifferentBucketSheet_IncludesAbsoluteDifferenceColumn()
    {
        var bytes = _sut.ExportToExcel(BuildSampleResult(), KeyColumns, OtherColumns, "Betrag", DifferenceGroupingConfig.Default());

        using var stream = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(stream);
        var bucketSheet = workbook.Worksheets.First(w => w.Name.StartsWith("Diff"));

        var headerRow = bucketSheet.Row(1).CellsUsed().Select(c => c.GetString()).ToList();
        Assert.Contains("AbsoluteDifference", headerRow);
        Assert.Equal("2", bucketSheet.Cell(2, 1).GetString());
    }

    [Fact]
    public void ExportToExcel_WarningsAreIncludedInSummarySheet()
    {
        var bytes = _sut.ExportToExcel(BuildSampleResult(), KeyColumns, OtherColumns, "Betrag", DifferenceGroupingConfig.Default());

        using var stream = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(stream);
        var summary = workbook.Worksheet("Summary");
        var allText = summary.CellsUsed().Select(c => c.GetString());

        Assert.Contains(allText, t => t.Contains("Duplicate key"));
    }
}
