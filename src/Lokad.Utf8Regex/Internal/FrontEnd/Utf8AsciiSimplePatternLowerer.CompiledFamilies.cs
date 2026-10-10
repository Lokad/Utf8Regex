using Lokad.Utf8Regex.Internal.Execution;
namespace Lokad.Utf8Regex.Internal.FrontEnd;

internal static partial class Utf8AsciiSimplePatternLowerer
{
    private static bool TryExtractAnchoredValidatorPlan(
        Utf8ConstructionSession storage,
        AsciiSimplePatternToken[][] branches,
        bool isStartAnchored,
        bool isEndAnchored,
        bool ignoreCase,
        out AsciiSimplePatternAnchoredValidatorPlan validatorPlan)
    {
        validatorPlan = default;
        if (!isStartAnchored || !isEndAnchored || branches.Length == 0)
        {
            return false;
        }

        if (branches.Length > 1 && ignoreCase)
        {
            return false;
        }

        var aggregateSegments = storage.Buffer<AsciiSimplePatternAnchoredValidatorSegment>();
        var isFirstBranch = true;
        foreach (var tokens in branches)
        {
            Utf8ConstructionScope.ChargeWork(1);
            if (tokens.Length == 0)
            {
                return false;
            }

            var segments = storage.Buffer<AsciiSimplePatternAnchoredValidatorSegment>();
            for (var i = 0; i < tokens.Length;)
            {
                Utf8ConstructionScope.ChargeWork(1);
                var token = tokens[i];
                if (token.Kind == AsciiSimplePatternTokenKind.Dot)
                {
                    return false;
                }

                if (token.Kind == AsciiSimplePatternTokenKind.Literal)
                {
                    var start = i;
                    i++;
                    while (i < tokens.Length && tokens[i].Kind == AsciiSimplePatternTokenKind.Literal)
                    {
                        Utf8ConstructionScope.ChargeWork(1);
                        i++;
                    }

                    var literal = storage.Allocate<byte>(i - start, false);
                    for (var j = 0; j < literal.Length; j++)
                    {
                        Utf8ConstructionScope.ChargeWork(1);
                        literal[j] = tokens[start + j].Literal;
                    }

                    segments.Add(new AsciiSimplePatternAnchoredValidatorSegment(literal));
                    continue;
                }

                if (token.Kind != AsciiSimplePatternTokenKind.CharClass || token.CharClass.IsEmpty)
                {
                    return false;
                }

                var charClass = token.CharClass;

                var runLength = 1;
                i++;
                while (i < tokens.Length &&
                    tokens[i].Kind == AsciiSimplePatternTokenKind.CharClass &&
                    !tokens[i].CharClass.IsEmpty &&
                    charClass.HasSameDefinition(tokens[i].CharClass))
                {
                    Utf8ConstructionScope.ChargeWork(1);
                    runLength++;
                    i++;
                }

                segments.Add(new AsciiSimplePatternAnchoredValidatorSegment(charClass, runLength, runLength));
            }

            if (isFirstBranch)
            {
                aggregateSegments = segments;
                isFirstBranch = false;
                continue;
            }

            if (!TryMergeAnchoredValidatorSegments(storage, aggregateSegments, segments, ignoreCase, out var mergedSegments))
            {
                return false;
            }

            aggregateSegments = mergedSegments;
        }

        // Merging constructs a candidate envelope, not a proof of equivalence.
        // Every length vector must be covered by an original expanded branch:
        // min/max widening alone loses both gaps and correlations.
        if (branches.Length > 1 && !IsAnchoredValidatorEnvelopeCovered(aggregateSegments, branches))
        {
            // Absent optional fields can coalesce neighboring class runs in
            // individual branches. Recover their boundaries from the existing
            // optional-field proof, which requires the complete product of
            // head lengths and both optional fields.
            if (!TryExtractAnchoredOptionalFieldPlan(branches, isStartAnchored, isEndAnchored, ignoreCase, out var optional))
                return false;
            aggregateSegments.Clear();
            aggregateSegments.Add(new(optional.HeadClass, optional.HeadMinCount, optional.HeadMaxCount));
            aggregateSegments.Add(new(optional.FirstRequiredClass, 1, 1));
            aggregateSegments.Add(new(optional.OptionalClass, 0, 1));
            aggregateSegments.Add(new(AsciiCharClass.ForByte(optional.OptionalLiteral), 0, 1));
            aggregateSegments.Add(new(optional.SecondRequiredClass, 1, 1));
            aggregateSegments.Add(new(optional.TailClass, optional.TailCount, optional.TailCount));
            if (!IsAnchoredValidatorEnvelopeCovered(aggregateSegments, branches)) return false;
        }

        validatorPlan = new AsciiSimplePatternAnchoredValidatorPlan(storage.Copy(aggregateSegments, false), ignoreCase);
        return validatorPlan.HasValue;
    }

