namespace Lokad.Utf8Regex.Pcre2;

/// <summary>Reports a PCRE2-compatible pattern compilation failure.</summary>
public class Pcre2CompileException : Exception
{
    /// <summary>Initializes an exception with its diagnostic message and closed error kind.</summary>
    /// <param name="message">The human-readable diagnostic.</param>
    /// <param name="errorKind">The machine-readable compile failure.</param>
    public Pcre2CompileException(string message, Pcre2ErrorKind errorKind)
        : base(message)
    {
        ErrorKind = errorKind;
    }

    /// <summary>Gets the machine-readable compile failure.</summary>
    public Pcre2ErrorKind ErrorKind { get; }
}

/// <summary>Reports a PCRE2-compatible matching failure.</summary>
public class Pcre2MatchException : Exception
{
    /// <summary>Initializes an exception with its diagnostic message and closed error kind.</summary>
    /// <param name="message">The human-readable diagnostic.</param>
    /// <param name="errorKind">The machine-readable matching failure.</param>
    public Pcre2MatchException(string message, Pcre2ErrorKind errorKind)
        : base(message)
    {
        ErrorKind = errorKind;
    }

    /// <summary>Gets the machine-readable matching failure.</summary>
    public Pcre2ErrorKind ErrorKind { get; }
}

/// <summary>Reports a PCRE2-compatible substitution failure.</summary>
public class Pcre2SubstitutionException : Exception
{
    /// <summary>Initializes an exception with its diagnostic message and closed error kind.</summary>
    /// <param name="message">The human-readable diagnostic.</param>
    /// <param name="errorKind">The machine-readable substitution failure.</param>
    public Pcre2SubstitutionException(string message, Pcre2ErrorKind errorKind)
        : base(message)
    {
        ErrorKind = errorKind;
    }

    /// <summary>Gets the machine-readable substitution failure.</summary>
    public Pcre2ErrorKind ErrorKind { get; }
}
