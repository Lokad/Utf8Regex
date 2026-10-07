using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReConstructionAdmissionTests
{
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
