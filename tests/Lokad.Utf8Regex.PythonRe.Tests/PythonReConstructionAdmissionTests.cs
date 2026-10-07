using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReConstructionAdmissionTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void WideScalarIrCannotBypassTheEnclosingByteOwner(bool bytes)
    {
        var pattern = new string('a', 32768);
        var encoded = System.Text.Encoding.UTF8.GetBytes(pattern);
        using var scope = new Utf8ConstructionScope(Utf8ConstructionScope.DefaultInstructionCapacity, 4096);
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
        {
            if (bytes) _ = new Utf8PythonRegex(encoded);
            else _ = new Utf8PythonRegex(pattern);
        });
        Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, failure.ResourceKind);
        Assert.Equal(4096ul, failure.Limit);
        Assert.True(scope.Budget.PeakBytes <= 4096);
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
