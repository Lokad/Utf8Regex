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
        Utf8PythonRangeMatch cursorRange = default;
        var policy = finite ? new Utf8ResourcePolicy
        {
            MaxWorkSteps = 1_000_000,
            MaxDepth = 1024,
            MaxWorkingBytes = 1_048_576,
            MaxResultBytes = 4_194_304
        } : Utf8ResourcePolicy.Unlimited;
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None, Regex.InfiniteMatchTimeout, policy);
        var input = Encoding.UTF8.GetBytes(subject);
        using var source = new CancellationTokenSource();
        foreach (var token in new[] { CancellationToken.None, source.Token })
        {
            Assert.True(regex.IsMatch(input, 0, token));
            Assert.True(regex.Match(input,0,cancellationToken: token).Success);
            Utf8PythonRangeMatch snapshot1 = default;
            using (var captureCursor1 = regex.CreateMatchCursor(input, 0, token, mode: Utf8PythonMatchMode.Search))
            {
                captureCursor1.MoveNext(out snapshot1);
            }
            Assert.True(snapshot1.Success);
            Assert.Equal(fullMatch, regex.Match(input,0,cancellationToken: token,mode: Utf8PythonMatchMode.FullMatch).Success);
            Assert.Equal(expectedRanges.Length, regex.Count(input, 0, token));
            Assert.Equal(expectedCaptures.Select(value => value ?? ""), PythonReFindAllAssert.Decode(regex.FindAllToUtf8(input, 0, token)).ScalarValues);
            var collectedRanges1 = new List<Utf8PythonRangeMatch>();
            using (var rangeCursor1 = regex.CreateMatchCursor(input, 0, token))
            {
                while (rangeCursor1.MoveNext(out var rangeMatch1))
                    collectedRanges1.Add(rangeMatch1);
            }
            var detailed = collectedRanges1.ToArray();
            Assert.Equal(expectedRanges, detailed.Select(value => (value.Value.StartOffsetInBytes, value.Value.EndOffsetInBytes)));
            using var cursor = regex.CreateMatchCursor(input, 0, token);
            var alias = cursor;
            var retained = new List<Utf8PythonRangeMatch>();
            for (var index = 0; index < expectedRanges.Length; index++)
            {
                Assert.True((index % 2 == 0 ? cursor : alias).MoveNext(out cursorRange));
                var snapshot = cursorRange;
                retained.Add(snapshot);
                PythonReCaptureAssert.Equal(detailed[index], snapshot);
                Assert.True(snapshot.TryGetFirstSetGroup("x", out var capture));
                Assert.Equal(expectedCaptures[index] is not null, capture.Success);
                if (expectedCaptures[index] is { } text) Assert.Equal(text, System.Text.Encoding.UTF8.GetString((capture.Success ? snapshot.GetGroupMemory(capture.Number) : default).Span));
            }
            Assert.False(alias.MoveNext(out cursorRange));
            alias.Dispose();
            Assert.Throws<ObjectDisposedException>(() => cursor.MoveNext(out _));
            for (var index = 0; index < retained.Count; index++)
            {
                Assert.True(retained[index].TryGetFirstSetGroup("x", out var capture));
                Assert.Equal(expectedCaptures[index] is not null, capture.Success);
                if (expectedCaptures[index] is { } text) Assert.Equal(text, System.Text.Encoding.UTF8.GetString((capture.Success ? retained[index].GetGroupMemory(capture.Number) : default).Span));
                PythonReCaptureAssert.Equal(detailed[index], retained[index]);
            }
        }
        var invalid = input.Concat(new byte[] { 0xff }).ToArray();

        source.Cancel();
        var error = Assert.Throws<OperationCanceledException>(() => regex.Count(invalid, 0, source.Token));
        Assert.Equal(source.Token, error.CancellationToken);
    }
}
