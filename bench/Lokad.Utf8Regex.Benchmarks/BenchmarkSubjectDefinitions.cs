using System.IO.Compression;
using System.Text;

namespace Lokad.Utf8Regex.Benchmarks;

internal readonly record struct BenchmarkSubjectDefinition(
    string Pattern,
    string Input,
    string Provenance);

internal static class BenchmarkSubjectDefinitions
{
    internal const string CodeLogMethodsPattern =
        @"\b(?:LogTrace|LogDebug|LogInformation|LogWarning|LogError)\b";
    internal const string CodeDeclarationsPattern =
        @"\b(?:record|struct|class)\s+[A-Z][A-Za-z0-9_]+";
    internal const string SherlockRussianPattern = "Шерлок Холмс";
    internal const string SherlockRussianRelativePath = @"opensubtitles\ru-sampled.txt";

    private const string DatePattern = @"\b\d{1,2}\/\d{1,2}\/\d{2,4}\b";
    private const string IpPattern =
        @"(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])";
    private const string MailEmailPattern = @"[\w\.+-]+@[\w\.-]+\.[\w\.-]+";

    private static readonly Lazy<string> s_mailNetworkCorpus = new(() => ReadGZipText(
        "DotNetPerformanceReplica/Public/Data",
        "mariomka.txt.gz"));
    private static readonly Lazy<string> s_codeCorpus = new(LoadCodeCorpus);
    private static readonly Lazy<string> s_sherlockRussianCorpus = new(() => File.ReadAllText(
        Path.Combine(
            BenchmarkDataFiles.GetDirectory("DotNetPerformanceReplica/Stress/Data"),
            SherlockRussianRelativePath),
        Encoding.UTF8));

    internal static string MailNetworkCorpus => s_mailNetworkCorpus.Value;

    internal static string CodeCorpus => s_codeCorpus.Value;

    internal static BenchmarkSubjectDefinition Get(string id) => id switch
    {
        "common/date-miss" => new(
            DatePattern,
            "Today is 11/18/201A and tomorrow is 11/19/201A.",
            "Shared definition common/date-miss"),
        "common/ip-match" => new(
            IpPattern,
            "012.200.033.199",
            "Shared definition common/ip-match"),
        "industry/mariomka-email-count" => new(
            MailEmailPattern,
            MailNetworkCorpus,
            "DotNetPerformanceReplica/Public/Data/mariomka.txt.gz"),
        "code/log-methods" => new(
            CodeLogMethodsPattern,
            CodeCorpus,
            "LokadReplica/Code/Data"),
        "code/declarations" => new(
            CodeDeclarationsPattern,
            CodeCorpus,
            "LokadReplica/Code/Data"),
        "sherlock/ru" => new(
            SherlockRussianPattern,
            s_sherlockRussianCorpus.Value,
            "DotNetPerformanceReplica/Stress/Data/opensubtitles/ru-sampled.txt"),
        _ => throw new ArgumentOutOfRangeException(nameof(id)),
    };

    private static string ReadGZipText(string relativeDirectory, string fileName)
    {
        var path = Path.Combine(BenchmarkDataFiles.GetDirectory(relativeDirectory), fileName);
        using var stream = File.OpenRead(path);
        using var gzip = new GZipStream(stream, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    private static string LoadCodeCorpus()
    {
        var dataRoot = BenchmarkDataFiles.GetDirectory("LokadReplica/Code/Data");
        var files = Directory.GetFiles(dataRoot, "*", SearchOption.AllDirectories)
            .OrderBy(static path => path, StringComparer.Ordinal)
            .ToArray();
        if (files.Length == 0)
        {
            throw new InvalidOperationException($"Lokad code data root is empty: {dataRoot}");
        }

        var builder = new StringBuilder(capacity: 2_000_000);
        foreach (var file in files)
        {
            var relativePath = Path.GetRelativePath(dataRoot, file).Replace('\\', '/');
            builder.Append("// file: ");
            builder.Append(relativePath);
            builder.Append('\n');
            builder.Append(File.ReadAllText(file, Encoding.UTF8));
            if (builder.Length == 0 || builder[^1] != '\n')
            {
                builder.Append('\n');
            }

            builder.Append('\n');
        }

        return builder.ToString();
    }
}
