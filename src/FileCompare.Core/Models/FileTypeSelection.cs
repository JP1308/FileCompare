namespace FileCompare.Models;

public enum InputFileFormat
{
    Csv,
    DelimitedText,
}

public record FileTypeSelection
{
    public InputFileFormat Format { get; init; } = InputFileFormat.DelimitedText;
    public char Delimiter { get; init; } = ';';

    public static FileTypeSelection Default() => new();
}
