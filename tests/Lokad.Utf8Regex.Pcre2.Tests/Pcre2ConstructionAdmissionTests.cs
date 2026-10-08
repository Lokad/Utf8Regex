using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2ConstructionAdmissionTests
{
    [Fact]
    public void Pcre2CandidateByteSearchSharesCoreAdmissionAndRecoversTheParent()
    {
        using var owner = new Utf8ConstructionScope(0, 48, ulong.MaxValue, ulong.MaxValue);
        var parent = owner.Budget.AllocateArray<byte>(1, false); parent[0] = 0xa5;
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
            Pcre2CandidateSearchProgram.FromLeadingAsciiSet(new byte[] { 0, 64, 96, 127 }, default));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, failure.ResourceKind);
        Assert.Equal(48ul, owner.Budget.ReservedBytes); Assert.Equal(48ul, owner.Budget.PeakBytes);
        Assert.Equal(0ul, owner.Budget.Depth); Assert.Null(owner.Budget.ObjectCheckpoint); Assert.Equal(0xa5, parent[0]);
        owner.Budget.ReleaseArray(parent, false);
        var program = Pcre2CandidateSearchProgram.FromLeadingAsciiSet(new byte[] { 64, 127 }, default);
        Assert.Equal(48ul, owner.Budget.ReservedBytes);
        Assert.Equal(1, program.Searcher.FindFirst(new byte[] { 128, 64, 255 }));
        Assert.Equal(-1, program.Searcher.FindFirst(new byte[] { 128, 255 }));
    }

    [Fact]
    public void BytePatternWorkAdmissionPrecedesValidationAndDecode()
    {
        using var scope = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, 0, ulong.MaxValue);
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => new Utf8Pcre2Regex(new byte[] { 0xff }));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, failure.ResourceKind);
        Assert.Equal(1ul, failure.Requested);
        Assert.Equal(0ul, scope.Budget.WorkSteps);
        Assert.Equal(0ul, scope.Budget.ReservedBytes);
        Assert.Throws<NotSupportedException>(() =>
            Utf8Pcre2Regex.Create("cat"u8, Pcre2CompileOptions.None, default,
                new Utf8ResourcePolicy { MaxResultBytes = 1 }, Timeout.InfiniteTimeSpan));
        Assert.Equal(0ul, scope.Budget.WorkSteps);
    }

    [Fact]
    public void DelegatedStringParserWorkCannotResetOrSuppressTheOuterOwner()
    {
        using var scope = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, 0, ulong.MaxValue);
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => new Utf8Pcre2Regex("cat"));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, failure.ResourceKind);
        Assert.Equal(0ul, scope.Budget.WorkSteps);
        Assert.Equal(0ul, scope.Budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CoreParserDepthCannotBeSuppressedAndRecoversWithTheSameOwner(bool bytes)
    {
        var pattern = new string('(', 64) + "." + new string(')', 64);
        var encoded = System.Text.Encoding.UTF8.GetBytes(pattern);
        using var scope = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, 8);
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
        {
            if (bytes) _ = new Utf8Pcre2Regex(encoded);
            else _ = new Utf8Pcre2Regex(pattern);
        });
        Assert.Equal(Utf8ResourceLimitKind.ConstructionDepth, failure.ResourceKind);
        Assert.Equal(8ul, failure.Limit);
        Assert.Equal(0ul, scope.Budget.Depth);
        Assert.Equal(0ul, scope.Budget.ReservedBytes);
        Assert.True(scope.Budget.WorkSteps > 0);
        Assert.True(new Utf8Pcre2Regex(".").IsMatch("😀"u8));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void EveryBytePatternEntryAdmitsDecodedTextBeforeParsing(int entry)
    {
        var encoded = System.Text.Encoding.UTF8.GetBytes(new string('(', 256));
        var decodedBytes = Utf8ConstructionBudget.StringBytes(256);
        using var scope = new Utf8ConstructionScope(Utf8ConstructionScope.DefaultInstructionCapacity, decodedBytes + 47);
        var parent = scope.Budget.AllocateArray<byte>(1, false);
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
        {
            _ = entry switch
            {
                0 => new Utf8Pcre2Regex(encoded),
                1 => new Utf8Pcre2Regex(encoded, Pcre2CompileOptions.None),
                2 => new Utf8Pcre2Regex(encoded, Pcre2CompileOptions.None, default, default(Utf8Pcre2ExecutionLimits), Timeout.InfiniteTimeSpan),
                _ => Utf8Pcre2Regex.Create(encoded, Pcre2CompileOptions.None, default, Utf8ResourcePolicy.Unlimited, Timeout.InfiniteTimeSpan),
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
            var regex = new Utf8Pcre2Regex(pattern);
            Assert.Same(pattern, regex.Pattern);
            held = borrowed.Budget.ReservedBytes;
            peak = borrowed.Budget.PeakBytes;
            slots = borrowed.Budget.Reserved;
        }
        var encoded = System.Text.Encoding.UTF8.GetBytes(pattern);
        using var decoded = new Utf8ConstructionScope(Utf8ConstructionScope.DefaultInstructionCapacity);
        var owned = new Utf8Pcre2Regex(encoded);
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
        using var scope = new Utf8ConstructionScope(Utf8ConstructionScope.DefaultInstructionCapacity, 4096);
        var parent = scope.Budget.AllocateArray<byte>(1, false);
        var invalid = new byte[] { 0xed, 0xa0, 0x80 };
        Assert.Throws<NotSupportedException>(() =>
            Utf8Pcre2Regex.Create(invalid, Pcre2CompileOptions.None, default,
                new Utf8ResourcePolicy { MaxResultBytes = 1 }, Timeout.InfiniteTimeSpan));
        var utf8 = Assert.Throws<ArgumentException>(() => new Utf8Pcre2Regex(invalid));
        Assert.Equal("byteOffset", utf8.ParamName);
        Assert.Contains("byte offset 0", utf8.Message);
        Assert.Equal(48ul, scope.Budget.PeakBytes);
        Assert.Throws<Pcre2CompileException>(() => new Utf8Pcre2Regex("("u8));
        Assert.Equal(48ul, scope.Budget.ReservedBytes);
        Assert.Equal(0ul, scope.Budget.Reserved);
        // Literal runtime fields are now admitted; "cat" no longer fits this
        // four-KiB owner. Recover through the smaller owned scalar route.
        var recovered = new Utf8Pcre2Regex("."u8);
        Assert.True(recovered.IsMatch("😀"u8));
        Assert.False(recovered.DebugFoundationCoreFallbackRegexIsCreated);
        Assert.Equal(0, parent[0]);
        Assert.InRange(scope.Budget.ReservedBytes, 48ul, 4096ul);
        scope.Budget.ReleaseArray(parent, false);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void DelegatedScalarIrCannotBypassTheEnclosingByteOwner(bool bytes)
    {
        var pattern = new string('a', 32768);
        var encoded = System.Text.Encoding.UTF8.GetBytes(pattern);
        using var scope = new Utf8ConstructionScope(Utf8ConstructionScope.DefaultInstructionCapacity, 4096);
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
        {
            if (bytes) _ = new Utf8Pcre2Regex(encoded);
            else _ = new Utf8Pcre2Regex(pattern);
        });
        Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, failure.ResourceKind);
        Assert.Equal(4096ul, failure.Limit);
        Assert.True(scope.Budget.PeakBytes <= 4096);
        Assert.Equal(0ul, scope.Budget.ReservedBytes);
        Assert.Equal(0ul, scope.Budget.Reserved);
        // Keep the original byte limit and prove recovery using an owned scalar
        // program whose complete retained construction state fits that limit.
        var recovered = new Utf8Pcre2Regex(".");
        Assert.True(recovered.IsMatch("😀"u8));
        Assert.False(recovered.DebugFoundationCoreFallbackRegexIsCreated);
        Assert.InRange(scope.Budget.ReservedBytes, 1ul, 4096ul);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CoreDelegationCannotSuppressByteAdmissionFailure(bool bytes)
    {
        using (var scope = new Utf8ConstructionScope(Utf8ConstructionScope.DefaultInstructionCapacity, 1))
        {
            var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
            {
                if (bytes) _ = new Utf8Pcre2Regex("cat"u8);
                else _ = new Utf8Pcre2Regex("cat");
            });
            Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, failure.ResourceKind);
            Assert.Equal(1ul, failure.Limit);
            Assert.True(failure.Requested > 1ul);
            Assert.Equal(0ul, scope.Budget.Reserved);
            Assert.Equal(0ul, scope.Budget.ReservedBytes);
        }
        Assert.False(Utf8ConstructionScope.IsActive);
        var recovered = new Utf8Pcre2Regex("cat");
        Assert.True(recovered.IsMatch("cat"u8));
        Assert.False(recovered.DebugFoundationCoreFallbackRegexIsCreated);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CoreDelegationUsesTheSharedOwnerAndCannotSuppressCapacityFailure(bool bytes)
    {
        using (var scope = new Utf8ConstructionScope(1))
        {
            var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
            {
                if (bytes) _ = new Utf8Pcre2Regex("cat"u8);
                else _ = new Utf8Pcre2Regex("cat");
            });
            Assert.Equal(Utf8ResourceLimitKind.ConstructionInstructionCapacity, failure.ResourceKind);
            Assert.Equal(1ul, failure.Limit);
            Assert.True(failure.Requested > 1ul);
            Assert.Equal(0ul, scope.Budget.Reserved);
        }
        Assert.False(Utf8ConstructionScope.IsActive);
        var recovered = new Utf8Pcre2Regex("cat");
        Assert.True(recovered.IsMatch("cat"u8));
        Assert.False(recovered.DebugFoundationCoreFallbackRegexIsCreated);
    }
}
