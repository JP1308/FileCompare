namespace FileCompare.Models;

public enum RowStatus
{
    Matching,
    Different,
    Added,
    Deleted,
}

public record RowResult
{
    public List<string> KeyValues { get; init; } = new();
    public Dictionary<string, string> OtherColumnValues { get; init; } = new();
    public decimal? ConvertorCompareValue { get; init; }
    public decimal? ClientCompareValue { get; init; }
    public RowStatus Status { get; init; }
    public decimal? AbsoluteDifference { get; init; }
    public string? DifferenceGroup { get; init; }
}
