using System.Text;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReClassUnionTests
{
    [Theory]
    [InlineData(@"[\w.+-]", "é²𐐀a._+-\u0301😀\u1C89\U0001E5F1", "é²𐐀a._+-")]
    [InlineData(@"[^\w.+-]", "é²𐐀a._+-\u0301😀\u1C89\U0001E5F1", "\u0301😀\u1C89\U0001E5F1")]
    [InlineData(@"[\d_-]", "١٢_𝟙-²a", "١٢_𝟙-")]
    [InlineData(@"[\w\s]", "é\u001c \u00a0_\u0301😀", "é\u001c \u00a0_")]
    [InlineData(@"[^\w\s]", "é\u001c \u00a0_\u0301😀", "\u0301😀")]
    [InlineData(@"[\W.-]", "é\u0001😀.+é", "\u0001😀.+")]
    [InlineData(@"[^\W.-]", "é\u0001😀.+é", "éé")]
    public void MembershipAndScalarProgressionFollowPinnedCpython(string pattern, string text, string matched)
    {
        var regex = new Utf8PythonRegex(pattern); var wanted = matched.EnumerateRunes().Select(r => r.Value).ToArray();
        var actual = new List<int>();
        foreach (var rune in text.EnumerateRunes())
        {
            var bytes = Encoding.UTF8.GetBytes(rune.ToString());
            var hit = regex.FullMatch(bytes).Success;
            Assert.Equal(hit ? 1 : 0, regex.Count(bytes));
            if (hit) actual.Add(rune.Value);
        }
        Assert.Equal(wanted, actual);
        var input = Encoding.UTF8.GetBytes(text); byte[] storage = [0xff, .. input, 0xff];
        Assert.Equal(wanted.Length, regex.Count(storage.AsSpan(1, input.Length)));
        var negative = new Utf8PythonRegex("(?!" + pattern + ")[\\s\\S]");
        Assert.Equal(text.EnumerateRunes().Count() - wanted.Length, negative.Count(input));
    }

    [Theory]
    [InlineData(@"(?i:[\wA])", "\u0345", false)]
    [InlineData(@"(?i:[\sA])", "\u0345", false)]
    [InlineData(@"(?i:[\sA])", "a", true)]
    [InlineData(@"(?i:[\W_a])", "\u0345", true)]
    [InlineData(@"(?ai:[\wA])", "é", false)]
    [InlineData(@"[\wé]", "é", true)]
    [InlineData(@"[\w😀]", "😀", true)]
    [InlineData(@"[^\wé]", "😀", true)]
    public void FoldingAndComplementOperandsRetainLexicalSemantics(string pattern, string text, bool expected)
    {
        var regex = new Utf8PythonRegex(pattern);
        Assert.Equal(expected, regex.FullMatch(Encoding.UTF8.GetBytes(text)).Success);
    }

    [Fact]
    public void AClassUnionDoesNotNeedPerScalarChoiceFrames()
    {
        var regex = new Utf8PythonRegex(@"[\w.+-]+@", PythonReCompileOptions.None, Timeout.InfiniteTimeSpan,
            new Utf8ResourcePolicy { MaxDepth = 1, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20, MaxWorkSteps = 100000 });
        Assert.Equal(2, regex.Count("é²𐐀._+-@ word@"u8));
        Assert.Equal(0, regex.Count("é²𐐀._+- no-at"u8));
        var denied = new Utf8PythonRegex(@"[\w.+-]+@", PythonReCompileOptions.None, Timeout.InfiniteTimeSpan,
            new Utf8ResourcePolicy { MaxWorkSteps = 1 });
        Assert.Throws<Utf8ResourceLimitExceededException>(() => denied.Count("abc@"u8));
        Assert.Throws<ArgumentException>(() => regex.Count(new byte[] { (byte)'a', (byte)'@', 0xff }));
        using var token = new CancellationTokenSource(); token.Cancel();
        Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => regex.Count("abc@"u8, 0, token.Token)).CancellationToken);
    }

    [Fact]
    public void CapturesAliasesAndEmptyProgressionKeepTheirOriginalOwnership()
    {
        var regex = new Utf8PythonRegex(@"([\w.]+)@");
        using var cursor = regex.CreateMatchCursor("😀é.a@ word.@"u8.ToArray());
        var alias = cursor;
        Assert.True(cursor.MoveNext()); var first = alias.Current;
        Assert.Equal("é.a", first.Groups[1].ValueText);
        Assert.True(alias.MoveNext()); Assert.Equal("word.@", cursor.Current.Value.ValueText);
        alias.Dispose(); Assert.Throws<ObjectDisposedException>(() => cursor.MoveNext());
        Assert.Equal("é.a", first.Groups[1].ValueText);
        Assert.Equal(3, new Utf8PythonRegex(@"[\w.]*").Count("😀a."u8));
    }
}
