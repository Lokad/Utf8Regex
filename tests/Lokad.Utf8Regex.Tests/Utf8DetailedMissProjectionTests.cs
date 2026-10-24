using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8DetailedMissProjectionTests
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
    public void MissesAvoidInputSizedProjectionAndKeepHitsAndValidation(string pattern, string hitText)
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
            var retained = regex.MatchDetailed(hitInput);
            var reference = new Utf8Regex(pattern, options, Regex.InfiniteMatchTimeout,
                new Utf8ResourcePolicy { MaxWorkSteps = 1_000_000 }).MatchDetailed(hitInput);
            Assert.True(retained.Success);
            Assert.Equal(reference.GetValueString(), retained.GetValueString());
            Assert.Equal((reference.IndexInBytes, reference.LengthInBytes, reference.IndexInUtf16, reference.LengthInUtf16),
                (retained.IndexInBytes, retained.LengthInBytes, retained.IndexInUtf16, retained.LengthInUtf16));
            Assert.False(regex.MatchDetailed(large).Success);
            for (var number = 0; number < retained.GroupCount; number++)
            {
                var group = retained.GetGroup(number);
                var expected = reference.GetGroup(number);
                Assert.Equal(expected.Success, group.Success);
                Assert.Equal(expected.GetValueString(), group.GetValueString());
                Assert.Equal(expected.CaptureCount, group.CaptureCount);
            }
            Assert.Throws<ArgumentException>(() => regex.MatchDetailed([0x61, 0xff]));
            using var canceled = new CancellationTokenSource();
            canceled.Cancel();
            Assert.Equal(canceled.Token, Assert.Throws<OperationCanceledException>(
                () => regex.MatchDetailed(large, canceled.Token)).CancellationToken);
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
        var miss = regex.MatchDetailed(input);
        if (miss.Success || miss.GroupCount != 1 || miss.IndexInBytes != 0 || miss.LengthInBytes != 0 ||
            !miss.TryGetByteRange(out var start, out var length) || start != 0 || length != 0 ||
            !miss.TryGetValueBytes(out var value) || !value.IsEmpty || miss.GetGroup(0).Success)
            throw new InvalidOperationException("An unsuccessful detailed result lost its empty group-zero contract.");
    }
}
