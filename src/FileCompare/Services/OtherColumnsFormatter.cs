using FileCompare.Models;

namespace FileCompare.Services;

/// <summary>Combines all non-key, non-compare column values for a row into a single "Name: Value; ..." display string.</summary>
public static class OtherColumnsFormatter
{
    public static string Combine(RowResult row, List<string> otherColumns)
    {
        return string.Join("; ", otherColumns.Select(c => $"{c}: {(row.OtherColumnValues.TryGetValue(c, out var v) ? v : string.Empty)}"));
    }
}
