namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2SliceDepthAdmissionTests
{
    private static Utf8Pcre2Regex Create(uint depth)
        => new("(😀+)(é+)", Pcre2CompileOptions.None, default,
            new Utf8Pcre2ExecutionLimits { DepthLimit = depth }, Utf8Pcre2Regex.DefaultMatchTimeout);

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void ScalarPublicOperationsPreserveExactDepthAndFailureIdentity(int operation)
    {
        var exact = Create(4); var denied = Create(3);
        Assert.IsType<Pcre2SliceDirectProgram>(exact.DebugCompiledProgram.Operations.Count);
        var input = "😀😀😀éé"u8.ToArray();
        Assert.Equal(operation == 3 ? "éé😀😀😀" : "1", Run(exact, input, operation));
        var failure = Assert.Throws<Pcre2MatchException>(() => Run(denied, input, operation));
        Assert.Equal(Pcre2ErrorKind.DepthLimit, failure.ErrorKind);
        Assert.Equal("1", Run(Create(4), input, 0));
    }

    private static string Run(Utf8Pcre2Regex regex, byte[] input, int operation) => operation switch
    {
        0 => regex.Count(input).ToString(),
        1 => regex.MatchDetailed(input).Success ? "1" : "0",
        2 => Count(regex, input).ToString(),
        _ => regex.ReplaceToString(input, "$2$1")
    };

    private static int Count(Utf8Pcre2Regex regex, byte[] input)
    {
        var count = 0;
        var matches = regex.EnumerateMatches(input);
        while (matches.MoveNext())
        { Assert.Equal("😀😀😀éé", matches.Current.GetValueString()); count++; }
        return count;
    }
}
