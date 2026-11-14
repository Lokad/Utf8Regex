using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8MixedLookupCountTests
{
    private const string Literal = "夏洛克·福尔摩斯";

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(7)]
    public void VectorStartsTailsBorrowedOffsetsAndDenseNonoverlapKeepPublicCounts(int borrowedOffset)
    {
        foreach (var pattern in new[] { Literal, "😀abc·夏洛克xx" })
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var reference = new Regex(pattern, options | RegexOptions.CultureInvariant);
            var regex = new Utf8Regex(pattern, options | RegexOptions.CultureInvariant);
            var trusted = new Utf8Regex(pattern, options | RegexOptions.CultureInvariant,
                inputValidation: Utf8InputValidation.AssumeValid);
            for (var padding = 0; padding < 134; padding++)
            foreach (var prefix in new[] { "", "é", "😀" })
            {
                var text = prefix + new string('a', padding) + pattern + pattern + "é😀" + pattern;
                var bytes = Encoding.UTF8.GetBytes(text);
                var storage = Enumerable.Repeat((byte)0xff, borrowedOffset + bytes.Length + 1).ToArray();
                bytes.CopyTo(storage.AsSpan(borrowedOffset));
                var input = storage.AsSpan(borrowedOffset, bytes.Length);
                Assert.Equal(reference.Count(text), regex.Count(input));
                Assert.Equal(reference.Count(text), trusted.Count(input));
            }
        }
    }

    [Fact]
    public void SparseMissesAndOverlappingPrefixesRemainNonoverlapping()
    {
        foreach (var pattern in new[] { Literal, Literal + Literal })
        {
            var regex = new Utf8Regex(pattern, RegexOptions.Compiled);
            foreach (var text in new[] { new string('a', 8192), new string('中', 4096),
                string.Concat(Enumerable.Repeat(Literal, 17)),
                string.Concat(Enumerable.Repeat("夏aaaaaa·not-it ", 1024)) + pattern })
                Assert.Equal(new Regex(pattern).Count(text), regex.Count(Encoding.UTF8.GetBytes(text)));
        }
    }

    [Fact]
    public void LateCompleteScalarsDoNotBecomeFalseTruncationsAfterAnAsciiPrefix()
    {
        var regex = new Utf8Regex(Literal, RegexOptions.Compiled);
        foreach (var tail in new[] { "é", "中", "😀", Literal })
        {
            var input = Encoding.UTF8.GetBytes(new string('a', 8192) + tail);
            Assert.Equal(tail == Literal ? 1 : 0, regex.Count(input));
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void MalformedWindowsAndUnsearchedTailsKeepAbsoluteFirstErrors(bool afterMatch)
    {
        var regex = new Utf8Regex(Literal, RegexOptions.Compiled);
        foreach (var bad in new byte[][] { [0x80], [0xc0, 0xaf], [0xe0, 0x80, 0x80],
            [0xed, 0xa0, 0x80], [0xf4, 0x90, 0x80, 0x80], [0xf5, 0x80, 0x80, 0x80],
            [0xe2, 0xff, 0xa1], [0xe2, 0x82] })
        for (var offset = 0; offset < 140; offset++)
        foreach (var suffix in new[] { "", new string('z', 129) })
        {
            var prefix = (afterMatch ? Literal : "") + new string('a', offset);
            var initial = Encoding.UTF8.GetBytes(prefix);
            byte[] input = [..initial, ..bad, ..Encoding.UTF8.GetBytes(suffix)];
            var error = Assert.Throws<ArgumentException>(() => regex.Count(input));
            Assert.Contains($"byte offset {initial.Length}.", error.Message);
        }
    }

    [Fact]
    public void DefaultAndTrustedCallsKeepTheirSubjectPolicyAndVectorRoute()
    {
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("é😀" + Literal + "!", 128)));
        foreach (var policy in new[] { Utf8InputValidation.Validate, Utf8InputValidation.AssumeValid })
        {
            var regex = new Utf8Regex(Literal, RegexOptions.Compiled, inputValidation: policy);
            var observation = Utf8SearchDiagnosticsSession.Start();
            try
            {
                Assert.Equal(128, regex.Count(input));
                Assert.Equal(policy == Utf8InputValidation.Validate ? 1 : 0, observation.SubjectValidations);
                if (policy == Utf8InputValidation.Validate && Vector512.IsHardwareAccelerated && Avx512BW.IsSupported)
                    Assert.Equal(Utf8ExecutionRoute.CompiledFusedUtf8LiteralCount, observation.ExecutionRoute);
            }
            finally { observation.Complete(); }
        }
    }

    [Fact]
    public void FiniteLiveAndDirectionalCallsKeepTheirExistingAuthority()
    {
        var input = Encoding.UTF8.GetBytes(new string('a', 8192) + Literal);
        using var token = new CancellationTokenSource();
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled,
            RegexOptions.RightToLeft, RegexOptions.NonBacktracking })
        {
            var regex = new Utf8Regex(Literal, options, Timeout.InfiniteTimeSpan,
                new() { MaxWorkSteps = 1000000, MaxDepth = 4096, MaxWorkingBytes = 1 << 24, MaxResultBytes = 1 });
            Assert.Equal(1, regex.Count(input, token.Token));
            Assert.Throws<ArgumentException>(() => regex.Count(input.Concat(new byte[] { 255 }).ToArray(), token.Token));
        }
        var denied = new Utf8Regex(Literal, resourcePolicy: new() { MaxWorkSteps = 1 });
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => denied.Count(input)).ResourceKind);
        token.Cancel();
        Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => new Utf8Regex(Literal).Count(input, token.Token)).CancellationToken);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void PublicCheckpointsKeepOriginalTokenAndDeadline(bool expire)
    {
        var input = Encoding.UTF8.GetBytes(new string('a', 262144) + Literal);
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var cancellation = new CancellationTokenSource();
        var regex = new Utf8Regex(Literal, RegexOptions.Compiled, TimeSpan.FromMilliseconds(20));
        var entered = false;
        var session = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            if (steps <= 2) return;
            entered = true;
            if (expire) ticks = 50;
            else cancellation.Cancel();
        });
        try
        {
            if (expire) Assert.Equal(Literal, Assert.Throws<RegexMatchTimeoutException>(() => regex.Count(input)).Pattern);
            else Assert.Equal(cancellation.Token, Assert.Throws<OperationCanceledException>(() => regex.Count(input, cancellation.Token)).CancellationToken);
        }
        finally { session.Complete(); }
        Assert.True(entered);
    }
}
