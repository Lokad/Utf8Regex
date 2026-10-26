using System.Text;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2ReplacementStackSpanTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CallbackCopiesLocalByteAndCharFormattingSpans(bool finite)
    {
        var policy = finite ? new Utf8ResourcePolicy
        {
            MaxWorkSteps = 1_000_000, MaxDepth = 1024,
            MaxWorkingBytes = 1_048_576, MaxResultBytes = 1_048_576,
        } : Utf8ResourcePolicy.Unlimited;
        var regex = Utf8Pcre2Regex.Create("a", Pcre2CompileOptions.None, default, policy, Timeout.InfiniteTimeSpan);
        var output = regex.Replace("éa😀a"u8, 0,
            static (in Utf8Pcre2MatchContext match, ref Utf8ReplacementWriter writer, ref int state) =>
            {
                Span<char> text = stackalloc char[16];
                Assert.True((++state).TryFormat(text[1..], out var charsWritten));
                writer.Append(text.Slice(1, charsWritten));
                Span<byte> bytes = stackalloc byte[8];
                var bytesWritten = new Rune(0x1F600).EncodeToUtf8(bytes[2..]);
                writer.Append(bytes.Slice(2, bytesWritten));
                text.Fill('\uD800');
                bytes.Fill(0xFF);
            });
        Assert.Equal("é1😀😀2😀", Encoding.UTF8.GetString(output));
    }
}
