using Lokad.Utf8Regex.PythonRe.Tests.Corpus;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReCorpusExecutionTests
{
    public static TheoryData<string> ActiveCaseIds()
    {
        var data = new TheoryData<string>();
        foreach (var corpusCase in PythonReCorpusLoader.LoadByStatus(PythonReCorpusCaseStatus.Active))
        {
            data.Add(corpusCase.Id);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(ActiveCaseIds))]
    public void ActiveCorpusCaseExecutesAsExpected(string caseId)
    {
        var corpusCase = PythonReCorpusLoader.LoadAll().Single(c => c.Id == caseId);
        var options = ParseOptions(corpusCase.CompileOptions);

        if (corpusCase.Expected.Outcome == PythonReCorpusOutcomeKind.PatternError)
        {
            var ex = Assert.Throws<PythonRePatternException>(() => new Utf8PythonRegex(corpusCase.Pattern, options));
            Assert.Contains(corpusCase.Expected.ErrorContains!, ex.Message, StringComparison.OrdinalIgnoreCase);
            return;
        }

        var regex = new Utf8PythonRegex(corpusCase.Pattern, options);
        var input = System.Text.Encoding.UTF8.GetBytes(corpusCase.InputText);
        switch (corpusCase.Operation)
        {
            case PythonReCorpusOperationKind.Search:
            {
                var match = regex.Search(input, corpusCase.StartOffsetInBytes);
                Assert.Equal(corpusCase.Expected.Success, match.Success);
                if (match.Success)
                {
                    Assert.Equal(corpusCase.Expected.ValueText, match.GetValueString());
                }

                break;
            }
            case PythonReCorpusOperationKind.Match:
            {
                var match = regex.Match(input, corpusCase.StartOffsetInBytes);
                Assert.Equal(corpusCase.Expected.Success, match.Success);
                if (match.Success)
                {
                    Assert.Equal(corpusCase.Expected.ValueText, match.GetValueString());
                }

                break;
            }
            case PythonReCorpusOperationKind.FullMatch:
            {
                var match = regex.FullMatch(input, corpusCase.StartOffsetInBytes);
                Assert.Equal(corpusCase.Expected.Success, match.Success);
                if (match.Success)
                {
                    Assert.Equal(corpusCase.Expected.ValueText, match.GetValueString());
                }

                break;
            }
            case PythonReCorpusOperationKind.Count:
                Assert.Equal(corpusCase.Expected.MatchCount, regex.Count(input, corpusCase.StartOffsetInBytes));
                break;
            case PythonReCorpusOperationKind.Replace:
                Assert.Equal(corpusCase.Expected.ReplacementText, regex.ReplaceToString(input, corpusCase.ReplacementPattern!, startOffsetInBytes: corpusCase.StartOffsetInBytes));
                break;
            case PythonReCorpusOperationKind.Compile:
                _ = regex;
                break;
            default:
                throw new InvalidOperationException($"Unsupported operation '{corpusCase.Operation}'.");
        }
    }

    private static PythonReCompileOptions ParseOptions(string[] optionNames)
    {
        var options = PythonReCompileOptions.None;
        foreach (var name in optionNames)
        {
            options |= Enum.Parse<PythonReCompileOptions>(name, ignoreCase: false);
        }

        return options;
    }
}
