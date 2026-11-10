using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8StructuralInputFactsTests
{
    [Theory]
    [InlineData(Utf8InputValidation.Validate, RegexOptions.None)]
    [InlineData(Utf8InputValidation.Validate, RegexOptions.Compiled)]
    [InlineData(Utf8InputValidation.AssumeValid, RegexOptions.None)]
    [InlineData(Utf8InputValidation.AssumeValid, RegexOptions.Compiled)]
    public void ByteOperationsAndCoordinateConsumersShareTheSelectedAuthority(
        Utf8InputValidation policy, RegexOptions options)
    {
        var regex = new Utf8Regex("Tom.{1,3}river", options, inputValidation: policy);
        var input = Encoding.UTF8.GetBytes("é😀 Tom😀river x Tomabriver");
        using var lifetime = new CancellationTokenSource();
        Assert.True(Observe(() => regex.IsMatch(input, lifetime.Token)));
        Assert.Equal(2, Observe(() => regex.Count(input)));
        Assert.Equal(2, Observe(() => regex.Count(input, lifetime.Token)));
        Assert.Equal((7, 12, 4, 10), Observe(() => Range(regex.Match(input, lifetime.Token))));
        Assert.Equal([(7, 12, 4, 10), (22, 10, 17, 10)], Observe(Enumerate));

        T Observe<T>(Func<T> operation)
        {
            var session = Utf8SearchDiagnosticsSession.Start();
            try
            {
                var result = operation();
                Assert.InRange(session.SubjectValidations, 0, policy == Utf8InputValidation.Validate ? 1 : 0);
                return result;
            }
            finally { session.Complete(); }
        }

        (int, int, int, int)[] Enumerate()
        {
            var ranges = new List<(int, int, int, int)>();
            foreach (var match in regex.EnumerateMatches(input, lifetime.Token)) ranges.Add(Range(match));
            return ranges.ToArray();
        }
    }

    [Fact]
    public void DefaultPreparationRejectsMalformedSuffixBeforeAnyResult()
    {
        var regex = new Utf8Regex("Tom.{1,3}river", RegexOptions.Compiled);
        byte[] input = [.. "Tom😀river"u8, 0xff];
        using var lifetime = new CancellationTokenSource();
        Assert.Throws<ArgumentException>(() => regex.IsMatch(input, lifetime.Token));
        Assert.Throws<ArgumentException>(() => regex.Count(input));
        Assert.Throws<ArgumentException>(() => regex.Match(input, lifetime.Token));
        Assert.Throws<ArgumentException>(() => { foreach (var _ in regex.EnumerateMatches(input, lifetime.Token)) { } });
    }

    private static (int, int, int, int) Range(Utf8ValueMatch match)
        => (match.IndexInBytes, match.LengthInBytes, match.IndexInUtf16, match.LengthInUtf16);
}