    private static bool IsAnchoredValidatorEnvelopeCovered(
        Utf8ConstructionBuffer<AsciiSimplePatternAnchoredValidatorSegment> segments,
        AsciiSimplePatternToken[][] branches)
    {
        using var construction = Utf8ConstructionScope.Enter();
        var owner = construction.Budget;
        long combinations = 1;
        foreach (var segment in segments)
        {
            owner.ChargeWork(1);
            var width = (long)segment.MaxLength - segment.MinLength + 1;
            // Keep this optional proof no larger than the already admitted
            // expansion. Redundant segment partitions may decline the native
            // plan; the owned scalar program remains available.
            if (width <= 0 || combinations > branches.Length / width) return false;
            combinations *= width;
        }

        var lengths = owner.AllocateArray<int>(segments.Count, false);
        try
        {
            for (var index = 0; index < lengths.Length; index++) { Utf8ConstructionScope.ChargeWork(1); lengths[index] = segments[index].MinLength; }
            while (true)
            {
                owner.ChargeWork(1);
                var covered = false;
                foreach (var branch in branches)
                {
                    owner.ChargeWork(1);
                    if (CoversLengthVector(branch, segments, lengths, owner))
                    {
                        covered = true;
                        break;
                    }
                }
                if (!covered) return false;

                var dimension = lengths.Length - 1;
                while (dimension >= 0 && lengths[dimension] == segments[dimension].MaxLength)
                {
                    owner.ChargeWork(1);
                    lengths[dimension] = segments[dimension].MinLength;
                    dimension--;
                }
                if (dimension < 0) return true;
                lengths[dimension]++;
            }
        }
        finally { owner.ReleaseArray(lengths, false); }

        static bool CoversLengthVector(AsciiSimplePatternToken[] branch,
            Utf8ConstructionBuffer<AsciiSimplePatternAnchoredValidatorSegment> segments, int[] lengths,
            Utf8ConstructionBudget owner)
        {
            var offset = 0;
            for (var index = 0; index < segments.Count; index++)
            {
                owner.ChargeWork(1);
                var segment = segments[index];
                var count = lengths[index];
                if (count > branch.Length - offset) return false;
                for (var position = 0; position < count; position++)
                {
                    owner.ChargeWork(1);
                    var token = branch[offset++];
                    var actual = token.Kind == AsciiSimplePatternTokenKind.Literal
                        ? AsciiCharClass.ForByte(token.Literal) : token.CharClass;
                    var candidate = segment.IsLiteral
                        ? AsciiCharClass.ForByte(segment.Literal[position]) : segment.CharClass;
                    if ((candidate.LowMask & ~actual.LowMask) != 0 ||
                        (candidate.HighMask & ~actual.HighMask) != 0) return false;
                }
            }
            return offset == branch.Length;
        }
    }

    private struct MergeFrame(int aggregate, int next, int restoreCount)
    {
        internal readonly int Aggregate = aggregate;
        internal readonly int Next = next;
        internal readonly int RestoreCount = restoreCount;
        internal int Stage;
    }

