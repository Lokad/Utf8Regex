using System.Text;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2Utf8NameAdmissionTests
{
    public static IEnumerable<object[]> CaptureNames()
    {
        foreach (var name in new[] { new string('a', 128), new string('é', 64),
            string.Concat(Enumerable.Repeat("𐐀", 32)) })
        foreach (var form in new[] { "(?<{0}>😀)", "(?'{0}'😀)", "(?P<{0}>😀)" })
            yield return [name, string.Format(form, name) + @"\k<" + name + ">"];
    }

    [Theory]
    [MemberData(nameof(CaptureNames))]
    public void NamesAt128Utf8BytesKeepCapturesNamesAndReferences(string name, string pattern)
    {
        Assert.Equal(128, Encoding.UTF8.GetByteCount(name));
        var regex = new Utf8Pcre2Regex(Encoding.UTF8.GetBytes(pattern), Pcre2CompileOptions.Ucp);
        var match = regex.MatchDetailed("é😀😀"u8);
        Assert.True(match.Success);
        Assert.Equal((2, 10), (match.Value.StartOffsetInBytes, match.Value.EndOffsetInBytes));
        Assert.Equal(2, match.CaptureSlotCount);
        Assert.True(match.TryGetFirstSetGroup(name, out var group));
        Assert.Equal("😀", group.GetValueString());
        Assert.Equal((2, 6), (group.StartOffsetInBytes, group.EndOffsetInBytes));
        var names = new Pcre2NameEntry[1];
        Assert.Equal(1, regex.CopyNameEntries(names, out var more));
        Assert.False(more);
        Assert.Equal(name, names[0].Name);
        Assert.Equal(1, names[0].Number);
    }

    public static IEnumerable<object[]> OverlongCaptures()
    {
        foreach (var name in new[] { new string('a', 129), new string('é', 65),
            string.Concat(Enumerable.Repeat("𐐀", 33)) })
        foreach (var form in new[] { "(?<{0}>a)", "(?'{0}'a)", "(?P<{0}>a)" })
            yield return [string.Format(form, name)];
    }

    [Theory]
    [MemberData(nameof(OverlongCaptures))]
    public void OverlongCaptureDefinitionsFailBeforeBackendSelection(string pattern)
    {
        foreach (var options in new[] { Pcre2CompileOptions.None, Pcre2CompileOptions.Ucp })
        {
            AssertCaptureFailure(() => new Utf8Pcre2Regex(pattern, options));
            AssertCaptureFailure(() => new Utf8Pcre2Regex(Encoding.UTF8.GetBytes(pattern), options));
        }
    }

    public static IEnumerable<object[]> Labels()
    {
        yield return [new string('x', 255)];
        yield return [new string('é', 127) + "x"];
        yield return [string.Concat(Enumerable.Repeat("😀", 63)) + "abc"];
    }

    [Theory]
    [MemberData(nameof(Labels))]
    public void LabelAt255Utf8BytesKeepsItsOriginalMark(string label)
    {
        Assert.Equal(255, Encoding.UTF8.GetByteCount(label));
        var regex = new Utf8Pcre2Regex("(*MARK:" + label + ")a");
        var match = regex.MatchDetailed("a"u8);
        Assert.True(match.Success);
        Assert.Equal(label, match.Mark);
        Assert.Equal(1, match.CaptureSlotCount);
    }

    public static IEnumerable<object[]> OverlongLabels()
    {
        foreach (var label in new[] { new string('x', 256), new string('é', 128),
            string.Concat(Enumerable.Repeat("😀", 64)) })
        foreach (var verb in new[] { "", "MARK", "PRUNE", "THEN", "COMMIT", "SKIP", "ACCEPT", "FAIL", "F" })
            yield return ["(*" + verb + ":" + label + ")a"];
    }

    [Theory]
    [MemberData(nameof(OverlongLabels))]
    public void OverlongLiteralLabelsUseStructuredCompileFailure(string pattern)
    {
        var error = Assert.Throws<Pcre2CompileException>(() => new Utf8Pcre2Regex(pattern));
        Assert.Equal(Pcre2ErrorKind.InvalidAfterParensQuery, error.ErrorKind);
        Assert.Contains("255 UTF-8 bytes", error.Message);
        var bytesError = Assert.Throws<Pcre2CompileException>(() => new Utf8Pcre2Regex(Encoding.UTF8.GetBytes(pattern)));
        Assert.Equal(error.ErrorKind, bytesError.ErrorKind);
    }

    [Fact]
    public void LongLiteralClassCommentAndLookbehindBodiesAreNotNames()
    {
        var name = new string('é', 65);
        var label = new string('é', 128);
        var literal = "(?<" + name + ">a)(*MARK:" + label + ")";
        var quoted = new Utf8Pcre2Regex(@"\Q" + literal + @"\E");
        Assert.True(quoted.IsMatch(Encoding.UTF8.GetBytes(literal)));
        Assert.Equal(1, quoted.MatchDetailed(Encoding.UTF8.GetBytes(literal)).CaptureSlotCount);
        Assert.True(new Utf8Pcre2Regex(@"[\Q" + literal + @"\E]").IsMatch("é"u8));
        Assert.True(new Utf8Pcre2Regex("# " + literal + "\na", Pcre2CompileOptions.Extended).IsMatch("a"u8));
        Assert.True(new Utf8Pcre2Regex("(?#(?<" + name + ">a)a").IsMatch("a"u8));
        var prefix = new string('a', 130) + ">";
        Assert.True(new Utf8Pcre2Regex("(?<=" + prefix + ")b").IsMatch(Encoding.UTF8.GetBytes(prefix + "b")));
        Assert.True(new Utf8Pcre2Regex("(?<!" + prefix + ")b").IsMatch("b"u8));
    }

    private static void AssertCaptureFailure(Action construct)
    {
        var error = Assert.Throws<Pcre2CompileException>(construct);
        Assert.Equal(Pcre2ErrorKind.InvalidAfterParensQuery, error.ErrorKind);
        Assert.Contains("128 UTF-8 bytes", error.Message);
    }
}
