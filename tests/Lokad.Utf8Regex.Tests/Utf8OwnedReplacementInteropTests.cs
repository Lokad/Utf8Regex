using System.Buffers;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8OwnedReplacementInteropTests
{
    private static readonly string[] Texts = ["\u00e9", "a\u00e9b", "\u00e9b\u00e9b",
        "a\u00e9a\u00e9a", "\u00e9\u00e9", "\u00e9\u00e9a", "a\n\u00e9b", ""];

    [Theory]
    [MemberData(nameof(Utf8BmpGrammarPatternCatalog.Patterns), MemberType = typeof(Utf8BmpGrammarPatternCatalog))]
    public void TemplatesPreserveBmpGrammarAndEncodeStringBoundaries(string pattern, RegexOptions options)
    {
        var oracle = new Regex(pattern, options, TimeSpan.FromSeconds(30));
        var regex = new Utf8Regex(pattern, options, TimeSpan.FromSeconds(30));
        using var source = new CancellationTokenSource();
        foreach (var text in Texts)
        foreach (var template in new[] { "", "Q", "$&", "<$1>|$2|$3", "$`|$&|$'|$_|$+", "$1$2$&",
            "\ud83d$1\ude00", "$&\ude00", "\ud83d$&", "$$${missing}" })
        {
            var expected = oracle.Replace(text, Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(template)));
            var expectedBytes = Encoding.UTF8.GetBytes(expected);
            var input = Encoding.UTF8.GetBytes(text);
            Assert.Equal(expected, regex.ReplaceToString(input, template, source.Token));
            Assert.Equal(expectedBytes, regex.Replace(input, template, source.Token));
            var destination = new byte[expectedBytes.Length + 3];
            Assert.Equal(OperationStatus.Done, regex.TryReplace(input, template, destination, out var written, source.Token));
            Assert.Equal(expectedBytes.Length, written);
            Assert.Equal(expectedBytes, destination.AsSpan(0, written).ToArray());
            if (expectedBytes.Length != 0)
            {
                var shortDestination = new byte[expectedBytes.Length - 1];
                Array.Fill(shortDestination, (byte)0xa5);
                Assert.Equal(OperationStatus.DestinationTooSmall,
                    regex.TryReplace(input, template, shortDestination, out written, source.Token));
                Assert.Equal(0, written);
                Assert.All(shortDestination, b => Assert.Equal((byte)0xa5, b));
            }
            var templateBytes = Encoding.UTF8.GetBytes(template);
            Assert.Equal(expectedBytes, regex.Replace(input, templateBytes, source.Token));
            Assert.Equal(OperationStatus.Done, regex.TryReplace(input, templateBytes, destination, out written, source.Token));
            Assert.Equal(expectedBytes.Length, written);
            Assert.Equal(expectedBytes, destination.AsSpan(0, written).ToArray());
        }
        AssertOwned(regex);
    }

    [Theory]
    [MemberData(nameof(Utf8BmpGrammarPatternCatalog.Patterns), MemberType = typeof(Utf8BmpGrammarPatternCatalog))]
    public void EvaluatorsRetainCaptureHistoriesAndDirectionSensitiveCallOrder(string pattern, RegexOptions options)
    {
        var oracle = new Regex(pattern, options, TimeSpan.FromSeconds(30));
        var regex = new Utf8Regex(pattern, options, TimeSpan.FromSeconds(30));
        using var source = new CancellationTokenSource();
        foreach (var text in Texts)
        {
            var expectedCalls = new List<string>();
            var expected = oracle.Replace(text, match =>
            {
                var signature = new StringBuilder($"{match.Index}:{match.Length}|");
                foreach (Group group in match.Groups)
                {
                    signature.Append($"{group.Success}:{group.Index}:{group.Length}:{group.Value}:");
                    foreach (Capture capture in group.Captures) signature.Append($"{capture.Index}:{capture.Length}:{capture.Value};");
                    signature.Append('|');
                }
                expectedCalls.Add(signature.ToString());
                return Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(match.Groups[^1].Value + "\ud83d" + match.Value + "\ude00"));
            });
            var calls = new List<string>();
            var actual = regex.ReplaceToString(Encoding.UTF8.GetBytes(text), calls,
                static (in Utf8MatchContext context, ref List<string> state) =>
                {
                    var signature = new StringBuilder($"{context.IndexInUtf16}:{context.LengthInUtf16}|");
                    for (var number = 0; number < context.GroupCount; number++)
                    {
                        var group = context.GetGroup(number);
                        signature.Append($"{group.Success}:{group.IndexInUtf16}:{group.LengthInUtf16}:{group.GetValueString()}:");
                        for (var index = 0; index < group.CaptureCount; index++)
                        {
                            var capture = group.GetCapture(index);
                            signature.Append($"{capture.IndexInUtf16}:{capture.LengthInUtf16}:{capture.GetValueString()};");
                        }
                        signature.Append('|');
                    }
                    state.Add(signature.ToString());
                    return context.GetGroup(context.GroupCount - 1).GetValueString() + "\ud83d" + context.GetValueString() + "\ude00";
                }, source.Token);
            Assert.Equal(expected, actual);
            Assert.Equal(expectedCalls, calls);
        }
        AssertOwned(regex);
    }

    private static void AssertOwned(Utf8Regex regex)
    {
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
        Assert.True(ReferenceEquals(regex.Inspection.PreparedRegex.SemanticRegex.CaptureCatalog, regex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StringLiteralsEncodeIndependentlyAcrossNativeMatchesAndOriginalGaps(bool rtl)
    {
        var options = RegexOptions.CultureInvariant | (rtl ? RegexOptions.RightToLeft : RegexOptions.None);
        foreach (var pattern in new[] { "a", "(a)", "(?<x>a)", "a|b" })
        foreach (var text in new[] { "a\U0001f600a", "aa", "ba" })
        foreach (var template in new[] { "\ud83d", "\ude00", "\ud83d$&\ude00" })
        {
            var oracle = new Regex(pattern, options);
            var regex = new Utf8Regex(pattern, options);
            var input = Encoding.UTF8.GetBytes(text);
            var expected = oracle.Replace(text, Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(template)));
            Assert.Equal(expected, regex.ReplaceToString(input, template));
            Assert.Equal(Encoding.UTF8.GetBytes(expected), regex.Replace(input, template));
            AssertOwned(regex);
        }
    }

    [Fact]
    public void OutputGrowthAndFinalMaterializationAdmitOverlapAndBalanceAfterDenial()
    {
        const ulong controls = 2 * Utf8SliceByteOutputScope.ControlBytes;
        var scope = Utf8SliceByteOutputScope.Enter(new(0, 0, controls + 111), Regex.InfiniteMatchTimeout, CancellationToken.None);
        var writer = scope.CreateBuffer();
        try
        {
            writer.Append("abcdefghijklmnop"u8);
            Assert.Equal(controls + 48, scope.Invocation.Ledger.ReservedBytes);
            Assert.Throws<InvalidOperationException>(() => writer.Append("q"u8));
            Assert.Equal(controls + 48, scope.Invocation.Ledger.ReservedBytes);
            var retained = (byte[])typeof(Utf8SliceByteOutputBuffer)
                .GetField("_buffer", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(writer)!;
            Assert.Equal("abcdefghijklmnop"u8.ToArray(), retained);
            Assert.Throws<InvalidOperationException>(() => writer.ToArray());
            Assert.Equal(Utf8ExecutionLimitKind.WorkingBytes, scope.Invocation.Budget.LimitBreach);
            Assert.Equal(controls + 48, scope.Invocation.Ledger.ReservedBytes);
            writer.Release();
            writer.Release();
            Assert.Equal(Utf8SliceByteOutputScope.ControlBytes, scope.Invocation.Ledger.ReservedBytes);
        }
        finally { scope.Release(); }
        Assert.Equal(0UL, scope.Invocation.Ledger.ReservedBytes);

        foreach (var encode in new[] { false, true })
        foreach (var exact in new[] { false, true })
        {
            var finalBytes = encode ? 48UL : 80UL;
            scope = Utf8SliceByteOutputScope.Enter(new(0, 0, controls + 48 + finalBytes - (exact ? 0UL : 1UL)),
                Regex.InfiniteMatchTimeout, CancellationToken.None);
            writer = scope.CreateBuffer();
            try
            {
                writer.Append("abcdefghijklmnop"u8);
                if (exact)
                {
                    if (encode) Assert.Equal("abcdefghijklmnop"u8.ToArray(), writer.ToArray());
                    else Assert.Equal("abcdefghijklmnop", writer.ToValidatedString());
                }
                else Assert.Throws<InvalidOperationException>(() =>
                {
                    if (encode) _ = writer.ToArray();
                    else _ = writer.ToValidatedString();
                });
                Assert.Equal(controls + 48, scope.Invocation.Ledger.ReservedBytes);
            }
            finally { scope.Release(); }
            Assert.Equal(0UL, scope.Invocation.Ledger.ReservedBytes);
        }
    }

    [Fact]
    public void ReversePiecesAndEncodingBlockBoundariesPreserveCompleteScalars()
    {
        var scope = Utf8SliceByteOutputScope.Enter(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, CancellationToken.None);
        var writer = scope.CreateBuffer();
        try
        {
            writer.Append("\ude00".AsSpan());
            writer.MarkPiece();
            writer.Append("\ud83d".AsSpan());
            writer.MarkPiece();
            writer.ReversePieces();
            Assert.Equal("\ufffd\ufffd", writer.ToValidatedString());
            Assert.Equal("\ufffd\ufffd"u8.ToArray(), writer.ToArray());
            writer.Release();
            writer = scope.CreateBuffer();
            var text = new string('x', 2047) + "\U0001f600" + new string('y', 2047) + "\ud83d";
            writer.Append(text.AsSpan());
            Assert.Equal(Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(text)), writer.ToValidatedString());
            Assert.Equal(Encoding.UTF8.GetBytes(text), writer.ToArray());
            writer.Release();
            writer = scope.CreateBuffer();
            writer.Append("\U0001f600"u8);
            writer.MarkPiece();
            writer.Append("\u00e9"u8);
            writer.MarkPiece();
            writer.ReversePieces();
            Assert.Equal("\u00e9\U0001f600", writer.ToValidatedString());
        }
        finally { scope.Release(); }
        Assert.Equal(0UL, scope.Invocation.Ledger.ReservedBytes);
    }

    [Fact]
    public void DestinationCopyRetainsItsByteBufferAndChecksCancellationBetweenBlocks()
    {
        using var source = new CancellationTokenSource();
        var scope = Utf8SliceByteOutputScope.Enter(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, source.Token);
        var writer = scope.CreateBuffer();
        var destination = new byte[300000];
        Array.Fill(destination, (byte)0xa5);
        var written = -1;
        try
        {
            writer.Append(new string('x', destination.Length).AsSpan());
            var held = scope.Invocation.Ledger.ReservedBytes;
            var entered = false;
            // Cancel only after destination copying has begun, not during the
            // preceding validation; this does not pin an implementation trip count.
            scope.Invocation.CheckpointHook = _ =>
            {
                if (destination[0] != (byte)'x') return;
                entered = true;
                source.Cancel();
            };
            var exception = Assert.Throws<OperationCanceledException>(() => writer.CopyUtf8(destination, out written));
            Assert.True(entered);
            Assert.Equal(source.Token, exception.CancellationToken);
            Assert.Equal(0, written);
            Assert.Equal(held, scope.Invocation.Ledger.ReservedBytes);
            var copied = destination.AsSpan().IndexOf((byte)0xa5);
            Assert.InRange(copied, 4096, destination.Length - 1);
            Assert.Equal(0, copied % 4096);
            Assert.True(destination.AsSpan(0, copied).IndexOfAnyExcept((byte)'x') < 0);
            Assert.True(destination.AsSpan(copied).IndexOfAnyExcept((byte)0xa5) < 0);
        }
        finally { scope.Release(); }
        Assert.Equal(0UL, scope.Invocation.Ledger.ReservedBytes);
    }

    [Theory]
    [InlineData("append")]
    [InlineData("string")]
    [InlineData("bytes")]
    [InlineData("reverse")]
    public void OutputWorkObservesCancellationAndReleasesTemporaryReservations(string operation)
    {
        using var source = new CancellationTokenSource();
        var scope = Utf8SliceByteOutputScope.Enter(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, source.Token);
        var writer = scope.CreateBuffer();
        try
        {
            var text = new string('x', 300000);
            if (operation != "append") writer.Append(text.AsSpan());
            if (operation == "reverse") writer.MarkPiece();
            var held = scope.Invocation.Ledger.ReservedBytes;
            var entered = false;
            scope.Invocation.CheckpointHook = _ => { entered = true; source.Cancel(); };
            var exception = Assert.Throws<OperationCanceledException>(() =>
            {
                switch (operation)
                {
                    case "append": writer.Append(text.AsSpan()); break;
                    case "string": _ = writer.ToValidatedString(); break;
                    case "bytes": _ = writer.ToArray(); break;
                    case "reverse": writer.ReversePieces(); break;
                }
            });
            Assert.True(entered);
            Assert.Equal(source.Token, exception.CancellationToken);
            if (operation != "append") Assert.Equal(held, scope.Invocation.Ledger.ReservedBytes);
        }
        finally { scope.Release(); }
        Assert.Equal(0UL, scope.Invocation.Ledger.ReservedBytes);
    }

    [Fact]
    public void CallbackCancellationIsObservedBeforeItsOutputIsCopiedAndRecoveryWorks()
    {
        var regex = new Utf8Regex("((.)+?)", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(30));
        using var source = new CancellationTokenSource();
        var calls = 0;
        var exception = Assert.Throws<OperationCanceledException>(() => regex.ReplaceToString("\U0001f600a"u8,
            source, (in Utf8MatchContext context, ref CancellationTokenSource state) =>
            {
                calls++;
                state.Cancel();
                return "large ignored output";
            }, source.Token));
        Assert.Equal(source.Token, exception.CancellationToken);
        Assert.Equal(1, calls);
        Assert.Equal("\U0001f600a", regex.ReplaceToString("\U0001f600a"u8, 0,
            static (in Utf8MatchContext context, ref int state) => context.GetValueString()));
        AssertOwned(regex);
    }

    [Theory]
    [InlineData("string")]
    [InlineData("bytes-string")]
    [InlineData("bytes-pattern")]
    [InlineData("destination-string")]
    [InlineData("destination-pattern")]
    public void LargeTemplateOutputCanBeCanceledAfterShortMatchingHasFinished(string operation)
    {
        foreach (var compiled in new[] { false, true })
        foreach (var rtl in new[] { false, true })
        {
            var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None) |
                (rtl ? RegexOptions.RightToLeft : RegexOptions.None);
            var regex = new Utf8Regex("(.)", options);
            var input = "\U0001f600x"u8.ToArray();
            var template = new string('Q', 500000);
            var templateBytes = Encoding.UTF8.GetBytes(template);
            var destination = new byte[1500000];
            Array.Fill(destination, (byte)0xa5);
            using var source = new CancellationTokenSource();
            var entered = false;
            var session = Utf8SearchDiagnosticsSession.Start(steps => { entered = true; source.Cancel(); });
            try
            {
                var exception = Assert.Throws<OperationCanceledException>(() =>
                {
                    switch (operation)
                    {
                        case "string": _ = regex.ReplaceToString(input, template, source.Token); break;
                        case "bytes-string": _ = regex.Replace(input, template, source.Token); break;
                        case "bytes-pattern": _ = regex.Replace(input, templateBytes, source.Token); break;
                        case "destination-string": _ = regex.TryReplace(input, template, destination, out _, source.Token); break;
                        case "destination-pattern": _ = regex.TryReplace(input, templateBytes, destination, out _, source.Token); break;
                    }
                });
                Assert.True(entered);
                Assert.Equal(source.Token, exception.CancellationToken);
            }
            finally { session.Complete(); }
            Assert.True(destination.AsSpan().IndexOfAnyExcept((byte)0xa5) < 0);
            Assert.Equal("QQ", regex.ReplaceToString(input, "Q"));
            AssertOwned(regex);
        }
    }

    [Fact]
    public void CallbackFailuresAndExpiredDeadlinesReleaseTheOperationForReuse()
    {
        using var clock = Utf8DeadlineTestClock.Start();
        var regex = new Utf8Regex("(.)", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250));
        var input = "\U0001f600x"u8.ToArray();
        var failure = new ApplicationException("evaluator failure");
        Assert.Same(failure, Assert.Throws<ApplicationException>(() => regex.ReplaceToString(input, failure,
            static (in Utf8MatchContext context, ref ApplicationException state) => throw state)));
        var calls = new List<int>();
        Assert.Throws<RegexMatchTimeoutException>(() => regex.ReplaceToString(input, calls,
            (in Utf8MatchContext context, ref List<int> state) =>
            {
                state.Add(context.IndexInUtf16);
                clock.Advance(300);
                return "Q";
            }));
        Assert.Single(calls);
        Assert.Equal("", regex.ReplaceToString(input, 0,
            static (in Utf8MatchContext context, ref int state) => null));
        Assert.Equal("QQ", regex.ReplaceToString(input, "Q"));
        AssertOwned(regex);
    }
}