    private static bool TryMergeAnchoredValidatorSegments(Utf8ConstructionSession storage,
        Utf8ConstructionBuffer<AsciiSimplePatternAnchoredValidatorSegment> aggregate,
        Utf8ConstructionBuffer<AsciiSimplePatternAnchoredValidatorSegment> next, bool ignoreCase,
        out Utf8ConstructionBuffer<AsciiSimplePatternAnchoredValidatorSegment> merged)
    {
        merged = storage.Buffer<AsciiSimplePatternAnchoredValidatorSegment>();
        using var frames = Utf8ConstructionFrames<MergeFrame>.Create(storage.Budget, new(0, 0, 0));
        using var seen = Utf8ConstructionMap<(int, int), byte>.Create(storage.Budget, EqualityComparer<(int, int)>.Default);
        while (frames.Count != 0)
        {
            storage.Budget.ChargeWork(1);
            var index = frames.Count - 1;
            var frame = frames[index];
            var a = frame.Aggregate;
            var n = frame.Next;
            if (frame.Stage == 0)
            {
                if (a == aggregate.Count && n == next.Count) return true;
                if (!Note(seen, (a, n))) { frames.Pop(); continue; }
            }
            // Unwind only the last attempted extension, preserving the original
            // pair/optional-left/optional-right DFS order and visited-pair memo.
            while (merged.Count > frame.RestoreCount) { Utf8ConstructionScope.ChargeWork(1); merged.Pop(); }
            AsciiSimplePatternAnchoredValidatorSegment segment;
            MergeFrame? child = null;
            switch (frame.Stage++)
            {
                case 0:
                    if (a < aggregate.Count && n < next.Count &&
                        TryMergeAnchoredValidatorSegmentPair(aggregate[a], next[n], ignoreCase, out segment))
                    { merged.Add(segment); child = new(a + 1, n + 1, merged.Count); }
                    break;
                case 1:
                    if (a < aggregate.Count && TryOptionalizeAnchoredValidatorSegment(aggregate[a], ignoreCase, out segment))
                    { merged.Add(segment); child = new(a + 1, n, merged.Count); }
                    break;
                case 2:
                    if (n < next.Count && TryOptionalizeAnchoredValidatorSegment(next[n], ignoreCase, out segment))
                    { merged.Add(segment); child = new(a, n + 1, merged.Count); }
                    break;
                default:
                    frames.Pop();
                    continue;
            }
            frames[index] = frame;
            if (child is { } pending) frames.Push(pending);
        }
        merged.Clear();
        return false;
    }

    private static bool Note<TKey>(Utf8ConstructionMap<TKey, byte> seen, TKey key) where TKey : notnull
    {
        if (seen.ContainsKey(key)) return false;
        seen[key] = 0;
        return true;
    }

    private static bool TryMergeAnchoredValidatorSegmentPair(
        AsciiSimplePatternAnchoredValidatorSegment current,
        AsciiSimplePatternAnchoredValidatorSegment incoming,
        bool ignoreCase,
        out AsciiSimplePatternAnchoredValidatorSegment merged)
    {
        if (current.IsLiteral && incoming.IsLiteral)
        {
            if (SameLiteral(current.Literal, incoming.Literal))
            {
                merged = current;
                return true;
            }

            merged = default;
            return false;
        }

        if (TryGetAnchoredValidatorSegmentCharClass(current, ignoreCase, out var currentClass) &&
            TryGetAnchoredValidatorSegmentCharClass(incoming, ignoreCase, out var incomingClass) &&
            currentClass.HasSameDefinition(incomingClass))
        {
            merged = new AsciiSimplePatternAnchoredValidatorSegment(
                currentClass,
                Math.Min(current.MinLength, incoming.MinLength),
                Math.Max(current.MaxLength, incoming.MaxLength));
            return true;
        }

        merged = default;
        return false;
    }

    private static bool TryOptionalizeAnchoredValidatorSegment(
        AsciiSimplePatternAnchoredValidatorSegment segment,
        bool ignoreCase,
        out AsciiSimplePatternAnchoredValidatorSegment optionalized)
    {
        if (TryGetAnchoredValidatorSegmentCharClass(segment, ignoreCase, out var charClass))
        {
            optionalized = new AsciiSimplePatternAnchoredValidatorSegment(charClass, 0, segment.MaxLength);
            return true;
        }

        optionalized = default;
        return false;
    }

