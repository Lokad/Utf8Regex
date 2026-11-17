using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8StructuralInputFactsTests
{
    [Theory]
    [InlineData(RegexOptions.None)]
    [InlineData(RegexOptions.Compiled)]
    public void ByteOperationsAndCoordinateConsumersShareTheSelectedAuthority(
RegexOptions options)
    {
        var regex = new Utf8Regex("Tom.{1,3}river", options);
        var input = Encoding.UTF8.GetBytes("é😀 Tom😀river x Tomabriver");
        using var lifetime = new CancellationTokenSource();
        Assert.True(Observe(() => regex.IsMatch(input, lifetime.Token)));
        Assert.Equal(2, Observe(() => regex.Count(input)));
        Assert.Equal(2, Observe(() => regex.Count(input, lifetime.Token)));
        Assert.Equal((7, 12), Observe(() => Range(regex.Match(input, cancellationToken: lifetime.Token))));
        Assert.Equal([(7, 12), (22, 10)], Observe(Enumerate));

        T Observe<T>(Func<T> operation)
        {
            var session = Utf8SearchDiagnosticsSession.Start();
            try
            {
                var result = operation();
                return result;
            }
            finally { session.Complete(); }
        }

        (int, int)[] Enumerate()
        {
            var ranges = new List<(int, int)>();
            foreach (var match in regex.EnumerateMatches(input, cancellationToken: lifetime.Token)) ranges.Add(Range(match));
            return ranges.ToArray();
        }
    }

    private static (int, int) Range(Utf8CaptureRange match)
        => (match.StartOffsetInBytes, match.EndOffsetInBytes - match.StartOffsetInBytes);
}
