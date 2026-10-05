using System.Buffers;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8SliceByteEvaluatorTests
{
    [Theory]
    [MemberData(nameof(Utf8SliceUnitSubjectTests.Patterns), MemberType = typeof(Utf8SliceUnitSubjectTests))]
    public void ByteCallbacksPreserveOriginalCaptureHistoriesAndWholeMatchAlignment(string pattern, RegexOptions options)
    {
        string[] texts = ["\U0001f600", "a\U0001f600b", "\U0001f600b\U0001f600b",
            "a\U0001f600a\U0001f600a", "\U0001f600\U0001f600", "\u00e9\U00010400a", "a\n\U0001f600b", ""];
        var oracle = new Regex(pattern, options, TimeSpan.FromSeconds(30));
        using var source = new CancellationTokenSource();
        foreach (var text in texts)
        {
            var expectedCalls = new List<string>();
            string? expected = null;
            var unaligned = false;
            try
            {
                expected = oracle.Replace(text, match =>
                {
                    if (!IsBoundary(text, match.Index) || !IsBoundary(text, match.Index + match.Length))
                        throw new InvalidOperationException("Oracle match splits a scalar.");
                    expectedCalls.Add(Signature(match));
                    return "[" + Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(match.Groups[^1].Value)) + "]\u00e9";
                });
            }
            catch (InvalidOperationException) { unaligned = true; }
            var regex = new Utf8Regex(pattern, options, TimeSpan.FromSeconds(30));
            var input = Encoding.UTF8.GetBytes(text);
            var calls = new List<string>();
            byte[]? actual = null;
            var failure = Record.Exception(() => actual = regex.Replace(input, calls,
                static (in Utf8MatchContext context, ref Utf8ReplacementWriter writer, ref List<string> state) =>
                {
                    Assert.NotNull(Utf8SliceByteOutputScope.Current);
                    state.Add(Signature(in context));
                    Assert.Equal(context.GetValueString(), Encoding.UTF8.GetString(context.GetGroup(0).GetValueBytes()));
                    writer.AppendAsciiByte((byte)'[');
                    writer.Append(context.GetGroup(context.GroupCount - 1).GetValueString().AsSpan());
                    writer.AppendAsciiByte((byte)']');
                    writer.Append(new Rune('\u00e9'));
                }, source.Token));
            if (unaligned)
            {
                var exception = Assert.IsType<InvalidOperationException>(failure);
                Assert.Equal("The evaluator match is not aligned to UTF-8 scalar boundaries.", exception.Message);
            }
            else
            {
                Assert.Null(failure);
                Assert.Equal(Encoding.UTF8.GetBytes(expected ?? ""), actual);
            }
            Assert.Equal(expectedCalls, calls);
            Assert.Equal(oracle.Replace(text, "Q"), regex.ReplaceToString(input, "Q"));
            AssertOwned(regex);
            Assert.Null(Utf8SliceByteOutputScope.Current);
        }
    }

    private static bool IsBoundary(string text, int index)
        => index == 0 || index == text.Length || !char.IsHighSurrogate(text[index - 1]) || !char.IsLowSurrogate(text[index]);

    private static string Signature(Match match)
    {
        var signature = new StringBuilder($"{match.Index}:{match.Length}|");
        foreach (Group group in match.Groups)
        {
            signature.Append($"{group.Success}:{group.Index}:{group.Length}:{group.Value}:");
            foreach (Capture capture in group.Captures) signature.Append($"{capture.Index}:{capture.Length}:{capture.Value};");
            signature.Append('|');
        }
        return signature.ToString();
    }

    private static string Signature(in Utf8MatchContext context)
    {
        var signature = new StringBuilder($"{context.IndexInUtf16}:{context.LengthInUtf16}|");
        for (var number = 0; number < context.GroupCount; number++)
        {
            var group = context.GetGroup(number);
            signature.Append($"{group.Success}:{group.IndexInUtf16}:{group.LengthInUtf16}:{group.GetValueString()}:");
            for (var numberCapture = 0; numberCapture < group.CaptureCount; numberCapture++)
            {
                var capture = group.GetCapture(numberCapture);
                signature.Append($"{capture.IndexInUtf16}:{capture.LengthInUtf16}:{capture.GetValueString()};");
            }
            signature.Append('|');
        }
        return signature.ToString();
    }

    private static void AssertOwned(Utf8Regex regex)
    {
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(regex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void RawFragmentsCanBecomeValidOnlyAcrossCallbacks(bool compiled, bool rtl)
    {
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None) |
            (rtl ? RegexOptions.RightToLeft : RegexOptions.None);
        var regex = new Utf8Regex("(a)(?=a|$)", options);
        var calls = new List<int>();
        var result = regex.Replace("aa"u8, calls,
            static (in Utf8MatchContext context, ref Utf8ReplacementWriter writer, ref List<int> state) =>
            {
                state.Add(context.IndexInUtf16);
                writer.Append(context.IndexInUtf16 == 0 ? new byte[] { 0xf0 } : new byte[] { 0x9f, 0x98, 0x80 });
            });
        Assert.Equal("\U0001f600"u8.ToArray(), result);
        Assert.Equal(rtl ? new[] { 1, 0 } : new[] { 0, 1 }, calls);
        AssertOwned(regex);
        Assert.Null(Utf8SliceByteOutputScope.Current);
    }

    [Fact]
    public void ResetAndCopiesPreserveTheLegacyWriterContractAndReclaimDiscardedBuffers()
    {
        static void Expand(in Utf8MatchContext context, ref Utf8ReplacementWriter writer, ref int state)
        {
            var alias = writer;
            alias.Append("discarded-before-reset"u8);
            writer = default;
            writer.Append("reset"u8);
            alias.Append("discarded-after-reset"u8);
            var returnedAlias = writer;
            returnedAlias.AppendAsciiByte((byte)'!');
        }
        var regex = new Utf8Regex("(a)(?=a|$)", RegexOptions.CultureInvariant);
        var input = "xaa"u8.ToArray();
        var result = regex.Replace(input, 0, Expand);
        var legacy = new Utf8ReplacementWriter(new ArrayBufferWriter<byte>());
        var oracle = new Regex("(a)(?=a|$)", RegexOptions.CultureInvariant);
        var end = 0;
        var state = 0;
        foreach (Match match in oracle.Matches("xaa"))
        {
            legacy.Append(input.AsSpan(end, match.Index - end));
            var context = new Utf8Regex("(a)").MatchDetailed("a"u8);
            Expand(in context, ref legacy, ref state);
            end = match.Index + match.Length;
        }
        legacy.Append(input.AsSpan(end));
        Assert.Equal(legacy.GetValidatedBytes().ToArray(), result);
        Assert.Equal("reset!"u8.ToArray(), result);
        AssertOwned(regex);

        const ulong control = Utf8SliceByteOutputScope.ControlBytes;
        var scope = Utf8SliceByteOutputScope.Enter(new(0, 0, control * 3 + 32), Regex.InfiniteMatchTimeout, CancellationToken.None);
        try
        {
            var buffer = scope.CreateBuffer();
            for (var match = 0; match < 1000; match++)
            {
                var next = scope.CreateBuffer();
                next.Append("X"u8);
                scope.Retain(next);
                buffer = next;
                Assert.Equal(control * 2 + 16, scope.Invocation.Ledger.ReservedBytes);
            }
            Assert.Equal("X"u8.ToArray(), buffer.ToArray());
        }
        finally { scope.Release(); }
        Assert.Equal(0UL, scope.Invocation.Ledger.ReservedBytes);
        Assert.Null(Utf8SliceByteOutputScope.Current);
    }

    [Fact]
    public void NestedOperationsRestoreTheOuterWriterInvocationAfterSuccessAndFailure()
    {
        var outer = new Utf8Regex("(a)(?=a|$)", RegexOptions.CultureInvariant);
        var inner = new Utf8Regex("(b)(?=b|$)", RegexOptions.CultureInvariant);
        var result = outer.Replace("aa"u8, inner,
            static (in Utf8MatchContext context, ref Utf8ReplacementWriter writer, ref Utf8Regex state) =>
            {
                var scope = Utf8SliceByteOutputScope.Current;
                Assert.NotNull(scope);
                var alias = writer;
                alias.Append("<"u8);
                var nested = state.Replace("bb"u8, 0,
                    static (in Utf8MatchContext nestedContext, ref Utf8ReplacementWriter nestedWriter, ref int nestedState) =>
                    {
                        nestedWriter = default;
                        nestedWriter.Append("B"u8);
                    });
                Assert.Same(scope, Utf8SliceByteOutputScope.Current);
                Assert.Equal("B"u8.ToArray(), nested);
                alias.Append(nested);
                var failure = new ApplicationException("nested evaluator failure");
                var nestedRegex = state;
                Assert.Same(failure, Assert.Throws<ApplicationException>(() => nestedRegex.Replace("b"u8, failure,
                    static (in Utf8MatchContext nestedContext, ref Utf8ReplacementWriter nestedWriter, ref ApplicationException nestedState) => throw nestedState)));
                Assert.Same(scope, Utf8SliceByteOutputScope.Current);
                writer.Append(">"u8);
            });
        Assert.Equal("<B><B>"u8.ToArray(), result);
        AssertOwned(outer);
        AssertOwned(inner);
        Assert.Null(Utf8SliceByteOutputScope.Current);
    }

    [Fact]
    public void OutputAdmissionCoversControlsGrowthOverlapAndFinalResults()
    {
        const ulong control = Utf8SliceByteOutputScope.ControlBytes;
        Assert.Throws<InvalidOperationException>(() => Utf8SliceByteOutputScope.Enter(new(0, 0, control - 1), Regex.InfiniteMatchTimeout, CancellationToken.None));
        Assert.Null(Utf8SliceByteOutputScope.Current);
        var scope = Utf8SliceByteOutputScope.Enter(new(0, 0, control), Regex.InfiniteMatchTimeout, CancellationToken.None);
        try
        {
            Assert.Throws<InvalidOperationException>(() => scope.CreateBuffer());
            Assert.Equal(control, scope.Invocation.Ledger.ReservedBytes);
        }
        finally { scope.Release(); }
        Assert.Equal(0UL, scope.Invocation.Ledger.ReservedBytes);

        scope = Utf8SliceByteOutputScope.Enter(new(0, 0, control * 2 + 47), Regex.InfiniteMatchTimeout, CancellationToken.None);
        try
        {
            var buffer = scope.CreateBuffer();
            buffer.Append("abcdefghijklmnop"u8);
            Assert.Throws<InvalidOperationException>(() => buffer.Append("q"u8));
            Assert.Equal(control * 2 + 16, scope.Invocation.Ledger.ReservedBytes);
            Assert.Equal("abcdefghijklmnop"u8.ToArray(), buffer.ToArray());
        }
        finally { scope.Release(); }
        Assert.Equal(0UL, scope.Invocation.Ledger.ReservedBytes);

        foreach (var textResult in new[] { false, true })
        foreach (var exact in new[] { false, true })
        {
            var resultBytes = textResult ? 32UL : 16UL;
            scope = Utf8SliceByteOutputScope.Enter(new(0, 0, control * 2 + 16 + resultBytes - (exact ? 0UL : 1UL)), Regex.InfiniteMatchTimeout, CancellationToken.None);
            try
            {
                var buffer = scope.CreateBuffer();
                buffer.Append("abcdefghijklmnop"u8);
                if (exact)
                {
                    if (textResult) Assert.Equal("abcdefghijklmnop", buffer.ToValidatedString());
                    else Assert.Equal("abcdefghijklmnop"u8.ToArray(), buffer.ToArray());
                }
                else Assert.Throws<InvalidOperationException>(() =>
                {
                    if (textResult) _ = buffer.ToValidatedString();
                    else _ = buffer.ToArray();
                });
                Assert.Equal(control * 2 + 16, scope.Invocation.Ledger.ReservedBytes);
            }
            finally { scope.Release(); }
            Assert.Equal(0UL, scope.Invocation.Ledger.ReservedBytes);
        }
        Assert.Null(Utf8SliceByteOutputScope.Current);
    }

    [Fact]
    public void CompleteOutputValidationPreservesAbsoluteInvalidOffsetsAcrossBlockEdges()
    {
        byte[][] suffixes = [[0xc0, 0x80], [0xed, 0xa0, 0x80], [0xf0, 0x80, 0x80, 0x80],
            [0xf4, 0x90, 0x80, 0x80], [0xf0, 0x9f, 0x98, 0x80, 0x80, 0x80, 0x80, 0x80],
            [0x80], [0xe2, 0x28, 0xa1], [0xf0, 0x9f, 0x98, 0x80], [0xc2, 0x80], [0xe2, 0x80, 0xa8]];
        for (var prefix = 4088; prefix <= 4100; prefix++)
        foreach (var suffix in suffixes)
        {
            var bytes = Enumerable.Repeat((byte)'x', prefix).Concat(suffix).ToArray();
            var expected = Record.Exception(() => Utf8Validation.Validate(bytes));
            var scope = Utf8SliceByteOutputScope.Enter(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, CancellationToken.None);
            try
            {
                var buffer = scope.CreateBuffer();
                buffer.Append(bytes);
                if (expected is ArgumentException invalid)
                {
                    var actual = Assert.Throws<ArgumentException>(() => buffer.ToArray());
                    Assert.Equal(invalid.Message, actual.Message);
                    Assert.Equal(invalid.ParamName, actual.ParamName);
                }
                else
                {
                    Assert.Null(expected);
                    Assert.Equal(bytes, buffer.ToArray());
                    Assert.Equal(Encoding.UTF8.GetString(bytes), buffer.ToValidatedString());
                }
            }
            finally { scope.Release(); }
            Assert.Equal(0UL, scope.Invocation.Ledger.ReservedBytes);
        }
    }

    [Fact]
    public void Utf16AppendKeepsPairsWithinItsFragmentAndReplacesSeparateLoneFragments()
    {
        var scope = Utf8SliceByteOutputScope.Enter(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, CancellationToken.None);
        try
        {
            var writer = new Utf8ReplacementWriter(scope.CreateBuffer());
            var text = new string('x', 2047) + "\U0001f600" + new string('y', 2047);
            writer.Append(text.AsSpan());
            writer.Append("\ud83d".AsSpan());
            writer.Append("\ude00".AsSpan());
            Assert.Equal(Encoding.UTF8.GetBytes(text + "\ufffd\ufffd"), writer.GetOwnedOutput(scope).ToArray());
        }
        finally { scope.Release(); }
        Assert.Equal(0UL, scope.Invocation.Ledger.ReservedBytes);
    }

    [Theory]
    [InlineData("append-bytes")]
    [InlineData("append-chars")]
    [InlineData("growth-copy")]
    [InlineData("reverse")]
    [InlineData("validate")]
    [InlineData("materialize")]
    [InlineData("string-size")]
    [InlineData("decode")]
    public void ByteOutputStagesObserveCancellationAndReleaseTheirTemporaryStorage(string operation)
    {
        using var source = new CancellationTokenSource();
        var scope = Utf8SliceByteOutputScope.Enter(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, source.Token);
        var buffer = scope.CreateBuffer();
        var bytes = Enumerable.Repeat((byte)'x', 300000).ToArray();
        if (!operation.StartsWith("append", StringComparison.Ordinal)) buffer.Append(bytes);
        if (operation == "reverse") buffer.MarkPiece();
        var held = scope.Invocation.Ledger.ReservedBytes;
        var start = scope.Invocation.Budget.Steps;
        var gate = operation is "materialize" or "string-size" ? start + 74 : operation == "decode" ? start + 148 : start;
        var entered = false;
        scope.Invocation.CheckpointHook = steps => { if (steps > gate) { entered = true; source.Cancel(); } };
        try
        {
            var exception = Assert.Throws<OperationCanceledException>(() =>
            {
                switch (operation)
                {
                    case "append-bytes": buffer.Append(bytes); break;
                    case "append-chars": buffer.Append(new string('x', 300000).AsSpan()); break;
                    case "growth-copy": buffer.Append("Y"u8); break;
                    case "reverse": buffer.ReversePieces(); break;
                    case "validate": _ = buffer.GetValidatedBytes(); break;
                    case "materialize": _ = buffer.ToArray(); break;
                    case "string-size": _ = buffer.ToValidatedString(); break;
                    case "decode": _ = buffer.ToValidatedString(); break;
                }
            });
            Assert.True(entered);
            Assert.Equal(source.Token, exception.CancellationToken);
            if (operation != "append-bytes") Assert.Equal(held, scope.Invocation.Ledger.ReservedBytes);
        }
        finally { scope.Release(); }
        Assert.Equal(0UL, scope.Invocation.Ledger.ReservedBytes);
        Assert.Null(Utf8SliceByteOutputScope.Current);
    }

    [Theory]
    [InlineData("bytes")]
    [InlineData("chars")]
    [InlineData("ascii")]
    [InlineData("rune")]
    public void PublicWriterCanBeCanceledInsideAnAppendAndRecover(string operation)
    {
        foreach (var compiled in new[] { false, true })
        foreach (var rtl in new[] { false, true })
        {
            var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None) |
                (rtl ? RegexOptions.RightToLeft : RegexOptions.None);
            var regex = new Utf8Regex("((.){2})", options);
            using var source = new CancellationTokenSource();
            var calls = 0;
            var entered = false;
            var session = Utf8SearchDiagnosticsSession.Start(steps => { if (calls != 0) { entered = true; source.Cancel(); } });
            try
            {
                var exception = Assert.Throws<OperationCanceledException>(() => regex.Replace("\U0001f600\U0001f600"u8, operation,
                    (in Utf8MatchContext context, ref Utf8ReplacementWriter writer, ref string state) =>
                    {
                        calls++;
                        switch (state)
                        {
                            case "bytes": writer.Append(new byte[400000]); break;
                            case "chars": writer.Append(new string('x', 400000).AsSpan()); break;
                            case "ascii": for (var i = 0; i < 400000; i++) writer.AppendAsciiByte((byte)'x'); break;
                            case "rune": for (var i = 0; i < 400000; i++) writer.Append(new Rune(0x1f600)); break;
                        }
                    }, source.Token));
                Assert.True(entered);
                Assert.Equal(1, calls);
                Assert.Equal(source.Token, exception.CancellationToken);
            }
            finally { session.Complete(); }
            Assert.Equal("\U0001f600\U0001f600"u8.ToArray(), regex.Replace("\U0001f600\U0001f600"u8, 0,
                static (in Utf8MatchContext context, ref Utf8ReplacementWriter writer, ref int state) => writer.Append(context.GetGroup(0).GetValueBytes())));
            AssertOwned(regex);
            Assert.Null(Utf8SliceByteOutputScope.Current);
        }
    }

    [Fact]
    public void CallbackExceptionsAndDeadlinesUnwindEveryWriterAliasAndPreserveFailureIdentity()
    {
        var regex = new Utf8Regex("((.){2})", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250));
        var input = "\U0001f600\U0001f600"u8.ToArray();
        var failure = new ApplicationException("writer evaluator failure");
        Assert.Same(failure, Assert.Throws<ApplicationException>(() => regex.Replace(input, failure,
            static (in Utf8MatchContext context, ref Utf8ReplacementWriter writer, ref ApplicationException state) =>
            {
                var alias = writer;
                writer = default;
                writer.Append("new"u8);
                alias.Append("old"u8);
                throw state;
            })));
        Assert.Null(Utf8SliceByteOutputScope.Current);
        var calls = new List<int>();
        var timeout = Assert.Throws<RegexMatchTimeoutException>(() => regex.Replace(input, calls,
            static (in Utf8MatchContext context, ref Utf8ReplacementWriter writer, ref List<int> state) =>
            {
                state.Add(context.IndexInUtf16);
                Thread.Sleep(300);
                writer.Append("ignored"u8);
            }));
        Assert.Single(calls);
        Assert.Equal(regex.Pattern, timeout.Pattern);
        Assert.Equal(Encoding.UTF8.GetString(input), timeout.Input);
        Assert.Equal(TimeSpan.FromMilliseconds(250), timeout.MatchTimeout);
        Assert.Null(Utf8SliceByteOutputScope.Current);
        Assert.Equal(input, regex.Replace(input, 0,
            static (in Utf8MatchContext context, ref Utf8ReplacementWriter writer, ref int state) => writer.Append(context.GetGroup(0).GetValueBytes())));
        AssertOwned(regex);
    }

    [Fact]
    public void ReturnedWriterIsTheOnlyLiveLeaseAfterItsCallbackBoundary()
    {
        var scope = Utf8SliceByteOutputScope.Enter(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, CancellationToken.None);
        var discarded = scope.CreateBuffer();
        var returned = scope.CreateBuffer();
        try
        {
            discarded.Append("old"u8);
            returned.Append("new"u8);
            scope.Retain(returned);
            Assert.Throws<InvalidOperationException>(() => discarded.Append("no longer live"u8));
            Assert.Equal("new"u8.ToArray(), returned.ToArray());
        }
        finally { scope.Release(); }
        scope.Release();
        Assert.Equal(0UL, scope.Invocation.Ledger.ReservedBytes);
        Assert.Throws<InvalidOperationException>(() => returned.ToArray());
        Assert.Null(Utf8SliceByteOutputScope.Current);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    public void WorkLimitsInterruptOutputBeforeAdmissionOrDuringCopyAndReleaseItsOwner(ulong steps)
    {
        var scope = Utf8SliceByteOutputScope.Enter(new(steps, 0, 0), Regex.InfiniteMatchTimeout, CancellationToken.None);
        try
        {
            var buffer = scope.CreateBuffer();
            Assert.Throws<InvalidOperationException>(() => buffer.Append(new byte[20000]));
            Assert.True(scope.Invocation.Budget.Steps > steps);
            Assert.Equal(Utf8SliceByteOutputScope.ControlBytes * 2 + (steps == 2 ? 0UL : 20000UL), scope.Invocation.Ledger.ReservedBytes);
        }
        finally { scope.Release(); }
        Assert.Equal(0UL, scope.Invocation.Ledger.ReservedBytes);
        Assert.Null(Utf8SliceByteOutputScope.Current);
    }

    [Theory]
    [InlineData("truncated")]
    [InlineData("surrogate")]
    [InlineData("overlong")]
    public void PublicInvalidOutputReportsTheFinalStreamOffsetAndLeavesTheRegexReusable(string kind)
    {
        byte[] invalid = kind switch
        {
            "truncated" => [0xf0],
            "surrogate" => [0xed, 0xa0, 0x80],
            _ => [0xc0, 0xaf],
        };
        var expectedBytes = new byte[invalid.Length + 1];
        expectedBytes[0] = (byte)'x';
        invalid.CopyTo(expectedBytes.AsSpan(1));
        var expected = Assert.Throws<ArgumentException>(() => Utf8Validation.Validate(expectedBytes));
        var regex = new Utf8Regex("(a)(?=$)", RegexOptions.CultureInvariant);
        var actual = Assert.Throws<ArgumentException>(() => regex.Replace("xa"u8, invalid,
            static (in Utf8MatchContext context, ref Utf8ReplacementWriter writer, ref byte[] state) => writer.Append(state)));
        Assert.Equal(expected.Message, actual.Message);
        Assert.Equal(expected.ParamName, actual.ParamName);
        Assert.Null(Utf8SliceByteOutputScope.Current);
        Assert.Equal("xa"u8.ToArray(), regex.Replace("xa"u8, 0,
            static (in Utf8MatchContext context, ref Utf8ReplacementWriter writer, ref int state) => writer.Append(context.GetGroup(0).GetValueBytes())));
        AssertOwned(regex);
    }
}