    private static bool TryGetAnchoredValidatorSegmentCharClass(
        AsciiSimplePatternAnchoredValidatorSegment segment,
        bool ignoreCase,
        out AsciiCharClass charClass)
    {
        if (!segment.CharClass.IsEmpty)
        {
            charClass = segment.CharClass;
            return true;
        }

        if (!ignoreCase && segment.Literal.Length == 1)
        {
            charClass = AsciiCharClass.ForByte(segment.Literal[0]);
            return true;
        }

        charClass = AsciiCharClass.Empty;
        return false;
    }

    private static bool TryExtractAnchoredBoundedDatePlan(
        AsciiSimplePatternToken[][] branches,
        bool isStartAnchored,
        bool isEndAnchored,
        out AsciiSimplePatternAnchoredBoundedDatePlan plan)
    {
        plan = default;
        if (!isStartAnchored || !isEndAnchored || branches.Length == 0)
        {
            return false;
        }

        var firstMin = int.MaxValue;
        var firstMax = 0;
        var secondMin = int.MaxValue;
        var secondMax = 0;
        var thirdMin = int.MaxValue;
        var thirdMax = 0;
        byte separator = 0;
        byte secondSeparator = 0;
        var initializedSeparators = false;

        foreach (var branch in branches)
        {
            Utf8ConstructionScope.ChargeWork(1);
            if (!TryParseAnchoredBoundedDateBranch(
                branch,
                out var firstCount,
                out var secondCount,
                out var thirdCount,
                out var branchSeparator,
                out var branchSecondSeparator))
            {
                return false;
            }

            if (!initializedSeparators)
            {
                separator = branchSeparator;
                secondSeparator = branchSecondSeparator;
                initializedSeparators = true;
            }
            else if (separator != branchSeparator || secondSeparator != branchSecondSeparator)
            {
                return false;
            }

            firstMin = Math.Min(firstMin, firstCount);
            firstMax = Math.Max(firstMax, firstCount);
            secondMin = Math.Min(secondMin, secondCount);
            secondMax = Math.Max(secondMax, secondCount);
            thirdMin = Math.Min(thirdMin, thirdCount);
            thirdMax = Math.Max(thirdMax, thirdCount);
        }

        if (!initializedSeparators ||
            firstMin <= 0 ||
            secondMin <= 0 ||
            thirdMin <= 0 ||
            firstMax > byte.MaxValue ||
            secondMax > byte.MaxValue ||
            thirdMax > byte.MaxValue)
        {
            return false;
        }

        // The specialized date matcher also executes a Cartesian length box.
        // Require every tuple, rather than treating independent extrema as
        // sufficient evidence that all combinations are legal.
        var tupleCount = (long)(firstMax - firstMin + 1) * (secondMax - secondMin + 1) * (thirdMax - thirdMin + 1);
        if (tupleCount > branches.Length) return false;
        for (var first = firstMin; first <= firstMax; first++)
        {
            Utf8ConstructionScope.ChargeWork(1);
            for (var second = secondMin; second <= secondMax; second++)
            {
                Utf8ConstructionScope.ChargeWork(1);
                for (var third = thirdMin; third <= thirdMax; third++)
                {
                    Utf8ConstructionScope.ChargeWork(1);
                    var covered = false;
                    foreach (var branch in branches)
                    {
                        Utf8ConstructionScope.ChargeWork(1);
                        Utf8ConstructionScope.ChargeWork((ulong)branch.Length + 1);
                        if (TryParseAnchoredBoundedDateBranch(branch, out var a, out var b, out var c, out _, out _) &&
                            a == first && b == second && c == third)
                        {
                            covered = true;
                            break;
                        }
                    }
                    if (!covered) return false;
                }
            }
        }

        plan = new AsciiSimplePatternAnchoredBoundedDatePlan(
            (byte)firstMin,
            (byte)firstMax,
            (byte)secondMin,
            (byte)secondMax,
            (byte)thirdMin,
            (byte)thirdMax,
            separator,
            secondSeparator);
        return plan.HasValue;
    }

