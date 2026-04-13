namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ReplacementWriterTests
{
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
