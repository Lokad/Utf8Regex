namespace Lokad.Utf8Regex.Pcre2;

public class Pcre2CompileException : Exception
{
    public Pcre2CompileException(string message, string errorKind)
        : base(message)
    {
        ErrorKind = errorKind;
    }

    internal Pcre2CompileException(string message, Pcre2ErrorKind errorKind)
        : this(message, errorKind.ToPublicString())
    {
    }

    public string ErrorKind { get; }
}

public class Pcre2MatchException : Exception
{
    public Pcre2MatchException(string message)
        : this(message, null)
    {
    }

    public Pcre2MatchException(string message, string? errorKind)
        : base(message)
    {
        ErrorKind = errorKind;
    }

    internal Pcre2MatchException(string message, Pcre2ErrorKind errorKind)
        : this(message, errorKind.ToPublicString())
    {
    }

    public string? ErrorKind { get; }
}

public class Pcre2SubstitutionException : Exception
{
    public Pcre2SubstitutionException(string message)
        : this(message, null)
    {
    }

    public Pcre2SubstitutionException(string message, string? errorKind)
        : base(message)
    {
        ErrorKind = errorKind;
    }

    internal Pcre2SubstitutionException(string message, Pcre2ErrorKind errorKind)
        : this(message, errorKind.ToPublicString())
    {
    }

    public string? ErrorKind { get; }
}