    /// <summary>
    /// Extracts the bounded optional-field execution family from a complete
    /// set of expanded, anchored simple-pattern branches.
    /// </summary>
    public static bool TryExtractAnchoredOptionalFieldPlan(
        AsciiSimplePatternToken[][] branches,
        bool isStartAnchored,
        bool isEndAnchored,
        bool ignoreCase,
        out AsciiSimplePatternAnchoredOptionalFieldPlan plan)
    {
        static bool TryParseTemplate(
            AsciiSimplePatternToken[] branch,
            out AsciiCharClass headClass,
            out int headCount,
            out AsciiCharClass firstRequiredClass,
            out AsciiCharClass optionalClass,
            out byte optionalLiteral,
            out AsciiCharClass secondRequiredClass,
            out AsciiCharClass tailClass,
            out int tailCount)
        {
            headClass = default;
            headCount = 0;
            firstRequiredClass = default;
            optionalClass = default;
            optionalLiteral = 0;
            secondRequiredClass = default;
            tailClass = default;
            tailCount = 0;
            if (branch.Length < 7 || !TryGetPositiveCharClass(branch[0], out headClass))
            {
                return false;
            }

            var index = 0;
            while (index < branch.Length &&
                TryGetPositiveCharClass(branch[index], out var candidateHeadClass) &&
                headClass.HasSameDefinition(candidateHeadClass))
            {
                Utf8ConstructionScope.ChargeWork(1);
                headCount++;
                index++;
            }

            if (!TryConsumePositiveCharClass(branch, ref index, out firstRequiredClass) ||
                !TryConsumePositiveCharClass(branch, ref index, out optionalClass) ||
                !TryConsumeLiteralByte(branch, ref index, out optionalLiteral) ||
                !TryConsumePositiveCharClass(branch, ref index, out secondRequiredClass) ||
                !TryConsumePositiveCharClass(branch, ref index, out tailClass))
            {
                return false;
            }

            tailCount = 1;
            while (index < branch.Length &&
                TryGetPositiveCharClass(branch[index], out var candidateTailClass) &&
                tailClass.HasSameDefinition(candidateTailClass))
            {
                Utf8ConstructionScope.ChargeWork(1);
                tailCount++;
                index++;
            }

            return index == branch.Length &&
                headCount > 0 &&
                tailCount > 0 &&
                !headClass.HasSameDefinition(firstRequiredClass) &&
                !firstRequiredClass.HasSameDefinition(optionalClass) &&
                !secondRequiredClass.HasSameDefinition(tailClass);
        }

        static bool TryParseBranch(
            AsciiSimplePatternToken[] branch,
            AsciiCharClass headClass,
            AsciiCharClass firstRequiredClass,
            AsciiCharClass optionalClass,
            byte optionalLiteral,
            AsciiCharClass secondRequiredClass,
            AsciiCharClass tailClass,
            int tailCount,
            out int headCount,
            out bool hasOptionalClass,
            out bool hasOptionalLiteral)
        {
            headCount = 0;
            hasOptionalClass = false;
            hasOptionalLiteral = false;
            var index = 0;
            while (index < branch.Length &&
                TryGetPositiveCharClass(branch[index], out var candidateHeadClass) &&
                headClass.HasSameDefinition(candidateHeadClass))
            {
                Utf8ConstructionScope.ChargeWork(1);
                headCount++;
                index++;
            }

            if (headCount == 0 ||
                !TryConsumeExpectedCharClass(branch, ref index, firstRequiredClass))
            {
                return false;
            }

            if (TryConsumeExpectedCharClass(branch, ref index, optionalClass))
            {
                hasOptionalClass = true;
            }

            if ((uint)index < (uint)branch.Length &&
                branch[index].Kind == AsciiSimplePatternTokenKind.Literal &&
                branch[index].Literal == optionalLiteral)
            {
                hasOptionalLiteral = true;
                index++;
            }

            if (!TryConsumeExpectedCharClass(branch, ref index, secondRequiredClass))
            {
                return false;
            }

            for (var i = 0; i < tailCount; i++)
            {
                Utf8ConstructionScope.ChargeWork(1);
                if (!TryConsumeExpectedCharClass(branch, ref index, tailClass))
                {
                    return false;
                }
            }

            return index == branch.Length;
        }

        static bool TryConsumePositiveCharClass(
            AsciiSimplePatternToken[] tokens,
            ref int index,
            out AsciiCharClass charClass)
        {
            charClass = default;
            if ((uint)index >= (uint)tokens.Length ||
                !TryGetPositiveCharClass(tokens[index], out charClass))
            {
                return false;
            }

            index++;
            return true;
        }

        static bool TryConsumeExpectedCharClass(
            AsciiSimplePatternToken[] tokens,
            ref int index,
            AsciiCharClass expected)
        {
            if ((uint)index >= (uint)tokens.Length ||
                !TryGetPositiveCharClass(tokens[index], out var actual) ||
                !expected.HasSameDefinition(actual))
            {
                return false;
            }

            index++;
            return true;
        }

        static bool TryGetPositiveCharClass(AsciiSimplePatternToken token, out AsciiCharClass charClass)
        {
            charClass = token.CharClass;
            return token.Kind == AsciiSimplePatternTokenKind.CharClass &&
                !charClass.IsEmpty &&
                !charClass.Negated;
        }

        plan = default;
        if (!isStartAnchored ||
            !isEndAnchored ||
            ignoreCase ||
            branches.Length is < 4 or > 32)
        {
            return false;
        }

        var longest = branches[0];
        foreach (var branch in branches)
        {
            Utf8ConstructionScope.ChargeWork(1);
            if (branch.Length > longest.Length)
            {
                longest = branch;
            }
        }

        if (!TryParseTemplate(
            longest,
            out var headClass,
            out var headMaxCount,
            out var firstRequiredClass,
            out var optionalClass,
            out var optionalLiteral,
            out var secondRequiredClass,
            out var tailClass,
            out var tailCount) ||
            !headClass.IsDisjoint(firstRequiredClass) ||
            !AsciiCharClass.ForByte(optionalLiteral).IsDisjoint(secondRequiredClass))
        {
            return false;
        }

        var headMinCount = int.MaxValue;
        using var combinations = Utf8ConstructionMap<(int, bool, bool), byte>.Create(Utf8ConstructionScope.CurrentBudget, EqualityComparer<(int, bool, bool)>.Default);
        foreach (var branch in branches)
        {
            Utf8ConstructionScope.ChargeWork(1);
            if (!TryParseBranch(
                branch,
                headClass,
                firstRequiredClass,
                optionalClass,
                optionalLiteral,
                secondRequiredClass,
                tailClass,
                tailCount,
                out var headCount,
                out var hasOptionalClass,
                out var hasOptionalLiteral) ||
                !Note(combinations, (headCount, hasOptionalClass, hasOptionalLiteral)))
            {
                return false;
            }

            headMinCount = Math.Min(headMinCount, headCount);
            headMaxCount = Math.Max(headMaxCount, headCount);
        }

        if (headMinCount <= 0 ||
            headMaxCount > byte.MaxValue ||
            tailCount > byte.MaxValue ||
            combinations.Count != (headMaxCount - headMinCount + 1) * 4)
        {
            return false;
        }

        for (var headCount = headMinCount; headCount <= headMaxCount; headCount++)
        {
            Utf8ConstructionScope.ChargeWork(1);
            if (!combinations.ContainsKey((headCount, false, false)) ||
                !combinations.ContainsKey((headCount, false, true)) ||
                !combinations.ContainsKey((headCount, true, false)) ||
                !combinations.ContainsKey((headCount, true, true)))
            {
                return false;
            }
        }

        plan = new AsciiSimplePatternAnchoredOptionalFieldPlan(
            headClass,
            (byte)headMinCount,
            (byte)headMaxCount,
            firstRequiredClass,
            optionalClass,
            optionalLiteral,
            secondRequiredClass,
            tailClass,
            (byte)tailCount);
        return plan.HasValue;
    }

