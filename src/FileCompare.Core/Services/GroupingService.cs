using FileCompare.Models;

namespace FileCompare.Services;

public class GroupingService
{
    public const string OverflowGroupLabel = "greater than last boundary";

    /// <summary>Returns a new ComparisonResult with DifferenceGroup assigned on every Different row, so grouping can be re-applied without re-parsing/re-comparing.</summary>
    public ComparisonResult ApplyGrouping(ComparisonResult comparisonResult, DifferenceGroupingConfig config)
    {
        var boundaries = config.Groups.OrderBy(g => g.UpperBound).ToList();

        var groupedRows = comparisonResult.Rows.Select(row =>
        {
            if (row.Status != RowStatus.Different || row.AbsoluteDifference is null)
            {
                return row;
            }

            return row with { DifferenceGroup = ResolveGroupLabel(row.AbsoluteDifference.Value, boundaries) };
        }).ToList();

        return comparisonResult with { Rows = groupedRows };
    }

    private static string ResolveGroupLabel(decimal absoluteDifference, List<GroupBoundary> boundaries)
    {
        decimal lower = 0m;
        foreach (var boundary in boundaries)
        {
            if (absoluteDifference <= boundary.UpperBound)
            {
                return $"{lower:0.####} < diff <= {boundary.UpperBound:0.####}";
            }
            lower = boundary.UpperBound;
        }

        return $"{OverflowGroupLabel} ({lower:0.####})";
    }
}
