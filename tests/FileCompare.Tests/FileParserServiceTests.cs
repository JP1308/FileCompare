using FileCompare.Models;
using FileCompare.Services;

namespace FileCompare.Tests;

public class FileParserServiceTests
{
    private readonly FileParserService _sut = new();

    [Fact]
    public void Parse_TrimsWhitespaceAroundHeadersAndValues()
    {
        var content = " PersonalNr ; Lohnart ; Betrag \n 1 ; A1 ; 10.5 \n";

        var result = _sut.Parse(content);

        Assert.Equal(new[] { "PersonalNr", "Lohnart", "Betrag" }, result.Headers);
        Assert.Equal("1", result.Rows[0]["PersonalNr"]);
        Assert.Equal("A1", result.Rows[0]["Lohnart"]);
        Assert.Equal("10.5", result.Rows[0]["Betrag"]);
    }

    [Fact]
    public void Parse_SkipsEmptyLines()
    {
        var content = "PersonalNr;Lohnart;Betrag\n\n1;A1;10\n\n2;A2;20\n";

        var result = _sut.Parse(content);

        Assert.Equal(2, result.Rows.Count);
    }

    [Fact]
    public void Parse_HeaderSetsMatch_DoesNotThrow()
    {
        var converter = _sut.Parse("PersonalNr;Lohnart;Betrag\n1;A1;10\n");
        var client = _sut.Parse("PersonalNr;Lohnart;Betrag\n1;A1;10\n");

        var exception = Record.Exception(() => _sut.ValidateHeaders(converter, client));

        Assert.Null(exception);
    }

    [Fact]
    public void ValidateHeaders_MissingColumn_ThrowsHeaderMismatchException()
    {
        var converter = _sut.Parse("PersonalNr;Lohnart;Betrag\n1;A1;10\n");
        var client = _sut.Parse("PersonalNr;Lohnart\n1;A1\n");

        var ex = Assert.Throws<HeaderMismatchException>(() => _sut.ValidateHeaders(converter, client));
        Assert.Contains("Betrag", ex.OnlyInConverterFile);
    }

    [Fact]
    public void ValidateHeaders_ExtraColumn_ThrowsHeaderMismatchException()
    {
        var converter = _sut.Parse("PersonalNr;Lohnart;Betrag;Extra\n1;A1;10;x\n");
        var client = _sut.Parse("PersonalNr;Lohnart;Betrag\n1;A1;10\n");

        var ex = Assert.Throws<HeaderMismatchException>(() => _sut.ValidateHeaders(converter, client));
        Assert.Contains("Extra", ex.OnlyInConverterFile);
    }

    [Fact]
    public void ValidateHeaders_RenamedColumn_CaseSensitive_ThrowsHeaderMismatchException()
    {
        var converter = _sut.Parse("PersonalNr;Lohnart;Betrag\n1;A1;10\n");
        var client = _sut.Parse("PersonalNr;lohnart;Betrag\n1;A1;10\n");

        var ex = Assert.Throws<HeaderMismatchException>(() => _sut.ValidateHeaders(converter, client));
        Assert.Contains("Lohnart", ex.OnlyInConverterFile);
        Assert.Contains("lohnart", ex.OnlyInClientFile);
    }

    [Fact]
    public void Parse_RowWithWrongFieldCount_IsReportedAsErrorAndSkipped()
    {
        var content = "PersonalNr;Lohnart;Betrag\n1;A1;10\n2;A2\n3;A3;30\n";

        var result = _sut.Parse(content);

        Assert.Equal(2, result.Rows.Count);
        Assert.Single(result.RowErrors);
        Assert.Contains("Line 3", result.RowErrors[0]);
    }
}
