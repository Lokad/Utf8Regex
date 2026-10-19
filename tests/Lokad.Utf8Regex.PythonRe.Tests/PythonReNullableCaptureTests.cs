using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReNullableCaptureTests
{
    public static IEnumerable<object?[]> Cases()
    {
        foreach (var finite in new[] { false, true })
        {
            yield return [@"(?P<x>\w*)", "😀𐐀é!", finite,
                new[] { (0, 0), (4, 10), (10, 10), (11, 11) },
                new string?[] { "", "𐐀é", "", "" }, false];
            yield return [@"(?P<x>foo)?(?(x)bar|)", "😀foobar!", finite,
                new[] { (0, 0), (4, 10), (10, 10), (11, 11) },
                new string?[] { null, "foo", null, null }, false];
            yield return [@"(?P<x>\w*?)", "𐐀a", finite,
                new[] { (0, 0), (0, 4), (4, 4), (4, 5), (5, 5) },
                new string?[] { "", "𐐀", "", "a", "" }, true];
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void NullableCapturesKeepScalarRetryOrderAndRetainedSnapshots(string pattern, string subject,
        bool finite, (int Start, int End)[] expectedRanges, string?[] expectedCaptures, bool fullMatch)
    {
        var policy = finite ? new Utf8ResourcePolicy
        {
            MaxWorkSteps = 1_000_000, MaxDepth = 1024,
            MaxWorkingBytes = 1_048_576, MaxResultBytes = 4_194_304,
        } : Utf8ResourcePolicy.Unlimited;
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None, Regex.InfiniteMatchTimeout, policy);
        var input = Encoding.UTF8.GetBytes(subject);
        using var source = new CancellationTokenSource();
        foreach (var token in new[] { CancellationToken.None, source.Token })
        {
            Assert.True(regex.IsMatch(input, 0, token));
            Assert.True(regex.Match(input, 0, token).Success);
            Assert.True(regex.SearchDetailedData(input, 0, token).Success);
            Assert.Equal(fullMatch, regex.FullMatch(input, 0, token).Success);
            Assert.Equal(expectedRanges.Length, regex.Count(input, 0, token));
            Assert.Equal(expectedCaptures.Select(value => value ?? ""), regex.FindAllToStrings(input, 0, token).ScalarValues);
            var detailed = regex.FindIterDetailed(input, 0, token);
            Assert.Equal(expectedRanges, detailed.Select(value => (value.Value.StartOffsetInBytes, value.Value.EndOffsetInBytes)));
            using var cursor = regex.CreateMatchCursor(input, 0, token);
            var alias = cursor;
            var retained = new List<Utf8PythonDetailedMatchData>();
            for (var index = 0; index < expectedRanges.Length; index++)
            {
                Assert.True((index % 2 == 0 ? cursor : alias).MoveNext());
                var snapshot = alias.Current;
                retained.Add(snapshot);
                Assert.Equal(detailed[index].Groups, snapshot.Groups);
                Assert.True(snapshot.TryGetFirstSetGroup("x", out var capture));
                Assert.Equal(expectedCaptures[index] is not null, capture.Success);
                if (expectedCaptures[index] is { } text) Assert.Equal(text, capture.ValueText);
                Assert.Equal(Encoding.UTF8.GetCharCount(input.AsSpan(0, expectedRanges[index].Start)), snapshot.Value.StartOffsetInUtf16);
                Assert.Equal(Encoding.UTF8.GetCharCount(input.AsSpan(0, expectedRanges[index].End)), snapshot.Value.EndOffsetInUtf16);
            }
            Assert.False(alias.MoveNext());
            alias.Dispose();
            Assert.Throws<ObjectDisposedException>(() => cursor.MoveNext());
            for (var index = 0; index < retained.Count; index++)
            {
                Assert.True(retained[index].TryGetFirstSetGroup("x", out var capture));
                Assert.Equal(expectedCaptures[index] is not null, capture.Success);
                if (expectedCaptures[index] is { } text) Assert.Equal(text, capture.ValueText);
                Assert.Equal(detailed[index].Groups, retained[index].Groups);
            }
        }
        var invalid = input.Concat(new byte[] { 0xff }).ToArray();
        Assert.Throws<ArgumentException>(() => regex.Count(invalid));
        source.Cancel();
        var error = Assert.Throws<OperationCanceledException>(() => regex.Count(invalid, 0, source.Token));
        Assert.Equal(source.Token, error.CancellationToken);
    }
}
