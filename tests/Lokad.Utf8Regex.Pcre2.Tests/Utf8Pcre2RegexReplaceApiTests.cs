using System.Buffers;
using System.Text;

using global::Lokad.Utf8Regex;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Utf8Pcre2RegexReplaceApiTests
{

    [Fact]
    public void TryReplaceWritesWhenDestinationIsLargeEnough()
    {
        var regex = new Utf8Pcre2Regex("abc");
        var destination = new byte[7];

        var status = regex.TryReplace("xxabcxx"u8, "tea"u8, destination, out var bytesWritten);

        Assert.Equal(OperationStatus.Done, status);
        Assert.Equal(7, bytesWritten);
        Assert.Equal("xxteaxx"u8.ToArray(), destination);
    }

    [Fact]
    public void TryReplaceLeavesDestinationUntouchedWhenTooSmall()
    {
        var regex = new Utf8Pcre2Regex("abc");
        Span<byte> destination = stackalloc byte[4];
        "stay"u8.CopyTo(destination);

        var status = regex.TryReplace("xxabcxx"u8, "longer"u8, destination, out var bytesWritten);

        Assert.Equal(OperationStatus.DestinationTooSmall, status);
        Assert.Equal(0, bytesWritten);
        Assert.Equal("stay"u8.ToArray(), destination.ToArray());
    }

    [Fact]
    public void TryReplaceReportsRequiredLengthWhenSubstituteOverflowLengthIsSet()
    {
        var regex = new Utf8Pcre2Regex("abc");
        Span<byte> destination = stackalloc byte[8];
        "staysafe"u8.CopyTo(destination);

        var status = regex.TryReplace(
            "123abc123"u8,
            "XYZ"u8,
            destination,
            out var bytesWritten,
            substitutionOptions: Pcre2SubstitutionOptions.SubstituteOverflowLength);

        Assert.Equal(OperationStatus.DestinationTooSmall, status);
        Assert.Equal(9, bytesWritten);
        Assert.Equal("staysafe"u8.ToArray(), destination.ToArray());
    }

    [Fact]
    public void TryReplaceReportsRequiredLengthForLiteralReplacementWhenSubstituteOverflowLengthIsSet()
    {
        var regex = new Utf8Pcre2Regex("abcd");
        Span<byte> destination = stackalloc byte[10];
        "0123456789"u8.CopyTo(destination);

        var status = regex.TryReplace(
            "XabcdYabcdZ"u8,
            "\\$1$2\\"u8,
            destination,
            out var bytesWritten,
            substitutionOptions: Pcre2SubstitutionOptions.SubstituteLiteral | Pcre2SubstitutionOptions.SubstituteOverflowLength);

        Assert.Equal(OperationStatus.DestinationTooSmall, status);
        Assert.Equal(15, bytesWritten);
        Assert.Equal("0123456789"u8.ToArray(), destination.ToArray());
    }

    [Fact]
    public void TryReplacePreservesKResetSubstitutionEmptyRetrySemantics()
    {
        var regex = new Utf8Pcre2Regex(@"(?:a\Kb)*");
        Span<byte> destination = stackalloc byte[16];

        var status = regex.TryReplace("ab xx ab"u8, "R"u8, destination, out var bytesWritten);

        Assert.Equal(OperationStatus.Done, status);
        Assert.Equal(13, bytesWritten);
        Assert.Equal("aRR RxRxR aRR"u8.ToArray(), destination[..bytesWritten].ToArray());
    }

    [Fact]
    public void TryReplaceReportsLengthForKResetSubstitutionEmptyRetries()
    {
        var regex = new Utf8Pcre2Regex(@"(?:a\Kb)*");
        Span<byte> destination = stackalloc byte[4];
        "stay"u8.CopyTo(destination);

        var status = regex.TryReplace(
            "ab xx ab"u8,
            "R"u8,
            destination,
            out var bytesWritten,
            substitutionOptions: Pcre2SubstitutionOptions.SubstituteOverflowLength);

        Assert.Equal(OperationStatus.DestinationTooSmall, status);
        Assert.Equal(13, bytesWritten);
        Assert.Equal("stay"u8.ToArray(), destination.ToArray());
    }

    [Fact]
    public void ReplaceSupportsExtendedBasicEscapes()
    {
        var regex = new Utf8Pcre2Regex("abc");

        var actual = System.Text.Encoding.UTF8.GetString(regex.Replace(
            "abc"u8,
            "\\a\\b\\e\\f\\n\\r\\t\\v\\\\",
0,            substitutionOptions: Pcre2SubstitutionOptions.Extended,Pcre2MatchOptions.None));

        Assert.Equal("\u0007\b\u001b\f\n\r\t\u000b\\", actual);
    }

    [Fact]
    public void ReplaceAcceptsSubstituteMatchedOption()
    {
        var regex = new Utf8Pcre2Regex("abcd");

        var actual = System.Text.Encoding.UTF8.GetString(regex.Replace(
            ">abcd1234abcd5678<"u8,
            "wxyz",
0,            substitutionOptions: Pcre2SubstitutionOptions.SubstituteMatched,Pcre2MatchOptions.None));

        Assert.Equal(">wxyz1234wxyz5678<", actual);
    }

    [Fact]
    public void ReplaceCanFallBackToManagedPathForFooOptionalBarOutsideExtendedSpecialCase()
    {
        var regex = new Utf8Pcre2Regex("foo(?<Bar>BAR)?");

        var actual = System.Text.Encoding.UTF8.GetString(regex.Replace(
            "fooBAR foo"u8,
            "<$0/$1>",
0,            substitutionOptions: Pcre2SubstitutionOptions.UnsetEmpty,Pcre2MatchOptions.None));

        Assert.Equal("<fooBAR/BAR> <foo/>", actual);
    }

    [Fact]
    public void ReplaceCanFallBackToManagedPathForAbPlusOutsidePartialMode()
    {
        var regex = new Utf8Pcre2Regex("(a)b+");

        var actual = System.Text.Encoding.UTF8.GetString(regex.Replace(
            "ab abb x"u8,
            "<$0/$1>"));

        Assert.Equal("<ab/a> <abb/a> x", actual);
    }

    [Fact]
    public void ReplaceReplacementOnlyUsesGenericBacktrackingIteration()
    {
        var regex = new Utf8Pcre2Regex("c*+(?<=[bc])");

        var actual = System.Text.Encoding.UTF8.GetString(regex.Replace(
            "cc"u8,
            "X",
0,            substitutionOptions: Pcre2SubstitutionOptions.SubstituteReplacementOnly,Pcre2MatchOptions.None));

        Assert.Equal("XX", actual);
    }

    [Fact]
    public void ReplaceLiteralUsesGenericBacktrackingIteration()
    {
        var regex = new Utf8Pcre2Regex("c*+(?<=[bc])");

        var actual = System.Text.Encoding.UTF8.GetString(regex.Replace(
            "cc"u8,
            "X",
0,            substitutionOptions: Pcre2SubstitutionOptions.SubstituteLiteral,Pcre2MatchOptions.None));

        Assert.Equal("XX", actual);
    }

    [Fact]
    public void ReplaceReplacementOnlyLiteralUsesGenericBacktrackingIteration()
    {
        var regex = new Utf8Pcre2Regex("c*+(?<=[bc])");

        var actual = System.Text.Encoding.UTF8.GetString(regex.Replace(
            "cc"u8,
            "X",
0,            substitutionOptions: Pcre2SubstitutionOptions.SubstituteReplacementOnly | Pcre2SubstitutionOptions.SubstituteLiteral,Pcre2MatchOptions.None));

        Assert.Equal("XX", actual);
    }

    [Fact]
    public void ReplaceTreatsReplacementAsLiteralWhenSubstituteLiteralIsSet()
    {
        var regex = new Utf8Pcre2Regex("abcd");

        var actual = System.Text.Encoding.UTF8.GetString(regex.Replace(
            "XabcdYabcdZ"u8,
            "\\$1$2\\",
0,            substitutionOptions: Pcre2SubstitutionOptions.SubstituteLiteral,Pcre2MatchOptions.None));

        Assert.Equal("X\\$1$2\\Y\\$1$2\\Z", actual);
    }

    [Fact]
    public void ReplaceReturnsOnlyReplacementFragmentsWhenSubstituteReplacementOnlyIsSet()
    {
        var regex = new Utf8Pcre2Regex("a(?<ONE>b)c(?<TWO>d)e");

        var actual = System.Text.Encoding.UTF8.GetString(regex.Replace(
            "abcde-abcde-"u8,
            "X$ONE+${TWO}Z",
0,            substitutionOptions: Pcre2SubstitutionOptions.SubstituteReplacementOnly,Pcre2MatchOptions.None));

        Assert.Equal("Xb+dZXb+dZ", actual);
    }

    [Fact]
    public void ReplaceReplacementOnlyHonorsStartOffset()
    {
        var regex = new Utf8Pcre2Regex("a");

        var actual = System.Text.Encoding.UTF8.GetString(regex.Replace(
            "XYaZ"u8,
            "foo",
            startOffsetInBytes: 2,
            substitutionOptions: Pcre2SubstitutionOptions.SubstituteReplacementOnly));

        Assert.Equal("foo", actual);
    }

    [Fact]
    public void ReplaceSubstituteMatchedCanUseCaptureTemplates()
    {
        var regex = new Utf8Pcre2Regex("a(..)d");

        var actual = System.Text.Encoding.UTF8.GetString(regex.Replace(
            "xyzabcdxyzabcdxyz"u8,
            ">$1<",
0,            substitutionOptions: Pcre2SubstitutionOptions.SubstituteMatched,Pcre2MatchOptions.None));

        Assert.Equal("xyz>bc<xyz>bc<xyz", actual);
    }

    [Fact]
    public void ReplaceNativeIterationCanUseLastCapturedReference()
    {
        var regex = new Utf8Pcre2Regex(
            "55|a(..)d");

        var actual = System.Text.Encoding.UTF8.GetString(regex.Replace(
            "xyz55abcdxyzabcdxyz"u8,
            ">$+<",
0,            substitutionOptions: Pcre2SubstitutionOptions.UnsetEmpty,Pcre2MatchOptions.None));

        Assert.Equal("xyz><>bc<xyz>bc<xyz", actual);
    }

    [Fact]
    public void ReplaceNativeIterationCanUseDuplicateNamedReference()
    {
        var regex = new Utf8Pcre2Regex(
            "(?:(?<n>foo)|(?<n>bar))\\k<n>",
            Pcre2CompileOptions.None,
            new Utf8Pcre2CompileSettings { AllowDuplicateNames = true },
            default(Utf8Pcre2ExecutionLimits),
            Utf8Pcre2Regex.DefaultMatchTimeout);

        var actual = System.Text.Encoding.UTF8.GetString(regex.Replace(
            "foofoo barbar"u8,
            "<$n>"));

        Assert.Equal("<foo> <bar>", actual);
    }

    [Fact]
    public void ReplaceNativeIterationCanUseUnsetEmptyAcrossBranchResetAlternatives()
    {
        var regex = new Utf8Pcre2Regex("(x)(?|(abc)(pqr)|(xyz))(x)");

        var actual = System.Text.Encoding.UTF8.GetString(regex.Replace(
            "xabcpqrxxxyzx"u8,
            "<$2/$3>",
0,            substitutionOptions: Pcre2SubstitutionOptions.UnsetEmpty,Pcre2MatchOptions.None));

        Assert.Equal("<abc/pqr><xyz/>", actual);
    }

    [Fact]
    public void ReplaceNativeIterationCanCombineDuplicateNameAndLastCapturedReference()
    {
        var regex = new Utf8Pcre2Regex(
            "(?|(?'a'aaa)|(?'a'b))(?'a'cccc)\\k'a'",
            Pcre2CompileOptions.None,
            new Utf8Pcre2CompileSettings { AllowDuplicateNames = true },
            default(Utf8Pcre2ExecutionLimits),
            Utf8Pcre2Regex.DefaultMatchTimeout);

        var actual = System.Text.Encoding.UTF8.GetString(regex.Replace(
            "aaaccccaaa bccccb"u8,
            "<$a|$+>"));

        Assert.Equal("<aaa|cccc> <b|cccc>", actual);
    }

    [Fact]
    public void ReplaceNativeIterationCanUseMarkReference()
    {
        var regex = new Utf8Pcre2Regex("(*MARK:A)(*SKIP:B)(C|X)");

        var actual = System.Text.Encoding.UTF8.GetString(regex.Replace(
            "C X"u8,
            "<${*MARK}:$1>"));

        Assert.Equal("<A:C> <A:X>", actual);
    }

    [Fact]
    public void ReplaceDetailedNativeMatchingCanUseSubroutineCaptureReferences()
    {
        var regex = new Utf8Pcre2Regex("^(?1)\\d{3}(a)");

        var actual = System.Text.Encoding.UTF8.GetString(regex.Replace(
            "a123a"u8,
            "<$1/$0>"));

        Assert.Equal("<a/a123a>", actual);
    }

    [Fact]
    public void ReplaceDetailedNativeMatchingCanUseCommitSubroutineCaptures()
    {
        var regex = new Utf8Pcre2Regex("(?1)(A(*COMMIT)|B)D");

        var actual = System.Text.Encoding.UTF8.GetString(regex.Replace(
            "ABD BAD"u8,
            "<$1>"));

        Assert.Equal("<B> <A>", actual);
    }

    [Fact]
    public void ReplaceDetailedNativeMatchingCanUseKResetCaptureReferences()
    {
        var regex = new Utf8Pcre2Regex("(foo)\\Kbar");

        var actual = System.Text.Encoding.UTF8.GetString(regex.Replace(
            "foobar xxfoobar"u8,
            "<$0/$1>"));

        Assert.Equal("foo<bar/foo> xxfoo<bar/foo>", actual);
    }

    [Fact]
    public void ReplaceDetailedNativeMatchingCanUseCapturedAtomicKResetCaptures()
    {
        var regex = new Utf8Pcre2Regex("((?>a\\Kb))");

        var actual = System.Text.Encoding.UTF8.GetString(regex.Replace(
            "abxab"u8,
            "<$0/$1>"));

        Assert.Equal("a<b/ab>xa<b/ab>", actual);
    }

    [Fact]
    public void ReplaceDetailedNativeMatchingCanUseKResetAlternationCaptures()
    {
        var regex = new Utf8Pcre2Regex("(foo)(\\Kbar|baz)");

        var actual = System.Text.Encoding.UTF8.GetString(regex.Replace(
            "foobar foobaz"u8,
            "<$0/$1/$2>"));

        Assert.Equal("foo<bar/foo/bar> <foobaz/foo/baz>", actual);
    }

    [Fact]
    public void ReplaceDetailedNativeMatchingCanUseGroupedKResetCaptures()
    {
        var regex = new Utf8Pcre2Regex("(foo\\Kbar)baz");

        var actual = System.Text.Encoding.UTF8.GetString(regex.Replace(
            "foobarbaz xxfoobarbaz"u8,
            "<$0/$1>"));

        Assert.Equal("foo<barbaz/foobar> xxfoo<barbaz/foobar>", actual);
    }

    [Fact]
    public void ReplaceDetailedNativeMatchingCanInsertAtKResetAnchor()
    {
        var regex = new Utf8Pcre2Regex("^abc\\K");

        var actual = System.Text.Encoding.UTF8.GetString(regex.Replace(
            "abcdef"u8,
            "<$0>"));

        Assert.Equal("abc<>def", actual);
    }

    [Fact]
    public void ReplaceDetailedNativeMatchingCanInsertAtOriginalAnchorWhenKResetIsAlternative()
    {
        var regex = new Utf8Pcre2Regex("^(?:(?=abc)|abc\\K)");

        var actual = System.Text.Encoding.UTF8.GetString(regex.Replace(
            "abcdef"u8,
            "<$0>"));

        Assert.Equal("<>abcdef", actual);
    }

    [Fact]
    public void ReplaceDetailedNativeMatchingCanUseRecursiveCapturedKResetCapture()
    {
        var regex = new Utf8Pcre2Regex("(a\\K.(?1)*)");

        var actual = System.Text.Encoding.UTF8.GetString(regex.Replace(
            "abac"u8,
            "<$0/$1>"));

        Assert.Equal("aba<c/abac>", actual);
    }

    [Fact]
    public void ReplaceDetailedNativeMatchingCanUseKResetDefineSubroutine()
    {
        var regex = new Utf8Pcre2Regex("^(?&t)(?(DEFINE)(?<t>a\\Kb))$");

        var actual = System.Text.Encoding.UTF8.GetString(regex.Replace(
            "ab"u8,
            "<$0>"));

        Assert.Equal("a<b>", actual);
    }
}
