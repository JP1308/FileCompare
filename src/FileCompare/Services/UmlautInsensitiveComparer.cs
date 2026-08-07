namespace FileCompare.Services;

/// <summary>
/// Ordinal string comparer that additionally folds the umlaut case pairs Ä/ä, Ö/ö, Ü/ü so header
/// names differing only in umlaut casing are treated as equal. All other characters (including ß,
/// which has no case pair here) remain strictly case-sensitive.
/// </summary>
public sealed class UmlautInsensitiveComparer : IEqualityComparer<string>
{
    public static readonly UmlautInsensitiveComparer Instance = new();

    public bool Equals(string? x, string? y)
    {
        if (ReferenceEquals(x, y))
        {
            return true;
        }
        if (x is null || y is null)
        {
            return false;
        }
        return string.Equals(Normalize(x), Normalize(y), StringComparison.Ordinal);
    }

    public int GetHashCode(string obj) => Normalize(obj).GetHashCode(StringComparison.Ordinal);

    private static string Normalize(string value)
    {
        if (value.IndexOfAny(UmlautUppercase) < 0)
        {
            return value;
        }

        var buffer = new char[value.Length];
        for (var i = 0; i < value.Length; i++)
        {
            buffer[i] = value[i] switch
            {
                'Ä' => 'ä',
                'Ö' => 'ö',
                'Ü' => 'ü',
                var c => c,
            };
        }
        return new string(buffer);
    }

    private static readonly char[] UmlautUppercase = { 'Ä', 'Ö', 'Ü' };
}
