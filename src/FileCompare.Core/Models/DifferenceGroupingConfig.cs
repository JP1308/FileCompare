namespace FileCompare.Models;

public record GroupBoundary
{
    public decimal UpperBound { get; init; }
}

public record DifferenceGroupingConfig
{
    public List<GroupBoundary> Groups { get; init; } = new();

    public static DifferenceGroupingConfig Default() => new()
    {
        Groups = new List<GroupBoundary>
        {
            new() { UpperBound = 0.5m },
        }
    };
}
