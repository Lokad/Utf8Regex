using System.Text;
using System.Text.RegularExpressions;
using BenchmarkDotNet.Attributes;

namespace Lokad.Utf8Regex.Benchmarks;

[MemoryDiagnoser]
[BenchmarkCategory("StructuralPositive", "AwsKeys")]
public class AwsKeyPositiveWindowBenchmarks
{
    private const string Pattern = "(('|\")((?:ASIA|AKIA|AROA|AIDA)([A-Z0-7]{16}))('|\").*?(\\n^.*?){0,4}(('|\")[a-zA-Z0-9+/]{40}('|\"))+|('|\")[a-zA-Z0-9+/]{40}('|\").*?(\\n^.*?){0,3}('|\")((?:ASIA|AKIA|AROA|AIDA)([A-Z0-7]{16}))('|\"))+";
    private const string AccessKey = "\"AIDAABCDEFGHIJKLMNOP\"";
    private const string Secret = "\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"";

    private Utf8Regex _utf8Regex = null!;
    private Regex _regex = null!;
    private byte[] _inputBytes = null!;
    private string _inputString = string.Empty;

    [Params("forward", "reverse")]
    public string Scenario { get; set; } = string.Empty;

    [GlobalSetup]
    public void Setup()
    {
        _inputString = BuildInput(Scenario);
        _inputBytes = Encoding.UTF8.GetBytes(_inputString);
        _utf8Regex = new Utf8Regex(Pattern, RegexOptions.Multiline);
        _regex = new Regex(Pattern, RegexOptions.Multiline, Regex.InfiniteMatchTimeout);

        var utf8Count = _utf8Regex.Count(_inputBytes);
        var dotnetCount = _regex.Count(_inputString);
        if (utf8Count != dotnetCount)
        {
            throw new InvalidOperationException($"Count mismatch for {Scenario}: utf8={utf8Count}, dotnet={dotnetCount}");
        }
    }

    [Benchmark(Baseline = true)]
    public int Utf8Regex() => _utf8Regex.Count(_inputBytes);

    [Benchmark]
    public int DotNetRegex() => _regex.Count(_inputString);

    private static string BuildInput(string scenario)
    {
        var builder = new StringBuilder(capacity: 256 * 256);
        for (var i = 0; i < 256; i++)
        {
            builder.AppendLine("prefix = \"AKIAAAAAAAAAAAAAAAA\"  # decoy key without secret");
            builder.AppendLine("gap = 1");
            builder.AppendLine("gap = 2");
            builder.AppendLine("gap = 3");
            builder.AppendLine("gap = 4");
            builder.AppendLine("gap = 5");
            builder.AppendLine("secret = \"bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb\"  # decoy secret without key");
            builder.AppendLine("unrelated = \"cccccccccccccccccccccccccccccccccccccccc\"");

            switch (scenario)
            {
                case "forward":
                    builder.AppendLine(AccessKey);
                    builder.AppendLine("ctx = 1");
                    builder.AppendLine("ctx = 2");
                    builder.AppendLine(Secret);
                    break;

                case "reverse":
                    builder.AppendLine(Secret);
                    builder.AppendLine("ctx = 1");
                    builder.AppendLine(AccessKey);
                    break;

                default:
                    throw new InvalidOperationException($"Unknown scenario '{scenario}'.");
            }

            builder.AppendLine("tail = \"dddddddddddddddddddddddddddddddddddddddd\"");
            builder.AppendLine("sep = 1");
            builder.AppendLine("sep = 2");
            builder.AppendLine("sep = 3");
            builder.AppendLine("sep = 4");
            builder.AppendLine("sep = 5");
            builder.AppendLine("sep = 6");
            builder.AppendLine();
        }

        return builder.ToString();
    }
}
