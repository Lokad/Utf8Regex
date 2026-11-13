namespace Lokad.Utf8Regex;

/// <summary>Controls validation of subjects supplied to a regular expression.</summary>
public enum Utf8InputValidation
{
    /// <summary>Validate UTF-8 before publishing a result: the whole subject in Core, or the native UTF checking window in PCRE2.</summary>
    Validate = 0,

    /// <summary>Trust the caller's guarantee that the entire subject is valid UTF-8 and borrowed bytes remain unchanged during use.</summary>
    /// <remarks>Pattern and replacement validation, managed bounds checks and cooperative interruption remain active. Malformed subjects violate this input contract.</remarks>
    AssumeValid,
}
