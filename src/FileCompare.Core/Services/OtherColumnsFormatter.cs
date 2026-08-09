using FileCompare.Models;

namespace FileCompare.Services;

/// <summary>Combines all non-key, non-compare column values for a row into a single "Value; Value; ..." display string, with the column header for that combined column being the joined column names.</summary>
public static class OtherColumnsFormatter
{
    public static string Header(List<string> otherColumns) => string.Join("; ", otherColumns);

    public static string Combine(RowResult row, List<string> otherColumns)
    {
        return string.Join("; ", otherColumns.Select(c => row.OtherColumnValues.TryGetValue(c, out var v) ? v : string.Empty));
    }
}