    private static bool TryExtractRepeatedDigitGroupPlan(
        Utf8ConstructionSession storage,
        AsciiSimplePatternToken[][] branches,
        bool ignoreCase,
        out AsciiSimplePatternRepeatedDigitGroupPlan plan)
    {
        plan = default;
        if (ignoreCase || branches.Length == 0)
        {
            return false;
        }

        AsciiCharClass digitClass = default;
        AsciiCharClass separatorClass = default;
        var initializedClasses = false;
        var repeatedGroupCount = -1;
        var groupDigitCount = -1;
        var trailingMinDigits = int.MaxValue;
        var trailingMaxDigits = 0;
        foreach (var branch in branches)
        {
            Utf8ConstructionScope.ChargeWork(1);
            if (!TryParseRepeatedDigitGroupBranch(
                branch,
                out var branchDigitClass,
                out var branchSeparatorClass,
                out var branchRepeatedGroupCount,
                out var branchGroupDigitCount,
                out var branchTrailingDigits))
            {
                return false;
            }

            if (!initializedClasses)
            {
                digitClass = branchDigitClass;
                separatorClass = branchSeparatorClass;
                repeatedGroupCount = branchRepeatedGroupCount;
                groupDigitCount = branchGroupDigitCount;
                initializedClasses = true;
            }
            else if (!digitClass.HasSameDefinition(branchDigitClass) ||
                !separatorClass.HasSameDefinition(branchSeparatorClass) ||
                repeatedGroupCount != branchRepeatedGroupCount ||
                groupDigitCount != branchGroupDigitCount)
            {
                return false;
            }

            trailingMinDigits = Math.Min(trailingMinDigits, branchTrailingDigits);
            trailingMaxDigits = Math.Max(trailingMaxDigits, branchTrailingDigits);
        }

        if (!initializedClasses ||
            !digitClass.TryGetKnownPredicateKind(out var digitPredicate) ||
            digitPredicate != AsciiCharClassPredicateKind.Digit)
        {
            return false;
        }

        Span<byte> separatorScratch = stackalloc byte[128];
        storage.Budget.ChargeWork(128);
        var separatorCount = separatorClass.CopyMatchBytes(separatorScratch);
        if (separatorCount == 0 || separatorCount > 4) return false;
        var separatorBytes = storage.Allocate<byte>(separatorCount, false);
        storage.Budget.ChargeWork((ulong)separatorCount);
        separatorScratch[..separatorCount].CopyTo(separatorBytes);

        plan = new AsciiSimplePatternRepeatedDigitGroupPlan(
            (byte)repeatedGroupCount,
            (byte)groupDigitCount,
            (byte)trailingMinDigits,
            (byte)trailingMaxDigits,
            separatorBytes);
        return plan.HasValue;
    }

