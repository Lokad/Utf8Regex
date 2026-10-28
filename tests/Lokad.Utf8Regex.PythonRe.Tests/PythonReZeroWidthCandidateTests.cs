using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReZeroWidthCandidateTests
{
    private static Utf8SliceProgram Program(Utf8PythonRegex regex) =>
        (Utf8SliceProgram)typeof(Utf8PythonRegex).GetField("_scalarProgram", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(regex)!;

    private static List<(int Start, int End)> Unguided(Utf8SliceProgram program, byte[] input, int start)
    {
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Timeout.InfiniteTimeSpan, default);
        _ = Utf8Validation.Validate(input, ref invocation);
        var slots = Utf8SliceRunner.RentSlots(program.GroupCount, ref invocation);
        var workspace = new Utf8SliceRunner.AttemptWorkspace();
        var ranges = new List<(int, int)>();
        try
        {
            while (start <= input.Length)
            {
                invocation.AnchorOriginByte = start;
                var match = Utf8SliceRunner.TryFindFirst(program, input, start,
                    slots.Starts, slots.Ends, false, ref workspace, ref invocation);
                Assert.Equal(Utf8ExecutionLimitKind.None, match.Breach);
                if (!match.Found) break;
                Assert.True(match.EndByte > match.StartByte); // Every test language is nonnullable.
                ranges.Add((match.StartByte, match.EndByte));
                start = match.EndByte;
            }
            return ranges;
        }
        finally
        {
            workspace.Release(ref invocation);
            Utf8SliceRunner.ReleaseSlots(slots, ref invocation);
        }
    }

    [Theory]
    [InlineData(@"\b\d{1,2}/\d{1,2}/\d{2,4}\b", "Today is 11/18/201A and tomorrow is 11/19/201A. 2/3/24!")]
    [InlineData(@"\b\d+\b", "a1 12_ 34 567x 89")]
    [InlineData(@"\b(?P<n>\d+)\b", "a1 12_ 3é ٤٥ ६७ 𝟙𝟚 9\u0301")]
    [InlineData(@"\b\d+\b", "é12 😀12 𐐀34 𝟙𝟚 56😀")]
    [InlineData(@"\B\d+", "a123 45 x6 78_")]
    [InlineData(@"(?=[A-Z])\d+", "AB 123")]
    [InlineData(@"(?=(?P<x>\d+))\d+-(?P=x)", "abc 12-12 34-56 78-78")]
    [InlineData(@"(?:^|(?=x))[a-z]+", "99xyz abc")]
    [InlineData(@"(?<=:)\w+", "a:one :_two 😀:é3 :𝟙𝟚")]
    [InlineData(@"(?<!:)\s+", ":    x \t \u2003")]
    public void PublicDiscoveryMatchesDirectVmAtEveryScalarOffset(string pattern, string text)
    {
        var regex = new Utf8PythonRegex(pattern);
        var input = Encoding.UTF8.GetBytes(text);
        var program = Program(regex);
        var finite = new Utf8PythonRegex(pattern, PythonReCompileOptions.None, Timeout.InfiniteTimeSpan,
            new() { MaxWorkSteps = 1000000, MaxDepth = 128, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20 });
        using var token = new CancellationTokenSource();
        for (var start = 0; start <= input.Length; start++)
        {
            if (start < input.Length && (input[start] & 0xc0) == 0x80) continue;
            var expected = Unguided(program, input, start);
            foreach (var owner in new[] { regex, finite })
            {
                Assert.Equal(expected.Count != 0, owner.IsMatch(input, start));
                Assert.Equal(expected.Count != 0, owner.IsMatch(input, start, token.Token));
                Assert.Equal(expected.Count, owner.Count(input, start, token.Token));
                var matches = owner.FindIterDetailed(input, start, token.Token);
                Assert.Equal(expected.Count, matches.Length);
                for (var index = 0; index < matches.Length; index++)
                    Assert.Equal(expected[index], (matches[index].Value.StartOffsetInBytes, matches[index].Value.EndOffsetInBytes));
                var match = owner.Match(input, start, token.Token);
                Assert.Equal(expected.Count != 0 && expected[0].Start == start, match.Success);
            }
        }
    }

    [Fact]
    public void PythonWordBoundaryAndRetainedCapturesKeepTheirOwnUnicodeDefinition()
    {
        var regex = new Utf8PythonRegex(@"\b(?P<n>\d+)\b");
        var input = Encoding.UTF8.GetBytes("a1 12_ 3é ٤٥ ६७ 𝟙𝟚 9\u0301");
        var matches = regex.FindIterDetailed(input);
        Assert.Equal(new[] { "٤٥", "६७", "𝟙𝟚", "9" }, matches.Select(match => match.Groups[1].ValueText));
        using var cursor = regex.CreateMatchCursor(input);
        var alias = cursor;
        Assert.True(cursor.MoveNext());
        var retained = alias.Current;
        while (alias.MoveNext()) { }
        alias.Dispose();
        Assert.Equal("٤٥", retained.Groups[1].ValueText);
        Assert.Equal("n", retained.LastGroup);
    }

    [Fact]
    public void CompleteValidationAndOriginalTokenPrecedeCandidateRejection()
    {
        var regex = new Utf8PythonRegex(@"\b\d+\b");
        var invalid = "no digits "u8.ToArray().Append((byte)0xff).ToArray();
        Assert.Throws<ArgumentException>(() => regex.IsMatch(invalid));
        using var token = new CancellationTokenSource();
        token.Cancel();
        Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => regex.IsMatch(invalid, 0, token.Token)).CancellationToken);
    }
}
