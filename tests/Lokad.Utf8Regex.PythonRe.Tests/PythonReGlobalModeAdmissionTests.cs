using System.Text;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReGlobalModeAdmissionTests
{
    [Theory]
    [InlineData("(?a)(?u)a", PythonReCompileOptions.None)]
    [InlineData("(?u)(?a)a", PythonReCompileOptions.None)]
    [InlineData("(?a)(?#x)(?u)a", PythonReCompileOptions.None)]
    [InlineData("(?u)a", PythonReCompileOptions.Ascii)]
    [InlineData("(?a)a", PythonReCompileOptions.Unicode)]
    [InlineData("(?a)(?u)(?(3)a|b)", PythonReCompileOptions.None)]
    [InlineData("(?a)(?u)(?<=a*)b", PythonReCompileOptions.None)]
    public void EffectiveGlobalAsciiAndUnicodeModesMustBeCompatible(string pattern, PythonReCompileOptions options)
    {
        foreach (var bytes in new[] { false, true })
        {
            var error = Assert.Throws<PythonRePatternException>(() =>
            {
                if (bytes) _ = new Utf8PythonRegex(Encoding.UTF8.GetBytes(pattern), options: options);
                else _ = new Utf8PythonRegex(pattern, options: options);
            });
            Assert.Equal("ASCII and UNICODE flags are incompatible", error.Message);
            Assert.Equal(-1, error.Position);
            Assert.False(Utf8ConstructionScope.IsActive);
        }
    }

    [Theory]
    [InlineData("(?a)(?u)[", "unterminated character set", 8)]
    [InlineData("(?a)(?u)(?P=x)", "unknown group name 'x'", 12)]
    [InlineData(@"(?a)(?u)(a)\2", "invalid group reference 2", 12)]
    public void BodySyntaxStillPrecedesGlobalModeCompatibility(string pattern, string message, int position)
    {
        var error = Assert.Throws<PythonRePatternException>(() => new Utf8PythonRegex(pattern));
        Assert.Equal(message, error.Message);
        Assert.Equal(position, error.Position);
    }

    [Theory]
    [InlineData(@"(?a)(?u:(?P<x>\w+))(?P<y>\w+)", "éa", "é", "a")]
    [InlineData(@"(?u)(?a:(?P<x>\w+))(?P<y>\w+)", "aé", "a", "é")]
    public void ScopedModesOverrideAndRestoreWithoutChangingCaptureIdentity(string pattern, string text, string first, string second)
    {
        var regex = new Utf8PythonRegex(pattern, options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = new()
            {
                MaxDepth = 128,
                MaxWorkingBytes = 1000000,
                MaxResultBytes = 1000000
            }
        });
        Utf8PythonRangeMatch match = default;
        using (var captureCursor1 = regex.CreateMatchCursor(Encoding.UTF8.GetBytes(text), mode: Utf8PythonMatchMode.FullMatch))
        {
            captureCursor1.MoveNext(out match);
        }
        Assert.True(match.Success);
        Assert.Equal(first, System.Text.Encoding.UTF8.GetString((match.Groups[1].Success ? match.GetGroupMemory(match.Groups[1].Number).Span : ReadOnlySpan<byte>.Empty)));
        Assert.Equal(second, System.Text.Encoding.UTF8.GetString((match.Groups[2].Success ? match.GetGroupMemory(match.Groups[2].Number).Span : ReadOnlySpan<byte>.Empty)));
        Assert.Equal(2, match.LastIndex);
    }

    [Fact]
    public void CombinedModeFailureUnwindsExistingConstructionOwner()
    {
        using var checkpointObservation = global::Lokad.Utf8Regex.Internal.Diagnostics.Utf8SearchDiagnosticsSession.Start();
        using (var owner = new Utf8ConstructionScope(ulong.MaxValue, 1000000, 128))
        {
            var error = Assert.Throws<PythonRePatternException>(() =>
                new PythonReParser("(?a)(?u)(?P<x>😀)").Parse(PythonReCompileOptions.None));
            Assert.Equal("ASCII and UNICODE flags are incompatible", error.Message);
            Assert.True(owner.Budget.DiagnosticCheckpoints > 0);
            Assert.True(owner.Budget.PeakBytes > 0);
            Assert.Equal(0ul, owner.Budget.ReservedBytes);
            Assert.Equal(0ul, owner.Budget.Depth);
        }
        Assert.True(new Utf8PythonRegex("(?a)a").Match("a"u8, mode: Utf8PythonMatchMode.FullMatch).Success);
    }
}