    private static bool TryParseRepeatedDigitGroupBranch(
        AsciiSimplePatternToken[] branch,
        out AsciiCharClass digitClass,
        out AsciiCharClass separatorClass,
        out int repeatedGroupCount,
        out int groupDigitCount,
        out int trailingDigits)
    {
        digitClass = default;
        separatorClass = default;
        repeatedGroupCount = 0;
        groupDigitCount = 0;
        trailingDigits = 0;
        if (branch.Length < 6 ||
            branch[0].Kind != AsciiSimplePatternTokenKind.CharClass ||
            branch[0].CharClass.IsEmpty)
        {
            return false;
        }

        var firstDigitClass = branch[0].CharClass;

        var separatorIndex = 0;
        while (separatorIndex < branch.Length &&
            branch[separatorIndex].Kind == AsciiSimplePatternTokenKind.CharClass &&
            !branch[separatorIndex].CharClass.IsEmpty &&
            firstDigitClass.HasSameDefinition(branch[separatorIndex].CharClass))
        {
            Utf8ConstructionScope.ChargeWork(1);
            separatorIndex++;
        }

        if (separatorIndex == 0 ||
            separatorIndex >= branch.Length ||
            branch[separatorIndex].Kind != AsciiSimplePatternTokenKind.CharClass ||
            (branch[separatorIndex].CharClass.IsEmpty || branch[separatorIndex].CharClass.Negated))
        {
            return false;
        }

        digitClass = firstDigitClass;
        separatorClass = branch[separatorIndex].CharClass;
        groupDigitCount = separatorIndex;

        var cursor = 0;
        while (cursor + groupDigitCount < branch.Length &&
            MatchesRepeatedDigitGroupDigitRun(branch, cursor, digitClass, groupDigitCount) &&
            branch[cursor + groupDigitCount].Kind == AsciiSimplePatternTokenKind.CharClass &&
            !branch[cursor + groupDigitCount].CharClass.IsEmpty &&
            separatorClass.HasSameDefinition(branch[cursor + groupDigitCount].CharClass))
        {
            Utf8ConstructionScope.ChargeWork(1);
            repeatedGroupCount++;
            cursor += groupDigitCount + 1;
        }

        trailingDigits = branch.Length - cursor;
        if (repeatedGroupCount <= 0 ||
            trailingDigits <= 0 ||
            trailingDigits > groupDigitCount ||
            !MatchesRepeatedDigitGroupDigitRun(branch, cursor, digitClass, trailingDigits))
        {
            return false;
        }

        return true;
    }

