namespace Lokad.Utf8Regex.Pcre2.Tests.Corpus;

public static class Pcre2CorpusExecutionData
{
    public static TheoryData<Pcre2CorpusCase> CompileCases
    {
        get
        {
            var data = new TheoryData<Pcre2CorpusCase>();
            foreach (var corpusCase in Pcre2CorpusLoader.LoadAll().Where(Pcre2CorpusExecutionFilter.CanExecuteCompile))
            {
                data.Add(corpusCase);
            }

            return data;
        }
    }

    public static TheoryData<Pcre2CorpusCase> MatchCases
    {
        get
        {
            var data = new TheoryData<Pcre2CorpusCase>();
            foreach (var corpusCase in Pcre2CorpusLoader.LoadAll().Where(Pcre2CorpusExecutionFilter.CanExecuteMatch))
            {
                data.Add(corpusCase);
            }

            return data;
        }
    }

    public static TheoryData<Pcre2CorpusCase> ProbeSubsetCases
    {
        get
        {
            var data = new TheoryData<Pcre2CorpusCase>();
            foreach (var corpusCase in Pcre2CorpusLoader.LoadAll().Where(Pcre2CorpusExecutionFilter.CanExecuteProbeSubset))
            {
                data.Add(corpusCase);
            }

            return data;
        }
    }

    public static TheoryData<Pcre2CorpusCase> ReplaceSubsetCases
    {
        get
        {
            var data = new TheoryData<Pcre2CorpusCase>();
            foreach (var corpusCase in Pcre2CorpusLoader.LoadAll().Where(Pcre2CorpusExecutionFilter.CanExecuteReplaceSubset))
            {
                data.Add(corpusCase);
            }

            return data;
        }
    }

    public static TheoryData<Pcre2CorpusCase> CountSubsetCases
    {
        get
        {
            var data = new TheoryData<Pcre2CorpusCase>();
            foreach (var corpusCase in Pcre2CorpusLoader.LoadAll().Where(Pcre2CorpusExecutionFilter.CanExecuteCountSubset))
            {
                data.Add(corpusCase);
            }

            return data;
        }
    }

    public static TheoryData<Pcre2CorpusCase> EnumerateSubsetCases
    {
        get
        {
            var data = new TheoryData<Pcre2CorpusCase>();
            foreach (var corpusCase in Pcre2CorpusLoader.LoadAll().Where(Pcre2CorpusExecutionFilter.CanExecuteEnumerateSubset))
            {
                data.Add(corpusCase);
            }

            return data;
        }
    }
}
