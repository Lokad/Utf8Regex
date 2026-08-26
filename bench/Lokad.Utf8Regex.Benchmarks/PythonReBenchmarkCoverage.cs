using System.Text;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Utf8Regex.Benchmarks;

internal static partial class PythonReBenchmarkReporter
{
    private static readonly string[] s_pythonReCoverageSections =
    [
        "Direct matching",
        "Detailed and scalar projections",
        "Count, FindAll, and FindIter",
        "Replace and Subn",
        "Split",
        "Real-corpus workloads",
        "Construction and first call",
        "Scaling evidence",
        "Comparator and semantic exclusions",
    ];

    private static int EmitPythonReCoverageReport()
    {
        var cases = PythonReBenchmarkCatalog.Cases;
        Console.WriteLine($"Catalog SHA-256     : {ComputePythonReCatalogSha256()}");
        Console.WriteLine($"Cases               : {cases.Count}");
        Console.WriteLine($"Distinct patterns   : {cases.Select(static item => item.Pattern).Distinct(StringComparer.Ordinal).Count()}");
        Console.WriteLine($"First sentinels     : {cases.Count(static item => item.Coverage.FirstMilestoneSentinel)}");
        PrintCoverageGroups("Sections", s_pythonReCoverageSections.Select(section => new KeyValuePair<string, int>(
            section,
            cases.Count(item => item.Coverage.Section.Equals(section, StringComparison.Ordinal)))));
        PrintCoverageGroups("Operations", Group(static item => item.Operation.ToString()));
        PrintCoverageGroups("Flags", Group(static item => item.Options.ToString()));
        PrintCoverageGroups("Feature families", Group(static item => item.Coverage.FeatureFamily));
        PrintCoverageGroups("Projection kinds", Group(static item => item.Coverage.ProjectionKind));
        PrintCoverageGroups("Managed routes", Group(static item => item.Coverage.IntendedManagedRouteClass));
        PrintCoverageGroups("Byte controls", Group(static item => item.Coverage.ByteControlExpectation));
        PrintCoverageGroups("Cardinalities", Group(static item => item.Coverage.ExpectedResultCardinality));
        PrintCoverageGroups("Input widths", Group(static item => GetPythonReInputWidthClass(item.Input)));
        PrintCoverageGroups("Claim classes", Group(static item => item.Coverage.ClaimClass));
        PrintCoverageGroups("Corpus sources", Group(static item => item.Coverage.CorpusProvenance));
        return 0;

        IEnumerable<KeyValuePair<string, int>> Group(Func<PythonReBenchmarkCase, string> selector) =>
            cases.GroupBy(selector, StringComparer.Ordinal)
                .OrderBy(static group => group.Key, StringComparer.Ordinal)
                .Select(static group => new KeyValuePair<string, int>(group.Key, group.Count()));
    }

    private static int VerifyPythonReCoverageContract()
    {
        var cases = PythonReBenchmarkCatalog.Cases;
        var failures = new List<string>();
        var duplicateIds = cases.GroupBy(static item => item.Id, StringComparer.Ordinal)
            .Where(static group => group.Count() != 1)
            .Select(static group => group.Key)
            .ToArray();
        if (duplicateIds.Length != 0)
        {
            failures.Add("Duplicate case IDs: " + string.Join(", ", duplicateIds));
        }

        foreach (var benchmarkCase in cases)
        {
            ValidatePythonReCoverageCase(benchmarkCase, failures);
        }

        if (cases.Count < 28)
        {
            failures.Add($"Catalog has {cases.Count} cases; the first milestone established 28 sentinels.");
        }

        if (cases.Count(static item => item.Coverage.FirstMilestoneSentinel) != 28)
        {
            failures.Add("Exactly 28 cases must remain marked as first-milestone sentinels.");
        }

        if (cases.Select(static item => item.Pattern).Distinct(StringComparer.Ordinal).Count() < 15)
        {
            failures.Add("Catalog must retain at least 15 distinct first-milestone patterns.");
        }

        var snapshot = LoadPythonReBenchmarkSnapshot();
        var catalogIds = GetPythonReCatalogCaseIds();
        if (snapshot.SchemaVersion != PythonReBenchmarkSchemaVersion ||
            !snapshot.CatalogSha256.Equals(ComputePythonReCatalogSha256(), StringComparison.Ordinal) ||
            !snapshot.CatalogCaseIds.SequenceEqual(catalogIds, StringComparer.Ordinal))
        {
            failures.Add("Snapshot schema, catalog fingerprint, or catalog order is stale.");
        }

        foreach (var benchmarkCase in cases)
        {
            if (!snapshot.Cases.TryGetValue(benchmarkCase.Id, out var measurement))
            {
                failures.Add($"Snapshot is missing catalog case '{benchmarkCase.Id}'.");
                continue;
            }

            if (measurement.Coverage != benchmarkCase.Coverage)
            {
                failures.Add($"Snapshot coverage metadata is stale for '{benchmarkCase.Id}'.");
            }
        }

        foreach (var snapshotId in snapshot.Cases.Keys.Except(catalogIds, StringComparer.Ordinal))
        {
            failures.Add($"Snapshot-only case '{snapshotId}' has no catalog definition.");
        }

        if (failures.Count != 0)
        {
            foreach (var failure in failures)
            {
                Console.Error.WriteLine(failure);
            }

            return 1;
        }

        Console.WriteLine(
            $"Verified PythonRe coverage contract: {cases.Count} cases, " +
            $"{cases.Select(static item => item.Pattern).Distinct(StringComparer.Ordinal).Count()} patterns, " +
            "28 first-milestone sentinels.");
        return 0;
    }

