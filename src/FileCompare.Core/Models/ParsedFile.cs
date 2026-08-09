namespace FileCompare.Models;

public class ParsedFile
{
    public List<string> Headers { get; init; } = new();
    public List<Dictionary<string, string>> Rows { get; init; } = new();
    public List<string> RowErrors { get; init; } = new();
}
