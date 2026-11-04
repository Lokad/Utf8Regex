using System.Text;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReResultArrayLifetimeTests
{
    [Theory]
    [InlineData(0)] [InlineData(16)] [InlineData(17)]
    [InlineData(256)] [InlineData(257)] [InlineData(2048)]
    public void FullAndPartialResultRootsKeepTheirValuesAcrossLaterCalls(int count)
    {
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("item-12 ", count)));
        var single = new Utf8PythonRegex("item-([0-9]+)");
        var strings = single.FindAllToStrings(input).ScalarValues;
        var utf8 = single.FindAllToUtf8(input).ScalarValues;
        var tupleRegex = new Utf8PythonRegex("([a-z]+)-([0-9]+)");
        var tuples = tupleRegex.FindAllToStrings(input).TupleValues;
        var bytes = tupleRegex.FindAllToUtf8(input).TupleValues;
        Assert.Equal(count, strings.Length);
        Assert.Equal(count, utf8.Length);
        Assert.Equal(count, tuples.Length);
        Assert.Equal(count, bytes.Length);
        for (var iteration = 0; iteration < 3; iteration++)
        {
            _ = single.FindAllToStrings("item-345 item-678"u8);
            _ = tupleRegex.FindAllToStrings("other-987"u8);
        }
        Assert.All(strings, value => Assert.Equal("12", value));
        Assert.All(utf8, value => Assert.Equal("12"u8.ToArray(), value));
        Assert.All(tuples, value => Assert.Equal(new[] { "item", "12" }, value));
        Assert.All(bytes, value =>
        {
            Assert.Equal("item"u8.ToArray(), value[0]);
            Assert.Equal("12"u8.ToArray(), value[1]);
        });
        if (count > 0)
        {
            strings[0] = "caller mutation";
            tuples[0][0] = "caller mutation";
            Assert.Equal("345", single.FindAllToStrings("item-345"u8).ScalarValues[0]);
            Assert.Equal("other", tupleRegex.FindAllToStrings("other-987"u8).TupleValues[0][0]);
        }
    }
}
