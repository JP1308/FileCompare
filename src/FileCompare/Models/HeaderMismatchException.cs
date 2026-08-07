namespace FileCompare.Models;

public class HeaderMismatchException : Exception
{
    public List<string> OnlyInConvertorFile { get; }
    public List<string> OnlyInClientFile { get; }

    public HeaderMismatchException(List<string> onlyInConvertorFile, List<string> onlyInClientFile)
        : base(BuildMessage(onlyInConvertorFile, onlyInClientFile))
    {
        OnlyInConvertorFile = onlyInConvertorFile;
        OnlyInClientFile = onlyInClientFile;
    }

    private static string BuildMessage(List<string> onlyInConvertorFile, List<string> onlyInClientFile)
    {
        var parts = new List<string>();
        if (onlyInConvertorFile.Count > 0)
        {
            parts.Add($"only in Convertor output file: {string.Join(", ", onlyInConvertorFile)}");
        }
        if (onlyInClientFile.Count > 0)
        {
            parts.Add($"only in Client expected file: {string.Join(", ", onlyInClientFile)}");
        }
        return $"Header columns do not match between files ({string.Join("; ", parts)}).";
    }
}
