using System.Text;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReLiteralConstructionTests
{
    private static Utf8PythonRegex Compile(string pattern, PythonReCompileOptions options = PythonReCompileOptions.None)
        => Utf8PythonRegex.Compile(pattern, options: options, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = Utf8ResourcePolicy.Unlimited,
            ConstructionPolicy = Utf8ConstructionPolicy.Default,
            ConstructionTimeout = Timeout.InfiniteTimeSpan
        }, cancellationToken: CancellationToken.None);

    [Fact]
    public void LargeLiteralDoesNotAcquirePerAtomParserScaffolding()
    {
        var pattern = new string('a', 8192);
        _ = Compile(pattern);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var expression = Compile(pattern);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(allocated < 16 * 1024 * 1024, $"Literal construction allocated {allocated} bytes.");
        Assert.Equal(pattern, expression.Pattern);
        Assert.True(expression.Match(Encoding.UTF8.GetBytes(pattern), mode: Utf8PythonMatchMode.FullMatch).Success);
        GC.KeepAlive(expression);
    }

    [Theory]
    [InlineData("aé中\0", "aé中\0")]
    [InlineData("\ud7ff\ue000\uffff", "\ud7ff\ue000\uffff")]
    [InlineData(@"a\.\[\]\{\}\\\^\$\|\?\*\+\(\)", "a.[]{}\\^$|?*+()")]
    [InlineData("a\n中", "a\n中")]
    [InlineData(@"(?x:a \# 中)", "a#中")]
    [InlineData(@"(?i:a)(?-i:é中)", "Aé中")]
    [InlineData(@"(?i:123)", "123")]
    [InlineData(@"(?a:é中)", "é中")]
    [InlineData("😀é中", "😀é中")]
    public void SemanticLiteralsKeepScalarAndScopedOptions(string pattern, string text)
    {
        var expression = Compile(pattern);
        var input = Encoding.UTF8.GetBytes(text);
        Assert.True(expression.Match(input, mode: Utf8PythonMatchMode.FullMatch).Success);
        Assert.Equal(1, expression.Count(input));
        Assert.False(expression.Match(Encoding.UTF8.GetBytes(text + "!"), mode: Utf8PythonMatchMode.FullMatch).Success);
    }

    [Fact]
    public void LiteralLoweringKeepsCaptureAndReferenceIdentity()
    {
        var expression = Compile(@"(é)(中)\1");
        var input = "é中é"u8.ToArray();
        Utf8PythonRangeMatch match = default;
        using (var captureCursor1 = expression.CreateMatchCursor(input, 0, mode: Utf8PythonMatchMode.Search))
        {
            captureCursor1.MoveNext(out match);
        }
        Assert.True(match.Success);
        Assert.Equal("é", System.Text.Encoding.UTF8.GetString((match.Groups[1].Success ? match.GetGroupMemory(match.Groups[1].Number).Span : ReadOnlySpan<byte>.Empty)));
        Assert.Equal("中", System.Text.Encoding.UTF8.GetString((match.Groups[2].Success ? match.GetGroupMemory(match.Groups[2].Number).Span : ReadOnlySpan<byte>.Empty)));
        Assert.True(expression.Match(input, mode: Utf8PythonMatchMode.FullMatch).Success);
    }

    [Theory]
    [InlineData(0xd800, false)]
    [InlineData(0xdfff, false)]
    [InlineData(0xd800, true)]
    [InlineData(0xdfff, true)]
    public void IsolatedSurrogateAtomsRemainImpossible(int scalar, bool escaped)
    {
        var pattern = escaped ? $@"\u{scalar:x4}" : new string((char)scalar, 1);
        var expression = Compile(pattern);
        Assert.False(expression.Match([], mode: Utf8PythonMatchMode.FullMatch).Success);
        Assert.Equal(0, expression.Count("😀�a"u8));
    }
}