    private static void ValidatePythonReCoverageCase(
        PythonReBenchmarkCase benchmarkCase,
        ICollection<string> failures)
    {
        var coverage = benchmarkCase.Coverage;
        if (!s_pythonReCoverageSections.Contains(coverage.Section, StringComparer.Ordinal))
        {
            failures.Add($"{benchmarkCase.Id}: unknown section '{coverage.Section}'.");
        }

        if (string.IsNullOrWhiteSpace(coverage.FeatureFamily) ||
            string.IsNullOrWhiteSpace(coverage.InputShape) ||
            string.IsNullOrWhiteSpace(coverage.ProjectionKind) ||
            string.IsNullOrWhiteSpace(coverage.ComparatorOwner) ||
            string.IsNullOrWhiteSpace(coverage.CorpusProvenance))
        {
            failures.Add($"{benchmarkCase.Id}: coverage text fields must be non-empty.");
        }

        if (coverage.StartOffsetInBytes < 0 || coverage.ReplacementCount < -1 || coverage.MaxSplit < -1)
        {
            failures.Add($"{benchmarkCase.Id}: operation limits are outside the supported metadata range.");
        }

        if (coverage.ClaimClass is not "Public" and not "Composed" and not "Construction" and not "Scaling")
        {
            failures.Add($"{benchmarkCase.Id}: unknown claim class '{coverage.ClaimClass}'.");
        }

        var expectedByteControl = GetPythonReByteControlExpectation(benchmarkCase);
        if (!coverage.ByteControlExpectation.Equals(expectedByteControl, StringComparison.Ordinal))
        {
            failures.Add(
                $"{benchmarkCase.Id}: byte-control expectation is '{coverage.ByteControlExpectation}', " +
                $"expected '{expectedByteControl}'.");
        }
    }

    private static string GetPythonReByteControlExpectation(PythonReBenchmarkCase benchmarkCase)
    {
        if (benchmarkCase.Operation is not PythonReBenchmarkOperation.IsMatch and
            not PythonReBenchmarkOperation.Search and
            not PythonReBenchmarkOperation.Match and
            not PythonReBenchmarkOperation.FullMatch)
        {
            return "OperationExcluded";
        }

        if (benchmarkCase.Pattern.Any(static character => character > 0x7f) ||
            benchmarkCase.Input.Any(static character => character > 0x7f))
        {
            return "NonAscii";
        }

        return (benchmarkCase.Options & ~PythonReCompileOptions.Ascii) == PythonReCompileOptions.None
            ? "Eligible"
            : "FlagsExcluded";
    }

    private static string GetPythonReInputWidthClass(string input)
    {
        var hasAscii = false;
        var hasTwoByte = false;
        var hasThreeByte = false;
        var hasFourByte = false;
        foreach (var rune in input.EnumerateRunes())
        {
            if (rune.Value <= 0x7f)
            {
                hasAscii = true;
            }
            else if (rune.Value <= 0x7ff)
            {
                hasTwoByte = true;
            }
            else if (rune.Value <= 0xffff)
            {
                hasThreeByte = true;
            }
            else
            {
                hasFourByte = true;
            }
        }

        var widths = new List<string>(4);
        Add(hasAscii, "Ascii");
        Add(hasTwoByte, "TwoByte");
        Add(hasThreeByte, "ThreeByte");
        Add(hasFourByte, "FourByte");
        return string.Join('+', widths);

        void Add(bool present, string name)
        {
            if (present)
            {
                widths.Add(name);
            }
        }
    }

    private static void PrintCoverageGroups(
        string title,
        IEnumerable<KeyValuePair<string, int>> groups)
    {
        Console.WriteLine();
        Console.WriteLine(title + ":");
        foreach (var (name, count) in groups)
        {
            Console.WriteLine($"  {name,-38} {count}");
        }
    }
}
