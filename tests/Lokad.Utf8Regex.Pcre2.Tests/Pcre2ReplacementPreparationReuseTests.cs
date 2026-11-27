using System.Buffers;
using System.Text;
using Lokad.Utf8Regex.Internal;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2ReplacementPreparationReuseTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UncacheableTemplateDoesNotNeedTwoCompilationAllowances(bool destinationOutput)
    {
        using var checkpointObservation = global::Lokad.Utf8Regex.Internal.Diagnostics.Utf8SearchDiagnosticsSession.Start();
        // The immutable key and its lowered payload together exceed the cache bound.
        var template = "$1" + new string('x', 2 << 20);
        var utf8 = Encoding.UTF8.GetBytes(template);
        ulong work, keyWork;
        using (var measured = Utf8ConstructionScope.Enter())
        {
            _ = Pcre2ReplacementPreparationControl.Prepare(utf8, Pcre2SubstitutionOptions.None, false);
            work = measured.Budget.DiagnosticCheckpoints;
            using var storage = Utf8ConstructionSession.Create(measured.Budget);
            _ = Pcre2ReplacementPreparationControl.FreezeTemplateKey(storage, utf8);
            keyWork = measured.Budget.DiagnosticCheckpoints - work;
        }
        foreach (var finite in new[] { false, true })
        {
            var regex = finite ? new Utf8Pcre2Regex("(a)", options: default, compileSettings: default, policy: new Utf8RegexPolicy
            {
                ResourcePolicy = new Utf8ResourcePolicy
                {
                    MaxDepth = 1024,
                    MaxWorkingBytes = 64 << 20,
                    MaxResultBytes = 64 << 20
                },
                MatchTimeout = Timeout.InfiniteTimeSpan
            })
                : new Utf8Pcre2Regex("(a)");
            var destination = new byte[(2 << 20) + 1];
            // The immutable cache key is copied before preparation. Leave room
            // for that copy and admission, while excluding a second compilation.
            var allowance = keyWork + work + work / 2;
            using var owner = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
            var output = destinationOutput ? ReplaceInto(regex, utf8, destination) : regex.Replace("a"u8, utf8);
            Assert.Equal(destination.Length, output.Length);
            Assert.Equal((byte)'a', output[0]);
            Assert.DoesNotContain(output.AsSpan(1).ToArray(), value => value != (byte)'x');
            Assert.Equal(0, regex.DebugReplacementCacheEntryCount);
        }
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
