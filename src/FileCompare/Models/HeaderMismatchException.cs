namespace FileCompare.Models;

public class HeaderMismatchException : Exception
{
    public List<string> OnlyInConverterFile { get; }
    public List<string> OnlyInClientFile { get; }

    public HeaderMismatchException(List<string> onlyInConverterFile, List<string> onlyInClientFile)
        : base(BuildMessage(onlyInConverterFile, onlyInClientFile))
    {
        OnlyInConverterFile = onlyInConverterFile;
        OnlyInClientFile = onlyInClientFile;
    }

    private static string BuildMessage(List<string> onlyInConverterFile, List<string> onlyInClientFile)
    {
        var parts = new List<string>();
        if (onlyInConverterFile.Count > 0)
        {
            parts.Add($"only in Converter output file: {string.Join(", ", onlyInConverterFile)}");
        }
        if (onlyInClientFile.Count > 0)
        {
            parts.Add($"only in Client expected file: {string.Join(", ", onlyInClientFile)}");
        }
        return $"Header columns do not match between files ({string.Join("; ", parts)}).";
    }
}
