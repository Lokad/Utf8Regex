using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Benchmarks;

internal sealed class Utf8Pcre2BenchmarkCase
{
    public Utf8Pcre2BenchmarkCase(
        string id,
        string pattern,
        string input,
        RegexOptions options = RegexOptions.CultureInvariant,
        Utf8Pcre2CompileSettings compileSettings = default,
        string replacement = "bar",
        Utf8Pcre2BenchmarkOperation supportedOperations = Utf8Pcre2BenchmarkOperation.All,
        Utf8Pcre2BenchmarkBackend supportedBackends = Utf8Pcre2BenchmarkBackend.All)
    {
        Id = id;
        Pattern = pattern;
        Input = input;
        Options = options;
        CompileSettings = compileSettings;
        Replacement = replacement;
        SupportedOperations = supportedOperations;
        SupportedBackends = supportedBackends;
    }

    public string Id { get; }

    public string Pattern { get; }

    public string Input { get; }

    public RegexOptions Options { get; }

    public Utf8Pcre2CompileSettings CompileSettings { get; }

    public string Replacement { get; }

    public Utf8Pcre2BenchmarkOperation SupportedOperations { get; }

    public Utf8Pcre2BenchmarkBackend SupportedBackends { get; }

    public bool Supports(Utf8Pcre2BenchmarkOperation operation, Utf8Pcre2BenchmarkBackend backends)
    {
        return (SupportedOperations & operation) != 0 &&
            (SupportedBackends & backends) == backends;
    }
}
