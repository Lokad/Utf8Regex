namespace Lokad.Utf8Regex.Internal.FrontEnd;

/// <summary>Borrowed construction input. Keeps byte patterns undecoded until
/// the constructor worker has entered its shared admission scope.</summary>
internal readonly ref struct Utf8ConstructionPattern
{
    internal Utf8ConstructionPattern(string text)
    {
        Text = text;
        Utf8 = [];
        IsUtf8 = false;
    }

    internal Utf8ConstructionPattern(ReadOnlySpan<byte> utf8)
    {
        Text = string.Empty;
        Utf8 = utf8;
        IsUtf8 = true;
    }

    internal string Text { get; }
    internal ReadOnlySpan<byte> Utf8 { get; }
    internal bool IsUtf8 { get; }
}
