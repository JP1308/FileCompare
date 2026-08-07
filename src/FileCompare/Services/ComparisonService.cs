using System.Globalization;
using FileCompare.Models;

namespace FileCompare.Services;

public class ComparisonService
{
    private const char KeyPartSeparator = '';

    /// <summary>Returns true only if every row in the file has a value in <paramref name="columnName"/> that parses as decimal.</summary>
    public bool IsColumnNumeric(ParsedFile file, string columnName)
    {
        return file.Rows.All(row => decimal.TryParse(row[columnName], NumberStyles.Number, CultureInfo.InvariantCulture, out _));
    }

    public ComparisonResult Compare(ParsedFile convertorFile, ParsedFile clientFile, KeySelection keySelection, CompareColumnSelection compareColumn)
    {
        var warnings = new List<string>();

        var convertorByKey = IndexByKey(convertorFile, keySelection.OrderedKeyColumns, "Convertor output file", warnings);
        var clientByKey = IndexByKey(clientFile, keySelection.OrderedKeyColumns, "Client expected file", warnings);

        var otherColumns = convertorFile.Headers
            .Where(h => !keySelection.OrderedKeyColumns.Contains(h) && h != compareColumn.ColumnName)
            .ToList();

        var allKeys = new List<string>(convertorByKey.Keys);
        foreach (var key in clientByKey.Keys)
        {
            if (!convertorByKey.ContainsKey(key))
            {
                allKeys.Add(key);
            }
        }

        var results = new List<RowResult>();

        foreach (var key in allKeys)
        {
            var hasConvertor = convertorByKey.TryGetValue(key, out var convertorRow);
            var hasClient = clientByKey.TryGetValue(key, out var clientRow);

            var sourceRow = convertorRow ?? clientRow!;
            var keyValues = keySelection.OrderedKeyColumns.Select(k => sourceRow[k]).ToList();
            var otherValues = otherColumns.ToDictionary(c => c, c => sourceRow.TryGetValue(c, out var v) ? v : string.Empty);

            decimal? convertorValue = hasConvertor ? ParseCompareValue(convertorRow!, compareColumn.ColumnName) : null;
            decimal? clientValue = hasClient ? ParseCompareValue(clientRow!, compareColumn.ColumnName) : null;

            RowStatus status;
            decimal? absoluteDifference = null;

            if (hasConvertor && hasClient)
            {
                if (convertorValue == clientValue)
                {
                    status = RowStatus.Matching;
                }
                else
                {
                    status = RowStatus.Different;
                    absoluteDifference = Math.Abs((convertorValue ?? 0) - (clientValue ?? 0));
                }
            }
            else if (hasConvertor)
            {
                status = RowStatus.Added;
            }
            else
            {
                status = RowStatus.Deleted;
            }

            results.Add(new RowResult
            {
                KeyValues = keyValues,
                OtherColumnValues = otherValues,
                ConvertorCompareValue = convertorValue,
                ClientCompareValue = clientValue,
                Status = status,
                AbsoluteDifference = absoluteDifference,
            });
        }

        return new ComparisonResult { Rows = results, Warnings = warnings };
    }

    private static decimal? ParseCompareValue(Dictionary<string, string> row, string compareColumnName)
    {
        return decimal.TryParse(row[compareColumnName], NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static Dictionary<string, Dictionary<string, string>> IndexByKey(
        ParsedFile file, List<string> keyColumns, string fileLabel, List<string> warnings)
    {
        var index = new Dictionary<string, Dictionary<string, string>>();

        foreach (var row in file.Rows)
        {
            var key = string.Join(KeyPartSeparator, keyColumns.Select(k => row[k]));
            if (index.ContainsKey(key))
            {
                warnings.Add($"Duplicate key ({string.Join(", ", keyColumns.Select(k => row[k]))}) found in {fileLabel}; only the first occurrence is used.");
                continue;
            }
            index[key] = row;
        }

        return index;
    }
}
