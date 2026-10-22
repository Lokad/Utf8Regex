namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ReplacementWriterTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public void AppendingOutsideCallbackThrowsIncludingEmptyFragments(int overload)
    {
        var error = Assert.Throws<InvalidOperationException>(() => AppendOutsideCallback(overload));
        Assert.Contains("callback", error.Message, StringComparison.Ordinal);
    }

    private static void AppendOutsideCallback(int overload)
    {
        var writer = new Utf8ReplacementWriter();
        switch (overload)
        {
            case 0: writer.Append("a"u8); break;
            case 1: writer.Append(ReadOnlySpan<byte>.Empty); break;
            case 2: writer.Append("a".AsSpan()); break;
            case 3: writer.Append(ReadOnlySpan<char>.Empty); break;
            case 4: writer.AppendAsciiByte((byte)'a'); break;
            case 5: writer.Append(new System.Text.Rune(0x1F600)); break;
        }
    }

    [Fact]
    public void AppendAsciiByteRejectsNonAsciiInput()
    {
        var writer = new Utf8ReplacementWriter();

        try
        {
            writer.AppendAsciiByte(0x80);
            throw new Xunit.Sdk.XunitException("Expected AppendAsciiByte to reject non-ASCII input.");
        }
        catch (ArgumentOutOfRangeException)
        {
        }
    }
}
