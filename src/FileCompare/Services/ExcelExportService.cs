using ClosedXML.Excel;
using FileCompare.Models;

namespace FileCompare.Services;

public class ExcelExportService
{
    private static readonly char[] InvalidSheetNameChars = { '\\', '/', '?', '*', '[', ']', ':' };

    public byte[] ExportToExcel(
        ComparisonResult result,
        List<string> keyColumns,
        List<string> otherColumns,
        string compareColumnName,
        DifferenceGroupingConfig groupingConfig)
    {
        using var workbook = new XLWorkbook();
        var usedSheetNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddSummarySheet(workbook, result, groupingConfig, usedSheetNames);

        AddRowsSheet(workbook, ReserveName("Matching", usedSheetNames),
            result.Rows.Where(r => r.Status == RowStatus.Matching).ToList(),
            keyColumns, otherColumns, compareColumnName, includeAbsoluteDifference: false);

        AddRowsSheet(workbook, ReserveName("Added", usedSheetNames),
            result.Rows.Where(r => r.Status == RowStatus.Added).ToList(),
            keyColumns, otherColumns, compareColumnName, includeAbsoluteDifference: false);

        AddRowsSheet(workbook, ReserveName("Deleted", usedSheetNames),
            result.Rows.Where(r => r.Status == RowStatus.Deleted).ToList(),
            keyColumns, otherColumns, compareColumnName, includeAbsoluteDifference: false);

        var bucketGroups = result.Rows
            .Where(r => r.Status == RowStatus.Different)
            .GroupBy(r => r.DifferenceGroup ?? "(ungrouped)")
            .OrderBy(g => g.Min(r => r.AbsoluteDifference ?? 0m));

        foreach (var bucket in bucketGroups)
        {
            var sheetName = ReserveName(BuildBucketSheetName(bucket.Key), usedSheetNames);
            AddRowsSheet(workbook, sheetName, bucket.ToList(), keyColumns, otherColumns, compareColumnName, includeAbsoluteDifference: true);
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void AddSummarySheet(
        XLWorkbook workbook, ComparisonResult result, DifferenceGroupingConfig groupingConfig, HashSet<string> usedSheetNames)
    {
        var sheet = workbook.Worksheets.Add(ReserveName("Summary", usedSheetNames));

        sheet.Cell(1, 1).Value = "Status";
        sheet.Cell(1, 2).Value = "Count";
        sheet.Row(1).Style.Font.Bold = true;
        sheet.Cell(2, 1).Value = "Matching";
        sheet.Cell(2, 2).Value = result.MatchingCount;
        sheet.Cell(3, 1).Value = "Added";
        sheet.Cell(3, 2).Value = result.AddedCount;
        sheet.Cell(4, 1).Value = "Deleted";
        sheet.Cell(4, 2).Value = result.DeletedCount;
        sheet.Cell(5, 1).Value = "Different";
        sheet.Cell(5, 2).Value = result.DifferentCount;

        var row = 7;
        sheet.Cell(row, 1).Value = "Difference-magnitude grouping boundaries";
        sheet.Cell(row, 1).Style.Font.Bold = true;
        row++;

        var lower = 0m;
        foreach (var boundary in groupingConfig.Groups.OrderBy(g => g.UpperBound))
        {
            sheet.Cell(row, 1).Value = $"{lower:0.####} < diff <= {boundary.UpperBound:0.####}";
            row++;
            lower = boundary.UpperBound;
        }
        sheet.Cell(row, 1).Value = $"{GroupingService.OverflowGroupLabel} ({lower:0.####})";
        row++;

        if (result.Warnings.Count > 0)
        {
            row++;
            sheet.Cell(row, 1).Value = "Warnings";
            sheet.Cell(row, 1).Style.Font.Bold = true;
            row++;
            foreach (var warning in result.Warnings)
            {
                sheet.Cell(row, 1).Value = warning;
                row++;
            }
        }

        sheet.Columns().AdjustToContents();
    }

    private static void AddRowsSheet(
        XLWorkbook workbook,
        string sheetName,
        List<RowResult> rows,
        List<string> keyColumns,
        List<string> otherColumns,
        string compareColumnName,
        bool includeAbsoluteDifference)
    {
        var sheet = workbook.Worksheets.Add(sheetName);

        var col = 1;
        foreach (var key in keyColumns)
        {
            sheet.Cell(1, col++).Value = key;
        }
        sheet.Cell(1, col++).Value = $"{compareColumnName} (Convertor)";
        sheet.Cell(1, col++).Value = $"{compareColumnName} (Client)";
        if (includeAbsoluteDifference)
        {
            sheet.Cell(1, col++).Value = "AbsoluteDifference";
        }
        sheet.Cell(1, col++).Value = "Other columns";
        sheet.Row(1).Style.Font.Bold = true;

        var rowIndex = 2;
        foreach (var row in rows)
        {
            col = 1;
            foreach (var keyValue in row.KeyValues)
            {
                sheet.Cell(rowIndex, col++).Value = keyValue;
            }
            sheet.Cell(rowIndex, col++).Value = row.ConvertorCompareValue.HasValue ? row.ConvertorCompareValue.Value : (XLCellValue)string.Empty;
            sheet.Cell(rowIndex, col++).Value = row.ClientCompareValue.HasValue ? row.ClientCompareValue.Value : (XLCellValue)string.Empty;
            if (includeAbsoluteDifference)
            {
                sheet.Cell(rowIndex, col++).Value = row.AbsoluteDifference.HasValue ? row.AbsoluteDifference.Value : (XLCellValue)string.Empty;
            }
            sheet.Cell(rowIndex, col++).Value = OtherColumnsFormatter.Combine(row, otherColumns);
            rowIndex++;
        }

        sheet.Columns().AdjustToContents();
    }

    private static string BuildBucketSheetName(string differenceGroupLabel)
    {
        var numbers = System.Text.RegularExpressions.Regex.Matches(differenceGroupLabel, @"[\d.]+")
            .Select(m => m.Value)
            .ToList();

        if (differenceGroupLabel.Contains(GroupingService.OverflowGroupLabel) && numbers.Count >= 1)
        {
            return $"Diff gt {numbers[0]}";
        }
        if (numbers.Count >= 2)
        {
            return $"Diff {numbers[0]}-{numbers[1]}";
        }
        return "Different";
    }

    private static string ReserveName(string proposed, HashSet<string> usedSheetNames)
    {
        var sanitized = SanitizeSheetName(proposed);
        var candidate = sanitized;
        var suffixIndex = 2;
        while (usedSheetNames.Contains(candidate))
        {
            var suffix = $" ({suffixIndex})";
            var maxBaseLength = 31 - suffix.Length;
            var basePart = sanitized.Length > maxBaseLength ? sanitized[..maxBaseLength] : sanitized;
            candidate = basePart + suffix;
            suffixIndex++;
        }
        usedSheetNames.Add(candidate);
        return candidate;
    }

    private static string SanitizeSheetName(string name)
    {
        foreach (var invalidChar in InvalidSheetNameChars)
        {
            name = name.Replace(invalidChar, '-');
        }
        name = name.Trim();
        if (name.Length > 31)
        {
            name = name[..31];
        }
        return string.IsNullOrWhiteSpace(name) ? "Sheet" : name;
    }
}
