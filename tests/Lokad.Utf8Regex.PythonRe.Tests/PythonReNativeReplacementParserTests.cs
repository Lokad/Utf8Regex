using System.Text;
using Lokad.Utf8Regex.Internal;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReNativeReplacementParserTests
{
    private static readonly IReadOnlyDictionary<string, int> Names =
        new Dictionary<string, int>(StringComparer.Ordinal) { ["word"] = 1, ["𝒜"] = 2 };

    public static IEnumerable<object[]> Templates()
    {
        foreach (var row in PythonReReplacementPreparationAdmissionTests.Shapes())
            if ((string)row[0] != "surrogate-fragments") yield return row;
        yield return ["é😀\0e\u0301\\é\\😀\\g<𝒜>\\377\\g<00000000000000000001>"];
    }

    [Theory]
    [MemberData(nameof(Templates))]
    public void NativeAndTextTemplatesPublishIdenticalBytePrograms(string template)
    {
        // Shapes uses two additional name spellings.
        var names = new Dictionary<string, int>(Names) { ["_é"] = 7 };
        var expected = PythonReReplacementParser.Parse(template, 12, names);
        var actual = PythonReReplacementParser.Parse(Encoding.UTF8.GetBytes(template).AsSpan(), 12, names);
        Assert.Equal(expected.LiteralUtf8, actual.LiteralUtf8);
        Assert.Equal(expected.Tokens.Length, actual.Tokens.Length);
        for (var i = 0; i < expected.Tokens.Length; i++)
        {
            Assert.Equal(expected.Tokens[i].Kind, actual.Tokens[i].Kind);
            Assert.Equal(expected.Tokens[i].Number, actual.Tokens[i].Number);
            Assert.Equal(expected.Tokens[i].Utf8, actual.Tokens[i].Utf8);
        }
    }

    [Theory]
    [InlineData(@"\q")]
    [InlineData("\\")]
    [InlineData(@"\g")]
    [InlineData(@"\g<")]
    [InlineData(@"\g<word")]
    [InlineData(@"\g<>")]
    [InlineData(@"\g<bad name>")]
    [InlineData(@"\g<unknown>")]
    [InlineData(@"\g<3>")]
    [InlineData(@"\g<00000000000000000000000099999999999999999999>")]
    [InlineData(@"\3")]
    [InlineData(@"\78")]
    [InlineData(@"\400")]
    [InlineData(@"\777")]
    public void ErrorsRetainTextCoordinatesAndVocabularyAfterUnicodePrefixes(string suffix)
    {
        foreach (var prefix in new[] { "", "é😀\0e\u0301", new string('x', 2047) + "😀" })
        {
            var template = prefix + suffix;
            var expected = Record.Exception(() => PythonReReplacementParser.Parse(template, 2, Names));
            var actual = Record.Exception(() => PythonReReplacementParser.Parse(Encoding.UTF8.GetBytes(template).AsSpan(), 2, Names));
            Assert.NotNull(expected); Assert.NotNull(actual);
            Assert.Equal(expected.GetType(), actual.GetType());
            Assert.Equal(expected.Message, actual.Message);
            if (expected is PythonRePatternException textError)
            {
                var nativeError = Assert.IsType<PythonRePatternException>(actual);
                Assert.Equal(textError.Position, nativeError.Position);
                Assert.Equal(textError.ErrorSource, nativeError.ErrorSource);
                Assert.Equal(textError.ErrorKind, nativeError.ErrorKind);
            }
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
        var template = "é😀\\g<𝒜>\\077\\g<1>\\377"u8.ToArray();
        ulong steps, peak;
        using (var probe = Utf8ConstructionScope.Enter())
        {
            _ = PythonReReplacementParser.Parse(template.AsSpan(), 2, Names);
            steps = probe.Budget.WorkSteps; peak = probe.Budget.PeakBytes;
        }
        for (ulong cap = 0; cap <= steps; cap++)
        {
            using var owner = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, cap, ulong.MaxValue);
            owner.Budget.ReserveBytes(7);
            using (var child = Utf8ConstructionScope.Enter())
            {
                var failure = Record.Exception(() => PythonReReplacementParser.Parse(template.AsSpan(), 2, Names));
                if (cap < steps)
                    Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps,
                        Assert.IsType<Utf8ResourceLimitExceededException>(failure).ResourceKind);
                else Assert.Null(failure);
            }
            Assert.Equal(7UL, owner.Budget.ReservedBytes);
            Assert.InRange(owner.Budget.WorkSteps, 0UL, cap);
        }
        foreach (var cap in new[] { 0UL, 256UL, peak / 2, peak - 1, peak })
        {
            using var owner = new Utf8ConstructionScope(ulong.MaxValue, 7 + cap, ulong.MaxValue, ulong.MaxValue);
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
            probe.Budget.CheckpointHook = _ => checkpoints++;
            _ = PythonReReplacementParser.Parse(template.AsSpan(), 2, Names);
        }
        for (var target = 1; target <= checkpoints; target++)
        {
            using var owner = Utf8ConstructionScope.Enter(); owner.Budget.ReserveBytes(7);
            using var token = new CancellationTokenSource(); var calls = 0;
            owner.Budget.CancellationToken = token.Token;
            owner.Budget.CheckpointHook = _ => { if (++calls == target) token.Cancel(); };
            var failure = Assert.Throws<OperationCanceledException>(() =>
                PythonReReplacementParser.Parse(template.AsSpan(), 2, Names));
            Assert.Equal(token.Token, failure.CancellationToken);
            Assert.Equal(7UL, owner.Budget.ReservedBytes);
        }
    }
}
