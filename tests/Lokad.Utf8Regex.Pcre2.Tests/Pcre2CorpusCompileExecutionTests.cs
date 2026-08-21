using Lokad.Utf8Regex.Pcre2;
using Lokad.Utf8Regex.Pcre2.Tests.Corpus;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2CorpusCompileExecutionTests
{
    [Theory]
    [MemberData(nameof(Pcre2CorpusExecutionData.CompileCases), MemberType = typeof(Pcre2CorpusExecutionData))]
    public void ActiveCompileCorpusCasesMatchExpectedCompileOutcome(Pcre2CorpusCase corpusCase)
    {
        void Compile()
        {
            var options = Pcre2CorpusOptions.ParseCompileOptions(corpusCase.CompileOptions);
            var settings = Pcre2CorpusOptions.CreateCompileSettings(corpusCase.CompileSettings);
            if (corpusCase.PatternBytesBase64 is { } patternBytesBase64)
            {
                var patternBytes = Convert.FromBase64String(patternBytesBase64);
                _ = new Utf8Pcre2Regex(patternBytes, options, settings, default, Utf8Pcre2Regex.DefaultMatchTimeout);
                return;
            }

            _ = new Utf8Pcre2Regex(corpusCase.Pattern, options, settings, default, Utf8Pcre2Regex.DefaultMatchTimeout);
        }

        Action action = Compile;

        if (corpusCase.Expected.Outcome == Pcre2CorpusOutcomeKind.CompileError)
        {
            var exception = Assert.Throws<Pcre2CompileException>(action);
            Assert.Equal(corpusCase.Expected.ErrorKind, exception.ErrorKind.ToString());
        }
        else
        {
            action();
        }
    }

}