    private static bool MatchesRepeatedDigitGroupDigitRun(
        AsciiSimplePatternToken[] branch,
        int start,
        AsciiCharClass digitClass,
        int length)
    {
        if (start + length > branch.Length)
        {
            return false;
        }

        for (var i = 0; i < length; i++)
        {
            Utf8ConstructionScope.ChargeWork(1);
            if (branch[start + i].Kind != AsciiSimplePatternTokenKind.CharClass ||
                branch[start + i].CharClass.IsEmpty ||
                !digitClass.HasSameDefinition(branch[start + i].CharClass))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryParseAnchoredBoundedDateBranch(
        AsciiSimplePatternToken[] tokens,
        out int firstCount,
        out int secondCount,
        out int thirdCount,
        out byte separator,
        out byte secondSeparator)
    {
        firstCount = 0;
        secondCount = 0;
        thirdCount = 0;
        separator = 0;
        secondSeparator = 0;

        var index = 0;
        if (!TryConsumeDigitTokenRun(tokens, ref index, out firstCount) ||
            !TryConsumeLiteralByte(tokens, ref index, out separator) ||
            !TryConsumeDigitTokenRun(tokens, ref index, out secondCount) ||
            !TryConsumeLiteralByte(tokens, ref index, out secondSeparator) ||
            !TryConsumeDigitTokenRun(tokens, ref index, out thirdCount))
        {
            return false;
        }

        return index == tokens.Length;
    }

    private static bool TryConsumeDigitTokenRun(AsciiSimplePatternToken[] tokens, ref int index, out int count)
    {
        count = 0;
        while ((uint)index < (uint)tokens.Length &&
            tokens[index].Kind == AsciiSimplePatternTokenKind.CharClass &&
            !tokens[index].CharClass.IsEmpty &&
            tokens[index].CharClass.TryGetKnownPredicateKind(out var predicateKind) &&
            predicateKind == AsciiCharClassPredicateKind.Digit)
        {
            Utf8ConstructionScope.ChargeWork(1);
            count++;
            index++;
        }

        return count > 0;
    }

    private static bool TryConsumeLiteralByte(AsciiSimplePatternToken[] tokens, ref int index, out byte value)
    {
        value = 0;
        if ((uint)index >= (uint)tokens.Length || tokens[index].Kind != AsciiSimplePatternTokenKind.Literal)
        {
            return false;
        }

        value = tokens[index].Literal;
        index++;
        return true;
    }
}
