using System.Text;
using ClosedXML.Excel;
using FileCompare.Models;
using FileCompare.Services;

namespace FileCompare.Tests;

public class FileTypeAndEncodingTests
{
    private readonly FileParserService _sut = new();

    [Theory]
    [InlineData(',')]
    [InlineData('\t')]
    [InlineData('|')]
    public void Parse_CustomDelimiter_ParsesHeadersAndRows(char delimiter)
    {
        var content = $"PersonalNr{delimiter}Lohnart{delimiter}Betrag\n1{delimiter}A1{delimiter}10.5\n";

        var result = _sut.Parse(content, delimiter);

        Assert.Equal(new[] { "PersonalNr", "Lohnart", "Betrag" }, result.Headers);
        Assert.Equal("10.5", result.Rows[0]["Betrag"]);
    }

    [Fact]
    public void Parse_DelimiterNotFoundInFile_ThrowsClearError()
    {
        var content = "PersonalNr,Lohnart,Betrag\n1,A1,10.5\n";

        var ex = Assert.Throws<InvalidOperationException>(() => _sut.Parse(content, ';'));
        Assert.Contains(";", ex.Message);
    }

    [Fact]
    public void ParseDelimitedText_Utf8WithBom_DecodesUmlautsCorrectly()
    {
        var text = "PersonalNr;Straße;Betrag\n1;Müllerstraße;10.5\n";
        var bytes = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes(text)).ToArray();

        var result = _sut.ParseDelimitedText(bytes, ';');

        Assert.Contains("Straße", result.Headers);
        Assert.Equal("Müllerstraße", result.Rows[0]["Straße"]);
    }

    [Fact]
    public void ParseDelimitedText_Utf8WithoutBom_DecodesUmlautsCorrectly()
    {
        var text = "PersonalNr;TextLohnart;Betrag\n1;Zusätzliche Zulage;10.5\n";
        var bytes = Encoding.UTF8.GetBytes(text);

        var result = _sut.ParseDelimitedText(bytes, ';');

        Assert.Equal("Zusätzliche Zulage", result.Rows[0]["TextLohnart"]);
    }

    [Fact]
    public void ParseDelimitedText_Windows1252Encoded_FallsBackAndDecodesUmlautsCorrectly()
    {
        var text = "PersonalNr;TextLohnart;Betrag\n1;Zusätzliche Zulage für Überstunden;10.5\n";
        var bytes = Encoding.GetEncoding(1252).GetBytes(text);

        var result = _sut.ParseDelimitedText(bytes, ';');

        Assert.Equal("Zusätzliche Zulage für Überstunden", result.Rows[0]["TextLohnart"]);
    }

    [Fact]
    public void ValidateHeaders_UmlautCaseVariant_IsTreatedAsEquivalent()
    {
        var convertor = _sut.Parse("PersonalNr;Überstunden;Betrag\n1;5;10.5\n", ';');
        var client = _sut.Parse("PersonalNr;überstunden;Betrag\n1;5;10.5\n", ';');

        var exception = Record.Exception(() => _sut.ValidateHeaders(convertor, client));

        Assert.Null(exception);
    }

    [Fact]
    public void ValidateHeaders_NonUmlautCaseDifference_StillMismatches()
    {
        var convertor = _sut.Parse("PersonalNr;Lohnart;Betrag\n1;A1;10.5\n", ';');
        var client = _sut.Parse("PersonalNr;lohnart;Betrag\n1;A1;10.5\n", ';');

        Assert.Throws<HeaderMismatchException>(() => _sut.ValidateHeaders(convertor, client));
    }

    [Fact]
    public void ParseExcel_ReadsFirstWorksheetHeadersAndRows()
    {
        var bytes = BuildWorkbookBytes(("PersonalNr", "Lohnart", "TextLohnart", "Betrag"),
            ("1", "1000", "Überstunden", "10.5"));

        var result = _sut.ParseExcel(bytes);

        Assert.Equal(new[] { "PersonalNr", "Lohnart", "TextLohnart", "Betrag" }, result.Headers);
        Assert.Equal("Überstunden", result.Rows[0]["TextLohnart"]);
        Assert.Equal("10.5", result.Rows[0]["Betrag"]);
    }

    [Fact]
    public void ComparisonService_MatchesRowsAcrossFiles_WhenHeaderCasingDiffersOnlyInUmlauts()
    {
        var parser = new FileParserService();
        var convertor = parser.Parse("PersonalNr;Überstunden;Betrag\n1;5;100.00\n", ';');
        var client = parser.Parse("PersonalNr;überstunden;Betrag\n1;5;100.00\n", ';');

        var comparisonService = new ComparisonService();
        var keySelection = new KeySelection { OrderedKeyColumns = new List<string> { "PersonalNr", "Überstunden" } };
        var compareSelection = new CompareColumnSelection { ColumnName = "Betrag" };

        var result = comparisonService.Compare(convertor, client, keySelection, compareSelection);

        var row = Assert.Single(result.Rows);
        Assert.Equal(RowStatus.Matching, row.Status);
    }

    private static byte[] BuildWorkbookBytes(
        (string, string, string, string) headers, (string, string, string, string) dataRow)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Sheet1");
        sheet.Cell(1, 1).Value = headers.Item1;
        sheet.Cell(1, 2).Value = headers.Item2;
        sheet.Cell(1, 3).Value = headers.Item3;
        sheet.Cell(1, 4).Value = headers.Item4;
        sheet.Cell(2, 1).Value = dataRow.Item1;
        sheet.Cell(2, 2).Value = dataRow.Item2;
        sheet.Cell(2, 3).Value = dataRow.Item3;
        sheet.Cell(2, 4).Value = dataRow.Item4;

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
