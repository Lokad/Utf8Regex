using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReConstructionAdmissionTests
{
    [Fact]
    public void BytePatternWorkAdmissionPrecedesValidationAndDecode()
    {
        using var scope = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, 0, ulong.MaxValue);
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => new Utf8PythonRegex(new byte[] { 0xff }));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, failure.ResourceKind);
        Assert.Equal(1ul, failure.Requested);
        Assert.Equal(0ul, scope.Budget.WorkSteps);
        Assert.Equal(0ul, scope.Budget.ReservedBytes);
        Assert.Throws<NotSupportedException>(() =>
            new Utf8PythonRegex("cat"u8, PythonReCompileOptions.None, Timeout.InfiniteTimeSpan,
                new Utf8ResourcePolicy { MaxWorkingBytes = 1 }));
        Assert.Equal(0ul, scope.Budget.WorkSteps);
        var acceptedPolicy = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
            new Utf8PythonRegex("cat"u8, PythonReCompileOptions.None, Timeout.InfiniteTimeSpan,
                new Utf8ResourcePolicy { MaxWorkSteps = 1 }));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, acceptedPolicy.ResourceKind);
        Assert.Equal(0ul, scope.Budget.WorkSteps);
        Assert.Equal(0ul, scope.Budget.ReservedBytes);
    }

    [Fact]
    public void StringParserWorkUsesTheOuterOwnerAndDoesNotSelectAnOptionalBackend()
    {
        using var scope = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, 0, ulong.MaxValue);
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => new Utf8PythonRegex("cat"));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, failure.ResourceKind);
        Assert.Equal(0ul, scope.Budget.WorkSteps);
        Assert.Equal(0ul, scope.Budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ScalarPreparationInheritsDepthAndRecoversWithTheSameOwner(bool bytes)
    {
        var pattern = new string('(', 64) + "." + new string(')', 64);
        var encoded = System.Text.Encoding.UTF8.GetBytes(pattern);
        using var scope = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, 8);
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
        {
            if (bytes) _ = new Utf8PythonRegex(encoded);
            else _ = new Utf8PythonRegex(pattern);
        });
        Assert.Equal(Utf8ResourceLimitKind.ConstructionDepth, failure.ResourceKind);
        Assert.Equal(8ul, failure.Limit);
        Assert.Equal(0ul, scope.Budget.Depth);
        Assert.Equal(0ul, scope.Budget.ReservedBytes);
        Assert.True(scope.Budget.WorkSteps > 0);
        Assert.True(new Utf8PythonRegex(".").IsMatch("😀"u8));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void EveryBytePatternEntryAdmitsDecodedTextBeforeParsing(int entry)
    {
        // Valid UTF-8, deliberately invalid grammar: admission must precede parsing.
        var encoded = System.Text.Encoding.UTF8.GetBytes(new string('(', 256));
        var decodedBytes = Utf8ConstructionBudget.StringBytes(256);
        using var scope = new Utf8ConstructionScope(Utf8ConstructionScope.DefaultInstructionCapacity, decodedBytes + 47);
        var parent = scope.Budget.AllocateArray<byte>(1, false);
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
        {
            _ = entry switch
            {
                0 => new Utf8PythonRegex(encoded),
                1 => new Utf8PythonRegex(encoded, PythonReCompileOptions.None),
                2 => new Utf8PythonRegex(encoded, PythonReCompileOptions.None, Timeout.InfiniteTimeSpan),
                _ => new Utf8PythonRegex(encoded, PythonReCompileOptions.None, Timeout.InfiniteTimeSpan, Utf8ResourcePolicy.Unlimited),
            };
        });
        Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, failure.ResourceKind);
        Assert.Equal(decodedBytes + 48, failure.Requested);
        Assert.Equal(48ul, scope.Budget.ReservedBytes);
        Assert.Equal(48ul, scope.Budget.PeakBytes);
        Assert.Equal(0ul, scope.Budget.Peak);
        scope.Budget.ReleaseArray(parent, false);
    }

    [Theory]
    [InlineData("")] [InlineData("cat")] [InlineData("é")]
    [InlineData("😀")] [InlineData("a😀é中")]
    public void RetainedDecodedPatternSharesTheCompilationLoanWithoutChargingBorrowedText(string pattern)
    {
        ulong held;
        ulong peak;
        ulong slots;
        using (var borrowed = new Utf8ConstructionScope(Utf8ConstructionScope.DefaultInstructionCapacity))
        {
            var regex = new Utf8PythonRegex(pattern);
            Assert.Same(pattern, regex.Pattern);
            held = borrowed.Budget.ReservedBytes;
            peak = borrowed.Budget.PeakBytes;
            slots = borrowed.Budget.Reserved;
        }
        var encoded = System.Text.Encoding.UTF8.GetBytes(pattern);
        using var decoded = new Utf8ConstructionScope(Utf8ConstructionScope.DefaultInstructionCapacity);
        var owned = new Utf8PythonRegex(encoded);
        Assert.Equal(pattern, owned.Pattern);
        Assert.True(owned.IsMatch(encoded));
        Assert.Equal(held + Utf8ConstructionBudget.StringBytes(pattern.Length), decoded.Budget.ReservedBytes);
        Assert.Equal(peak + Utf8ConstructionBudget.StringBytes(pattern.Length), decoded.Budget.PeakBytes);
        Assert.Equal(slots, decoded.Budget.Reserved);
        if (encoded.Length > 0) encoded[0] = (byte)'z';
        Assert.Equal(pattern, owned.Pattern);
    }

    [Fact]
    public void PolicyAndUtf8ErrorsPrecedeDecodeAdmissionAndFailedParsingRestoresParent()
    {
        // Core scalar/prepared/cursor plan objects share the compiler/AST/IR loan. The original "cat"
        // retry retains the bounded 48-KiB variable envelope plus the core's
        // one conservative fixed-metadata charge.
        using var scope = new Utf8ConstructionScope(Utf8ConstructionScope.DefaultInstructionCapacity,
            49152 + Utf8Regex.ProgramFixedMetadataBytes);
        var parent = scope.Budget.AllocateArray<byte>(1, false);
        var invalid = new byte[] { 0xed, 0xa0, 0x80 };
        Assert.Throws<NotSupportedException>(() =>
            new Utf8PythonRegex(invalid, PythonReCompileOptions.None, Timeout.InfiniteTimeSpan,
                new Utf8ResourcePolicy { MaxWorkingBytes = 1 }));
        var utf8 = Assert.Throws<ArgumentException>(() => new Utf8PythonRegex(invalid));
        Assert.Equal("patternUtf8", utf8.ParamName);
        Assert.IsType<System.Text.DecoderFallbackException>(utf8.InnerException);
        Assert.Equal(48ul, scope.Budget.PeakBytes);
        Assert.Throws<PythonRePatternException>(() => new Utf8PythonRegex("("u8));
        Assert.Equal(48ul, scope.Budget.ReservedBytes);
        Assert.Equal(0ul, scope.Budget.Reserved);
        Assert.True(new Utf8PythonRegex("cat"u8).IsMatch("cat"u8));
        scope.Budget.ReleaseArray(parent, false);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void WideScalarIrCannotBypassTheEnclosingByteOwner(bool bytes)
    {
        // Preserve wide denial and the original same-owner "cat" recovery;
        // its variable AST/IR/plan envelope stays at 48 KiB; separately cover
        // the core's fixed metadata while still denying the wide pattern.
        const ulong maximumBytes = 49152 + Utf8Regex.ProgramFixedMetadataBytes;
        var pattern = new string('a', 32768);
        var encoded = System.Text.Encoding.UTF8.GetBytes(pattern);
        using var scope = new Utf8ConstructionScope(Utf8ConstructionScope.DefaultInstructionCapacity, maximumBytes);
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
        {
            if (bytes) _ = new Utf8PythonRegex(encoded);
            else _ = new Utf8PythonRegex(pattern);
        });
        Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, failure.ResourceKind);
        Assert.Equal(maximumBytes, failure.Limit);
        Assert.True(scope.Budget.PeakBytes <= maximumBytes);
        Assert.Equal(0ul, scope.Budget.ReservedBytes);
        Assert.Equal(0ul, scope.Budget.Reserved);
        Assert.True(new Utf8PythonRegex("cat").IsMatch("cat"u8));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ScalarConstructionPropagatesByteAdmissionFailure(bool bytes)
    {
        using (var scope = new Utf8ConstructionScope(Utf8ConstructionScope.DefaultInstructionCapacity, 1))
        {
            var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
            {
                if (bytes) _ = new Utf8PythonRegex("cat"u8);
                else _ = new Utf8PythonRegex("cat");
            });
            Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, failure.ResourceKind);
            Assert.Equal(1ul, failure.Limit);
            Assert.True(failure.Requested > 1ul);
            Assert.Equal(0ul, scope.Budget.Reserved);
            Assert.Equal(0ul, scope.Budget.ReservedBytes);
        }
        Assert.False(Utf8ConstructionScope.IsActive);
        Assert.True(new Utf8PythonRegex("cat").IsMatch("cat"u8));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ScalarConstructionUsesTheSharedOwnerAndPropagatesCapacityFailure(bool bytes)
    {
        using (var scope = new Utf8ConstructionScope(1))
        {
            var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
            {
                if (bytes) _ = new Utf8PythonRegex("cat"u8);
                else _ = new Utf8PythonRegex("cat");
            });
            Assert.Equal(Utf8ResourceLimitKind.ConstructionInstructionCapacity, failure.ResourceKind);
            Assert.Equal(1ul, failure.Limit);
            Assert.True(failure.Requested > 1ul);
            Assert.Equal(0ul, scope.Budget.Reserved);
        }
        Assert.False(Utf8ConstructionScope.IsActive);
        Assert.True(new Utf8PythonRegex("cat").IsMatch("cat"u8));
    }
}
