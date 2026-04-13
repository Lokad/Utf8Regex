namespace Lokad.Utf8Regex.Pcre2.Tests.Corpus;

public static class Pcre2CorpusCaseData
{
    public static TheoryData<Pcre2CorpusCase> ActiveCases
    {
        get
        {
            var data = new TheoryData<Pcre2CorpusCase>();
            foreach (var corpusCase in Pcre2CorpusLoader.LoadByStatus(Pcre2CorpusCaseStatus.Active))
            {
                data.Add(corpusCase);
            }

            return data;
        }
    }
}
