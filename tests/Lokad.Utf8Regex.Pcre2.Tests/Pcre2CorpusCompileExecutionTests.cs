using Lokad.Utf8Regex.Pcre2;
using Lokad.Utf8Regex.Pcre2.Tests.Corpus;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2CorpusCompileExecutionTests
{
    [Theory]
    [MemberData(nameof(Pcre2CorpusExecutionData.CompileCases), MemberType = typeof(Pcre2CorpusExecutionData))]
    public void ActiveCompileCorpusCasesMatchExpectedCompileOutcome(Pcre2CorpusCase corpusCase)
    {
        var action = () => _ = new Utf8Pcre2Regex(
            corpusCase.Pattern,
            ParseCompileOptions(corpusCase.CompileOptions),
            CreateSettings(corpusCase.CompileSettings));

        if (corpusCase.Expected.Outcome == Pcre2CorpusOutcomeKind.CompileError)
        {
            var exception = Assert.Throws<Pcre2CompileException>(action);
            Assert.Equal(corpusCase.Expected.ErrorKind, exception.ErrorKind);
        }
        else
        {
            action();
        }
    }

    private static Pcre2CompileOptions ParseCompileOptions(string[] options)
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

    private static Utf8Pcre2CompileSettings CreateSettings(Pcre2CorpusCompileSettings settings)
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
