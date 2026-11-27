using System.Text;
using Lokad.Utf8Regex.Internal;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReNativeReplacementParserTests
{
    private static readonly IReadOnlyDictionary<string, int> Names =
        new Dictionary<string, int>(StringComparer.Ordinal) { ["word"] = 1, ["𝒜"] = 2 };

    [Theory]
    [InlineData(2046)] [InlineData(2047)] [InlineData(2048)] [InlineData(4095)]
    public void LiteralBlocksRejoinScalars(int prefixLength)
    {
        var prefix = new string('x', prefixLength);
        var template = prefix + "😀é\\g<word>" + prefix + "😀é";
        var native = PythonReReplacementParser.Parse(Encoding.UTF8.GetBytes(template).AsSpan(), 2, Names);
        Assert.Equal(3, native.Tokens.Length);
        var literal = Encoding.UTF8.GetBytes(prefix + "😀é");
        Assert.Equal(literal, native.Tokens[0].RequiredUtf8);
        Assert.Equal(PythonReReplacementToken.ForGroupNumber(1), native.Tokens[1]);
        Assert.Equal(literal, native.Tokens[2].RequiredUtf8);
    }

    [Theory]
    [InlineData(@"\q", 0)]
    [InlineData("\\", 0)]
    [InlineData(@"\g", 2)]
    [InlineData(@"\g<", 3)]
    [InlineData(@"\g<word", 3)]
    [InlineData(@"\g<>", 3)]
    [InlineData(@"\g<bad name>", 3)]
    [InlineData(@"\g<3>", 3)]
    [InlineData(@"\g<00000000000000000000000099999999999999999999>", 3)]
    [InlineData(@"\3", 1)]
    [InlineData(@"\78", 1)]
    [InlineData(@"\400", 0)]
    [InlineData(@"\777", 0)]
    public void ErrorsRetainUtf16CoordinatesAfterUnicodePrefixes(string suffix, int relativePosition)
    {
        foreach (var prefix in new[] { "", "é😀\0e\u0301", new string('x', 2047) + "😀" })
        {
            var template = Encoding.UTF8.GetBytes(prefix + suffix);
            var error = Assert.Throws<PythonRePatternException>(() => PythonReReplacementParser.Parse(template, 2, Names));
            Assert.Equal(prefix.Length + relativePosition, error.Position);
            Assert.Equal(PythonReErrorSource.Replacement, error.ErrorSource);
        }
    }

    [Theory]
    [InlineData(new byte[] { 0x80 })]
    [InlineData(new byte[] { 0xf0, 0x9f, 0x98 })]
    [InlineData(new byte[] { 0xed, 0xa0, 0x80 })]
    [InlineData(new byte[] { 0xc0, 0x80 })]
    public void StrictValidationIncludesTheTailBeforeTemplateGrammar(byte[] malformed)
    {
        byte[] template = [.. "é😀\\q"u8, .. malformed];
        using var owner = Utf8ConstructionScope.Enter();
        var before = owner.Budget.ReservedBytes;
        Assert.Throws<ArgumentException>(() => PythonReReplacementParser.Parse(template.AsSpan(), 2, Names));
        Assert.Equal(before, owner.Budget.ReservedBytes);
    }

    [Fact]
    public void NativeConstructionDenialsAndCancellationRefundTheirParentLoans()
    {
        using var checkpointObservation = global::Lokad.Utf8Regex.Internal.Diagnostics.Utf8SearchDiagnosticsSession.Start();
        var template = "é😀\\g<𝒜>\\077\\g<1>\\377"u8.ToArray();
        ulong steps, peak;
        using (var probe = Utf8ConstructionScope.Enter())
        {
            _ = PythonReReplacementParser.Parse(template.AsSpan(), 2, Names);
            steps = probe.Budget.DiagnosticCheckpoints; peak = probe.Budget.PeakBytes;
        }
        foreach (var cap in new[] { 0UL, 256UL, peak / 2, peak - 1, peak })
        {
            using var owner = new Utf8ConstructionScope(ulong.MaxValue, 7 + cap, ulong.MaxValue);
            owner.Budget.ReserveBytes(7);
            using (var child = Utf8ConstructionScope.Enter())
            {
                var failure = Record.Exception(() => PythonReReplacementParser.Parse(template.AsSpan(), 2, Names));
                if (cap < peak)
                    Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes,
                        Assert.IsType<Utf8ResourceLimitExceededException>(failure).ResourceKind);
                else Assert.Null(failure);
            }
            Assert.Equal(7UL, owner.Budget.ReservedBytes);
        }
        var checkpoints = 0;
        using (var probe = Utf8ConstructionScope.Enter())
        {
            probe.Budget.CheckpointHook = () => checkpoints++;
            _ = PythonReReplacementParser.Parse(template.AsSpan(), 2, Names);
        }
        for (var target = 1; target <= checkpoints; target++)
        {
            using var owner = Utf8ConstructionScope.Enter(); owner.Budget.ReserveBytes(7);
            using var token = new CancellationTokenSource(); var calls = 0;
            owner.Budget.CancellationToken = token.Token;
            owner.Budget.CheckpointHook = () => { if (++calls == target) token.Cancel(); };
            var failure = Assert.Throws<OperationCanceledException>(() =>
                PythonReReplacementParser.Parse(template.AsSpan(), 2, Names));
            Assert.Equal(token.Token, failure.CancellationToken);
            Assert.Equal(7UL, owner.Budget.ReservedBytes);
        }
    }
}
