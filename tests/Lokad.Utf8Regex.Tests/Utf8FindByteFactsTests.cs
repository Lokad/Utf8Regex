using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8FindByteFactsTests
{
    [Fact]
    public void MandatoryLiteralFactsCoverIndependentScalarMatchesAndByteWidths()
    {
        var cases = Utf8OwnedFindOptimizationsTests.LiteralCases().Select(row =>
            (Pattern: Assert.IsType<string>(row[0]), Options: RegexOptions.CultureInvariant))
            .Concat(Utf8OwnedFindOptimizationsTests.PrefixCases().Select(row =>
                (Assert.IsType<string>(row[0]), Assert.IsType<RegexOptions>(row[1]))))
            .Concat(new[] { (".ab|.c", RegexOptions.CultureInvariant),
                ("(?i:ab)|cd", RegexOptions.CultureInvariant), ("😀[ab]cd", RegexOptions.CultureInvariant),
                ("(?i:K)[ab]cd", RegexOptions.CultureInvariant), ("(?i:😀)", RegexOptions.CultureInvariant),
                ("x(?:ab|)y", RegexOptions.CultureInvariant), ("[😀🙏]{2}x", RegexOptions.CultureInvariant),
                ("(a)?(?(1)bc|d)", RegexOptions.CultureInvariant), ("(?(?=a)ab|cd)", RegexOptions.CultureInvariant) })
            .Select(row => (Pattern: row.Item1, Options: row.Item2)).Distinct().ToArray();
        var subjects = new[] { "", "a", "b", "c", "ab", "cd", "ef", "def", "abc", "Ab", "AB", "aB", "Cd",
            "!ab!c", "aXbcdef", "abcde", "foobar", "foobaz", "aaaaaaaab", "abababcdcd", "abcabcabcdcd",
            "xay", "xy", "xabcy", "xababy", "😀", "😀acd", "😀bcd", "😀😀x", "😀🙏x", "🙏😀x",
            "Kacd", "Kacd", "kacd", "x😀😀y", "a\nb", "\0", "ad", "bd", "bc", "d" }
            .Select(Encoding.UTF8.GetBytes).ToArray();
        var observed = 0;
        foreach (var row in cases)
        foreach (var rtl in new[] { RegexOptions.None, RegexOptions.RightToLeft })
        {
            // ECMAScript forbids RTL; keep the catalog's valid option profile.
            if ((row.Options & RegexOptions.ECMAScript) != 0 && rtl != RegexOptions.None) continue;
            var options = row.Options | rtl;
            var tree = Utf8OwnedTreeParser.TryParse(row.Pattern, options);
            Assert.NotNull(tree);
            var leading = Utf8OwnedFindOptimizations.TryExtractLeadingLiteral(tree.Root);
            var mandatory = Utf8OwnedFindOptimizations.TryExtractLongestLiteral(tree.Root);
            var fixedLiteral = Utf8OwnedFindOptimizations.TryExtractFixedDistanceLiteral(tree.Root);
            var widths = Utf8OwnedFindOptimizations.ComputeByteWidth(tree.Root);
            var program = Utf8OwnedTreeSliceLowerer.TryLower(tree, options);
            Assert.NotNull(program);
            var starts = new int[program.GroupCount];
            var ends = new int[program.GroupCount];
            foreach (var input in subjects)
            for (var boundary = 0; boundary <= input.Length; boundary++)
            {
                if (boundary < input.Length && (input[boundary] & 0xc0) == 0x80) continue;
                var invocation = new Utf8SliceInvocation(new Utf8ExecutionLimits(100_000, 0, 0), Regex.InfiniteMatchTimeout, CancellationToken.None);
                // Direct VM probes never consult Find facts or candidate search.
                var match = Utf8SliceRunner.TryMatchAt(program, input, boundary, starts, ends, true, ref invocation);
                Assert.Equal(Utf8ExecutionLimitKind.None, match.Breach);
                if (!match.IsMatch) continue;
                observed++;
                var start = Math.Min(boundary, match.EndIndex);
                var length = Math.Abs(boundary - match.EndIndex);
                var value = Encoding.UTF8.GetString(input.AsSpan(start, length));
                Assert.True(length >= widths.Minimum, $"Unsound minimum: {row.Pattern} / {options}");
                if (widths.Maximum is int maximum) Assert.True(length <= maximum);
                if (leading is not null) Assert.True(value.StartsWith(leading, StringComparison.Ordinal), $"Unsound leading literal: {row.Pattern} / {options}");
                if (mandatory is not null) Assert.True(value.Contains(mandatory, StringComparison.Ordinal), $"Unsound mandatory literal: {row.Pattern} / {options}");
                if (fixedLiteral is { } fact)
                {
                    Assert.InRange(fact.Distance, 0, length);
                    Assert.True(input.AsSpan(start + fact.Distance, length - fact.Distance).StartsWith(Encoding.UTF8.GetBytes(fact.Literal)),
                        $"Unsound fixed-byte literal: {row.Pattern} / {options}");
                }
            }
        }
        Assert.True(observed > 100);
    }

    [Fact]
    public void PublicSearchKeepsEveryAlternativeAndScopedCaseAtScalarByteRanges()
    {
        var rows = new (string Pattern, string Subject, (int Start, int Length)[] Matches)[]
        {
            (".ab|.c", "!ab!c", [(0, 3), (3, 2)]),
            ("(?:ab|)", "X", [(0, 0), (1, 0)]),
            ("(?i)ab|cd", "!AB!CD", [(1, 2), (4, 2)]),
            ("(?i:ab)|cd", "!AB!cd", [(1, 2), (4, 2)]),
            (".(?i:ab)|.cd", "!AB!cd", [(0, 3), (3, 3)]),
            ("(?i:ab)c", "!ABc!aBc", [(1, 3), (5, 3)]),
            ("😀[ab]cd", "!😀acd!😀bcd", [(1, 7), (9, 7)]),
            ("(?i:K)[ab]cd", "!kacd!Kbcd", [(1, 4), (6, 6)]),
            ("[😀🙏]{2}x", "!😀🙏x!🙏😀x", [(1, 9), (11, 9)]),
            ("ab.|ab\\d", "!ab7!abX", [(1, 3), (5, 3)]),
        };
        foreach (var row in rows)
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft,
            RegexOptions.Compiled | RegexOptions.RightToLeft })
        {
            var input = Encoding.UTF8.GetBytes(row.Subject);
            var regex = new Utf8Regex(row.Pattern, RegexOptions.CultureInvariant | mode);
            var expected = (mode & RegexOptions.RightToLeft) == 0 ? row.Matches : row.Matches.Reverse().ToArray();
            Assert.True(regex.IsMatch(input));
            Assert.Equal(expected.Length, regex.Count(input));
            var actual = new List<(int, int)>();
            foreach (var match in regex.EnumerateMatches(input)) actual.Add((match.IndexInBytes, match.LengthInBytes));
            Assert.Equal(expected, actual);
            var first = regex.Match(input);
            Assert.True(first.Success);
            Assert.Equal(expected[0], (first.IndexInBytes, first.LengthInBytes));
        }
    }

    [Fact]
    public void ExactLiteralProducersDeclineMalformedTextAndChangingCaseFibers()
    {
        foreach (var text in new[] { "\ud800", "\udfff", "a\ud800b", "\udc00😀" })
        {
            var node = Utf8TreeNode.Create(Utf8TreeNodeKind.Multi, RegexOptions.CultureInvariant, text);
            Assert.Null(Utf8OwnedFindOptimizations.TryExtractLeadingLiteral(node));
            Assert.Null(Utf8OwnedFindOptimizations.TryExtractLongestLiteral(node));
            Assert.Null(Utf8OwnedFindOptimizations.TryExtractFixedDistanceLiteral(node));
        }
        foreach (var value in new[] { '\ud800', '\udfff' })
        {
            var node = Utf8TreeNode.Create(Utf8TreeNodeKind.One, RegexOptions.CultureInvariant, value);
            Assert.Null(Utf8OwnedFindOptimizations.TryExtractLeadingLiteral(node));
            Assert.Null(Utf8OwnedFindOptimizations.TryExtractLongestLiteral(node));
        }
        foreach (var text in new[] { "ab", "K", "I", "😀" })
        {
            var node = Utf8TreeNode.Create(Utf8TreeNodeKind.Multi, RegexOptions.IgnoreCase, text);
            Assert.Null(Utf8OwnedFindOptimizations.TryExtractLeadingLiteral(node));
            Assert.Null(Utf8OwnedFindOptimizations.TryExtractLongestLiteral(node));
            Assert.Null(Utf8OwnedFindOptimizations.TryExtractFixedDistanceLiteral(node));
        }
        var punctuation = Utf8TreeNode.Create(Utf8TreeNodeKind.Multi, RegexOptions.IgnoreCase, "12!😀");
        Assert.Null(Utf8OwnedFindOptimizations.TryExtractLeadingLiteral(punctuation));
        punctuation.Str = "12!";
        Assert.Equal("12!", Utf8OwnedFindOptimizations.TryExtractLeadingLiteral(punctuation));
        Assert.Equal("12!", Utf8OwnedFindOptimizations.TryExtractLongestLiteral(punctuation));
        var unknown = Utf8TreeNode.Create((Utf8TreeNodeKind)255, RegexOptions.None);
        Assert.Equal(new Utf8OwnedFindOptimizations.ByteWidth(0, null), Utf8OwnedFindOptimizations.ComputeByteWidth(unknown));
    }

    [Fact]
    public void LiteralCertificatesRecoverEveryWorkCutoffWithParentLoansIntact()
    {
        foreach (var pattern in new[] { "a😀b", "ab.|ab\\d", "(?i:ab)c", "😀[ab]cd" })
        {
            var tree = Utf8OwnedTreeParser.TryParse(pattern, RegexOptions.CultureInvariant);
            Assert.NotNull(tree);
            for (var route = 0; route < 4; route++)
            {
                ulong work;
                object? expected;
                using (var measured = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
                {
                    expected = ProduceLiteral(tree.Root, route);
                    work = measured.Budget.WorkSteps;
                }
                Assert.InRange(work, 1ul, 10_000ul);
                for (ulong cutoff = 0; cutoff <= work; cutoff++)
                {
                    using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, cutoff + 2, ulong.MaxValue);
                    var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
                    var checkpoint = owner.Budget.ObjectCheckpoint;
                    Exception? failure;
                    using (var child = Utf8ConstructionScope.Enter()) failure = Record.Exception(() => Assert.Equal(expected, ProduceLiteral(tree.Root, route)));
                    if (cutoff == work) Assert.Null(failure);
                    else Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, Assert.IsType<Utf8ResourceLimitExceededException>(failure).ResourceKind);
                    Assert.Equal(112ul, owner.Budget.ReservedBytes);
                    Assert.Equal(0ul, owner.Budget.Reserved);
                    Assert.Equal(0ul, owner.Budget.Depth);
                    Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
                    Assert.Equal(17, parent.Value);
                    Assert.InRange(owner.Budget.WorkSteps, 2ul, cutoff + 2);
                }
            }
        }

        static object? ProduceLiteral(Utf8TreeNode node, int route) => route switch
        {
            0 => Utf8OwnedFindOptimizations.TryExtractLeadingLiteral(node),
            1 => Utf8OwnedFindOptimizations.TryExtractLongestLiteral(node),
            2 => Utf8OwnedFindOptimizations.TryExtractFixedDistanceLiteral(node),
            _ => Utf8OwnedFindOptimizations.ComputeByteWidth(node),
        };
    }

    private sealed class ParentValue(int value) { internal readonly int Value = value; }

    [Fact]
    public void CoveringFamiliesKeepStoppedBranchesScopedCaseAndScalarByteRanges()
    {
        (string Pattern, string Subject, (int Start, int Length)[] Matches)[] cases =
        [
            (@"ab|cd\d|ef", "xab!xcd7!xef", [(1, 2), (5, 3), (10, 2)]),
            (@"^ab|cd\d|ef", "ab!cd7!ef", [(0, 2), (3, 3), (7, 2)]),
            (@"(ef|a.cd|gh)", "ef!aXcd!gh", [(0, 2), (3, 4), (8, 2)]),
            (@"(a|(b|c))d", "ad!bd!cd", [(0, 2), (3, 2), (6, 2)]),
            (@"(?i:ab)c|def", "ABc!aBc!abc!def", [(0, 3), (4, 3), (8, 3), (12, 3)]),
            (@"(?:ab){2}c", "xababc!ababc", [(1, 5), (7, 5)]),
            ("(?:😀\\d|🙏|ab)", "😀8!🙏!ab", [(0, 5), (6, 4), (11, 2)]),
            (@"ab\p{L}{0}cd|ef", "abcd!ef", [(0, 4), (5, 2)]),
        ];
        foreach (var row in cases)
        foreach (var options in new[] { RegexOptions.CultureInvariant,
            RegexOptions.CultureInvariant | RegexOptions.Compiled,
            RegexOptions.CultureInvariant | RegexOptions.RightToLeft,
            RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.RightToLeft })
        {
            var tree = Utf8OwnedTreeParser.TryParse(row.Pattern, options);
            Assert.NotNull(tree);
            var prefixes = Utf8OwnedFindOptimizations.FindAlternatePrefixes(tree.Root, options);
            Assert.NotNull(prefixes);
            var input = Encoding.UTF8.GetBytes(row.Subject);
            var regex = new Utf8Regex(row.Pattern, options);
            Assert.Equal(row.Matches.Length, regex.Count(input));
            var expected = (options & RegexOptions.RightToLeft) == 0 ? row.Matches : row.Matches.Reverse().ToArray();
            var matches = new List<(int, int)>();
            foreach (var match in regex.EnumerateMatches(input)) matches.Add((match.IndexInBytes, match.LengthInBytes));
            Assert.Equal(expected, matches);
            var first = regex.Match(input);
            Assert.True(first.Success);
            Assert.Equal(expected[0], (first.IndexInBytes, first.LengthInBytes));
            foreach (var range in row.Matches)
            {
                var value = Encoding.UTF8.GetString(input.AsSpan(range.Start, range.Length));
                Assert.Contains(prefixes, prefix => value.StartsWith(prefix, StringComparison.Ordinal));
            }
        }
    }

    [Fact]
    public void EveryReturnedFamilyCoversIndependentScalarProgramMatches()
    {
        var subjects = new List<string> { "", "a", "b", "c", "d", "e", "f", "ab", "abc", "cd", "ef", "gh",
            "aXcd", "aXbcdef", "abXcd", "cd7", "abcd", "abab", "ababc", "ababab", "abcdcde", "abababcdcd",
            "ad", "bd", "ac", "bc", "xy", "xz", "AbC", "aBc", "ABc", "abC", "def", "foo", "foobar", "foobaz",
            "xÅ", "xa", "xb", "😀", "🙏", "😀8", "x😀😀y", "x😀🙏y", "a😀b", "K", "k", "K", "I", "i", "a\nb", "\n", "\0",
            "aaaaaaaab", "abaaaaaac", "xaaaaaaab", new string('a', 32) + "b" };
        foreach (var a in "abc") foreach (var b in "abc") foreach (var c in "abc") subjects.Add(new string([a, b, c]));
        var inputs = subjects.Select(Encoding.UTF8.GetBytes).ToArray();
        var observedMatches = 0;
        foreach (var row in Utf8OwnedFindOptimizationsTests.PrefixCases())
        {
            var pattern = Assert.IsType<string>(row[0]);
            var options = Assert.IsType<RegexOptions>(row[1]);
            var tree = Utf8OwnedTreeParser.TryParse(pattern, options);
            Assert.NotNull(tree);
            var prefixes = Utf8OwnedFindOptimizations.FindAlternatePrefixes(tree.Root, options);
            if (prefixes is null) continue;
            var program = Utf8OwnedTreeSliceLowerer.TryLower(tree, options);
            Assert.NotNull(program);
            var starts = new int[program.GroupCount];
            var ends = new int[program.GroupCount];
            foreach (var input in inputs)
            for (var boundary = 0; boundary <= input.Length; boundary++)
            {
                if (boundary < input.Length && (input[boundary] & 0xc0) == 0x80) continue;
                var invocation = new Utf8SliceInvocation(new Utf8ExecutionLimits(100_000, 0, 0), Regex.InfiniteMatchTimeout, CancellationToken.None);
                // The VM scans every scalar boundary directly: it never uses
                // these Find families or native candidate search as its oracle.
                var found = Utf8SliceRunner.TryMatchAt(program, input, boundary, starts, ends, true, ref invocation);
                Assert.Equal(Utf8ExecutionLimitKind.None, found.Breach);
                if (!found.IsMatch) continue;
                observedMatches++;
                var start = Math.Min(boundary, found.EndIndex);
                var value = Encoding.UTF8.GetString(input.AsSpan(start, Math.Abs(found.EndIndex - boundary)));
                if ((options & RegexOptions.IgnoreCase) != 0) value = AsciiFold(value);
                Assert.True(prefixes.Any(prefix => value.StartsWith((options & RegexOptions.IgnoreCase) != 0 ? AsciiFold(prefix) : prefix, StringComparison.Ordinal)),
                    $"Uncovered scalar match for {pattern} / {options}: {value}");
            }
        }
        Assert.True(observedMatches > 100);

        static string AsciiFold(string value) => string.Create(value.Length, value, static (output, source) =>
        {
            for (var index = 0; index < source.Length; index++)
                output[index] = source[index] is >= 'A' and <= 'Z' ? (char)(source[index] + 32) : source[index];
        });
    }

    [Fact]
    public void PrefixFamiliesAndFactsRecoverEveryWorkCutoffAndRootAdmissionDenial()
    {
        foreach (var pattern in new[] { @"ab\d|cd\d|ef", @"(?i:ab)c|def", "x[😀🙏]{2}y", @"(?:ab|cd){2,3}ef" })
        {
            var tree = Utf8OwnedTreeParser.TryParse(pattern, RegexOptions.CultureInvariant);
            Assert.NotNull(tree);
            foreach (var facts in new[] { false, true })
            {
                ulong work;
                using (var measured = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
                {
                    var result = Produce(tree, facts);
                    Assert.NotNull(result);
                    work = measured.Budget.WorkSteps;
                }
                Assert.InRange(work, 1ul, 20_000ul);
                for (ulong cutoff = 0; cutoff <= work; cutoff++)
                {
                    using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, cutoff + 2, ulong.MaxValue);
                    var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
                    var checkpoint = owner.Budget.ObjectCheckpoint;
                    if (cutoff < work)
                    {
                        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Produce(tree, facts));
                        Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, failure.ResourceKind);
                        Assert.Equal(112ul, owner.Budget.ReservedBytes);
                        Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
                    }
                    else Assert.NotNull(Produce(tree, facts));
                    Assert.Equal(17, parent.Value);
                    Assert.Equal(0ul, owner.Budget.Depth);
                    Assert.InRange(owner.Budget.WorkSteps, 2ul, cutoff + 2);
                }
                foreach (var depthDenial in new[] { false, true })
                {
                    using var owner = new Utf8ConstructionScope(0, depthDenial ? ulong.MaxValue : 112, ulong.MaxValue, depthDenial ? 0ul : ulong.MaxValue);
                    var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
                    var checkpoint = owner.Budget.ObjectCheckpoint;
                    var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Produce(tree, facts));
                    Assert.Equal(depthDenial ? Utf8ResourceLimitKind.ConstructionDepth : Utf8ResourceLimitKind.ConstructionBytes, failure.ResourceKind);
                    Assert.Equal(112ul, owner.Budget.ReservedBytes);
                    Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
                    Assert.Equal(0ul, owner.Budget.Depth);
                    Assert.Equal(17, parent.Value);
                    if (depthDenial && !facts)
                    {
                        Assert.Equal(112ul, owner.Budget.PeakBytes);
                        Assert.Equal(2ul, owner.Budget.WorkSteps);
                    }
                }
            }
        }
        var declined = Utf8OwnedTreeParser.TryParse(@"ab|\d", RegexOptions.CultureInvariant);
        Assert.NotNull(declined);
        using var recovery = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
        var held = recovery.Budget.AllocateObject(17, static value => new ParentValue(value));
        var heldCheckpoint = recovery.Budget.ObjectCheckpoint;
        Assert.Null(Utf8OwnedFindOptimizations.FindAlternatePrefixes(declined.Root, declined.Options));
        Assert.Equal(112ul, recovery.Budget.ReservedBytes);
        Assert.Same(heldCheckpoint, recovery.Budget.ObjectCheckpoint);
        Assert.Equal(0ul, recovery.Budget.Depth);
        Assert.True(recovery.Budget.WorkSteps > 2);
        Assert.Equal(17, held.Value);

        static object? Produce(Utf8OwnedRegexTree tree, bool facts) => facts
            ? Utf8OwnedFindFacts.FromTree(tree) : Utf8OwnedFindOptimizations.FindAlternatePrefixes(tree.Root, tree.Options);
    }

    [Fact]
    public void LargeCoveringUnionsUseLinearWorkAndBoundedScratch()
    {
        var small = Measure(1024);
        var large = Measure(2048);
        Assert.Equal(1024, small.Count);
        Assert.Equal(2048, large.Count);
        Assert.True(large.Work < small.Work * 3, $"work: {small.Work} / {large.Work}");
        Assert.True(large.Peak < small.Peak * 3, $"peak bytes: {small.Peak} / {large.Peak}");
        Assert.InRange(large.Peak, 1ul, 4ul * 1024 * 1024);

        static (ulong Work, ulong Peak, int Count) Measure(int count)
        {
            var pattern = string.Join('|', Enumerable.Repeat(@"(a+)(b+)", count));
            var tree = Utf8OwnedTreeParser.TryParse(pattern, RegexOptions.CultureInvariant);
            Assert.NotNull(tree);
            using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
            var prefixes = Utf8OwnedFindOptimizations.FindAlternatePrefixes(tree.Root, tree.Options);
            Assert.NotNull(prefixes);
            Assert.All(prefixes, prefix => Assert.Equal("a", prefix));
            return (owner.Budget.WorkSteps, owner.Budget.PeakBytes, prefixes.Length);
        }
    }

    [Fact]
    public void CanonicalPrefixesCoverFoldedClassesAndCompleteSupplementaryScalars()
    {
        var folded = Utf8OwnedTreeParser.TryParse("x[ab]", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        Assert.NotNull(folded);
        Assert.Equal(new[] { "xa", "xb" }, Utf8OwnedFindOptimizations.FindAlternatePrefixes(folded.Root, folded.Options));
        var supplementary = Utf8OwnedTreeParser.TryParse("x[😀🙏]", RegexOptions.CultureInvariant);
        Assert.NotNull(supplementary);
        Assert.Equal(new[] { "x😀", "x🙏" }, Utf8OwnedFindOptimizations.FindAlternatePrefixes(supplementary.Root, supplementary.Options));
        var scalar = Utf8OwnedTreeParser.TryParse("[😀]", RegexOptions.CultureInvariant);
        Assert.NotNull(scalar);
        var repeat = Utf8TreeNode.CreateSetRepeat(scalar.Root.Child(0), RegexOptions.CultureInvariant, 9, 9, false);
        var concat = Utf8TreeNode.Create(Utf8TreeNodeKind.Concatenate, RegexOptions.CultureInvariant);
        concat.AddChild(Utf8TreeNode.Create(Utf8TreeNodeKind.One, RegexOptions.CultureInvariant, 'x'));
        concat.AddChild(repeat);
        concat.AddChild(Utf8TreeNode.Create(Utf8TreeNodeKind.One, RegexOptions.CultureInvariant, 'y'));
        Assert.Equal(new[] { "x" + string.Concat(Enumerable.Repeat("😀", 7)) },
            Utf8OwnedFindOptimizations.FindAlternatePrefixes(concat, RegexOptions.CultureInvariant));

        foreach (var options in new[] { RegexOptions.CultureInvariant, RegexOptions.CultureInvariant | RegexOptions.Compiled,
            RegexOptions.CultureInvariant | RegexOptions.RightToLeft,
            RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.RightToLeft })
        {
            var regex = new Utf8Regex("x[ab]", options | RegexOptions.IgnoreCase);
            foreach (var input in new[] { "!XA!", "!XB!", "!xa!", "!xb!" })
            {
                var match = regex.Match(Encoding.UTF8.GetBytes(input));
                Assert.True(match.Success);
                Assert.Equal(1, match.IndexInBytes);
                Assert.Equal(2, match.LengthInBytes);
            }
            var wide = new Utf8Regex("x[😀🙏]", options);
            Assert.Equal(2, wide.Count("!x😀!x🙏!"u8));
            var matchWide = wide.Match("!x😀!x🙏!"u8);
            Assert.True(matchWide.Success);
            Assert.Equal((options & RegexOptions.RightToLeft) == 0 ? 1 : 7, matchWide.IndexInBytes);
            Assert.Equal(5, matchWide.LengthInBytes);
        }
    }

    [Fact]
    public void CanonicalFixedMasksKeepCaseWidthProofAndRecoverConstructionAdmission()
    {
        var tree = Utf8OwnedTreeParser.TryParse("😀[ab][0-9]", RegexOptions.CultureInvariant);
        Assert.NotNull(tree);
        ulong work;
        using (var measure = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
        {
            var sets = Utf8OwnedFindOptimizations.FindFixedDistanceSets(tree.Root);
            Assert.NotNull(sets);
            Assert.Equal(new[] { 4, 5 }, sets.Select(static set => set.Distance));
            for (var value = 0; value < 128; value++)
            {
                Assert.Equal(value is 'a' or 'b', sets[0].CharClass.Contains((byte)value));
                Assert.Equal(value is >= '0' and <= '9', sets[1].CharClass.Contains((byte)value));
            }
            Assert.Equal(0ul, measure.Budget.Depth);
            work = measure.Budget.WorkSteps;
        }
        var folded = Utf8OwnedTreeParser.TryParse("[a-z][ab]", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        Assert.NotNull(folded);
        Assert.Null(Utf8OwnedFindOptimizations.FindFixedDistanceSets(folded.Root));
        for (var denial = 0; denial < 3; denial++)
        {
            using var owner = new Utf8ConstructionScope(0, denial == 0 ? 112ul : ulong.MaxValue,
                denial == 2 ? 2ul : ulong.MaxValue, denial == 1 ? 0ul : ulong.MaxValue);
            var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
            var checkpoint = owner.Budget.ObjectCheckpoint;
            var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
                Utf8OwnedFindOptimizations.FindFixedDistanceSets(tree.Root));
            Assert.Equal(denial switch { 0 => Utf8ResourceLimitKind.ConstructionBytes,
                1 => Utf8ResourceLimitKind.ConstructionDepth, _ => Utf8ResourceLimitKind.ConstructionWorkSteps }, failure.ResourceKind);
            Assert.Equal(112ul, owner.Budget.ReservedBytes);
            Assert.Equal(0ul, owner.Budget.Depth);
            Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
            Assert.Equal(17, parent.Value);
        }
        for (var cutoff = 0ul; cutoff < work; cutoff++)
        {
            using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, cutoff + 2, ulong.MaxValue);
            var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
            var checkpoint = owner.Budget.ObjectCheckpoint;
            Assert.Throws<Utf8ResourceLimitExceededException>(() => Utf8OwnedFindOptimizations.FindFixedDistanceSets(tree.Root));
            Assert.Equal(112ul, owner.Budget.ReservedBytes);
            Assert.Equal(0ul, owner.Budget.Depth);
            Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
            Assert.Equal(17, parent.Value);
        }
        var decline = Utf8OwnedTreeParser.TryParse("[ab](?=c)", RegexOptions.CultureInvariant);
        Assert.NotNull(decline);
        using (var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
        {
            var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
            var checkpoint = owner.Budget.ObjectCheckpoint;
            Assert.Null(Utf8OwnedFindOptimizations.FindFixedDistanceSets(decline.Root));
            Assert.Equal(112ul, owner.Budget.ReservedBytes);
            Assert.Equal(0ul, owner.Budget.Depth);
            Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
            Assert.True(owner.Budget.WorkSteps > 2);
            Assert.Equal(17, parent.Value);
        }
    }

    public static IEnumerable<object?[]> WidthCases()
    {
        var cases = new (string Pattern, int Minimum, int? Maximum)[]
        {
            ("😀", 4, 4), ("a😀é", 7, 7), (".", 1, 4), ("[😀-🙏]", 4, 4),
            ("[a😀]", 1, 4), ("[éê]", 2, 2), ("[中]", 3, 3), ("[^a]", 1, 4),
            (@"\d", 1, 4), (@"\p{L}", 1, 4), ("(?i:k)", 1, 3), ("(?i:a)", 1, 1),
            ("(?i:é)", 2, 2), (@"(a)\1", 1, null), (@"(?<x>😀)\k<x>", 4, null),
            ("(?(?=.{2})é|abc)", 2, 3), ("(a)?(?(1)é)", 0, 3),
            ("(?(?=a)😀|😀)", 4, 4), ("(?<=😀)é", 2, 2), ("(?=😀)", 0, 0),
            ("(?:é){2,3}", 4, 6), ("(?:😀){2}", 8, 8), ("(?:😀?){3}", 0, 12),
            ("é{2147483647}", int.MaxValue, null), ("^*", 0, 0), (@"\b+", 0, 0),
        };
        foreach (var row in cases)
        foreach (var options in new[] { RegexOptions.CultureInvariant,
            RegexOptions.CultureInvariant | RegexOptions.Compiled,
            RegexOptions.CultureInvariant | RegexOptions.RightToLeft,
            RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.RightToLeft })
            yield return [row.Pattern, options, row.Minimum, row.Maximum];
    }

    [Theory]
    [MemberData(nameof(WidthCases))]
    public void BoundsDescribeConsumedScalarBytes(string pattern, RegexOptions options, int minimum, int? maximum)
    {
        var tree = Utf8OwnedTreeParser.TryParse(pattern, options) ?? throw new InvalidOperationException();
        var width = Utf8OwnedFindOptimizations.ComputeByteWidth(tree.Root);
        Assert.Equal(minimum, width.Minimum);
        Assert.Equal(maximum, width.Maximum);
        Assert.Equal(maximum is int high && high == minimum, width.IsFixed);
    }

    [Theory]
    [InlineData("éab", "éab", 0)]
    [InlineData("(é)[ab]cd", "cd", 3)]
    [InlineData("(😀)[ab]cd", "cd", 5)]
    [InlineData("[😀-🙏]ab", "ab", 4)]
    [InlineData("[éê]ab", "ab", 2)]
    [InlineData("(?:é|ê)ab", "ab", 2)]
    [InlineData("(?:é|中)ab", null, 0)]
    [InlineData(".ab", null, 0)]
    [InlineData("a{2,4}bc", null, 0)]
    [InlineData("(?:é){2}ab", "ab", 4)]
    [InlineData("ab.*longer", "ab", 0)]
    [InlineData(@"(a)\1longer", "a", 0)]
    [InlineData("(?i:k)ab", null, 0)]
    [InlineData("(?i:é)ab", "ab", 2)]
    [InlineData("(?(?=x)é|ê)ab", "ab", 2)]
    [InlineData("(?(?=x)é|中)ab", null, 0)]
    public void FixedDistancesRequireAConstantBytePrefix(string pattern, string? literal, int distance)
    {
        var tree = Utf8OwnedTreeParser.TryParse(pattern, RegexOptions.CultureInvariant) ?? throw new InvalidOperationException();
        var result = Utf8OwnedFindOptimizations.TryExtractFixedDistanceLiteral(tree.Root);
        if (literal is null) Assert.Null(result);
        else Assert.Equal(((string Literal, int Distance)?)(literal, distance), result);
    }

    [Theory]
    [InlineData("(é)[ab]", 2)]
    [InlineData("(😀)[ab]", 4)]
    public void AsciiSetDistancesIncludeTheWholeEncodedPrefix(string pattern, int distance)
    {
        var tree = Utf8OwnedTreeParser.TryParse(pattern, RegexOptions.CultureInvariant) ?? throw new InvalidOperationException();
        var sets = Utf8OwnedFindOptimizations.FindFixedDistanceSets(tree.Root);
        Assert.NotNull(sets);
        Assert.Equal(distance, Assert.Single(sets).Distance);
    }

    [Theory]
    [InlineData(false, false)] [InlineData(true, false)] [InlineData(false, true)] [InlineData(true, true)]
    public void PublicSearchKeepsVariableScalarPrefixesAndByteCoordinates(bool compiled, bool rtl)
    {
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None) |
            (rtl ? RegexOptions.RightToLeft : RegexOptions.None);
        foreach (var row in new[] { (Pattern: "(?:é|中)ab", Subject: "中ab"), (Pattern: ".ab", Subject: "😀ab"),
            (Pattern: "a{2,4}bc", Subject: "aaaabc"),
            (Pattern: rtl ? @"\k<x>(?<x>😀)ab" : @"(?<x>😀)\k<x>ab", Subject: "😀😀ab") })
        {
            var regex = new Utf8Regex(row.Pattern, options);
            var input = Encoding.UTF8.GetBytes("!" + row.Subject + "!");
            Assert.True(regex.IsMatch(input), row.Pattern);
            Assert.Equal(1, regex.Count(input));
            var match = regex.Match(input);
            Assert.True(match.Success);
            Assert.True(match.IsByteAligned);
            Assert.Equal(1, match.IndexInBytes);
            Assert.Equal(Encoding.UTF8.GetByteCount(row.Subject), match.LengthInBytes);
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)]
    public void BorrowedScalarValidationAndFixedProofNeverRefundSpentWork(int route)
    {
        var text = string.Concat(Enumerable.Repeat("😀", 100000));
        var node = Utf8TreeNode.Create(Utf8TreeNodeKind.Multi, RegexOptions.None, text);
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, 1000, ulong.MaxValue);
        var parent = owner.Budget.AllocateArray<byte>(1, false);
        var checkpoint = owner.Budget.ObjectCheckpoint;
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
        {
            if (route == 0) Utf8OwnedFindOptimizations.ComputeByteWidth(node);
            else Utf8OwnedFindOptimizations.TryExtractFixedDistanceLiteral(node);
        });
        Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, failure.ResourceKind);
        Assert.Equal(48ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Equal(0ul, owner.Budget.Reserved);
        Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
        Assert.Equal(1000ul, owner.Budget.WorkSteps);
        Assert.Single(parent);
        var spent = owner.Budget.WorkSteps;
        using (var recovery = Utf8ConstructionScope.Enter())
            Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps,
                Assert.Throws<Utf8ResourceLimitExceededException>(() =>
                    Utf8OwnedFindOptimizations.ComputeByteWidth(Utf8TreeNode.Create(Utf8TreeNodeKind.One, RegexOptions.None, 'a'))).ResourceKind);
        Assert.Equal(spent, owner.Budget.WorkSteps);
        Assert.Equal(48ul, owner.Budget.ReservedBytes);
    }

    [Fact]
    public void NonInvariantCaseBoundsCoverCultureDependentByteWidths()
    {
        var node = Utf8TreeNode.Create(Utf8TreeNodeKind.One, RegexOptions.IgnoreCase, 'I');
        Assert.Equal(new Utf8OwnedFindOptimizations.ByteWidth(1, 2), Utf8OwnedFindOptimizations.ComputeByteWidth(node));
    }

    [Fact]
    public void ObsoleteWidthWrappersAndLiteralHolderAreAbsentFromMetadata()
    {
        Assert.Null(typeof(Utf8OwnedFindOptimizations).GetMethod("ComputeMinLength"));
        Assert.Null(typeof(Utf8OwnedFindOptimizations).GetMethod("ComputeMaxLength"));
        Assert.Null(typeof(Utf8OwnedFindOptimizations).GetNestedType("LiteralHolder", System.Reflection.BindingFlags.NonPublic));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void FixedLiteralProofAdmitsItsRootBeforeAnyAcquisition(int denial)
    {
        var node = Utf8TreeNode.Create(Utf8TreeNodeKind.One, RegexOptions.None, 'a');
        using var owner = new Utf8ConstructionScope(0, denial == 0 ? 48ul : ulong.MaxValue,
            denial == 2 ? 1ul : ulong.MaxValue, denial == 1 ? 0ul : ulong.MaxValue);
        var parent = owner.Budget.AllocateArray<byte>(1, false);
        var checkpoint = owner.Budget.ObjectCheckpoint;
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Utf8OwnedFindOptimizations.TryExtractFixedDistanceLiteral(node));
        Assert.Equal(denial switch { 0 => Utf8ResourceLimitKind.ConstructionBytes,
            1 => Utf8ResourceLimitKind.ConstructionDepth, _ => Utf8ResourceLimitKind.ConstructionWorkSteps }, failure.ResourceKind);
        Assert.Equal(48ul, owner.Budget.ReservedBytes);
        Assert.Equal(48ul, owner.Budget.PeakBytes);
        Assert.Equal(1ul, owner.Budget.WorkSteps);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
        Assert.Single(parent);
    }
}
