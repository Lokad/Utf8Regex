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
        var regex = new Utf8Pcre2Regex(Encoding.UTF8.GetBytes(pattern), options: Pcre2CompileOptions.Ucp);
        ReadOnlyMemory<byte> match1Input = ("é😀😀"u8).ToArray();
        using var match1Cursor = regex.CreateMatchCursor(match1Input);
        match1Cursor.MoveNext(out var match1Result);
        match1Cursor.Dispose();
        var match = match1Result;
        Assert.True(match.Success);
        Assert.Equal((2, 10), (match.Value.StartOffsetInBytes, match.Value.EndOffsetInBytes));
        Assert.Equal(2, match.Groups.Length);
        Span<int> numbersByName = stackalloc int[1];
        Assert.Equal(1, regex.CopyNumbersForUtf8Name(Encoding.UTF8.GetBytes(name), numbersByName, out var moreByName));
        Assert.False(moreByName);
        Assert.Equal(1, numbersByName[0]);
        Assert.True(match.TryGetGroup(numbersByName[0], out var group)); Assert.True(group.Success);
        Assert.Equal("😀", System.Text.Encoding.UTF8.GetString((group.Success ? match1Input.Span.Slice(group.StartOffsetInBytes, group.EndOffsetInBytes - group.StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
        Assert.Equal((2, 6), (group.StartOffsetInBytes, group.EndOffsetInBytes));
        var nameBytes = new byte[128];
        Assert.Equal(System.Buffers.OperationStatus.Done,
            regex.TryCopyNameEntryUtf8(0, nameBytes, out var bytesRequired, out var groupNumber));
        Assert.Equal(Encoding.UTF8.GetBytes(name), nameBytes);
        Assert.Equal(128, bytesRequired);
        Assert.Equal(1, groupNumber);
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
            AssertCaptureFailure(() => new Utf8Pcre2Regex(pattern, options: options));
            AssertCaptureFailure(() => new Utf8Pcre2Regex(Encoding.UTF8.GetBytes(pattern), options: options));
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
        var match = regex.Match("a"u8);
        Assert.True(match.Success);
        Assert.Equal(label, match.Mark);
        Assert.Equal(0, regex.CaptureGroupCount);
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
        ReadOnlySpan<byte> result2Input = Encoding.UTF8.GetBytes(literal);
        var result2Captures = new Utf8Pcre2CaptureRange[quoted.CaptureGroupCount + 1];
        var result2Result = quoted.Match(result2Input, captures: result2Captures);
        Assert.Equal(1, (result2Result.Success ? result2Captures.Length : 0));
        Assert.True(new Utf8Pcre2Regex(@"[\Q" + literal + @"\E]").IsMatch("é"u8));
        Assert.True(new Utf8Pcre2Regex("# " + literal + "\na", options: Pcre2CompileOptions.Extended).IsMatch("a"u8));
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
