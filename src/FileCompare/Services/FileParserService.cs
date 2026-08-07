using FileCompare.Models;

namespace FileCompare.Services;

public class FileParserService
{
    private const char Delimiter = ';';

    public ParsedFile Parse(string content)
    {
        var rowErrors = new List<string>();
        var lines = content.Split('\n')
            .Select(l => l.TrimEnd('\r'))
            .Where(l => l.Trim().Length > 0)
            .ToList();

        if (lines.Count == 0)
        {
            return new ParsedFile { Headers = new List<string>(), Rows = new List<Dictionary<string, string>>() };
        }

        var headers = lines[0].Split(Delimiter).Select(h => h.Trim()).ToList();
        var rows = new List<Dictionary<string, string>>();

        for (var lineIndex = 1; lineIndex < lines.Count; lineIndex++)
        {
            var fields = lines[lineIndex].Split(Delimiter).Select(f => f.Trim()).ToArray();
            if (fields.Length != headers.Count)
            {
                rowErrors.Add($"Line {lineIndex + 1}: expected {headers.Count} fields but found {fields.Length}. Row skipped.");
                continue;
            }

            var row = new Dictionary<string, string>();
            for (var i = 0; i < headers.Count; i++)
            {
                row[headers[i]] = fields[i];
            }
            rows.Add(row);
        }

        return new ParsedFile { Headers = headers, Rows = rows, RowErrors = rowErrors };
    }

    public void ValidateHeaders(ParsedFile convertorFile, ParsedFile clientFile)
    {
        var convertorSet = new HashSet<string>(convertorFile.Headers, StringComparer.Ordinal);
        var clientSet = new HashSet<string>(clientFile.Headers, StringComparer.Ordinal);

        var onlyInConvertor = convertorFile.Headers.Where(h => !clientSet.Contains(h)).ToList();
        var onlyInClient = clientFile.Headers.Where(h => !convertorSet.Contains(h)).ToList();

        if (onlyInConvertor.Count > 0 || onlyInClient.Count > 0)
        {
            throw new HeaderMismatchException(onlyInConvertor, onlyInClient);
        }
    }
}
