using System.Text;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2DocumentationExampleTests
{
    [Fact]
    public void ReadmeExampleUsesThePackedValueMatchContract()
    {
        var regex = new Utf8Pcre2Regex(
            @"(?<scheme>https?)://(?<host>[A-Za-z0-9.-]+)",
            Pcre2CompileOptions.None);

        ReadOnlySpan<byte> input = Encoding.UTF8.GetBytes(
            "Visit https://example.com and http://localhost.");

        var values = new List<string>();
        var matches = regex.EnumerateMatches(input);
        while (matches.MoveNext())
        {
            var match = matches.Current;
            if (match.Success && match.IsUtf8SliceWellFormed)
            {
                values.Add(
                    Encoding.UTF8.GetString(input.Slice(match.StartOffsetInBytes, match.EndOffsetInBytes - match.StartOffsetInBytes)));
            }
        }

        Assert.Equal(["https://example.com", "http://localhost."], values);

        byte[] redacted = regex.Replace(input, "<$host>");
        Assert.Equal("Visit <example.com> and <localhost.>", Encoding.UTF8.GetString(redacted));
    }
}