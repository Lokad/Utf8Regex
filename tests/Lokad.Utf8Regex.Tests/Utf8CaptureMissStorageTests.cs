using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8CaptureMissStorageTests
{
    [Theory]
    [InlineData("z", "é😀 z")]
    [InlineData("^z", "zé😀")]
    [InlineData("z$", "é😀z")]
    [InlineData("(z)", "é😀 z")]
    [InlineData("(?<left>z)|(?<right>w)", "é😀 w")]
    [InlineData("😀", "é😀")]
    [InlineData("(?<letter>😀)", "é😀")]
    [InlineData("(?<tail>ab)|(?<tail>cd)", "é😀 cd")]
    public void MissStorageStaysBoundedAndRetainedHitsSurviveLaterCalls(string pattern, string hitText)
    {
        var small = Encoding.UTF8.GetBytes("é𐐀aaa");
        var large = Encoding.UTF8.GetBytes(new string('é', 65536) + "𐐀aaa");
        foreach (var compiled in new[] { false, true })
        {
            var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : 0);
            var regex = new Utf8Regex(pattern, options);
            var smallBytes = MissAllocation(regex, small);
            var largeBytes = MissAllocation(regex, large);
            Assert.True(largeBytes <= smallBytes + 4096, $"Miss allocation grew from {smallBytes} to {largeBytes} bytes.");
            var hitInput = Encoding.UTF8.GetBytes(hitText);
            var retained = Utf8MatchSequence.First(regex.CreateMatchCursor(hitInput));
            var expected = new Regex(pattern, options).Match(hitText);
            Assert.True(retained.Success);
            Utf8MatchSequence.AssertCaptures(expected, retained, hitInput, hitText);
            Assert.False(Utf8MatchSequence.First(regex.CreateMatchCursor(large)).Success);
            Utf8MatchSequence.AssertCaptures(expected, retained, hitInput, hitText);
            Assert.Throws<ArgumentException>(() => Utf8MatchSequence.First(regex.CreateMatchCursor(new byte[] { 0x61, 0xff })));
            using var canceled = new CancellationTokenSource();
            canceled.Cancel();
            Assert.Equal(canceled.Token, Assert.Throws<OperationCanceledException>(
                () => Utf8MatchSequence.First(regex.CreateMatchCursor(large, cancellationToken: canceled.Token))).CancellationToken);
        }
    }

    private static long MissAllocation(Utf8Regex regex, byte[] input)
    {
        for (var n = 0; n < 4; n++) CheckMiss(regex, input);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var n = 0; n < 4; n++) CheckMiss(regex, input);
        return (GC.GetAllocatedBytesForCurrentThread() - before) / 4;
    }

    private static void CheckMiss(Utf8Regex regex, byte[] input)
    {
        var miss = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
        if (miss.Success || !miss.GroupNumbers.IsEmpty || miss.Value.StartOffsetInBytes != 0 ||
            miss.Value.EndOffsetInBytes != 0 || !miss.GetValueMemory().IsEmpty || miss.TryGetGroup(0, out _))
            throw new InvalidOperationException("An unsuccessful capture result lost its empty byte-range contract.");
    }
}
