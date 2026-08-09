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

    public ComparisonResult Compare(ParsedFile converterFile, ParsedFile clientFile, KeySelection keySelection, CompareColumnSelection compareColumn)
    {
        var warnings = new List<string>();

        var converterByKey = IndexByKey(converterFile, keySelection.OrderedKeyColumns, "Converter output file", warnings);
        var clientByKey = IndexByKey(clientFile, keySelection.OrderedKeyColumns, "Client expected file", warnings);

        var otherColumns = converterFile.Headers
            .Where(h => !keySelection.OrderedKeyColumns.Contains(h) && h != compareColumn.ColumnName)
            .ToList();

        var allKeys = new List<string>(converterByKey.Keys);
        foreach (var key in clientByKey.Keys)
        {
            if (!converterByKey.ContainsKey(key))
            {
                allKeys.Add(key);
            }
        }

        var results = new List<RowResult>();

        foreach (var key in allKeys)
        {
            var hasConverter = converterByKey.TryGetValue(key, out var converterRow);
            var hasClient = clientByKey.TryGetValue(key, out var clientRow);

            var sourceRow = converterRow ?? clientRow!;
            var keyValues = keySelection.OrderedKeyColumns.Select(k => sourceRow[k]).ToList();
            var otherValues = otherColumns.ToDictionary(c => c, c => sourceRow.TryGetValue(c, out var v) ? v : string.Empty);

            decimal? converterValue = hasConverter ? ParseCompareValue(converterRow!, compareColumn.ColumnName) : null;
            decimal? clientValue = hasClient ? ParseCompareValue(clientRow!, compareColumn.ColumnName) : null;

            RowStatus status;
            decimal? absoluteDifference = null;

            if (hasConverter && hasClient)
            {
                if (converterValue == clientValue)
                {
                    status = RowStatus.Matching;
                }
                else
                {
                    status = RowStatus.Different;
                    absoluteDifference = Math.Abs((converterValue ?? 0) - (clientValue ?? 0));
                }
            }
            else if (hasConverter)
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
                ConverterCompareValue = converterValue,
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
