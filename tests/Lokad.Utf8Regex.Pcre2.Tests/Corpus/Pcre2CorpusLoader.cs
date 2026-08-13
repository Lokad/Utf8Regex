using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lokad.Utf8Regex.Pcre2.Tests.Corpus;

public static class Pcre2CorpusLoader
{
    private static readonly JsonSerializerOptions s_jsonOptions = CreateOptions();
    private const string SharedPatternsFileName = "shared-patterns.json";
    private const string CorpusFilePattern = "ported-*.json";

    public static IReadOnlyList<Pcre2CorpusCase> LoadAll()
    {
        var corpusDirectory = ResolveCorpusDirectory();
        var files = Directory.GetFiles(corpusDirectory, CorpusFilePattern, SearchOption.TopDirectoryOnly);
        Array.Sort(files, StringComparer.Ordinal);
        var sharedPatterns = LoadSharedPatterns(corpusDirectory);

        var cases = new List<Pcre2CorpusCase>();
        foreach (var file in files)
        {
            var json = File.ReadAllText(file);
            var loaded = JsonSerializer.Deserialize<Pcre2CorpusCase[]>(json, s_jsonOptions)
                ?? throw new InvalidOperationException($"Failed to deserialize corpus file '{file}'.");
            cases.AddRange(loaded.Select(c => ResolveSharedPattern(c, sharedPatterns)));
        }

        return cases;
    }

    public static IReadOnlyList<Pcre2CorpusCase> LoadByStatus(Pcre2CorpusCaseStatus status)
    {
        return LoadAll().Where(c => c.Status == status).ToArray();
    }

    private static string ResolveCorpusDirectory()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "Corpus");
        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException($"PCRE2 corpus directory not found: '{directory}'.");
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

    private static IReadOnlyDictionary<string, string> LoadSharedPatterns(string corpusDirectory)
    {
        var path = Path.Combine(corpusDirectory, SharedPatternsFileName);
        if (!File.Exists(path))
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<Dictionary<string, string>>(json, s_jsonOptions)
            ?? throw new InvalidOperationException($"Failed to deserialize shared patterns file '{path}'.");
    }

    private static Pcre2CorpusCase ResolveSharedPattern(Pcre2CorpusCase corpusCase, IReadOnlyDictionary<string, string> sharedPatterns)
    {
        var hasInlinePattern = !string.IsNullOrWhiteSpace(corpusCase.Pattern);
        var hasPatternReference = !string.IsNullOrWhiteSpace(corpusCase.PatternRef);
        var hasBytePattern = !string.IsNullOrWhiteSpace(corpusCase.PatternBytesBase64);
        var payloadCount = (hasInlinePattern ? 1 : 0) + (hasPatternReference ? 1 : 0) + (hasBytePattern ? 1 : 0);
        if (payloadCount != 1)
        {
            throw new InvalidOperationException($"Corpus case '{corpusCase.Id}' must declare exactly one of Pattern, PatternRef, or PatternBytesBase64.");
        }

        if (corpusCase.PatternBytesBase64 is { } patternBytesBase64 && hasBytePattern)
        {
            if (!string.Equals(corpusCase.PatternEncoding, "Utf8BytesBase64", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Byte-pattern corpus case '{corpusCase.Id}' must use PatternEncoding 'Utf8BytesBase64'.");
            }

            try
            {
                _ = Convert.FromBase64String(patternBytesBase64);
            }
            catch (FormatException exception)
            {
                throw new InvalidOperationException($"Corpus case '{corpusCase.Id}' has an invalid base64 pattern payload.", exception);
            }

            return corpusCase;
        }

        if (string.IsNullOrWhiteSpace(corpusCase.PatternRef))
        {
            if (!string.Equals(corpusCase.PatternEncoding, "Utf16Text", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Text-pattern corpus case '{corpusCase.Id}' must use PatternEncoding 'Utf16Text'.");
            }

            return corpusCase;
        }

        if (hasInlinePattern)
        {
            throw new InvalidOperationException($"Corpus case '{corpusCase.Id}' cannot declare both Pattern and PatternRef.");
        }

        if (!sharedPatterns.TryGetValue(corpusCase.PatternRef, out var resolvedPattern))
        {
            throw new InvalidOperationException($"Corpus case '{corpusCase.Id}' references unknown shared pattern '{corpusCase.PatternRef}'.");
        }

        return new Pcre2CorpusCase
        {
            Id = corpusCase.Id,
            Pattern = resolvedPattern,
            PatternRef = corpusCase.PatternRef,
            PatternBytesBase64 = corpusCase.PatternBytesBase64,
            PatternEncoding = corpusCase.PatternEncoding,
            CompileOptions = corpusCase.CompileOptions,
            CompileSettings = corpusCase.CompileSettings,
            MatchOptions = corpusCase.MatchOptions,
            InputText = corpusCase.InputText,
            StartOffsetInBytes = corpusCase.StartOffsetInBytes,
            ReplacementPattern = corpusCase.ReplacementPattern,
            SubstitutionOptions = corpusCase.SubstitutionOptions,
            PartialMode = corpusCase.PartialMode,
            Operation = corpusCase.Operation,
            Expected = corpusCase.Expected,
            Status = corpusCase.Status,
            Tags = corpusCase.Tags,
            Source = corpusCase.Source,
            Notes = corpusCase.Notes,
        };
    }
}
