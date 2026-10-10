using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Globalization;
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
    [InlineData(typeof(Utf8AsciiEmailWholeExecutor))]
    [InlineData(typeof(Utf8AsciiPrefixTokenExecutor))]
    [InlineData(typeof(Utf8AsciiTokenFinderExecutor))]
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
        foreach (var length in new[] { 0, 1, 15, 16, 17, 31, 32, 33, 63, 64, 65, 129, 257 })
            for (var value = 0; value < 256; value++)
            {
                var storage = new byte[offset + length + 2];
                var input = storage.AsSpan(offset);
                input.Fill((byte)'a'); input[^1] = (byte)value;
                var letter = value is >= 'A' and <= 'Z' or >= 'a' and <= 'z';
                var digit = value is >= '0' and <= '9';
                Assert.True(Utf8AsciiPrefixTokenExecutor.TryMatchIdentifierPrefixIgnoreCase(input, out var width));
                Assert.Equal(length + 1 + (letter || digit || value == '_' ? 1 : 0), width);

                input.Fill((byte)'+'); input[^1] = (byte)value;
                Assert.True(Utf8AsciiPrefixTokenExecutor.TryMatchOperatorRunPrefix(input, out width));
                Assert.Equal(length + 1 + ("=+-^*/.<>~!&|?"u8.Contains((byte)value) ? 1 : 0), width);

                input.Fill((byte)'a'); input[0] = (byte)'!'; input[^1] = (byte)value;
                var found = Utf8AsciiTokenFinderExecutor.TryFindAsciiIdentifierToken(input, 0, out var index, out width);
                Assert.Equal(length > 0 || letter, found);
                Assert.Equal(found ? 1 : -1, index);
                Assert.Equal(found ? length + (letter || digit ? 1 : 0) : 0, width);
            }
    }

    [Fact]
    public void FrozenAsciiCategoriesAndMixedScalarCountsAgreeWithScalarClassification()
    {
        foreach (var category in Enum.GetValues<UnicodeCategory>())
        {
            for (var value = 0; value < 128; value++)
            {
                Span<byte> input = stackalloc byte[1]; input[0] = (byte)value;
                Assert.Equal(Rune.GetUnicodeCategory(new Rune(value)) == category ? 1 : 0,
                    Utf8UnicodeCategoryExecutor.CountCategory(input, false, category));
            }
            const string text = "+×<😀𝛼|~ä>=é";
            var expected = text.EnumerateRunes().Count(rune => Rune.GetUnicodeCategory(rune) == category);
            Assert.Equal(expected, Utf8UnicodeCategoryExecutor.CountCategory(Encoding.UTF8.GetBytes(text), true, category));
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
        var input = Encoding.UTF8.GetBytes(text);
        Assert.Equal(expected, Utf8AsciiEmailWholeExecutor.TryMatchWhole(input, out var width));
        Assert.Equal(expected ? input.Length - (text.EndsWith('\n') ? 1 : 0) : 0, width);
    }
}
