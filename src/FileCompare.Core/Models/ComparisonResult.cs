namespace FileCompare.Models;

public record ComparisonResult
{
    public List<RowResult> Rows { get; init; } = new();
    public List<string> Warnings { get; init; } = new();

    public int MatchingCount => Rows.Count(r => r.Status == RowStatus.Matching);
    public int AddedCount => Rows.Count(r => r.Status == RowStatus.Added);
    public int DeletedCount => Rows.Count(r => r.Status == RowStatus.Deleted);
    public int DifferentCount => Rows.Count(r => r.Status == RowStatus.Different);
}
