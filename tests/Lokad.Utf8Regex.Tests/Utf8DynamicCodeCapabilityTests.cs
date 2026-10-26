using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

// Also run this fixture with IsDynamicCodeSupported disabled in the test host's
// runtimeconfig. No alternate library build or execution mode is required.
public sealed class Utf8DynamicCodeCapabilityTests
{
    [Theory]
    [InlineData("abc|xyz", "é😀 xyz abc", 2)]
    [InlineData(@"\b(?:record|struct|class)\s+[A-Z][A-Za-z0-9_]+", "é😀 class Bravo", 1)]
    [InlineData("^[0-9]{4}-[0-9]{2}-[0-9]{2}$", "2026-10-26", 1)]
    [InlineData("^ab[0-9]{2}$", "ab12", 1)]
    [InlineData("(?<x>😀)+\\k<x>", "é😀😀", 1)]
    [InlineData("(?<x>.)", "é😀", 2)]
    public void PublicResultsAndControlsDoNotRequireEmission(string pattern, string text, int count)
    {
        var input = Encoding.UTF8.GetBytes(text);
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.NonBacktracking, RegexOptions.RightToLeft })
        {
            if (pattern.Contains("\\k") && mode is RegexOptions.NonBacktracking or RegexOptions.RightToLeft) continue;
            var options = mode | RegexOptions.CultureInvariant;
            var regex = new Utf8Regex(pattern, options);
            Assert.Equal(options, regex.Options);
            Assert.True(regex.IsMatch(input));
            Assert.Equal(count, regex.Count(input));
            var match = regex.MatchDetailed(input);
            Assert.True(match.Success);
            Assert.Equal(Encoding.UTF8.GetString(match.GetGroup(0).GetValueBytes()), match.GetValueString());
            if (pattern.Contains("?<x>"))
                Assert.Equal(pattern == "(?<x>.)" && mode != RegexOptions.RightToLeft ? "é" : "😀",
                    match.GetGroup("x").GetValueString());
            Assert.Throws<ArgumentException>(() => regex.Count([0xff]));
            using var canceled = new CancellationTokenSource();
            canceled.Cancel();
            Assert.Equal(canceled.Token,
                Assert.Throws<OperationCanceledException>(() => regex.Count(input, canceled.Token)).CancellationToken);
            var finite = new Utf8Regex(pattern, options, Regex.InfiniteMatchTimeout,
                new Utf8ResourcePolicy { MaxWorkSteps = 1_000_000, MaxDepth = 2048,
                    MaxWorkingBytes = 1_048_576, MaxResultBytes = 1_048_576 });
            Assert.Equal(count, finite.Count(input));
        }
    }

    [Fact]
    public void OptionalStructuralPreparationFollowsHostCapability()
    {
        var regex = new Utf8Regex(@"\b(?:record|struct|class)\s+[A-Z][A-Za-z0-9_]+");
        var program = (Utf8RegexProgram)typeof(Utf8Regex)
            .GetField("_program", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(regex)!;
        Assert.Equal(RuntimeFeature.IsDynamicCodeSupported, program.DirectStructuralFamilyKernelMatcher is not null);
    }
}
