using System.Text;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Utf8Pcre2GlobalIterationTests
{
    [Fact]
    public void Count_and_enumerate_repeated_kreset_ab()
    {
        var regex = new Utf8Pcre2Regex(@"(?:a\Kb)*", Pcre2CompileOptions.None);
        var input = Encoding.UTF8.GetBytes("ab xx ab yy ab");

        Assert.Equal(3, regex.Count(input));

        var starts = new List<int>();
        var enumerator = regex.EnumerateMatches(input);
        while (enumerator.MoveNext())
        {
            starts.Add(enumerator.Current.StartOffsetInBytes);
        }

        Assert.Equal([1, 7, 13], starts);
    }

    [Fact]
    public void Count_and_enumerate_captured_repeated_kreset_ab()
    {
        var regex = new Utf8Pcre2Regex(@"(a\Kb)*", Pcre2CompileOptions.None);
        var input = Encoding.UTF8.GetBytes("ab xx ab yy ab");

        Assert.Equal(3, regex.Count(input));

        var starts = new List<int>();
        var enumerator = regex.EnumerateMatches(input);
        while (enumerator.MoveNext())
        {
            starts.Add(enumerator.Current.StartOffsetInBytes);
        }

        Assert.Equal([1, 7, 13], starts);
    }

    [Fact]
    public void Replace_repeated_kreset_ab_uses_match_spans()
    {
        var regex = new Utf8Pcre2Regex(@"(?:a\Kb)*", Pcre2CompileOptions.None);
        var input = Encoding.UTF8.GetBytes("ab xx ab yy ab");

        var replaced = regex.Replace(input, "R");

        Assert.Equal("aR xx aR yy aR", Encoding.UTF8.GetString(replaced));
    }

    [Fact]
    public void Count_and_enumerate_atomic_kreset_alternation()
    {
        var regex = new Utf8Pcre2Regex(@"(?>a\Kbz|ab)", Pcre2CompileOptions.None);
        var input = Encoding.UTF8.GetBytes("abz ab xx abz");

        Assert.Equal(3, regex.Count(input));

        var starts = new List<int>();
        var lengths = new List<int>();
        var enumerator = regex.EnumerateMatches(input);
        while (enumerator.MoveNext())
        {
            starts.Add(enumerator.Current.StartOffsetInBytes);
            lengths.Add(enumerator.Current.EndOffsetInBytes - enumerator.Current.StartOffsetInBytes);
        }

        Assert.Equal([1, 4, 11], starts);
        Assert.Equal([2, 2, 2], lengths);
    }

    [Fact]
    public void Replace_atomic_kreset_alternation_uses_selected_spans()
    {
        var regex = new Utf8Pcre2Regex(@"(?>a\Kbz|ab)", Pcre2CompileOptions.None);
        var input = Encoding.UTF8.GetBytes("abz ab xx abz");

        var replaced = regex.Replace(input, "R");

        Assert.Equal("aR R xx aR", Encoding.UTF8.GetString(replaced));
    }
}
