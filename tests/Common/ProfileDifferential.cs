using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace Lokad.Utf8Regex.TestSupport;

// Only the unambiguous grammar shared by all profiles. POSIX alternatives here
// have disjoint languages; this does not use .NET as an oracle for POSIX priority.
internal static class ProfileDifferential
{
    internal readonly record struct ByteRange(bool Success, int Start, int End);
    internal sealed record Observation(bool Exists, int Count, ByteRange First, List<ByteRange[]> Matches);
    internal delegate Observation Observe(byte[] input, int start, CancellationToken token);

    internal static void Run(string mode, Func<string, TimeSpan?, Utf8ResourcePolicy, int, Observe> create)
    {
        string[] patterns = ["ab", "a|b", "(a+)(b*)", "([a-z]+): ([0-9]+)", "([A-Z][a-z]+) ([A-Z][a-z]+)",
            "(é|中)+", "([a-z]+)ing", "[0-9]+([.][0-9]+)?", @"""[^""\n]*""", "(ab|cd)+", "", "(.)"];
        string[] tokens = ["a", "b", "ab", "cd", "ing", "A", "word: 12", "Holmes Watson", "12.5", "\"é😀\"",
            "é", "中", "😀", " ", "\n", "\t", "1", ".", ":"];
        using var cancellation = new CancellationTokenSource();
        var token = mode is "token" or "all" ? cancellation.Token : default;
        TimeSpan? timeout = mode is "timeout" or "all" ? TimeSpan.FromSeconds(10) : null;
        var policy = mode is "budget" or "all" ? new Utf8ResourcePolicy {
            MaxWorkSteps = 1_000_000, MaxDepth = 4096, MaxWorkingBytes = 16 << 20, MaxResultBytes = 16 << 20 } : default;
        foreach (var pattern in patterns)
        {
            var oracle = new Regex(pattern, RegexOptions.CultureInvariant);
            var observe = create(pattern, timeout, policy, oracle.GetGroupNumbers().Length);
            var random = new Random(0x5ca1a);
            for (var sample = 0; sample < 64; sample++)
            {
                var text = string.Concat(Enumerable.Range(0, random.Next(0, 12)).Select(_ => tokens[random.Next(tokens.Length)]));
                var symbols = text.Replace("😀", "\ue000");
                var boundaries = new List<int> { 0 };
                foreach (var scalar in text.EnumerateRunes()) boundaries.Add(boundaries[^1] + scalar.Utf8SequenceLength);
                var input = Encoding.UTF8.GetBytes(text);
                var start = random.Next(symbols.Length + 1);
                var expected = oracle.Matches(symbols, start);
                var actual = observe(input, boundaries[start], token);
                var context = $"{pattern}; {mode}; start={start}; subject={System.Text.Json.JsonSerializer.Serialize(text)}";
                Assert.True(actual.Exists == oracle.IsMatch(symbols), context);
                Assert.True(actual.Count == oracle.Count(symbols), context);
                Assert.True(actual.Matches.Count == expected.Count, context);
                var first = expected.Count == 0 ? default : Convert(expected[0]);
                Assert.True(actual.First == first, context);
                for (var i = 0; i < expected.Count; i++)
                {
                    var groups = expected[i].Groups.Cast<Group>().Select(Convert).ToArray();
                    Assert.True(actual.Matches[i].SequenceEqual(groups), context);
                }
                ByteRange Convert(Group group) => group.Success
                    ? new(true, boundaries[group.Index], boundaries[group.Index + group.Length]) : default;
            }
        }
    }
}
