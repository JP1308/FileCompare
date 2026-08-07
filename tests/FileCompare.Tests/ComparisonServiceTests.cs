using FileCompare.Models;
using FileCompare.Services;

namespace FileCompare.Tests;

public class ComparisonServiceTests
{
    private readonly FileParserService _parser = new();
    private readonly ComparisonService _sut = new();

    private static KeySelection Keys(params string[] columns) => new() { OrderedKeyColumns = columns.ToList() };
    private static CompareColumnSelection Compare(string column) => new() { ColumnName = column };

    [Fact]
    public void Compare_EqualCompareValues_StatusIsMatching()
    {
        var convertor = _parser.Parse("PersonalNr;Lohnart;Betrag\n1;A1;100.00\n");
        var client = _parser.Parse("PersonalNr;Lohnart;Betrag\n1;A1;100.00\n");

        var result = _sut.Compare(convertor, client, Keys("PersonalNr", "Lohnart"), Compare("Betrag"));

        var row = Assert.Single(result.Rows);
        Assert.Equal(RowStatus.Matching, row.Status);
        Assert.Null(row.AbsoluteDifference);
    }

    [Fact]
    public void Compare_DifferingCompareValues_StatusIsDifferentWithAbsoluteDifference()
    {
        var convertor = _parser.Parse("PersonalNr;Lohnart;Betrag\n1;A1;100.00\n");
        var client = _parser.Parse("PersonalNr;Lohnart;Betrag\n1;A1;97.50\n");

        var result = _sut.Compare(convertor, client, Keys("PersonalNr", "Lohnart"), Compare("Betrag"));

        var row = Assert.Single(result.Rows);
        Assert.Equal(RowStatus.Different, row.Status);
        Assert.Equal(2.50m, row.AbsoluteDifference);
    }

    [Fact]
    public void Compare_KeyOnlyInConvertorFile_StatusIsAdded()
    {
        var convertor = _parser.Parse("PersonalNr;Lohnart;Betrag\n1;A1;100\n");
        var client = _parser.Parse("PersonalNr;Lohnart;Betrag\n");

        var result = _sut.Compare(convertor, client, Keys("PersonalNr", "Lohnart"), Compare("Betrag"));

        var row = Assert.Single(result.Rows);
        Assert.Equal(RowStatus.Added, row.Status);
    }

    [Fact]
    public void Compare_KeyOnlyInClientFile_StatusIsDeleted()
    {
        var convertor = _parser.Parse("PersonalNr;Lohnart;Betrag\n");
        var client = _parser.Parse("PersonalNr;Lohnart;Betrag\n1;A1;100\n");

        var result = _sut.Compare(convertor, client, Keys("PersonalNr", "Lohnart"), Compare("Betrag"));

        var row = Assert.Single(result.Rows);
        Assert.Equal(RowStatus.Deleted, row.Status);
    }

    [Fact]
    public void Compare_DuplicateKeyWithinAFile_IsReportedAsWarningNotCrash()
    {
        var convertor = _parser.Parse("PersonalNr;Lohnart;Betrag\n1;A1;100\n1;A1;105\n");
        var client = _parser.Parse("PersonalNr;Lohnart;Betrag\n1;A1;100\n");

        var result = _sut.Compare(convertor, client, Keys("PersonalNr", "Lohnart"), Compare("Betrag"));

        Assert.Single(result.Warnings);
        Assert.Contains("Duplicate key", result.Warnings[0]);
    }

    [Fact]
    public void Compare_KeyOrderingDoesNotAffectMatchCorrectness()
    {
        var convertor = _parser.Parse("Lohnart;PersonalNr;Betrag\nA1;1;100\n");
        var client = _parser.Parse("PersonalNr;Lohnart;Betrag\n1;A1;100\n");

        var result = _sut.Compare(convertor, client, Keys("PersonalNr", "Lohnart"), Compare("Betrag"));

        var row = Assert.Single(result.Rows);
        Assert.Equal(RowStatus.Matching, row.Status);
    }

    [Fact]
    public void IsColumnNumeric_NonNumericValue_ReturnsFalse()
    {
        var file = _parser.Parse("PersonalNr;Lohnart;Betrag\n1;A1;not-a-number\n");

        Assert.False(_sut.IsColumnNumeric(file, "Betrag"));
    }

    [Fact]
    public void IsColumnNumeric_AllNumericValues_ReturnsTrue()
    {
        var file = _parser.Parse("PersonalNr;Lohnart;Betrag\n1;A1;10.5\n2;A2;-3\n");

        Assert.True(_sut.IsColumnNumeric(file, "Betrag"));
    }
}
