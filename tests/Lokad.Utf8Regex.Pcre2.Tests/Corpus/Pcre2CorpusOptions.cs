using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests.Corpus;

internal static class Pcre2CorpusOptions
{
    internal static Pcre2CompileOptions ParseCompileOptions(string[] options)
    {
        var result = Pcre2CompileOptions.None;
        foreach (var option in options)
        {
            result |= option switch
            {
                "IgnoreCase" => Pcre2CompileOptions.Caseless,
                _ => Enum.Parse<Pcre2CompileOptions>(option, ignoreCase: false),
            };
        }

        return result;
    }

    internal static Utf8Pcre2CompileSettings CreateCompileSettings(Pcre2CorpusCompileSettings settings)
    {
        return new Utf8Pcre2CompileSettings
        {
            AllowDuplicateNames = settings.AllowDuplicateNames,
            BackslashC = Enum.Parse<Pcre2BackslashCPolicy>(settings.BackslashC, ignoreCase: false),
            AllowLookaroundBackslashK = settings.AllowLookaroundBackslashK,
            Newline = Enum.Parse<Pcre2NewlineConvention>(settings.Newline, ignoreCase: false),
            Bsr = Enum.Parse<Pcre2BsrConvention>(settings.Bsr, ignoreCase: false),
        };
    }
}
