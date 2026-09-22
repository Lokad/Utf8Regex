using Lokad.Utf8Regex.Pcre2;
using Lokad.Utf8Regex.Pcre2.Tests.Corpus;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2CorpusRoutingTests
{
    [Fact]
    public void ActivePcre2CorpusCasesRouteToSupportedExecutionBuckets()
    {
        foreach (var corpusCase in Pcre2CorpusLoader.LoadByStatus(Pcre2CorpusCaseStatus.Active))
        {
            var executionKind = Pcre2CorpusCaseRouting.GetExecutionKind(corpusCase);
            Assert.True(Enum.IsDefined(executionKind), $"Unexpected execution bucket for '{corpusCase.Id}'.");
        }
    }

    [Fact]
    public void NoCorpusCaseSelectsABclManagedProgram()
    {
        foreach (var corpusCase in Pcre2CorpusLoader.LoadAll())
        {
            Utf8Pcre2Regex regex;
            try
            {
                regex = new Utf8Pcre2Regex(
                    corpusCase.Pattern,
                    Pcre2CorpusOptions.ParseCompileOptions(corpusCase.CompileOptions),
                    Pcre2CorpusOptions.CreateCompileSettings(corpusCase.CompileSettings),
                    default,
                    Utf8Pcre2Regex.DefaultMatchTimeout);
            }
            catch (Exception ex) when (ex is Pcre2CompileException or ArgumentException or NotSupportedException)
            {
                continue;
            }

            Assert.DoesNotContain("ManagedRegex", regex.DebugDescribeExecutionPlan(), StringComparison.Ordinal);
        }
    }
}
