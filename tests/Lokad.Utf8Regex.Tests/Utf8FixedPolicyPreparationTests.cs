using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8FixedPolicyPreparationTests
{
    [Theory]
    [InlineData("^[0-9]{4}-[0-9]{2}-[0-9]{2}$", "2026-10-23", RegexOptions.None)]
    [InlineData("^ab[0-9]{2}$", "ab12", RegexOptions.None)]
    [InlineData("(?:ab|xy)", "é😀 ab xy", RegexOptions.None)]
    [InlineData("(?<x>😀)+\\k<x>", "é😀😀", RegexOptions.None)]
    [InlineData("(?<x>.)", "é😀", RegexOptions.RightToLeft)]
    [InlineData("(?<x>.)+", "é😀", RegexOptions.NonBacktracking)]
    public void EachFiniteControlOmitsUnreachableRuntimeAndKeepsPublicResults(string pattern, string value, RegexOptions mode)
    {
        var input = Encoding.UTF8.GetBytes(value);
        foreach (var compiled in new[] { false, true })
        {
            var options = RegexOptions.CultureInvariant | mode | (compiled ? RegexOptions.Compiled : 0);
            var baseline = new Utf8Regex(pattern, options);
            var expected = baseline.MatchDetailed(input);
            Assert.True(expected.Success);
            foreach (var policy in new[] {
                new Utf8ResourcePolicy { MaxWorkSteps = 1_000_000 },
                new Utf8ResourcePolicy { MaxDepth = 2048 },
                new Utf8ResourcePolicy { MaxWorkingBytes = 1_048_576 },
                new Utf8ResourcePolicy { MaxResultBytes = 1_048_576 } })
            {
                var regex = new Utf8Regex(pattern, options, Regex.InfiniteMatchTimeout, policy);
                var program = (Utf8RegexProgram)typeof(Utf8Regex)
                    .GetField("_program", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(regex)!;
                Assert.Null(program.VerifierRuntime);
                Assert.Null(program.CompiledEngineRuntime);
                Assert.Null(program.DirectStructuralFamilyKernelMatcher);
                Assert.NotNull(program.PreparedRegex.ScalarProgram);
                Assert.Equal(options, regex.Options);
                Assert.True(regex.IsMatch(input));
                Assert.Equal(baseline.Count(input), regex.Count(input));
                var actual = regex.MatchDetailed(input);
                Assert.Equal((expected.IndexInBytes, expected.LengthInBytes, expected.IndexInUtf16, expected.LengthInUtf16),
                    (actual.IndexInBytes, actual.LengthInBytes, actual.IndexInUtf16, actual.LengthInUtf16));
                if (pattern.Contains("?<x>"))
                    Assert.Equal(expected.GetGroup("x").CaptureCount, actual.GetGroup("x").CaptureCount);
                Assert.Throws<ArgumentException>(() => regex.Count([0xff]));
                using var canceled = new CancellationTokenSource();
                canceled.Cancel();
                Assert.Equal(canceled.Token,
                    Assert.Throws<OperationCanceledException>(() => regex.Count(input, canceled.Token)).CancellationToken);
                Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes,
                    Assert.Throws<Utf8ResourceLimitExceededException>(() => Utf8Regex.Compile(pattern, options,
                        Regex.InfiniteMatchTimeout, policy, new Utf8ConstructionPolicy { MaxBytes = 1 },
                        Regex.InfiniteMatchTimeout, CancellationToken.None)).ResourceKind);
            }
        }
    }
}
