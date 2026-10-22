using System.Buffers;
using System.Text;
using Lokad.Utf8Regex.Internal;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2ReplacementPreparationReuseTests
{
    [Theory]
    [InlineData("Bytes")]
    [InlineData("Text")]
    [InlineData("Utf8Bytes")]
    [InlineData("Destination")]
    public void UncacheableTemplateDoesNotNeedTwoCompilationAllowances(string route)
    {
        var template = "$1" + new string('x', 1 << 20);
        ulong work;
        using (var measured = Utf8ConstructionScope.Enter())
        {
            _ = Pcre2ReplacementPreparationControl.Prepare(template, Pcre2SubstitutionOptions.None, false);
            work = measured.Budget.WorkSteps;
        }
        foreach (var finite in new[] { false, true })
        {
            var regex = finite ? Utf8Pcre2Regex.Create("(a)", default, default,
                new Utf8ResourcePolicy { MaxWorkSteps = 50_000_000, MaxDepth = 1024,
                    MaxWorkingBytes = 64 << 20, MaxResultBytes = 64 << 20 }, Timeout.InfiniteTimeSpan)
                : new Utf8Pcre2Regex("(a)");
            var utf8 = Encoding.UTF8.GetBytes(template);
            var destination = new byte[(1 << 20) + 1];
            // Leave room for cache admission work, but not a second compilation.
            var allowance = work + work / 2;
            using var owner = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, allowance, ulong.MaxValue);
            var output = route switch
            {
                "Bytes" => regex.Replace("a"u8, template),
                "Text" => Encoding.UTF8.GetBytes(regex.ReplaceToString("a"u8, template)),
                "Utf8Bytes" => regex.Replace("a"u8, utf8),
                _ => ReplaceInto(regex, utf8, destination)
            };
            Assert.Equal(destination.Length, output.Length);
            Assert.Equal((byte)'a', output[0]);
            Assert.DoesNotContain(output.AsSpan(1).ToArray(), value => value != (byte)'x');
            Assert.Equal(0, regex.DebugReplacementCacheEntryCount);
            Assert.InRange(owner.Budget.WorkSteps, work, allowance);
        }
    }

    [Fact]
    public void OnePreparationStillHonorsAnInsufficientConstructionAllowance()
    {
        var template = "$1" + new string('x', 8192);
        ulong work;
        using (var measured = Utf8ConstructionScope.Enter())
        {
            _ = Pcre2ReplacementPreparationControl.Prepare(template, Pcre2SubstitutionOptions.None, false);
            work = measured.Budget.WorkSteps;
        }
        var regex = new Utf8Pcre2Regex("(a)");
        using (var denied = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, work - 1, ulong.MaxValue))
            Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps,
                Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.Replace("a"u8, template)).ResourceKind);
        Assert.Equal("a" + template[2..], Encoding.UTF8.GetString(regex.Replace("a"u8, template)));
    }

    [Theory]
    [InlineData("a", "$0", Pcre2SubstitutionOptions.SubstituteMatched)]
    [InlineData("(a)", "$1", Pcre2SubstitutionOptions.SubstituteOverflowLength)]
    [InlineData("(a)", "$1", Pcre2SubstitutionOptions.SubstituteReplacementOnly)]
    [InlineData("(a)", "${1:+a:b}", Pcre2SubstitutionOptions.Extended)]
    public void ReusedPreparationsPreserveTemplateOptions(string pattern, string template, Pcre2SubstitutionOptions options)
    {
        var regex = new Utf8Pcre2Regex(pattern);
        var destination = new byte[3];
        Assert.Equal(OperationStatus.Done, regex.TryReplace("xay"u8, Encoding.UTF8.GetBytes(template), destination,
            out var written, 0, options, Pcre2MatchOptions.None));
        Assert.Equal(options.HasFlag(Pcre2SubstitutionOptions.SubstituteReplacementOnly) ? "a" : "xay",
            Encoding.UTF8.GetString(destination.AsSpan(0, written)));
    }

    private static byte[] ReplaceInto(Utf8Pcre2Regex regex, byte[] template, byte[] destination)
    {
        Assert.Equal(OperationStatus.Done, regex.TryReplace("a"u8, template, destination, out var written));
        Assert.Equal(destination.Length, written);
        return destination;
    }
}
