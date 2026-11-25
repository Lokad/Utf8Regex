using System.Text;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2ConstructionAdmissionTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void InvalidArgumentsPrecedeByteDecodingAndLeaveConstructionUnspent(int kind)
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
                _ = new Utf8Pcre2Regex(pattern, options, settings, new() { MatchTimeout = timeout });
            });
            Assert.Equal(kind == 0 ? "policy" : kind == 1 ? "options" : "compileSettings", error.ParamName);
            Assert.Equal(0UL, scope.Budget.WorkSteps);
            Assert.Equal(0UL, scope.Budget.ReservedBytes);
            Assert.Equal(0UL, scope.Budget.PeakBytes);
            Assert.Equal(0UL, scope.Budget.Depth);
        }
    }

    [Fact]
    public void InvalidPolicyPrecedesOtherInvalidArguments()
    {
        using var scope = new Utf8ConstructionScope(0, 0, 0, 0);
        var error = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Utf8Pcre2Regex(new byte[] { 0xff }, options: (Pcre2CompileOptions)(1 << 29), compileSettings:                 new Utf8Pcre2CompileSettings { Bsr = (Pcre2BsrConvention)999 }, policy: new Utf8RegexPolicy
            {
                ResourcePolicy = new Utf8ResourcePolicy
                {
                    MaxResultBytes = 0
                },
                MatchTimeout = TimeSpan.Zero
            }));
        Assert.Equal("policy", error.ParamName);
        Assert.Equal(0UL, scope.Budget.WorkSteps);
        Assert.Equal(0UL, scope.Budget.ReservedBytes);
    }

    [Fact]
    public void TimeoutPrecedenceIsConsistentAcrossStringAndByteEntries()
    {
        var options = (Pcre2CompileOptions)(1 << 29);
        var settings = new Utf8Pcre2CompileSettings { Newline = (Pcre2NewlineConvention)999 };
        Assert.Equal("policy", Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Utf8Pcre2Regex("[invalid", options: options, compileSettings: settings, policy: new Utf8RegexPolicy
            {
                MatchTimeout = TimeSpan.Zero
            })).ParamName);
        Assert.Equal("policy", Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Utf8Pcre2Regex(new byte[] { 0xff }, options: options, compileSettings: settings, policy: new Utf8RegexPolicy
            {
                MatchTimeout = TimeSpan.Zero
            })).ParamName);
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
    public void CandidateBranchesReserveTheTableAndLiteralBeforeEncoding()
    {
        const int scalarCount = 513;
        var scalar = new Rune(0x1f600);
        var root = new Pcre2AlternationBacktrackingNode([LiteralBranch(scalar, scalarCount), LiteralBranch(new Rune('x'), 2)]);
        const ulong parentBytes = 48;
        var tableBytes = Utf8ConstructionBudget.ArrayBytes<byte[]>(2);
        var limit = parentBytes + tableBytes;
        using var owner = new Utf8ConstructionScope(ulong.MaxValue, limit, ulong.MaxValue, ulong.MaxValue);
        var parent = owner.Budget.AllocateArray<byte>(1, false);
        parent[0] = 17;
        var request = new Pcre2CompileRequest("", default, default, default, Timeout.InfiniteTimeSpan);

        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Pcre2CandidateSearchAnalyzer.Compile(root, request));

        Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, failure.ResourceKind);
        Assert.Equal(limit + Utf8ConstructionBudget.ArrayBytes<byte>(scalarCount * scalar.Utf8SequenceLength), failure.Requested);
        Assert.Equal(parentBytes, owner.Budget.ReservedBytes);
        Assert.Equal(limit, owner.Budget.PeakBytes);
        Assert.Equal(17, parent[0]);
    }

    [Fact]
    public void DeclinedCandidateFamilyReleasesItsScratchAndKeepsWorkSpent()
    {
        using var owner = Utf8ConstructionScope.Enter();
        var root = new Pcre2AlternationBacktrackingNode([LiteralBranch(new Rune('é'), 300), Pcre2EmptyBacktrackingNode.Instance]);
        var request = new Pcre2CompileRequest("", default, default, default, Timeout.InfiniteTimeSpan);

        Assert.False(Pcre2CandidateSearchAnalyzer.Compile(root, request).HasValue);
        Assert.Equal(0UL, owner.Budget.ReservedBytes);
        Assert.True(owner.Budget.PeakBytes >= Utf8ConstructionBudget.ArrayBytes<byte>(600));
        Assert.True(owner.Budget.WorkSteps >= 300);
    }

    [Fact]
    public void CandidateEncodingObservesTheOriginalTokenAndRefundsScratch()
    {
        var root = new Pcre2AlternationBacktrackingNode([LiteralBranch(new Rune('é'), 513), LiteralBranch(new Rune('x'), 2)]);
        var request = new Pcre2CompileRequest("", default, default, default, Timeout.InfiniteTimeSpan);
        using var source = new CancellationTokenSource();
        using var owner = Utf8ConstructionScope.Enter();
        owner.Budget.CancellationToken = source.Token;
        var reserved = Utf8ConstructionBudget.ArrayBytes<byte[]>(2) + Utf8ConstructionBudget.ArrayBytes<byte>(1026);
        var encodeCheckpoints = 0;
        owner.Budget.CheckpointHook = _ =>
        {
            if (owner.Budget.ReservedBytes == reserved && ++encodeCheckpoints == 3) source.Cancel();
        };

        var failure = Assert.Throws<OperationCanceledException>(() => Pcre2CandidateSearchAnalyzer.Compile(root, request));
        Assert.Equal(source.Token, failure.CancellationToken);
        Assert.Equal(0UL, owner.Budget.ReservedBytes);
    }

    private static Pcre2SequenceBacktrackingNode LiteralBranch(Rune scalar, int count)
    {
        var token = new Pcre2TokenBacktrackingNode(Pcre2CharacterToken.CreateLiteral(scalar, default));
        return new(Enumerable.Repeat<IPcre2BacktrackingNode>(token, count).ToArray());
    }

    [Fact]
    public void BytePatternWorkAdmissionPrecedesValidationAndAcquisition()
    {
        using var scope = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, 0, ulong.MaxValue);
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => new Utf8Pcre2Regex(new byte[] { 0xff }));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, failure.ResourceKind);
        Assert.Equal(1ul, failure.Requested);
        Assert.Equal(0ul, scope.Budget.WorkSteps);
        Assert.Equal(0ul, scope.Budget.ReservedBytes);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Utf8Pcre2Regex("cat"u8, options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
            {
                ResourcePolicy = new Utf8ResourcePolicy
                {
                    MaxResultBytes = 0
                },
                MatchTimeout = Timeout.InfiniteTimeSpan
            }));
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
    public void EveryBytePatternEntryAdmitsOwnedSourceBeforeParsing(int entry)
    {
        var encoded = System.Text.Encoding.UTF8.GetBytes(new string('(', 256));
        var sourceBytes = Utf8ConstructionBudget.ArrayBytes<byte>(encoded.Length);
        using var scope = new Utf8ConstructionScope(Utf8ConstructionScope.DefaultInstructionCapacity, sourceBytes + 47);
        var parent = scope.Budget.AllocateArray<byte>(1, false);
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
        {
            _ = entry switch
            {
                0 => new Utf8Pcre2Regex(encoded),
                1 => new Utf8Pcre2Regex(encoded, options: Pcre2CompileOptions.None),
                2 => new Utf8Pcre2Regex(encoded, options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
                {
                    MatchTimeout = Timeout.InfiniteTimeSpan
                }),
                _ => new Utf8Pcre2Regex(encoded, options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
                {
                    ResourcePolicy = Utf8ResourcePolicy.Unlimited,
                    MatchTimeout = Timeout.InfiniteTimeSpan
                }),
            };
        });
        Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, failure.ResourceKind);
        Assert.Equal(sourceBytes + 48, failure.Requested);
        Assert.Equal(48ul, scope.Budget.ReservedBytes);
        Assert.Equal(48ul, scope.Budget.PeakBytes);
        Assert.Equal(0ul, scope.Budget.Peak);
        scope.Budget.ReleaseArray(parent, false);
    }

    [Theory]
    [InlineData("")] [InlineData("cat")] [InlineData("é")]
    [InlineData("😀")] [InlineData("a😀é中")]
    public void RetainedNativePatternSharesTheCompilationLoanWithoutChargingBorrowedText(string pattern)
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
        using var native = new Utf8ConstructionScope(Utf8ConstructionScope.DefaultInstructionCapacity);
        var owned = new Utf8Pcre2Regex(encoded);
        Assert.Equal(pattern, owned.Pattern);
        Assert.True(owned.IsMatch(encoded));
        Assert.Equal(held + Utf8ConstructionBudget.ArrayBytes<byte>(encoded.Length), native.Budget.ReservedBytes);
        Assert.Equal(peak + Utf8ConstructionBudget.ArrayBytes<byte>(encoded.Length), native.Budget.PeakBytes);
        Assert.Equal(slots, native.Budget.Reserved);
        if (encoded.Length > 0) encoded[0] = (byte)'z';
        Assert.Equal(pattern, owned.Pattern);
    }

    [Fact]
    public void PolicyAndUtf8ErrorsPrecedeSourceAdmissionAndFailedParsingRestoresParent()
    {
        // The boundary recovery includes admitted scalar/prepared/cursor plans;
        // the byte-pattern probe peaks at 22,672 bytes, plus this parent loan.
        using var scope = new Utf8ConstructionScope(Utf8ConstructionScope.DefaultInstructionCapacity, 32768);
        var parent = scope.Budget.AllocateArray<byte>(1, false);
        var invalid = new byte[] { 0xed, 0xa0, 0x80 };
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Utf8Pcre2Regex(invalid, options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
            {
                ResourcePolicy = new Utf8ResourcePolicy
                {
                    MaxResultBytes = 0
                },
                MatchTimeout = Timeout.InfiniteTimeSpan
            }));
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
