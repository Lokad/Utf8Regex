using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2ConstructionAdmissionTests
{
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
