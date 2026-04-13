using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Benchmarks;

internal sealed class Utf8Pcre2BenchmarkContext
{
    public Utf8Pcre2BenchmarkContext(Utf8Pcre2BenchmarkCase benchmarkCase)
    {
        BenchmarkCase = benchmarkCase;
        InputString = benchmarkCase.Input;
        InputBytes = Encoding.UTF8.GetBytes(benchmarkCase.Input);
        Utf8Pcre2Regex = new Utf8Pcre2Regex(
            benchmarkCase.Pattern,
            Utf8Pcre2BenchmarkCatalog.ToPcre2Options(benchmarkCase.Options),
            benchmarkCase.CompileSettings);

        if ((benchmarkCase.SupportedBackends & Utf8Pcre2BenchmarkBackend.Utf8Regex) != 0)
        {
            Utf8Regex = new Utf8Regex(benchmarkCase.Pattern, benchmarkCase.Options);
        }

        if ((benchmarkCase.SupportedBackends & (Utf8Pcre2BenchmarkBackend.DecodeThenRegex | Utf8Pcre2BenchmarkBackend.PredecodedRegex)) != 0)
        {
            Regex = new Regex(benchmarkCase.Pattern, benchmarkCase.Options, Regex.InfiniteMatchTimeout);
        }
    }

    public Utf8Pcre2BenchmarkCase BenchmarkCase { get; }

    public string InputString { get; }

    public byte[] InputBytes { get; }

    public Utf8Regex? Utf8Regex { get; }

    public Utf8Pcre2Regex Utf8Pcre2Regex { get; }

    public Regex? Regex { get; }

    public string Replacement => BenchmarkCase.Replacement;
}
