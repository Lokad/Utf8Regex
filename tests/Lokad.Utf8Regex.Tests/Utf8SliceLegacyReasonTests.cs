using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

// Slice-first construction: owned programs report the legacy fallback
// taxonomy probed from the current pipeline, so reporting survives the
// construction cut without migrating inspection pins. Expected reasons come
// from the pipeline itself (the oracle); the program mapping must agree.
public sealed class Utf8SliceLegacyReasonTests
{
    public static IEnumerable<object?[]> SliceReasonShapes()
    {
        yield return new object?[] { "ab+" };
        yield return new object?[] { "\\d+" };
        yield return new object?[] { "[a-z]+" };
        yield return new object?[] { "\\p{L}+" };
        yield return new object?[] { "\\cA+" };
        yield return new object?[] { ".+" };
        yield return new object?[] { "(a+)(b+)" };
        yield return new object?[] { "(a)+" };
        yield return new object?[] { "(\\p{L}+)+$" };
        yield return new object?[] { "(a|b)+c" };
        yield return new object?[] { "^(?:ab)+$" };
        yield return new object?[] { "^[a-z]+$" };
        yield return new object?[] { "\\b\\w+\\b" };
        yield return new object?[] { "\\t*\\n" };
        yield return new object?[] { "\\p{L}{8,13}" };
        yield return new object?[] { "\\p{L}{2}" };
        yield return new object?[] { "()" };
        yield return new object?[] { "()+" };
        yield return new object?[] { "(?i)(a+)(b+)" };
        yield return new object?[] { "^(\\p{L}+)$" };
        yield return new object?[] { "a(?!b)" };
        yield return new object?[] { "(?<=a)b" };
        yield return new object?[] { "(?<!a)b" };
        yield return new object?[] { "(\\d+)(?=\\n)" };
        yield return new object?[] { "(?<=\\n)x+" };
        yield return new object?[] { "(\\w+)(?<!x)" };
        yield return new object?[] { "(a+)(?!b)" };
        yield return new object?[] { "(?<=(a)b)c" };
        yield return new object?[] { "(a)\\1" };
        yield return new object?[] { "(a)\\1+" };
        yield return new object?[] { "(a)(?=b)\\1" };
        yield return new object?[] { "((?>ab)c)\\1" };
        yield return new object?[] { "((?<o>a)(?<-o>b))\\1" };
        yield return new object?[] { "(?>ab)c" };
        yield return new object?[] { "(?>a+)+b" };
        yield return new object?[] { "(?>a+)(?<o>b)(?<-o>c)" };
        yield return new object?[] { "(?>a(?=b))c" };
        yield return new object?[] { "(?>a(?<=b))c" };
        yield return new object?[] { "(?>a(?<!b))c" };
        yield return new object?[] { "(a)?(?(1)ab|cd)" };
        yield return new object?[] { "(?(?=a)b|c)" };
        yield return new object?[] { "(?<n>a)+(?(n)b|c)" };
        yield return new object?[] { "(a)?(?(1)ab|cd)+" };
        yield return new object?[] { "(a)\\1(?(1)x|y)" };
        yield return new object?[] { "(a)(?>b+)(?(1)x|y)" };
        yield return new object?[] { "(?<o>a)(?<-o>b)(?(o)x|y)" };
        yield return new object?[] { "(?<o>a)(?<-o>b)" };
        yield return new object?[] { "(?<=(?:a)b)c" };
        yield return new object?[] { "(?<=ab)x" };
        yield return new object?[] { "(?<=a{2})x" };
        yield return new object?[] { "(?<=\\p{L})x" };
        yield return new object?[] { "(?=(a)b)c" };
        yield return new object?[] { "(?!(a)b)c" };
        yield return new object?[] { "(a)(?=(b))\\1" };
        yield return new object?[] { "(?<=(a)b)c+" };
        yield return new object?[] { "(?<=((a)b))c+" };
        yield return new object?[] { "(?<=(a)b)\\1" };
        yield return new object?[] { "(?<!(a)b)c" };
        yield return new object?[] { "(?<=(a)b)(?(1)x|y)" };
        yield return new object?[] { "(?<=(a)b)(?>c)" };
        yield return new object?[] { "(?<o>a)+(?<-o>b)" };
    }

    [Theory]
    [MemberData(nameof(SliceReasonShapes))]
    public void OwnedProgramReportsLegacyReason(string pattern)
    {
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        var program = regex.Inspection.OwnedSliceProgram;
        Assert.NotNull(program);
        Assert.Equal(regex.Inspection.FallbackReason, program.LegacyFallbackReason);
    }
}
