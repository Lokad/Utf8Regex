using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lokad.Utf8Regex.Pcre2.Tests.Corpus;

public static class Pcre2CorpusProvenanceLoader
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    static Pcre2CorpusProvenanceLoader()
    {
        s_jsonOptions.Converters.Add(new JsonStringEnumConverter());
    }

    public static IReadOnlyList<Pcre2CorpusProvenanceEntry> LoadAll()
    {
        var path = Path.Combine(ResolveCorpusDirectory(), "local-provenance-index.json");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Unable to locate the local PCRE2 provenance index.", path);
        }

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<Pcre2CorpusProvenanceEntry[]>(json, s_jsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize the local PCRE2 provenance index.");
    }

    private static string ResolveCorpusDirectory()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "Corpus");
        if (Directory.Exists(directory))
        {
            return directory;
        }

        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "tests", "Lokad.Utf8Regex.Pcre2.Tests", "Corpus");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate the PCRE2 corpus directory.");
    }
}

public sealed class Pcre2CorpusProvenanceEntry
{
    public required string Id { get; init; }

    public required string Source { get; init; }

    public required string SourceFile { get; init; }

    public required Pcre2CorpusOperationKind Operation { get; init; }

    public required Pcre2CorpusCaseStatus Status { get; init; }
}
