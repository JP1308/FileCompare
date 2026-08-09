using System.Text;
using FileCompare.Models;

namespace FileCompare.Services;

public class FileParserService
{
    public ParsedFile ParseDelimitedText(byte[] content, char delimiter)
    {
        var text = DecodeText(content);
        return Parse(text, delimiter);
    }

    public ParsedFile Parse(string content, char delimiter = ';')
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

        var headers = lines[0].Split(delimiter).Select(h => h.Trim()).ToList();

        if (headers.Count == 1 && !lines.Any(l => l.Contains(delimiter)))
        {
            throw new InvalidOperationException(
                $"No '{delimiter}' delimiter found in the file. Check that the selected delimiter matches the file's format.");
        }

        var rows = new List<Dictionary<string, string>>();

        for (var lineIndex = 1; lineIndex < lines.Count; lineIndex++)
        {
            var fields = lines[lineIndex].Split(delimiter).Select(f => f.Trim()).ToArray();
            if (fields.Length != headers.Count)
            {
                rowErrors.Add($"Line {lineIndex + 1}: expected {headers.Count} fields but found {fields.Length}. Row skipped.");
                continue;
            }

            var row = new Dictionary<string, string>(UmlautInsensitiveComparer.Instance);
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
        var convertorSet = new HashSet<string>(convertorFile.Headers, UmlautInsensitiveComparer.Instance);
        var clientSet = new HashSet<string>(clientFile.Headers, UmlautInsensitiveComparer.Instance);

        var onlyInConvertor = convertorFile.Headers.Where(h => !clientSet.Contains(h)).ToList();
        var onlyInClient = clientFile.Headers.Where(h => !convertorSet.Contains(h)).ToList();

        if (onlyInConvertor.Count > 0 || onlyInClient.Count > 0)
        {
            throw new HeaderMismatchException(onlyInConvertor, onlyInClient);
        }
    }

    /// <summary>Decodes bytes as UTF-8 (honoring a BOM if present); falls back to Windows-1252 if the bytes aren't valid UTF-8.</summary>
    private static string DecodeText(byte[] content)
    {
        if (content.Length >= 3 && content[0] == 0xEF && content[1] == 0xBB && content[2] == 0xBF)
        {
            return Encoding.UTF8.GetString(content, 3, content.Length - 3);
        }

        var strictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        try
        {
            return strictUtf8.GetString(content);
        }
        catch (DecoderFallbackException)
        {
            var windows1252 = Encoding.GetEncoding(1252);
            return windows1252.GetString(content);
        }
    }
}
