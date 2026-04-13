namespace Lokad.Utf8Regex.Pcre2;

public class Pcre2CompileException : Exception
{
    public Pcre2CompileException(string message, string errorKind)
        : base(message)
    {
        ErrorKind = errorKind;
    }

    public string ErrorKind { get; }
}

public class Pcre2MatchException : Exception
{
    public Pcre2MatchException(string message, string? errorKind = null)
        : base(message)
    {
        ErrorKind = errorKind;
    }

    public string? ErrorKind { get; }
}

public class Pcre2SubstitutionException : Exception
{
    public Pcre2SubstitutionException(string message, string? errorKind = null)
        : base(message)
    {
        ErrorKind = errorKind;
    }

    public string? ErrorKind { get; }
}
