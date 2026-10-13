using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2ConstructionAdmissionTests
{
    [Theory]
    [InlineData(0, false)] [InlineData(0, true)]
    [InlineData(1, false)] [InlineData(1, true)]
    [InlineData(2, false)] [InlineData(2, true)]
    [InlineData(3, false)] [InlineData(3, true)]
    [InlineData(4, false)] [InlineData(4, true)]
    public void InvalidArgumentsPrecedeByteDecodingAndLeaveConstructionUnspent(int kind, bool factory)
    {
        using var scope = new Utf8ConstructionScope(0, 0, 0, 0);
        var options = kind == 1 ? (Pcre2CompileOptions)(1 << 29) : Pcre2CompileOptions.None;
        var settings = kind switch
        {
            2 => new Utf8Pcre2CompileSettings { Newline = (Pcre2NewlineConvention)999 },
            3 => new Utf8Pcre2CompileSettings { Bsr = (Pcre2BsrConvention)999 },
            4 => new Utf8Pcre2CompileSettings { BackslashC = (Pcre2BackslashCPolicy)999 },
            _ => default,
        };
        var timeout = kind == 0 ? TimeSpan.Zero : Timeout.InfiniteTimeSpan;
        foreach (var pattern in new[] { new byte[] { 0xff }, new byte[4096] })
        {
            var error = Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                if (factory) _ = Utf8Pcre2Regex.Create(pattern, options, settings, Utf8ResourcePolicy.Unlimited, timeout);
                else _ = new Utf8Pcre2Regex(pattern, options, settings, default(Utf8Pcre2ExecutionLimits), timeout);
            });
            Assert.Equal(kind == 0 ? "matchTimeout" : kind == 1 ? "options" : "compileSettings", error.ParamName);
            Assert.Equal(0UL, scope.Budget.WorkSteps);
            Assert.Equal(0UL, scope.Budget.ReservedBytes);
            Assert.Equal(0UL, scope.Budget.PeakBytes);
            Assert.Equal(0UL, scope.Budget.Depth);
        }
    }

    [Fact]
    public void FactoryZeroPolicyStillPrecedesOtherInvalidArguments()
    {
        using var scope = new Utf8ConstructionScope(0, 0, 0, 0);
        var error = Assert.Throws<ArgumentOutOfRangeException>(() =>
            Utf8Pcre2Regex.Create(new byte[] { 0xff }, (Pcre2CompileOptions)(1 << 29),
                new Utf8Pcre2CompileSettings { Bsr = (Pcre2BsrConvention)999 },
                new Utf8ResourcePolicy { MaxResultBytes = 0 }, TimeSpan.Zero));
        Assert.Equal("resourcePolicy", error.ParamName);
        Assert.Equal(0UL, scope.Budget.WorkSteps);
        Assert.Equal(0UL, scope.Budget.ReservedBytes);
    }

    [Fact]
    public void TimeoutPrecedenceIsConsistentAcrossStringAndByteEntries()
    {
        var options = (Pcre2CompileOptions)(1 << 29);
        var settings = new Utf8Pcre2CompileSettings { Newline = (Pcre2NewlineConvention)999 };
        Assert.Equal("matchTimeout", Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Utf8Pcre2Regex("[invalid", options, settings, default(Utf8Pcre2ExecutionLimits), TimeSpan.Zero)).ParamName);
        Assert.Equal("matchTimeout", Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Utf8Pcre2Regex(new byte[] { 0xff }, options, settings, default(Utf8Pcre2ExecutionLimits), TimeSpan.Zero)).ParamName);
    }

    [Fact]
    public void Pcre2CandidateByteSearchSharesCoreAdmissionAndRecoversTheParent()
    {
        using var owner = new Utf8ConstructionScope(0, 48, ulong.MaxValue, ulong.MaxValue);
        var parent = owner.Budget.AllocateArray<byte>(1, false); parent[0] = 0xa5;
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
            Pcre2CandidateSearchProgram.FromLeadingAsciiSet(new byte[] { 0, 64, 96, 127 }, default));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, failure.ResourceKind);
        Assert.Equal(48ul, owner.Budget.ReservedBytes); Assert.Equal(48ul, owner.Budget.PeakBytes);
        Assert.Equal(0ul, owner.Budget.Depth); Assert.Equal(0xa5, parent[0]);
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
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Utf8Pcre2Regex.Create("cat"u8, Pcre2CompileOptions.None, default,
                new Utf8ResourcePolicy { MaxResultBytes = 0 }, Timeout.InfiniteTimeSpan));
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
        // The boundary recovery includes admitted scalar/prepared/cursor plans;
        // the byte-pattern probe peaks at 22,672 bytes, plus this parent loan.
        using var scope = new Utf8ConstructionScope(Utf8ConstructionScope.DefaultInstructionCapacity, 32768);
        var parent = scope.Budget.AllocateArray<byte>(1, false);
        var invalid = new byte[] { 0xed, 0xa0, 0x80 };
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Utf8Pcre2Regex.Create(invalid, Pcre2CompileOptions.None, default,
                new Utf8ResourcePolicy { MaxResultBytes = 0 }, Timeout.InfiniteTimeSpan));
        var utf8 = Assert.Throws<ArgumentException>(() => new Utf8Pcre2Regex(invalid));
        Assert.Equal("byteOffset", utf8.ParamName);
        Assert.Contains("byte offset 0", utf8.Message);
        Assert.Equal(48ul, scope.Budget.PeakBytes);
        Assert.Throws<Pcre2CompileException>(() => new Utf8Pcre2Regex("("u8));
        Assert.Equal(48ul, scope.Budget.ReservedBytes);
        Assert.Equal(0ul, scope.Budget.Reserved);
        // Keep the original owned boundary recovery within a bounded 32 KiB.
        var recovered = new Utf8Pcre2Regex("\\b"u8);
        Assert.True(recovered.IsMatch("a😀"u8));
        Assert.Equal(2, recovered.Count("a😀"u8));
        Assert.True(recovered.DebugCompiledProgram is not null);
        Assert.Equal(0, parent[0]);
        Assert.InRange(scope.Budget.ReservedBytes, 48ul, 32768ul);
        scope.Budget.ReleaseArray(parent, false);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void DelegatedScalarIrCannotBypassTheEnclosingByteOwner(bool bytes)
    {
        var pattern = new string('a', 32768);
        var encoded = System.Text.Encoding.UTF8.GetBytes(pattern);
        // The boundary route now admits tree, scalar program and plan descriptors;
        // the funded byte-pattern probe peaks at 22,672 bytes before a parent loan.
        // 32 KiB still rejects the wide IR and funds the same-owner recovery.
        using var scope = new Utf8ConstructionScope(Utf8ConstructionScope.DefaultInstructionCapacity, 32768);
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
        {
            if (bytes) _ = new Utf8Pcre2Regex(encoded);
            else _ = new Utf8Pcre2Regex(pattern);
        });
        Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, failure.ResourceKind);
        Assert.Equal(32768ul, failure.Limit);
        Assert.True(scope.Budget.PeakBytes <= 32768);
        Assert.Equal(0ul, scope.Budget.ReservedBytes);
        Assert.Equal(0ul, scope.Budget.Reserved);
        // Keep this byte limit and prove recovery using an owned scalar
        // program whose complete retained construction state fits that limit.
        var recovered = new Utf8Pcre2Regex(@"\b");
        Assert.True(recovered.IsMatch("a😀"u8));
        Assert.Equal(2, recovered.Count("a😀"u8));
        Assert.True(recovered.DebugCompiledProgram is not null);
        Assert.InRange(scope.Budget.ReservedBytes, 1ul, 32768ul);
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
        Assert.True(recovered.DebugCompiledProgram is not null);
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
        Assert.True(recovered.DebugCompiledProgram is not null);
    }
}
