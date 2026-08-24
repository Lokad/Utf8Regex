using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class FusedAsciiBoundaryLiteralFamilyCountTests
{
    private const string Pattern = @"\b(?:LogTrace|LogDebug|LogInformation|LogWarning|LogError)\b";

    [Fact]
    public void CompiledPascalCaseFamilyFusesAsciiValidationWithCount()
    {
        var regex = new Utf8Regex(Pattern, RegexOptions.CultureInvariant | RegexOptions.Compiled);
        var input = Encoding.ASCII.GetBytes("LogTrace λ LogDebug_ LogInformation. LogWarning LogError");

        Assert.True(regex.Inspection.DebugHasFusedAsciiBoundaryLiteralFamilyCount);
        Assert.Equal(4, regex.Count(input));
        Assert.Equal(
            "compiled_fused_ascii_literal_family_count",
            regex.CollectCountDiagnostics(input).ExecutionRoute);
    }

    [Fact]
    public void FusedCountCoversEveryVectorLaneAndTheScalarTail()
    {
        var regex = new Utf8Regex(Pattern, RegexOptions.CultureInvariant | RegexOptions.Compiled);
        var expectedRegex = new Regex(
            Pattern,
            RegexOptions.CultureInvariant | RegexOptions.Compiled,
            Regex.InfiniteMatchTimeout);

        for (var offset = 0; offset < 96; offset++)
        {
            var input = Enumerable.Repeat((byte)'.', 128).ToArray();
            "LogInformation"u8.CopyTo(input.AsSpan(offset));

            Assert.Equal(expectedRegex.Count(Encoding.ASCII.GetString(input)), regex.Count(input));
        }
    }

    [Fact]
    public void FusedCountMatchesDotNetOnDeterministicAsciiSubjects()
    {
        var regex = new Utf8Regex(Pattern, RegexOptions.CultureInvariant | RegexOptions.Compiled);
        var expectedRegex = new Regex(
            Pattern,
            RegexOptions.CultureInvariant | RegexOptions.Compiled,
            Regex.InfiniteMatchTimeout);
        var random = new Random(0x51A7C11);
        string[] inserts =
        [
            "LogTrace",
            "LogDebug",
            "LogInformation",
            "LogWarning",
            "LogError",
            "xLogTrace",
            "LogError_",
            "LogUnknown",
        ];

        for (var subjectIndex = 0; subjectIndex < 256; subjectIndex++)
        {
            var input = new byte[random.Next(1, 513)];
            random.NextBytes(input);
            for (var i = 0; i < input.Length; i++)
            {
                input[i] = input[i] switch
                {
                    < 26 => (byte)('A' + input[i]),
                    < 52 => (byte)('a' + input[i] - 26),
                    < 62 => (byte)('0' + input[i] - 52),
                    < 70 => (byte)'_',
                    _ => (byte)'.',
                };
            }

            var insert = Encoding.ASCII.GetBytes(inserts[random.Next(inserts.Length)]);
            if (insert.Length <= input.Length)
            {
                insert.CopyTo(input, random.Next(input.Length - insert.Length + 1));
            }

            Assert.Equal(expectedRegex.Count(Encoding.ASCII.GetString(input)), regex.Count(input));
        }
    }

    [Fact]
    public void ValidNonAsciiInputRetainsTheGeneralSemanticRoute()
    {
        const string input = "é LogTrace λLogError LogWarning_ LogDebug.";
        var regex = new Utf8Regex(Pattern, RegexOptions.CultureInvariant | RegexOptions.Compiled);
        var bytes = Encoding.UTF8.GetBytes(input);

        Assert.Equal(
            Regex.Count(input, Pattern, RegexOptions.CultureInvariant | RegexOptions.Compiled),
            regex.Count(bytes));
        Assert.Equal("native_structural_family_emit", regex.CollectCountDiagnostics(bytes).ExecutionRoute);
    }

    [Fact]
    public void FusedCountDoesNotBypassMalformedUtf8Rejection()
    {
        var regex = new Utf8Regex(Pattern, RegexOptions.CultureInvariant | RegexOptions.Compiled);
        byte[] malformed = [.. "LogTrace "u8, 0xFF, .. " LogError"u8];

        Assert.Throws<ArgumentException>(() => regex.Count(malformed));
    }

    [Fact]
    public void FiniteTimeoutRetainsTheBudgetedCountRoute()
    {
        var regex = new Utf8Regex(
            Pattern,
            RegexOptions.CultureInvariant | RegexOptions.Compiled,
            TimeSpan.FromSeconds(1));
        var input = "LogTrace LogError"u8.ToArray();

        Assert.False(regex.Inspection.DebugHasFusedAsciiBoundaryLiteralFamilyCount);
        Assert.Equal(2, regex.Count(input));
    }

    [Theory]
    [InlineData(@"\b(?:logTrace|logDebug|logInformation|logWarning)\b")]
    [InlineData(@"\b(?:LogTrace|LogDebug|LogInformation)\b")]
    public void SelectorRejectsUnprovenFamilyShapes(string pattern)
    {
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant | RegexOptions.Compiled);

        Assert.False(regex.Inspection.DebugHasFusedAsciiBoundaryLiteralFamilyCount);
    }
}
