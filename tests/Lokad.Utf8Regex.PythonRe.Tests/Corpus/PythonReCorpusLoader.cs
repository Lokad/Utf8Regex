using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lokad.Utf8Regex.PythonRe.Tests.Corpus;

public static class PythonReCorpusLoader
{
    private static readonly JsonSerializerOptions s_jsonOptions = CreateOptions();
    private const string CorpusFilePattern = "ported-*.json";

    public static IReadOnlyList<PythonReCorpusCase> LoadAll()
    {
        var corpusDirectory = ResolveCorpusDirectory();
        var files = Directory.GetFiles(corpusDirectory, CorpusFilePattern, SearchOption.TopDirectoryOnly);
        Array.Sort(files, StringComparer.Ordinal);
        var cases = new List<PythonReCorpusCase>();
        foreach (var file in files)
        {
            var json = File.ReadAllText(file);
            var loaded = JsonSerializer.Deserialize<PythonReCorpusCase[]>(json, s_jsonOptions)
                ?? throw new InvalidOperationException($"Failed to deserialize corpus file '{file}'.");
            cases.AddRange(loaded);
        }

        return cases;
    }

    public static IReadOnlyList<PythonReCorpusCase> LoadByStatus(PythonReCorpusCaseStatus status)
        => LoadAll().Where(c => c.Status == status).ToArray();

    private static string ResolveCorpusDirectory()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "Corpus");
        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException($"PythonRe corpus directory not found: '{directory}'.");
        }

        return directory;
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = false,
            ReadCommentHandling = JsonCommentHandling.Skip,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
