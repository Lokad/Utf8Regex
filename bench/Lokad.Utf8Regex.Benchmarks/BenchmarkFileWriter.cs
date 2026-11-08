using System.Text;

namespace Lokad.Utf8Regex.Benchmarks;

internal static class BenchmarkFileWriter
{
    internal static string ReplaceGeneratedSection(string document, string name, string markdown)
    {
        var begin = $"<!-- BEGIN GENERATED {name} BENCHMARKS -->";
        var end = $"<!-- END GENERATED {name} BENCHMARKS -->";
        var start = document.IndexOf(begin, StringComparison.Ordinal);
        var finish = document.IndexOf(end, StringComparison.Ordinal);
        if (start < 0 || finish < start ||
            start != document.LastIndexOf(begin, StringComparison.Ordinal) ||
            finish != document.LastIndexOf(end, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"README requires one ordered pair of {name} benchmark markers.");
        }

        return document[..start] + begin + Environment.NewLine + markdown.TrimEnd() +
            Environment.NewLine + end + document[(finish + end.Length)..];
    }

    internal static void WriteTextAtomically(string path, string contents)
    {
        var temporaryPath = path + ".tmp";
        try
        {
            File.WriteAllText(temporaryPath, contents, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }
}
