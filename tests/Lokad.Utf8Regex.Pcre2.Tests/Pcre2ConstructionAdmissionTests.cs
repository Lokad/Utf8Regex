using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2ConstructionAdmissionTests
{
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
        Assert.True(new Utf8Pcre2Regex("cat").IsMatch("cat"u8));
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
