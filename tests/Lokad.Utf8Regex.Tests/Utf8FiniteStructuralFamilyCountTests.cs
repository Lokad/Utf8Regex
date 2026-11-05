using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8FiniteStructuralFamilyCountTests
{
    private const string MethodPattern = @"\b(?:LogError|LogWarning|LogInformation)\s*\(";
    private const string IdentifierPattern = @"(?:get|set)[A-Z][A-Za-z0-9_]*";
    private const string KeywordPattern = @"\b(?:record|struct|class)\s+[A-Z][A-Za-z0-9_]+";

    [Theory]
    [InlineData(MethodPattern, "LogError ( LogWarning( LogInformation\t\r\n(", 3)]
    [InlineData(MethodPattern, "xLogError( _LogWarning( LogInformation)", 0)]
    [InlineData(MethodPattern, "LogError" + "                                                                " + "(", 1)]
    [InlineData(IdentifierPattern, "getAlpha setBeta getZ_09 getlower", 3)]
    [InlineData(IdentifierPattern, "get set getlower", 0)]
    [InlineData(KeywordPattern, "class Alpha struct Beta record Gamma", 3)]
    [InlineData(KeywordPattern, "classstruct Wrong xclass Wrong class lower record Good", 1)]
    [InlineData(KeywordPattern, "class A struct Z record A1", 1)]
    [InlineData(@"\b(?:ab|ba)\s+[A-Z][A-Za-z0-9_]+", "aba Wrong ab Good ba Right", 2)]
    [InlineData(@"\b(?:foobar|bar)\s+[A-Z][A-Za-z0-9_]+", "foobar Good bar Right", 2)]
    public void CountNeedsNoVmSlotsOrFrames(string pattern, string text, int expected)
    {
        var input = Encoding.UTF8.GetBytes("pad" + text + "tail");
        using var cancellation = new CancellationTokenSource();
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var regex = new Utf8Regex(pattern, options, Regex.InfiniteMatchTimeout,
                new() { MaxWorkSteps = 100000, MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 });
            var slice = input.AsSpan(3, Encoding.UTF8.GetByteCount(text));
            Assert.Equal(expected, regex.Count(slice));
            Assert.Equal(expected, regex.Count(slice, cancellation.Token));
        }
    }

    [Theory]
    [InlineData(MethodPattern, "😀 LogInformation\u2003( éLogError( LogWarning(", 2)]
    [InlineData(IdentifierPattern, "😀 getAlphaé setBeta中 getZ", 3)]
    [InlineData(@"(?:foo|foobar)[A-Z][A-Za-z0-9_]*", "foobarAlpha fooBeta foobarGamma", 3)]
    [InlineData(@"\b(?:foo|foobar)[A-Z][A-Za-z0-9_]*", "foobarAlpha fooBeta foobarGamma", 3)]
    [InlineData(@"\b(?:foo:|:bar)[A-Z][A-Za-z0-9_]*", "foo:barAlpha foo:Beta", 2)]
    [InlineData(@"(?:class|struct)\s+[A-Z][A-Za-z0-9_]+", "classstruct Good class Right", 2)]
    [InlineData(KeywordPattern, "éclass Wrong 😀class\u2003Right record Good", 2)]
    public void UnicodeAndOverlappingPrefixesRetainScalarVerification(string pattern, string text, int expected)
    {
        var regex = new Utf8Regex(pattern, RegexOptions.None, Regex.InfiniteMatchTimeout,
            new() { MaxWorkSteps = 100000, MaxDepth = 128, MaxWorkingBytes = 1000000, MaxResultBytes = 1000000 });
        Assert.Equal(expected, regex.Count(Encoding.UTF8.GetBytes(text)));
    }

    [Theory]
    [InlineData(MethodPattern, "LogError", "(")]
    [InlineData(KeywordPattern, "class", "Alpha")]
    public void ValidationAndDiscoveryShareOneWorkAllowance(string pattern, string prefix, string suffix)
    {
        var input = Encoding.UTF8.GetBytes(prefix + new string(' ', 4096) + suffix);
        var invocation = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 0), Regex.InfiniteMatchTimeout, default);
        _ = Utf8Validation.Validate(input, ref invocation);
        var regex = new Utf8Regex(pattern, RegexOptions.None, Regex.InfiniteMatchTimeout,
            new() { MaxWorkSteps = invocation.Budget.Steps, MaxDepth = 128, MaxWorkingBytes = 1000000, MaxResultBytes = 1000000 });
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.Count(input)).ResourceKind);
        var malformed = Encoding.UTF8.GetBytes(prefix + " " + suffix).Append((byte)0xff).ToArray();
        var tiny = new Utf8Regex(pattern, RegexOptions.None, Regex.InfiniteMatchTimeout,
            new() { MaxWorkSteps = 100000, MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 });
        Assert.Throws<ArgumentException>(() => tiny.Count(malformed));
    }

    [Theory]
    [InlineData(MethodPattern, "LogError", "(", false, false)] [InlineData(MethodPattern, "LogError", "(", false, true)]
    [InlineData(MethodPattern, "LogError", "(", true, false)] [InlineData(MethodPattern, "LogError", "(", true, true)]
    [InlineData(KeywordPattern, "class", "Alpha", false, false)] [InlineData(KeywordPattern, "class", "Alpha", false, true)]
    [InlineData(KeywordPattern, "class", "Alpha", true, false)] [InlineData(KeywordPattern, "class", "Alpha", true, true)]
    public void FinitePublicSearchAndVerifierKeepOriginalInterruption(string pattern, string prefix, string suffix, bool verifier, bool expire)
    {
        using var clock = Utf8DeadlineTestClock.Start();
        using var cancellation = new CancellationTokenSource();
        var input = Encoding.UTF8.GetBytes(verifier ? prefix + new string(' ', 320000) + suffix : new string('x', 320000));
        var validation = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 0), Regex.InfiniteMatchTimeout, default);
        _ = Utf8Validation.Validate(input, ref validation);
        var validationSteps = validation.Budget.Steps;
        var entered = false;
        var session = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            if (entered || steps <= validationSteps || verifier && Utf8SearchDiagnosticsSession.Current!.VerifierInvocations == 0) return;
            entered = true;
            if (expire) clock.Advance(250);
            else cancellation.Cancel();
        });
        try
        {
            var regex = new Utf8Regex(pattern, RegexOptions.None,
                expire ? TimeSpan.FromMilliseconds(200) : Regex.InfiniteMatchTimeout,
                new() { MaxWorkSteps = 10000000, MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 });
            if (expire)
            {
                var timeout = Assert.Throws<RegexMatchTimeoutException>(() => regex.Count(input, cancellation.Token));
                Assert.Equal(pattern, timeout.Pattern);
                Assert.Equal(TimeSpan.FromMilliseconds(200), timeout.MatchTimeout);
                Assert.Equal(string.Empty, timeout.Input);
            }
            else Assert.Equal(cancellation.Token,
                Assert.Throws<OperationCanceledException>(() => regex.Count(input, cancellation.Token)).CancellationToken);
            Assert.True(entered);
            Assert.Equal(1, regex.Count(Encoding.UTF8.GetBytes(prefix + " " + suffix)));
        }
        finally { session.Complete(); }
    }
}
