using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lokad.Utf8Regex.Pcre2.Tests.Corpus;

public static class Pcre2CorpusBacklogLoader
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };
    private const string BacklogFilePattern = "local-backlog-*.json";

    static Pcre2CorpusBacklogLoader()
    {
        s_jsonOptions.Converters.Add(new JsonStringEnumConverter());
    }

    public static IReadOnlyList<Pcre2CorpusBacklogEntry> LoadAll()
    {
        return LoadManifests()
            .SelectMany(static manifest => manifest.Entries)
            .ToArray();
    }

    public static IReadOnlyList<Pcre2CorpusBacklogManifest> LoadManifests()
    {
        var directory = ResolveCorpusDirectory();
        var files = Directory.GetFiles(directory, BacklogFilePattern, SearchOption.TopDirectoryOnly);
        Array.Sort(files, StringComparer.Ordinal);
        if (files.Length == 0)
        {
            throw new FileNotFoundException("Unable to locate any local PCRE2 backlog manifests.", Path.Combine(directory, BacklogFilePattern));
        }

        var manifests = new List<Pcre2CorpusBacklogManifest>(files.Length);
        foreach (var file in files)
        {
            var json = File.ReadAllText(file);
            var entries = JsonSerializer.Deserialize<Pcre2CorpusBacklogEntry[]>(json, s_jsonOptions)
                ?? throw new InvalidOperationException($"Failed to deserialize the local PCRE2 backlog manifest '{file}'.");
            manifests.Add(new Pcre2CorpusBacklogManifest
            {
                ManifestName = Path.GetFileNameWithoutExtension(file),
                Entries = entries,
            });
        }

        return manifests;
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

public sealed class Pcre2CorpusBacklogManifest
{
    public required string ManifestName { get; init; }

    public required IReadOnlyList<Pcre2CorpusBacklogEntry> Entries { get; init; }
}

public sealed class Pcre2CorpusBacklogEntry
{
    public required string Id { get; init; }

    public required Pcre2CorpusOperationKind Operation { get; init; }

    public required Pcre2CorpusCaseStatus Status { get; init; }

    public required string Source { get; init; }

    public required string Reason { get; init; }
}
