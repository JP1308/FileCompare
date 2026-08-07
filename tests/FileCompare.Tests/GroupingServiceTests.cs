using FileCompare.Models;
using FileCompare.Services;

namespace FileCompare.Tests;

public class GroupingServiceTests
{
    private readonly GroupingService _sut = new();

    private static ComparisonResult ResultWithDifference(decimal absoluteDifference) => new()
    {
        Rows = new List<RowResult>
        {
            new()
            {
                KeyValues = new List<string> { "1" },
                Status = RowStatus.Different,
                AbsoluteDifference = absoluteDifference,
            }
        }
    };

    private static DifferenceGroupingConfig ConfigWithBoundaries(params decimal[] boundaries) => new()
    {
        Groups = boundaries.Select(b => new GroupBoundary { UpperBound = b }).ToList()
    };

    [Fact]
    public void ApplyGrouping_DifferenceExactlyOnBoundary_FallsIntoLowerBucket()
    {
        var result = ResultWithDifference(0.5m);
        var config = ConfigWithBoundaries(0.5m, 1m, 1.5m);

        var grouped = _sut.ApplyGrouping(result, config);

        Assert.Contains("<= 0.5", grouped.Rows[0].DifferenceGroup);
    }

    [Fact]
    public void ApplyGrouping_DifferenceGreaterThanLastBoundary_FallsIntoOverflowGroup()
    {
        var result = ResultWithDifference(5m);
        var config = ConfigWithBoundaries(0.5m, 1m, 1.5m);

        var grouped = _sut.ApplyGrouping(result, config);

        Assert.Contains(GroupingService.OverflowGroupLabel, grouped.Rows[0].DifferenceGroup);
    }

    [Fact]
    public void DefaultConfig_HasBoundaries_0_5_1_1_5()
    {
        var config = DifferenceGroupingConfig.Default();

        Assert.Equal(new[] { 0.5m, 1m, 1.5m }, config.Groups.Select(g => g.UpperBound));
    }

    [Fact]
    public void ApplyGrouping_ChangingBoundaries_UpdatesGroupWithoutMutatingOriginalRows()
    {
        var result = ResultWithDifference(0.8m);

        var groupedNarrow = _sut.ApplyGrouping(result, ConfigWithBoundaries(0.5m, 1m));
        var groupedWide = _sut.ApplyGrouping(result, ConfigWithBoundaries(1m, 2m));

        Assert.Contains("<= 1", groupedNarrow.Rows[0].DifferenceGroup);
        Assert.Contains("<= 1", groupedWide.Rows[0].DifferenceGroup);
        Assert.Null(result.Rows[0].DifferenceGroup);
    }

    [Fact]
    public void ApplyGrouping_NonDifferentRows_AreLeftUngrouped()
    {
        var result = new ComparisonResult
        {
            Rows = new List<RowResult> { new() { Status = RowStatus.Matching } }
        };

        var grouped = _sut.ApplyGrouping(result, DifferenceGroupingConfig.Default());

        Assert.Null(grouped.Rows[0].DifferenceGroup);
    }
}
