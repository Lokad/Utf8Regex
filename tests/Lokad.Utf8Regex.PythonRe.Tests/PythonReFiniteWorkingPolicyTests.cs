using System.Text;
using System.Text.RegularExpressions;
using System.Runtime.CompilerServices;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReFiniteWorkingPolicyTests
{
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_discovery")]
    private static extern ref PythonReScalarMatchState Discovery(Utf8PythonMatchCursor cursor);
    private static Utf8PythonRegex Create(string pattern, ulong bytes, bool bytePattern)
    {
        var policy = new Utf8ResourcePolicy { MaxWorkingBytes = bytes };
        return bytePattern ? new(Encoding.UTF8.GetBytes(pattern), PythonReCompileOptions.None, Regex.InfiniteMatchTimeout, policy)
            : new(pattern, PythonReCompileOptions.None, Regex.InfiniteMatchTimeout, policy);
    }

    private static void Denied(Utf8ResourceLimitExceededException error, ulong bytes)
    {
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes, error.ResourceKind);
        Assert.Equal(bytes, error.Limit); Assert.True(error.Requested > bytes);
    }

    private static void Denied(Utf8ResourceLimitExceededException error, ulong bytes, int operation)
    {
        if (operation is 3 or 7 or 10 or 13 or 20 or 21 && error.ResourceKind == Utf8ResourceLimitKind.ConstructionBytes)
        {
            // Cold FullMatch/template preparation has a byte ceiling before
            // execution. Its diagnostic totals include the enclosing cache
            // preparation; all other routes retain execution-only diagnostics.
            Assert.True(error.Limit >= bytes);
            Assert.True(error.Requested > error.Limit);
        }
        else Denied(error, bytes);
    }

    public static IEnumerable<object[]> Operations()
    {
        for (var operation = 0; operation < 25; operation++)
        foreach (var bytePattern in new[] { false, true }) yield return [operation, bytePattern];
    }

    [Theory]
    [MemberData(nameof(Operations))]
    public void EveryPublicOperationHasFiniteWorkingSuccessAndExactCutoff(int operation, bool bytePattern)
    {
        const string pattern = "(?P<x>😀+)(?P<y>ab)";
        var input = "😀😀ab"u8.ToArray();
        var expected = PythonReFiniteWorkPolicyTests.Run(new(pattern), input, operation);
        Assert.Equal(expected, PythonReFiniteWorkPolicyTests.Run(Create(pattern, ulong.MaxValue, bytePattern), input, operation));
        Denied(Assert.Throws<Utf8ResourceLimitExceededException>(() =>
            PythonReFiniteWorkPolicyTests.Run(Create(pattern, 1, bytePattern), input, operation)), 1, operation);
        ulong low = 1, high = 1_048_576;
        while (high - low > 1)
        {
            var middle = low + (high - low) / 2;
            try
            {
                Assert.Equal(expected, PythonReFiniteWorkPolicyTests.Run(Create(pattern, middle, bytePattern), input, operation));
                high = middle;
            }
            catch (Utf8ResourceLimitExceededException error) { Denied(error, middle, operation); low = middle; }
        }
        Assert.Equal(expected, PythonReFiniteWorkPolicyTests.Run(Create(pattern, high, bytePattern), input, operation));
        Denied(Assert.Throws<Utf8ResourceLimitExceededException>(() =>
            PythonReFiniteWorkPolicyTests.Run(Create(pattern, high - 1, bytePattern), input, operation)), high - 1, operation);
    }

    [Theory]
    [InlineData("cat", "cat")]
    [InlineData("cat\\d+", "cat123")]
    [InlineData("(?s:cat.*dog)", "cat😀dog")]
    [InlineData("(?<=😀)(?P<x>é+)\\1", "😀éé")]
    [InlineData(".*?", "😀a")]
    public void NativeEligibleScalarAndEmptyRoutesCannotBypassWorkingPolicy(string pattern, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        for (var operation = 0; operation < 25; operation++)
            Assert.Equal(PythonReFiniteWorkPolicyTests.Run(new(pattern), input, operation),
                PythonReFiniteWorkPolicyTests.Run(Create(pattern, 1_048_576, false), input, operation));
    }

    [Theory]
    [InlineData(Utf8ResourceLimitKind.ExecutionWorkSteps)]
    [InlineData(Utf8ResourceLimitKind.ExecutionDepth)]
    [InlineData(Utf8ResourceLimitKind.ExecutionWorkingBytes)]
    [InlineData(Utf8ResourceLimitKind.ExecutionResultBytes)]
    public void CombinedPolicyPreservesEachPublicFailureKind(Utf8ResourceLimitKind kind)
    {
        var policy = new Utf8ResourcePolicy
        {
            MaxWorkSteps = kind == Utf8ResourceLimitKind.ExecutionWorkSteps ? 1 : ulong.MaxValue,
            MaxDepth = kind == Utf8ResourceLimitKind.ExecutionDepth ? 1 : uint.MaxValue,
            MaxWorkingBytes = kind == Utf8ResourceLimitKind.ExecutionWorkingBytes ? 1 : ulong.MaxValue,
            MaxResultBytes = kind == Utf8ResourceLimitKind.ExecutionResultBytes ? 1 : ulong.MaxValue,
        };
        var regex = new Utf8PythonRegex("(?P<x>😀+)(?P<y>ab)", PythonReCompileOptions.None, Regex.InfiniteMatchTimeout, policy);
        var error = Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.SearchDetailedData("😀😀ab"u8));
        Assert.Equal(kind, error.ResourceKind); Assert.Equal(1UL, error.Limit); Assert.True(error.Requested > 1);
    }

    [Fact]
    public void OwnedCursorWorkingFailureReleasesScratchAndPreservesPublishedSnapshot()
    {
        const string pattern = "(?P<x>ab)|(😀|😀😀)+ab";
        var input = Encoding.UTF8.GetBytes("ab " + string.Concat(Enumerable.Repeat("😀", 70)) + "ab");
        ulong cap;
        using (var probe = Create(pattern, ulong.MaxValue, false).CreateMatchCursor(input))
        {
            Assert.True(probe.MoveNext());
            cap = Discovery(probe).WorkingInvocation.Budget.PeakWorkingBytes;
        }
        var regex = Create(pattern, cap, false);
        using var cursor = regex.CreateMatchCursor(input);
        Assert.True(cursor.MoveNext());
        var retained = cursor.Current;
        var authority = Discovery(cursor).WorkingInvocation;
        Denied(Assert.Throws<Utf8ResourceLimitExceededException>(() => cursor.MoveNext()), cap);
        Assert.Throws<InvalidOperationException>(() => cursor.MoveNext());
        cursor.Dispose(); cursor.Dispose();
        Assert.Equal(0UL, authority.Ledger.ReservedBytes);
        Assert.Equal("ab", retained.Value.ValueText);
        Assert.True(regex.IsMatch("ab"u8));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FiniteWorkingPolicyStillValidatesMalformedTailsAndByteScalarOffsets(bool bytePattern)
    {
        var regex = Create("ab", 1_048_576, bytePattern);
        Assert.Throws<ArgumentException>(() => regex.IsMatch([97, 98, 255]));
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.Search("😀ab"u8, 1));
        Assert.Equal(4, regex.Search("😀ab"u8, 4).StartOffsetInBytes);
    }
}
