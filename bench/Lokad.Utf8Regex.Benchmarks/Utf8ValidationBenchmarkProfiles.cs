using System.Text;

namespace Lokad.Utf8Regex.Benchmarks;

internal static class Utf8ValidationBenchmarkProfiles
{
    public static byte[] Create(string profileName)
    {
        return profileName switch
        {
            "ascii-small" => Encoding.UTF8.GetBytes(BuildRepeatedToByteLength("namespace Demo.Service;\npublic sealed class Worker {}\n", 4 * 1024)),
            "ascii-large" => Encoding.UTF8.GetBytes(BuildRepeatedToByteLength("namespace Demo.Service;\npublic sealed class Worker {}\n", 512 * 1024)),
            "two-byte-small" => Encoding.UTF8.GetBytes(BuildRepeatedToByteLength("Шерлок Холмс и доктор Ватсон.\n", 4 * 1024)),
            "two-byte-large" => Encoding.UTF8.GetBytes(BuildRepeatedToByteLength("Шерлок Холмс и доктор Ватсон.\n", 512 * 1024)),
            "three-byte-small" => Encoding.UTF8.GetBytes(BuildRepeatedToByteLength("夏洛克福尔摩斯与华生医生。\n", 4 * 1024)),
            "three-byte-large" => Encoding.UTF8.GetBytes(BuildRepeatedToByteLength("夏洛克福尔摩斯与华生医生。\n", 512 * 1024)),
            "mixed-small" => Encoding.UTF8.GetBytes(BuildRepeatedToByteLength("Sherlock Холмс 夏洛克 Holmes.\n", 4 * 1024)),
            "mixed-large" => Encoding.UTF8.GetBytes(BuildRepeatedToByteLength("Sherlock Холмс 夏洛克 Holmes.\n", 512 * 1024)),
            "dotnet-performance-ru-live" => LoadRebarProfile(@"opensubtitles\ru-sampled.txt"),
            "dotnet-performance-zh-live" => LoadRebarProfile(@"opensubtitles\zh-sampled.txt"),
            _ => throw new ArgumentOutOfRangeException(nameof(profileName), profileName, "Unknown UTF-8 validation benchmark profile."),
        };
    }

    public static string[] GetProfileNames()
    {
        return
        [
            "ascii-small",
            "ascii-large",
            "two-byte-small",
            "two-byte-large",
            "three-byte-small",
            "three-byte-large",
            "mixed-small",
            "mixed-large",
            "dotnet-performance-ru-live",
            "dotnet-performance-zh-live",
        ];
    }

    private static string BuildRepeatedToByteLength(string seed, int targetBytes)
    {
        var builder = new StringBuilder(targetBytes);
        while (Encoding.UTF8.GetByteCount(builder.ToString()) < targetBytes)
        {
            builder.Append(seed);
        }

        return builder.ToString();
    }

    private static byte[] LoadRebarProfile(string relativePath)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "DotNetPerformanceReplica", "Stress", "Data", relativePath);
        return File.ReadAllBytes(path);
    }
}
