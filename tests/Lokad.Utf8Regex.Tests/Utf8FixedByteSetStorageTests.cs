using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Runtime.CompilerServices;
using Lokad.Utf8Regex.Internal.Search;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8FixedByteSetStorageTests
{
    [Fact]
    public void FixedValueKernelRetainsNoManagedReferences()
    {
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<PreparedByteSetState>());
    }

    [Theory]
    [InlineData(typeof(Utf8UnicodeCategoryExecutor))]
    public void FixedExecutorTablesHaveNoRetainedHeapStorage(Type executor)
    {
        var fields = executor.GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.All(fields, field => Assert.True(field.FieldType.IsValueType,
            $"Fixed executor table {executor.Name}.{field.Name} retains heap storage."));
    }

    [Fact]
    public void ShippedCoreHasNoBclByteSetTypeReferences()
    {
        using var stream = File.OpenRead(typeof(Utf8Regex).Assembly.Location);
        using var pe = new PEReader(stream);
        var metadata = pe.GetMetadataReader();
        foreach (var handle in metadata.TypeReferences)
        {
            var type = metadata.GetTypeReference(handle);
            Assert.False(metadata.GetString(type.Namespace) == "System.Buffers" &&
                metadata.GetString(type.Name).StartsWith("SearchValues", StringComparison.Ordinal));
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(31)]
    public void FixedIdentifierAndOperatorRunsKeepEveryByteAcrossVectorBoundaries(int offset)
    {
        var identifier = new Utf8Regex(@"\G[A-Za-z][A-Za-z0-9_]*");
        var operators = new Utf8Regex(@"\G[=+\-^*/.<>~!&|?]+");
        var token = new Utf8Regex("[A-Za-z][A-Za-z0-9]*");
        foreach (var length in new[] { 0, 1, 15, 16, 17, 31, 32, 33, 63, 64, 65, 129, 257 })
        for (var value = 0; value < 128; value++)
        {
            var storage = new byte[offset + length + 2]; var input = storage.AsSpan(offset);
            var letter = value is >= 'A' and <= 'Z' or >= 'a' and <= 'z'; var digit = value is >= '0' and <= '9';
            input.Fill((byte)'a'); input[^1] = (byte)value;
            Assert.Equal(length + 1 + (letter || digit || value == '_' ? 1 : 0), identifier.Match(input).EndOffsetInBytes);
            input.Fill((byte)'+'); input[^1] = (byte)value;
            Assert.Equal(length + 1 + ("=+-^*/.<>~!&|?"u8.Contains((byte)value) ? 1 : 0), operators.Match(input).EndOffsetInBytes);
            input.Fill((byte)'a'); input[0] = (byte)'!'; input[^1] = (byte)value;
            var match = token.Match(input);
            Assert.Equal(length > 0 || letter, match.Success);
            if (match.Success) Assert.Equal((1, 1 + length + (letter || digit ? 1 : 0)), (match.StartOffsetInBytes, match.EndOffsetInBytes));
        }
    }

    [Theory]
    [InlineData("user.name-42_@sub-domain.example", true)]
    [InlineData("😀@sub-domain.example", false)]
    [InlineData("name@例子.com", false)]
    [InlineData("a@[001.002.003.004]", true)]
    [InlineData("x@a.co\n", true)]
    public void FixedEmailMembershipKeepsWholeMatchExtents(string text, bool expected)
    {
        const string pattern = @"^[a-zA-Z0-9_\-\.]+@(\[[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.|([a-zA-Z0-9\-]+\.)+)([a-zA-Z]{2,12}|[0-9]{1,3})\]?$";
        var input = Encoding.UTF8.GetBytes(text); var match = new Utf8Regex(pattern).Match(input);
        Assert.Equal(expected, match.Success);
        if (match.Success) Assert.Equal(input.Length - (text.EndsWith('\n') ? 1 : 0), match.EndOffsetInBytes);
    }}
