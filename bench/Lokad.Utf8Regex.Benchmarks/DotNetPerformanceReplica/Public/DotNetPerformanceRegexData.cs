using System.IO.Compression;
using System.Text;

namespace Lokad.Utf8Regex.Benchmarks;

internal static class DotNetPerformanceRegexData
{
    private static readonly Lazy<string> s_commonSearchText = new(BuildCommonSearchText);
    private static readonly Lazy<string> s_mailNetworkCorpus = new(() => ReadGZipText("mariomka.txt.gz"));
    private static readonly Lazy<string> s_detectiveCorpus = new(() => ReadGZipText("sherlock.txt.gz"));
    private static readonly Lazy<string> s_riverCorpus = new(() => ReadGZipText("3200.txt.gz"));

    public static string CommonSearchText => s_commonSearchText.Value;

    public static string MailNetworkCorpus => s_mailNetworkCorpus.Value;

    public static string DetectiveCorpus => s_detectiveCorpus.Value;

    public static string RiverCorpus => s_riverCorpus.Value;

    private static string BuildCommonSearchText()
    {
        const string paragraph =
            "Vitae magna tempus nibh, sed semper arcu posuere a. " +
            "Tempus erat at magna aliquet, non feugiat nunc semper. " +
            "Aliquam magna vel lectus tempus placerat, sed semper mi dictum. ";

        var builder = new StringBuilder(8_192);
        while (builder.Length < 8_192)
        {
            builder.Append(paragraph);
        }

        return builder.ToString();
    }

    private static string ReadGZipText(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "DotNetPerformanceReplica", "Public", "Data", fileName);
        using var stream = File.OpenRead(path);
        using var gzip = new GZipStream(stream, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }
}
