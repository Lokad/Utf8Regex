using System.Buffers;
using System.Reflection;
using System.Reflection.Emit;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Internal.Execution;

internal sealed class Utf8EmittedKernelMatcher
{
    internal delegate int FindNextDelegate(Utf8EmittedKernelMatcher matcher, ReadOnlySpan<byte> input, int startIndex, out int matchedLength);
    internal delegate int CountDelegate(Utf8EmittedKernelMatcher matcher, ReadOnlySpan<byte> input);

    private static readonly MethodInfo s_getSpanLengthMethod =
        typeof(ReadOnlySpan<byte>).GetProperty(nameof(ReadOnlySpan<byte>.Length))!.GetMethod!;

    private static readonly MethodInfo s_getSpanItemMethod =
        typeof(ReadOnlySpan<byte>).GetProperty("Item")!.GetMethod!;

    private static readonly MethodInfo s_findNextAnchorRelativeMethod =
        typeof(Utf8EmittedKernelMatcher).GetMethod(nameof(FindNextAnchorRelative), BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static readonly MethodInfo s_findNextCommonPrefixRelativeMethod =
        typeof(Utf8EmittedKernelMatcher).GetMethod(nameof(FindNextCommonPrefixRelative), BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static readonly MethodInfo s_findPreviousLeadingLiteralStartMethod =
        typeof(Utf8EmittedKernelMatcher).GetMethod(nameof(FindPreviousLeadingLiteralStart), BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static readonly MethodInfo s_tryMatchBoundedLeadingBeforeTrailingMethod =
        typeof(Utf8EmittedKernelMatcher).GetMethod(nameof(TryMatchBoundedLeadingBeforeTrailing), BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static readonly MethodInfo s_tryFindNextPairedTrailingFamilyMethod =
        typeof(Utf8EmittedKernelMatcher).GetMethod(nameof(TryFindNextPairedTrailingFamily), BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static readonly MethodInfo s_tryMatchPairedLeadingBeforeTrailingMethod =
        typeof(Utf8EmittedKernelMatcher).GetMethod(nameof(TryMatchPairedLeadingBeforeTrailing), BindingFlags.Instance | BindingFlags.NonPublic)!;

    private readonly Utf8EmittedKernelPlan _plan;
    private readonly SearchValues<byte>? _anchorSearchValues;
    private readonly PreparedLiteralSetSearch? _trailingFamilySearch;
    private readonly FindNextDelegate _findNext;
    private readonly CountDelegate _count;

    private Utf8EmittedKernelMatcher(Utf8EmittedKernelPlan plan, SearchValues<byte>? anchorSearchValues, PreparedLiteralSetSearch? trailingFamilySearch, FindNextDelegate findNext, CountDelegate count)
    {
        _plan = plan;
        _anchorSearchValues = anchorSearchValues;
        _trailingFamilySearch = trailingFamilySearch;
        _findNext = findNext;
        _count = count;
    }

    internal Utf8EmittedKernelPlan Plan => _plan;

    internal static bool TryCreate(
        Utf8RegexPlan regexPlan,
        out Utf8EmittedKernelMatcher? matcher)
    {
        matcher = null;
        if (!Utf8EmittedKernelLowerer.TryLower(regexPlan, out var kernelPlan))
        {
            return false;
        }

        matcher = new Utf8EmittedKernelMatcher(
            kernelPlan,
            kernelPlan.FindOptimization.Kind == Utf8CompiledFindOptimizationKind.AnchorByteSetAtOffset
                ? SearchValues.Create(kernelPlan.FindOptimization.AnchorBytes)
                : null,
            kernelPlan.TrailingLiterals is { Length: > 0 } trailingLiterals
                ? new PreparedLiteralSetSearch(trailingLiterals)
                : null,
            CompileFindNext(kernelPlan),
            CompileCount(kernelPlan));
        return true;
    }

    internal static bool TryCreate(
        in AsciiStructuralIdentifierFamilyPlan familyPlan,
        Utf8SearchPlan searchPlan,
        out Utf8EmittedKernelMatcher? matcher)
    {
        matcher = null;
        if (!Utf8EmittedKernelLowerer.TryLower(familyPlan, searchPlan, out var kernelPlan))
        {
            return false;
        }

        matcher = new Utf8EmittedKernelMatcher(
            kernelPlan,
            kernelPlan.FindOptimization.Kind == Utf8CompiledFindOptimizationKind.AnchorByteSetAtOffset
                ? SearchValues.Create(kernelPlan.FindOptimization.AnchorBytes)
                : null,
            kernelPlan.TrailingLiterals is { Length: > 0 } trailingLiterals
                ? new PreparedLiteralSetSearch(trailingLiterals)
                : null,
            CompileFindNext(kernelPlan),
            CompileCount(kernelPlan));
        return true;
    }

    internal int FindNext(ReadOnlySpan<byte> input, int startIndex, out int matchedLength) => _findNext(this, input, startIndex, out matchedLength);

    internal int Count(ReadOnlySpan<byte> input) => _count(this, input);

    private int FindNextAnchorRelative(ReadOnlySpan<byte> input, int startIndex) => input[startIndex..].IndexOfAny(_anchorSearchValues!);

    private int FindNextCommonPrefixRelative(ReadOnlySpan<byte> input, int startIndex) => input[startIndex..].IndexOf(_plan.FindOptimization.CommonPrefix);

    private int FindPreviousLeadingLiteralStart(ReadOnlySpan<byte> input, int earliestStart, int latestStart)
    {
        if (latestStart < earliestStart)
        {
            return -1;
        }

        var relative = input.Slice(earliestStart, latestStart - earliestStart + _plan.Prefixes[0].Length).LastIndexOf(_plan.Prefixes[0]);
        return relative < 0 ? -1 : earliestStart + relative;
    }

    private bool TryMatchBoundedLeadingBeforeTrailing(ReadOnlySpan<byte> input, int leadingStart, int trailingStart)
    {
        var leadingLiteral = _plan.Prefixes[0];
        if (!AsciiStructuralIdentifierFamilyMatcher.MatchesBoundaryRequirement(_plan.LeadingLeadingBoundary, input, leadingStart) ||
            !AsciiStructuralIdentifierFamilyMatcher.MatchesBoundaryRequirement(_plan.LeadingTrailingBoundary, input, leadingStart + leadingLiteral.Length))
        {
            return false;
        }

        var gapSearchStart = leadingStart + leadingLiteral.Length;
        var separatorCount = 0;
        while (gapSearchStart + separatorCount < input.Length &&
               input[gapSearchStart + separatorCount] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n' or (byte)'\f' or (byte)'\v')
        {
            separatorCount++;
        }

        if (separatorCount < _plan.RequiredSeparatorCount)
        {
            return false;
        }

        gapSearchStart += separatorCount;
        if (trailingStart < gapSearchStart || trailingStart - gapSearchStart > _plan.MaxGap)
        {
            return false;
        }

        if (_plan.GapSameLine && input[gapSearchStart..trailingStart].IndexOfAny((byte)'\r', (byte)'\n') >= 0)
        {
            return false;
        }

        return true;
    }

    private bool TryFindNextPairedTrailingFamily(ReadOnlySpan<byte> input, int searchFrom, out int trailingStart, out int trailingLength)
    {
        trailingStart = -1;
        trailingLength = 0;
        if (_trailingFamilySearch is null || searchFrom >= input.Length)
        {
            return false;
        }

        while (searchFrom < input.Length)
        {
            if (!_trailingFamilySearch.Value.TryFindFirstMatchWithLength(input[searchFrom..], out var relativeIndex, out var matchLength))
            {
                return false;
            }

            trailingStart = searchFrom + relativeIndex;
            trailingLength = matchLength;
            if (AsciiStructuralIdentifierFamilyMatcher.MatchesBoundaryRequirement(_plan.TrailingLeadingBoundary, input, trailingStart) &&
                AsciiStructuralIdentifierFamilyMatcher.MatchesBoundaryRequirement(_plan.TrailingTrailingBoundary, input, trailingStart + matchLength))
            {
                return true;
            }

            searchFrom = trailingStart + 1;
        }

        trailingStart = -1;
        trailingLength = 0;
        return false;
    }

    private bool TryMatchPairedLeadingBeforeTrailing(ReadOnlySpan<byte> input, int trailingStart, int trailingLength, out int leadingStart)
    {
        leadingStart = -1;
        if (_plan.TrailingLiterals is not { Length: > 0 } trailingLiterals)
        {
            return false;
        }

        ReadOnlySpan<byte> leadingLiteral = default;
        var foundBranch = false;
        for (var i = 0; i < trailingLiterals.Length; i++)
        {
            var trailingLiteral = trailingLiterals[i];
            if (trailingLiteral.Length != trailingLength)
            {
                continue;
            }

            if (input[trailingStart..].StartsWith(trailingLiteral))
            {
                leadingLiteral = _plan.Prefixes[i];
                foundBranch = true;
                break;
            }
        }

        if (!foundBranch)
        {
            return false;
        }

        var maxLeadingLength = 0;
        var minLeadingLength = int.MaxValue;
        foreach (var prefix in _plan.Prefixes)
        {
            if (prefix.Length > maxLeadingLength)
            {
                maxLeadingLength = prefix.Length;
            }

            if (prefix.Length < minLeadingLength)
            {
                minLeadingLength = prefix.Length;
            }
        }

        var earliestStart = Math.Max(0, trailingStart - (_plan.MaxGap + _plan.RequiredSeparatorCount + maxLeadingLength));
        var latestStart = trailingStart - (_plan.RequiredSeparatorCount + minLeadingLength);
        if (latestStart < earliestStart)
        {
            return false;
        }

        var searchLength = latestStart - earliestStart + 1;
        while (searchLength > 0)
        {
            var relative = AsciiSearch.LastIndexOfExact(input.Slice(earliestStart, searchLength), leadingLiteral);
            if (relative < 0)
            {
                return false;
            }

            leadingStart = earliestStart + relative;
            if (!AsciiStructuralIdentifierFamilyMatcher.MatchesBoundaryRequirement(_plan.LeadingLeadingBoundary, input, leadingStart) ||
                !AsciiStructuralIdentifierFamilyMatcher.MatchesBoundaryRequirement(_plan.LeadingTrailingBoundary, input, leadingStart + leadingLiteral.Length))
            {
                searchLength = relative;
                continue;
            }

            var gapSearchStart = leadingStart + leadingLiteral.Length;
            var separatorCount = 0;
            while (gapSearchStart + separatorCount < input.Length &&
                   input[gapSearchStart + separatorCount] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n' or (byte)'\f' or (byte)'\v')
            {
                separatorCount++;
            }

            if (separatorCount < _plan.RequiredSeparatorCount)
            {
                searchLength = relative;
                continue;
            }

            gapSearchStart += separatorCount;
            if (trailingStart < gapSearchStart || trailingStart - gapSearchStart > _plan.MaxGap)
            {
                searchLength = relative;
                continue;
            }

            if (_plan.GapSameLine && input[gapSearchStart..trailingStart].IndexOfAny((byte)'\r', (byte)'\n') >= 0)
            {
                searchLength = relative;
                continue;
            }

            return true;
        }

        return false;
    }

    private bool TryGetPrefixMatchAtAnchor(ReadOnlySpan<byte> input, int anchorIndex, out int matchIndex, out int prefixLength)
    {
        matchIndex = -1;
        prefixLength = 0;
        var anchorOffset = _plan.FindOptimization.AnchorOffset;
        if (_plan.FindOptimization.AnchorPrefixesByByte is not { } anchorPrefixesByByte)
        {
            return false;
        }

        var prefix = anchorPrefixesByByte[input[anchorIndex]];
        if (prefix is null || anchorOffset >= prefix.Length || prefix[anchorOffset] != input[anchorIndex])
        {
            return false;
        }

        var candidateStart = anchorIndex - anchorOffset;
        if (candidateStart < 0 ||
            input.Length - candidateStart < prefix.Length ||
            !input.Slice(candidateStart, prefix.Length).SequenceEqual(prefix) ||
            (candidateStart > 0 && AsciiStructuralIdentifierFamilyMatcher.IsAsciiWordByte(input[candidateStart - 1])))
        {
            return false;
        }

        matchIndex = candidateStart;
        prefixLength = prefix.Length;
        return true;
    }

    private static FindNextDelegate CompileFindNext(Utf8EmittedKernelPlan plan)
    {
        var dynamicMethod = new DynamicMethod(
            $"Utf8Regex_Emit_{plan.Kind}_FindNext",
            typeof(int),
            [typeof(Utf8EmittedKernelMatcher), typeof(ReadOnlySpan<byte>), typeof(int), typeof(int).MakeByRefType()],
            typeof(Utf8EmittedKernelMatcher),
            skipVisibility: false);

        EmitCore(dynamicMethod.GetILGenerator(), plan, forCount: false);
        return dynamicMethod.CreateDelegate<FindNextDelegate>();
    }

    private static CountDelegate CompileCount(Utf8EmittedKernelPlan plan)
    {
        var dynamicMethod = new DynamicMethod(
            $"Utf8Regex_Emit_{plan.Kind}_Count",
            typeof(int),
            [typeof(Utf8EmittedKernelMatcher), typeof(ReadOnlySpan<byte>)],
            typeof(Utf8EmittedKernelMatcher),
            skipVisibility: false);

        EmitCore(dynamicMethod.GetILGenerator(), plan, forCount: true);
        return dynamicMethod.CreateDelegate<CountDelegate>();
    }

    private static void EmitCore(ILGenerator il, Utf8EmittedKernelPlan plan, bool forCount)
    {
        switch (plan.Kind)
        {
            case Utf8EmittedKernelKind.UpperWordIdentifierFamily:
                EmitUpperWordIdentifierCore(il, plan, forCount);
                return;

            case Utf8EmittedKernelKind.SharedPrefixAsciiWhitespaceSuffix:
                EmitSharedPrefixSuffixCore(il, plan, forCount);
                return;

            case Utf8EmittedKernelKind.OrderedAsciiWhitespaceLiteralWindow:
                EmitOrderedAsciiWhitespaceLiteralWindowCore(il, plan, forCount);
                return;

            case Utf8EmittedKernelKind.PairedOrderedAsciiWhitespaceLiteralWindow:
                EmitPairedOrderedAsciiWhitespaceLiteralWindowCore(il, plan, forCount);
                return;

            default:
                throw new InvalidOperationException($"Unsupported emitted kernel kind '{plan.Kind}'.");
        }
    }

    private static void EmitUpperWordIdentifierCore(ILGenerator il, Utf8EmittedKernelPlan plan, bool forCount)
    {
        var emitter = new Utf8IlEmitter(il, s_getSpanLengthMethod, s_getSpanItemMethod);
        var offsetLocal = emitter.DeclareLocal<int>();
        var matchIndexLocal = emitter.DeclareLocal<int>();
        var prefixLengthLocal = emitter.DeclareLocal<int>();
        var indexLocal = emitter.DeclareLocal<int>();
        var inputLengthLocal = emitter.DeclareLocal<int>();
        var separatorCountLocal = emitter.DeclareLocal<int>();
        var valueLocal = emitter.DeclareLocal<byte>();
        var relativeIndexLocal = emitter.DeclareLocal<int>();
        var countLocal = forCount ? emitter.DeclareLocal<int>() : null;

        var searchLoopLabel = emitter.DefineLabel();
        var continueScanLabel = emitter.DefineLabel();
        var separatorLoopCheckLabel = emitter.DefineLabel();
        var separatorLoopConsumeLabel = emitter.DefineLabel();
        var afterSeparatorLoopLabel = emitter.DefineLabel();
        var tailLoopLabel = emitter.DefineLabel();
        var successLabel = emitter.DefineLabel();
        var prefixMatchedLabel = emitter.DefineLabel();
        var advanceLabel = emitter.DefineLabel();
        var notFoundLabel = emitter.DefineLabel();

        emitter.LoadInputLength();
        emitter.StoreLocal(inputLengthLocal);

        if (forCount)
        {
            emitter.LdcI4(0);
        }
        else
        {
            emitter.LoadArg(2);
        }

        emitter.StoreLocal(offsetLocal);

        if (forCount)
        {
            emitter.LdcI4(0);
            emitter.StoreLocal(countLocal!);
        }

        emitter.MarkLabel(searchLoopLabel);
        emitter.LoadLocal(offsetLocal);
        emitter.LoadLocal(inputLengthLocal);
        emitter.Emit(OpCodes.Bge, notFoundLabel);

        emitter.LoadArg(0);
        emitter.LoadArg(1);
        emitter.LoadLocal(offsetLocal);
        emitter.Emit(OpCodes.Call, s_findNextAnchorRelativeMethod);
        emitter.StoreLocal(relativeIndexLocal);
        emitter.LoadLocal(relativeIndexLocal);
        emitter.LdcI4(0);
        emitter.Emit(OpCodes.Blt, notFoundLabel);

        emitter.LoadLocal(offsetLocal);
        emitter.LoadLocal(relativeIndexLocal);
        emitter.Emit(OpCodes.Add);
        emitter.StoreLocal(offsetLocal);

        EmitUpperWordIdentifierPrefixDispatch(
            emitter,
            plan,
            offsetLocal,
            matchIndexLocal,
            prefixLengthLocal,
            inputLengthLocal,
            valueLocal,
            prefixMatchedLabel,
            continueScanLabel);

        emitter.MarkLabel(prefixMatchedLabel);
        emitter.LoadLocal(matchIndexLocal);
        emitter.LoadLocal(prefixLengthLocal);
        emitter.Emit(OpCodes.Add);
        emitter.StoreLocal(indexLocal);

        emitter.LdcI4(0);
        emitter.StoreLocal(separatorCountLocal);

        emitter.MarkLabel(separatorLoopCheckLabel);
        emitter.LoadLocal(indexLocal);
        emitter.LoadLocal(inputLengthLocal);
        emitter.Emit(OpCodes.Bge, afterSeparatorLoopLabel);
        emitter.LoadInputByte(indexLocal);
        emitter.StoreLocal(valueLocal);
        emitter.EmitAsciiWhitespaceBranch(valueLocal, separatorLoopConsumeLabel, afterSeparatorLoopLabel);
        emitter.MarkLabel(separatorLoopConsumeLabel);
        emitter.AddToLocal(indexLocal, 1);
        emitter.AddToLocal(separatorCountLocal, 1);
        emitter.Emit(OpCodes.Br, separatorLoopCheckLabel);

        emitter.MarkLabel(afterSeparatorLoopLabel);
        emitter.LoadLocal(separatorCountLocal);
        emitter.LdcI4(1);
        emitter.Emit(OpCodes.Blt, advanceLabel);

        emitter.LoadLocal(indexLocal);
        emitter.LoadLocal(inputLengthLocal);
        emitter.Emit(OpCodes.Bge, advanceLabel);
        emitter.LoadInputByte(indexLocal);
        emitter.StoreLocal(valueLocal);
        emitter.EmitAsciiUpperBranch(valueLocal, successLabel: null, failureLabel: advanceLabel);

        emitter.AddToLocal(indexLocal, 1);

        emitter.MarkLabel(tailLoopLabel);
        emitter.LoadLocal(indexLocal);
        emitter.LoadLocal(inputLengthLocal);
        emitter.Emit(OpCodes.Bge, successLabel);
        emitter.LoadInputByte(indexLocal);
        emitter.StoreLocal(valueLocal);
        emitter.EmitAsciiWordBranch(valueLocal, successLabel: null, failureLabel: successLabel);
        emitter.AddToLocal(indexLocal, 1);
        emitter.Emit(OpCodes.Br, tailLoopLabel);

        emitter.MarkLabel(successLabel);
        if (forCount)
        {
            emitter.AddToLocal(countLocal!, 1);
            emitter.LoadLocal(indexLocal);
            emitter.StoreLocal(offsetLocal);
            emitter.Emit(OpCodes.Br, searchLoopLabel);
        }
        else
        {
            emitter.LoadArg(3);
            emitter.LoadLocal(indexLocal);
            emitter.LoadLocal(matchIndexLocal);
            emitter.Emit(OpCodes.Sub);
            emitter.Emit(OpCodes.Stind_I4);
            emitter.LoadLocal(matchIndexLocal);
            emitter.Emit(OpCodes.Ret);
        }

        emitter.MarkLabel(continueScanLabel);
        emitter.AddToLocal(offsetLocal, 1);
        emitter.Emit(OpCodes.Br, searchLoopLabel);

        emitter.MarkLabel(advanceLabel);
        emitter.AddToLocal(offsetLocal, 1);
        emitter.Emit(OpCodes.Br, searchLoopLabel);

        emitter.MarkLabel(notFoundLabel);
        if (forCount)
        {
            emitter.LoadLocal(countLocal!);
            emitter.Emit(OpCodes.Ret);
        }
        else
        {
            emitter.LoadArg(3);
            emitter.LdcI4(0);
            emitter.Emit(OpCodes.Stind_I4);
            emitter.LdcI4(-1);
            emitter.Emit(OpCodes.Ret);
        }
    }

    private static void EmitUpperWordIdentifierPrefixDispatch(
        Utf8IlEmitter emitter,
        Utf8EmittedKernelPlan plan,
        LocalBuilder anchorIndexLocal,
        LocalBuilder matchIndexLocal,
        LocalBuilder prefixLengthLocal,
        LocalBuilder inputLengthLocal,
        LocalBuilder valueLocal,
        Label matchedLabel,
        Label failureLabel)
    {
        var anchorOffset = plan.FindOptimization.AnchorOffset;
        var prefixes = plan.Prefixes;

        emitter.LoadLocal(anchorIndexLocal);
        emitter.LdcI4(anchorOffset);
        emitter.Emit(OpCodes.Sub);
        emitter.StoreLocal(matchIndexLocal);

        emitter.LoadLocal(matchIndexLocal);
        emitter.LdcI4(0);
        emitter.Emit(OpCodes.Blt, failureLabel);

        var pastBoundaryCheckLabel = emitter.DefineLabel();
        emitter.LoadLocal(matchIndexLocal);
        emitter.Emit(OpCodes.Brfalse, pastBoundaryCheckLabel);

        emitter.LoadInputByte(matchIndexLocal, -1);
        emitter.StoreLocal(valueLocal);
        emitter.EmitAsciiWordBranch(valueLocal, successLabel: failureLabel, failureLabel: pastBoundaryCheckLabel);
        emitter.MarkLabel(pastBoundaryCheckLabel);

        emitter.LoadInputByte(anchorIndexLocal);
        emitter.StoreLocal(valueLocal);

        for (var i = 0; i < prefixes.Length; i++)
        {
            var nextPrefixLabel = emitter.DefineLabel();
            emitter.LoadLocal(valueLocal);
            emitter.LdcI4(prefixes[i][anchorOffset]);
            emitter.Emit(OpCodes.Bne_Un, nextPrefixLabel);
            EmitUpperWordIdentifierPrefixMatch(emitter, prefixes[i], anchorOffset, matchIndexLocal, prefixLengthLocal, inputLengthLocal, matchedLabel, nextPrefixLabel);
            emitter.MarkLabel(nextPrefixLabel);
        }

        emitter.Emit(OpCodes.Br, failureLabel);
    }

    private static void EmitUpperWordIdentifierPrefixMatch(
        Utf8IlEmitter emitter,
        byte[] prefix,
        int anchorOffset,
        LocalBuilder matchIndexLocal,
        LocalBuilder prefixLengthLocal,
        LocalBuilder inputLengthLocal,
        Label successLabel,
        Label failureLabel)
    {
        var passLengthLabel = emitter.DefineLabel();
        var suffixMatchedLabel = emitter.DefineLabel();
        emitter.EmitEnsureAvailable(matchIndexLocal, inputLengthLocal, prefix.Length, passLengthLabel, failureLabel);
        emitter.MarkLabel(passLengthLabel);
        emitter.EmitSpanLiteralEquals(matchIndexLocal, prefix, anchorOffset + 1, suffixMatchedLabel, failureLabel);
        emitter.MarkLabel(suffixMatchedLabel);
        for (var i = 0; i < anchorOffset; i++)
        {
            emitter.LoadInputByte(matchIndexLocal, i);
            emitter.LdcI4(prefix[i]);
            emitter.Emit(OpCodes.Bne_Un, failureLabel);
        }
        emitter.LdcI4(prefix.Length);
        emitter.StoreLocal(prefixLengthLocal);
        emitter.Emit(OpCodes.Br, successLabel);
    }

    private static void EmitSharedPrefixSuffixCore(ILGenerator il, Utf8EmittedKernelPlan plan, bool forCount)
    {
        var emitter = new Utf8IlEmitter(il, s_getSpanLengthMethod, s_getSpanItemMethod);
        var offsetLocal = emitter.DeclareLocal<int>();
        var relativeIndexLocal = emitter.DeclareLocal<int>();
        var matchIndexLocal = emitter.DeclareLocal<int>();
        var matchedLengthLocal = emitter.DeclareLocal<int>();
        var inputLengthLocal = emitter.DeclareLocal<int>();
        var discriminatorIndexLocal = emitter.DeclareLocal<int>();
        var indexLocal = emitter.DeclareLocal<int>();
        var valueLocal = emitter.DeclareLocal<byte>();
        var countLocal = forCount ? emitter.DeclareLocal<int>() : null;

        var searchLoopLabel = emitter.DefineLabel();
        var notFoundLabel = emitter.DefineLabel();
        var matchTryLabel = emitter.DefineLabel();
        var advanceLabel = emitter.DefineLabel();
        var suffixLabel = emitter.DefineLabel();
        var successLabel = emitter.DefineLabel();

        emitter.LoadInputLength();
        emitter.StoreLocal(inputLengthLocal);

        if (forCount)
        {
            emitter.LdcI4(0);
            emitter.StoreLocal(offsetLocal);
            emitter.LdcI4(0);
            emitter.StoreLocal(countLocal!);
        }
        else
        {
            emitter.LoadArg(2);
            emitter.StoreLocal(offsetLocal);
        }

        emitter.MarkLabel(searchLoopLabel);
        emitter.LoadLocal(offsetLocal);
        emitter.LoadLocal(inputLengthLocal);
        emitter.Emit(OpCodes.Bge, notFoundLabel);

        emitter.LoadArg(0);
        emitter.LoadArg(1);
        emitter.LoadLocal(offsetLocal);
        emitter.Emit(OpCodes.Call, s_findNextCommonPrefixRelativeMethod);
        emitter.StoreLocal(relativeIndexLocal);
        emitter.LoadLocal(relativeIndexLocal);
        emitter.LdcI4(0);
        emitter.Emit(OpCodes.Blt, notFoundLabel);

        emitter.LoadLocal(offsetLocal);
        emitter.LoadLocal(relativeIndexLocal);
        emitter.Emit(OpCodes.Add);
        emitter.StoreLocal(matchIndexLocal);
        emitter.Emit(OpCodes.Br, matchTryLabel);

        emitter.MarkLabel(advanceLabel);
        emitter.LoadLocal(matchIndexLocal);
        emitter.LdcI4(1);
        emitter.Emit(OpCodes.Add);
        emitter.StoreLocal(offsetLocal);
        emitter.Emit(OpCodes.Br, searchLoopLabel);

        emitter.MarkLabel(matchTryLabel);
        EmitSharedPrefixSuffixMatcher(
            emitter,
            matchIndexLocal,
            matchedLengthLocal,
            inputLengthLocal,
            discriminatorIndexLocal,
            indexLocal,
            valueLocal,
            successLabel,
            advanceLabel);

        emitter.MarkLabel(successLabel);
        if (forCount)
        {
            emitter.LoadLocal(countLocal!);
            emitter.LdcI4(1);
            emitter.Emit(OpCodes.Add);
            emitter.StoreLocal(countLocal!);
            emitter.LoadLocal(matchIndexLocal);
            emitter.LoadLocal(matchedLengthLocal);
            emitter.Emit(OpCodes.Add);
            emitter.StoreLocal(offsetLocal);
            emitter.Emit(OpCodes.Br, searchLoopLabel);
        }
        else
        {
            emitter.LoadArg(3);
            emitter.LoadLocal(matchedLengthLocal);
            emitter.Emit(OpCodes.Stind_I4);
            emitter.LoadLocal(matchIndexLocal);
            emitter.Emit(OpCodes.Ret);
        }

        emitter.MarkLabel(notFoundLabel);
        if (forCount)
        {
            emitter.LoadLocal(countLocal!);
            emitter.Emit(OpCodes.Ret);
        }
        else
        {
            emitter.LoadArg(3);
            emitter.LdcI4(0);
            emitter.Emit(OpCodes.Stind_I4);
            emitter.LdcI4(-1);
            emitter.Emit(OpCodes.Ret);
        }

        void EmitSharedPrefixSuffixMatcher(
            Utf8IlEmitter e,
            LocalBuilder matchIndex,
            LocalBuilder matchedLength,
            LocalBuilder inputLength,
            LocalBuilder discriminatorIndex,
            LocalBuilder index,
            LocalBuilder value,
            Label success,
            Label failure)
        {
            var pastLeadingBoundaryLabel = e.DefineLabel();

            e.LoadLocal(matchIndex);
            e.Emit(OpCodes.Brfalse, pastLeadingBoundaryLabel);
            e.LoadInputByte(matchIndex, -1);
            e.StoreLocal(value);
            e.EmitAsciiWordBranch(value, successLabel: failure, failureLabel: pastLeadingBoundaryLabel);
            e.MarkLabel(pastLeadingBoundaryLabel);

            if (plan.FindOptimization.PrefixDiscriminator.HasValue)
            {
                e.LoadLocal(matchIndex);
                e.LdcI4(plan.FindOptimization.PrefixDiscriminator.Offset);
                e.Emit(OpCodes.Add);
                e.StoreLocal(discriminatorIndex);

                e.LoadLocal(discriminatorIndex);
                e.LoadLocal(inputLength);
                e.Emit(OpCodes.Bge_Un, failure);

                e.LoadInputByte(discriminatorIndex);
                e.StoreLocal(value);

                for (var i = 0; i < plan.Prefixes.Length; i++)
                {
                    var literal = plan.Prefixes[i];
                    var nextLiteralLabel = e.DefineLabel();
                    e.LoadLocal(value);
                    e.LdcI4(literal[plan.FindOptimization.PrefixDiscriminator.Offset]);
                    e.Emit(OpCodes.Bne_Un, nextLiteralLabel);
                    EmitSharedPrefixLiteralMatch(e, plan, literal, matchIndex, inputLength, index, value, suffixLabel, failure);
                    e.MarkLabel(nextLiteralLabel);
                }

                e.Emit(OpCodes.Br, failure);
            }
            else
            {
                for (var i = 0; i < plan.Prefixes.Length; i++)
                {
                    var literal = plan.Prefixes[i];
                    var nextLiteralLabel = e.DefineLabel();
                    EmitSharedPrefixLiteralMatch(e, plan, literal, matchIndex, inputLength, index, value, suffixLabel, nextLiteralLabel);
                    e.MarkLabel(nextLiteralLabel);
                }

                e.Emit(OpCodes.Br, failure);
            }

            e.MarkLabel(suffixLabel);
            e.LoadLocal(index);
            e.LoadLocal(inputLength);
            e.Emit(OpCodes.Bge_Un, failure);
            e.LoadInputByte(index);
            e.LdcI4(plan.RequiredSuffixByte);
            e.Emit(OpCodes.Bne_Un, failure);
            e.LoadLocal(index);
            e.LdcI4(1);
            e.Emit(OpCodes.Add);
            e.LoadLocal(matchIndex);
            e.Emit(OpCodes.Sub);
            e.StoreLocal(matchedLength);
            e.Emit(OpCodes.Br, success);
        }
    }

    private static void EmitSharedPrefixLiteralMatch(
        Utf8IlEmitter emitter,
        Utf8EmittedKernelPlan plan,
        byte[] literal,
        LocalBuilder matchIndexLocal,
        LocalBuilder inputLengthLocal,
        LocalBuilder indexLocal,
        LocalBuilder valueLocal,
        Label successLabel,
        Label failureLabel)
    {
        var commonPrefixLength = plan.FindOptimization.CommonPrefix.Length;

        var enoughLengthLabel = emitter.DefineLabel();
        var literalMatchedLabel = emitter.DefineLabel();
        emitter.EmitEnsureAvailable(matchIndexLocal, inputLengthLocal, literal.Length, enoughLengthLabel, failureLabel);
        emitter.MarkLabel(enoughLengthLabel);

        emitter.EmitSpanLiteralEquals(matchIndexLocal, literal, commonPrefixLength, literalMatchedLabel, failureLabel);
        emitter.MarkLabel(literalMatchedLabel);
        emitter.LoadLocal(matchIndexLocal);
        emitter.LdcI4(literal.Length);
        emitter.Emit(OpCodes.Add);
        emitter.StoreLocal(indexLocal);

        var whitespaceLoopLabel = emitter.DefineLabel();
        var whitespaceConsumeLabel = emitter.DefineLabel();
        emitter.MarkLabel(whitespaceLoopLabel);
        emitter.LoadLocal(indexLocal);
        emitter.LoadLocal(inputLengthLocal);
        emitter.Emit(OpCodes.Bge, successLabel);
        emitter.LoadInputByte(indexLocal);
        emitter.StoreLocal(valueLocal);
        emitter.EmitAsciiWhitespaceBranch(valueLocal, whitespaceConsumeLabel, successLabel);
        emitter.MarkLabel(whitespaceConsumeLabel);
        emitter.AddToLocal(indexLocal, 1);
        emitter.Emit(OpCodes.Br, whitespaceLoopLabel);
    }

    private static void EmitOrderedAsciiWhitespaceLiteralWindowCore(ILGenerator il, Utf8EmittedKernelPlan plan, bool forCount)
    {
        if (plan.MaxGap > 0)
        {
            EmitBoundedOrderedAsciiWhitespaceLiteralWindowCore(il, plan, forCount);
            return;
        }

        var emitter = new Utf8IlEmitter(il, s_getSpanLengthMethod, s_getSpanItemMethod);
        var searchFromLocal = emitter.DeclareLocal<int>();
        var relativeIndexLocal = emitter.DeclareLocal<int>();
        var trailingStartLocal = emitter.DeclareLocal<int>();
        var separatorStartLocal = emitter.DeclareLocal<int>();
        var leadingStartLocal = emitter.DeclareLocal<int>();
        var indexLocal = emitter.DeclareLocal<int>();
        var matchedLengthLocal = emitter.DeclareLocal<int>();
        var inputLengthLocal = emitter.DeclareLocal<int>();
        var valueLocal = emitter.DeclareLocal<byte>();
        var countLocal = forCount ? emitter.DeclareLocal<int>() : null;

        var searchLoopLabel = emitter.DefineLabel();
        var notFoundLabel = emitter.DefineLabel();
        var searchContinueLabel = emitter.DefineLabel();
        var separatorLoopLabel = emitter.DefineLabel();
        var separatorDoneLabel = emitter.DefineLabel();
        var leadingCheckLabel = emitter.DefineLabel();
        var successLabel = emitter.DefineLabel();

        emitter.LoadInputLength();
        emitter.StoreLocal(inputLengthLocal);

        if (forCount)
        {
            emitter.LdcI4(plan.Prefixes[0].Length + plan.RequiredSeparatorCount);
            emitter.StoreLocal(searchFromLocal);
            emitter.LdcI4(0);
            emitter.StoreLocal(countLocal!);
        }
        else
        {
            emitter.LoadArg(2);
            emitter.LdcI4(plan.Prefixes[0].Length + plan.RequiredSeparatorCount);
            emitter.Emit(OpCodes.Add);
            emitter.StoreLocal(searchFromLocal);
        }

        emitter.MarkLabel(searchLoopLabel);
        emitter.LoadLocal(searchFromLocal);
        emitter.LoadLocal(inputLengthLocal);
        emitter.LdcI4(plan.FindOptimization.CommonPrefix.Length);
        emitter.Emit(OpCodes.Sub);
        emitter.Emit(OpCodes.Bgt, notFoundLabel);

        emitter.LoadArg(0);
        emitter.LoadArg(1);
        emitter.LoadLocal(searchFromLocal);
        emitter.Emit(OpCodes.Call, s_findNextCommonPrefixRelativeMethod);
        emitter.StoreLocal(relativeIndexLocal);
        emitter.LoadLocal(relativeIndexLocal);
        emitter.LdcI4(0);
        emitter.Emit(OpCodes.Blt, notFoundLabel);

        emitter.LoadLocal(searchFromLocal);
        emitter.LoadLocal(relativeIndexLocal);
        emitter.Emit(OpCodes.Add);
        emitter.StoreLocal(trailingStartLocal);

        emitter.LoadLocal(trailingStartLocal);
        emitter.StoreLocal(separatorStartLocal);

        emitter.MarkLabel(separatorLoopLabel);
        emitter.LoadLocal(separatorStartLocal);
        if (forCount)
        {
            emitter.LdcI4(0);
        }
        else
        {
            emitter.LoadArg(2);
        }
        emitter.Emit(OpCodes.Ble, searchContinueLabel);
        emitter.LoadInputByte(separatorStartLocal, -1);
        emitter.StoreLocal(valueLocal);
        emitter.EmitAsciiWhitespaceBranch(valueLocal, separatorDoneLabel, leadingCheckLabel);
        emitter.MarkLabel(separatorDoneLabel);
        emitter.AddToLocal(separatorStartLocal, -1);
        emitter.Emit(OpCodes.Br, separatorLoopLabel);

        emitter.MarkLabel(leadingCheckLabel);
        emitter.LoadLocal(trailingStartLocal);
        emitter.LoadLocal(separatorStartLocal);
        emitter.Emit(OpCodes.Sub);
        emitter.LdcI4(plan.RequiredSeparatorCount);
        emitter.Emit(OpCodes.Blt, searchContinueLabel);

        emitter.LoadLocal(separatorStartLocal);
        emitter.LdcI4(plan.Prefixes[0].Length);
        emitter.Emit(OpCodes.Sub);
        emitter.StoreLocal(leadingStartLocal);

        if (!forCount)
        {
            emitter.LoadLocal(leadingStartLocal);
            emitter.LoadArg(2);
            emitter.Emit(OpCodes.Blt, searchContinueLabel);
        }

        emitter.LoadLocal(leadingStartLocal);
        emitter.LdcI4(0);
        emitter.Emit(OpCodes.Blt, searchContinueLabel);

        var enoughLeadingLabel = emitter.DefineLabel();
        emitter.EmitEnsureAvailable(leadingStartLocal, inputLengthLocal, plan.Prefixes[0].Length, enoughLeadingLabel, searchContinueLabel);
        emitter.MarkLabel(enoughLeadingLabel);
        emitter.EmitSpanLiteralEquals(leadingStartLocal, plan.Prefixes[0], 0, successLabel, searchContinueLabel);

        emitter.MarkLabel(successLabel);
        var pastLeadingBoundaryLabel = emitter.DefineLabel();
        var pastTrailingBoundaryLabel = emitter.DefineLabel();

        emitter.LoadLocal(leadingStartLocal);
        emitter.Emit(OpCodes.Brfalse, pastLeadingBoundaryLabel);
        emitter.LoadInputByte(leadingStartLocal, -1);
        emitter.StoreLocal(valueLocal);
        emitter.EmitAsciiWordBranch(valueLocal, successLabel: searchContinueLabel, failureLabel: pastLeadingBoundaryLabel);
        emitter.MarkLabel(pastLeadingBoundaryLabel);

        emitter.LoadLocal(trailingStartLocal);
        emitter.LdcI4(plan.FindOptimization.CommonPrefix.Length);
        emitter.Emit(OpCodes.Add);
        emitter.StoreLocal(indexLocal);
        emitter.LoadLocal(indexLocal);
        emitter.LoadLocal(inputLengthLocal);
        emitter.Emit(OpCodes.Bge, pastTrailingBoundaryLabel);
        emitter.LoadInputByte(indexLocal);
        emitter.StoreLocal(valueLocal);
        emitter.EmitAsciiWordBranch(valueLocal, successLabel: searchContinueLabel, failureLabel: pastTrailingBoundaryLabel);
        emitter.MarkLabel(pastTrailingBoundaryLabel);

        emitter.LoadLocal(trailingStartLocal);
        emitter.LdcI4(plan.FindOptimization.CommonPrefix.Length);
        emitter.Emit(OpCodes.Add);
        emitter.LoadLocal(leadingStartLocal);
        emitter.Emit(OpCodes.Sub);
        emitter.StoreLocal(matchedLengthLocal);

        if (forCount)
        {
            emitter.AddToLocal(countLocal!, 1);
            emitter.LoadLocal(leadingStartLocal);
            emitter.LoadLocal(matchedLengthLocal);
            emitter.Emit(OpCodes.Add);
            emitter.StoreLocal(searchFromLocal);
            emitter.Emit(OpCodes.Br, searchLoopLabel);
        }
        else
        {
            emitter.LoadArg(3);
            emitter.LoadLocal(matchedLengthLocal);
            emitter.Emit(OpCodes.Stind_I4);
            emitter.LoadLocal(leadingStartLocal);
            emitter.Emit(OpCodes.Ret);
        }

        emitter.MarkLabel(searchContinueLabel);
        emitter.LoadLocal(trailingStartLocal);
        emitter.LdcI4(1);
        emitter.Emit(OpCodes.Add);
        emitter.StoreLocal(searchFromLocal);
        emitter.Emit(OpCodes.Br, searchLoopLabel);

        emitter.MarkLabel(notFoundLabel);
        if (forCount)
        {
            emitter.LoadLocal(countLocal!);
            emitter.Emit(OpCodes.Ret);
        }
        else
        {
            emitter.LoadArg(3);
            emitter.LdcI4(0);
            emitter.Emit(OpCodes.Stind_I4);
            emitter.LdcI4(-1);
            emitter.Emit(OpCodes.Ret);
        }
    }

    private static void EmitBoundedOrderedAsciiWhitespaceLiteralWindowCore(ILGenerator il, Utf8EmittedKernelPlan plan, bool forCount)
    {
        var emitter = new Utf8IlEmitter(il, s_getSpanLengthMethod, s_getSpanItemMethod);
        var searchFromLocal = emitter.DeclareLocal<int>();
        var relativeIndexLocal = emitter.DeclareLocal<int>();
        var trailingStartLocal = emitter.DeclareLocal<int>();
        var earliestLeadingStartLocal = emitter.DeclareLocal<int>();
        var latestLeadingStartLocal = emitter.DeclareLocal<int>();
        var candidateLeadingStartLocal = emitter.DeclareLocal<int>();
        var inputLengthLocal = emitter.DeclareLocal<int>();
        var indexLocal = emitter.DeclareLocal<int>();
        var valueLocal = emitter.DeclareLocal<byte>();
        var matchedLengthLocal = emitter.DeclareLocal<int>();
        var countLocal = forCount ? emitter.DeclareLocal<int>() : null;

        var searchLoopLabel = emitter.DefineLabel();
        var leadingSearchLoopLabel = emitter.DefineLabel();
        var leadingTryMatchLabel = emitter.DefineLabel();
        var successLabel = emitter.DefineLabel();
        var searchContinueLabel = emitter.DefineLabel();
        var notFoundLabel = emitter.DefineLabel();

        emitter.LoadInputLength();
        emitter.StoreLocal(inputLengthLocal);

        if (forCount)
        {
            emitter.LdcI4(plan.Prefixes[0].Length + plan.RequiredSeparatorCount);
            emitter.StoreLocal(searchFromLocal);
            emitter.LdcI4(0);
            emitter.StoreLocal(countLocal!);
        }
        else
        {
            emitter.LoadArg(2);
            emitter.LdcI4(plan.Prefixes[0].Length + plan.RequiredSeparatorCount);
            emitter.Emit(OpCodes.Add);
            emitter.StoreLocal(searchFromLocal);
        }

        emitter.MarkLabel(searchLoopLabel);
        emitter.LoadLocal(searchFromLocal);
        emitter.LoadLocal(inputLengthLocal);
        emitter.LdcI4(plan.FindOptimization.CommonPrefix.Length);
        emitter.Emit(OpCodes.Sub);
        emitter.Emit(OpCodes.Bgt, notFoundLabel);

        emitter.LoadArg(0);
        emitter.LoadArg(1);
        emitter.LoadLocal(searchFromLocal);
        emitter.Emit(OpCodes.Call, s_findNextCommonPrefixRelativeMethod);
        emitter.StoreLocal(relativeIndexLocal);
        emitter.LoadLocal(relativeIndexLocal);
        emitter.LdcI4(0);
        emitter.Emit(OpCodes.Blt, notFoundLabel);

        emitter.LoadLocal(searchFromLocal);
        emitter.LoadLocal(relativeIndexLocal);
        emitter.Emit(OpCodes.Add);
        emitter.StoreLocal(trailingStartLocal);

        if (plan.TrailingLeadingBoundary == Utf8BoundaryRequirement.Boundary)
        {
            var trailingLeadingBoundaryOkLabel = emitter.DefineLabel();
            emitter.LoadLocal(trailingStartLocal);
            emitter.Emit(OpCodes.Brfalse, trailingLeadingBoundaryOkLabel);
            emitter.LoadInputByte(trailingStartLocal, -1);
            emitter.StoreLocal(valueLocal);
            emitter.EmitAsciiWordBranch(valueLocal, successLabel: searchContinueLabel, failureLabel: trailingLeadingBoundaryOkLabel);
            emitter.MarkLabel(trailingLeadingBoundaryOkLabel);
        }

        if (plan.TrailingTrailingBoundary == Utf8BoundaryRequirement.Boundary)
        {
            var trailingTrailingBoundaryOkLabel = emitter.DefineLabel();
            emitter.EmitStoreSum(indexLocal, trailingStartLocal, plan.FindOptimization.CommonPrefix.Length);
            emitter.LoadLocal(indexLocal);
            emitter.LoadLocal(inputLengthLocal);
            emitter.Emit(OpCodes.Bge, trailingTrailingBoundaryOkLabel);
            emitter.LoadInputByte(indexLocal);
            emitter.StoreLocal(valueLocal);
            emitter.EmitAsciiWordBranch(valueLocal, successLabel: searchContinueLabel, failureLabel: trailingTrailingBoundaryOkLabel);
            emitter.MarkLabel(trailingTrailingBoundaryOkLabel);
        }

        emitter.EmitStoreDifference(earliestLeadingStartLocal, trailingStartLocal, plan.MaxGap + plan.RequiredSeparatorCount + plan.Prefixes[0].Length);
        emitter.EmitClampLocalMinZero(earliestLeadingStartLocal);

        if (!forCount)
        {
            var startBoundOkLabel = emitter.DefineLabel();
            emitter.LoadLocal(earliestLeadingStartLocal);
            emitter.LoadArg(2);
            emitter.Emit(OpCodes.Bge, startBoundOkLabel);
            emitter.LoadArg(2);
            emitter.StoreLocal(earliestLeadingStartLocal);
            emitter.MarkLabel(startBoundOkLabel);
        }

        emitter.EmitStoreDifference(latestLeadingStartLocal, trailingStartLocal, plan.RequiredSeparatorCount + plan.Prefixes[0].Length);
        emitter.EmitLocalLessThanLocalBranch(latestLeadingStartLocal, earliestLeadingStartLocal, searchContinueLabel, leadingSearchLoopLabel);

        emitter.MarkLabel(leadingSearchLoopLabel);
        emitter.LoadArg(0);
        emitter.LoadArg(1);
        emitter.LoadLocal(earliestLeadingStartLocal);
        emitter.LoadLocal(latestLeadingStartLocal);
        emitter.Emit(OpCodes.Call, s_findPreviousLeadingLiteralStartMethod);
        emitter.StoreLocal(candidateLeadingStartLocal);
        emitter.LoadLocal(candidateLeadingStartLocal);
        emitter.LdcI4(0);
        emitter.Emit(OpCodes.Blt, searchContinueLabel);

        emitter.MarkLabel(leadingTryMatchLabel);
        emitter.LoadArg(0);
        emitter.LoadArg(1);
        emitter.LoadLocal(candidateLeadingStartLocal);
        emitter.LoadLocal(trailingStartLocal);
        emitter.Emit(OpCodes.Call, s_tryMatchBoundedLeadingBeforeTrailingMethod);
        emitter.Emit(OpCodes.Brtrue, successLabel);

        emitter.EmitStoreDifference(latestLeadingStartLocal, candidateLeadingStartLocal, 1);
        emitter.EmitLocalLessThanLocalBranch(latestLeadingStartLocal, earliestLeadingStartLocal, searchContinueLabel, leadingSearchLoopLabel);

        emitter.MarkLabel(successLabel);
        emitter.EmitStoreSum(indexLocal, trailingStartLocal, plan.FindOptimization.CommonPrefix.Length);
        emitter.EmitStoreDifference(matchedLengthLocal, indexLocal, candidateLeadingStartLocal);

        if (forCount)
        {
            emitter.AddToLocal(countLocal!, 1);
            emitter.LoadLocal(candidateLeadingStartLocal);
            emitter.LoadLocal(matchedLengthLocal);
            emitter.Emit(OpCodes.Add);
            emitter.StoreLocal(searchFromLocal);
            emitter.Emit(OpCodes.Br, searchLoopLabel);
        }
        else
        {
            emitter.LoadArg(3);
            emitter.LoadLocal(matchedLengthLocal);
            emitter.Emit(OpCodes.Stind_I4);
            emitter.LoadLocal(candidateLeadingStartLocal);
            emitter.Emit(OpCodes.Ret);
        }

        emitter.MarkLabel(searchContinueLabel);
        emitter.EmitStoreSum(searchFromLocal, trailingStartLocal, 1);
        emitter.Emit(OpCodes.Br, searchLoopLabel);

        emitter.MarkLabel(notFoundLabel);
        if (forCount)
        {
            emitter.LoadLocal(countLocal!);
            emitter.Emit(OpCodes.Ret);
        }
        else
        {
            emitter.LoadArg(3);
            emitter.LdcI4(0);
            emitter.Emit(OpCodes.Stind_I4);
            emitter.LdcI4(-1);
            emitter.Emit(OpCodes.Ret);
        }
    }

    private static void EmitPairedOrderedAsciiWhitespaceLiteralWindowCore(ILGenerator il, Utf8EmittedKernelPlan plan, bool forCount)
    {
        var emitter = new Utf8IlEmitter(il, s_getSpanLengthMethod, s_getSpanItemMethod);
        var searchFromLocal = emitter.DeclareLocal<int>();
        var trailingStartLocal = emitter.DeclareLocal<int>();
        var trailingLengthLocal = emitter.DeclareLocal<int>();
        var leadingStartLocal = emitter.DeclareLocal<int>();
        var matchedLengthLocal = emitter.DeclareLocal<int>();
        var countLocal = forCount ? emitter.DeclareLocal<int>() : null;

        var searchLoopLabel = emitter.DefineLabel();
        var searchContinueLabel = emitter.DefineLabel();
        var successLabel = emitter.DefineLabel();
        var notFoundLabel = emitter.DefineLabel();

        if (forCount)
        {
            emitter.LdcI4(plan.RequiredSeparatorCount + 1);
            emitter.StoreLocal(searchFromLocal);
            emitter.LdcI4(0);
            emitter.StoreLocal(countLocal!);
        }
        else
        {
            emitter.LoadArg(2);
            emitter.LdcI4(plan.RequiredSeparatorCount + 1);
            emitter.Emit(OpCodes.Add);
            emitter.StoreLocal(searchFromLocal);
        }

        emitter.MarkLabel(searchLoopLabel);
        emitter.LoadArg(0);
        emitter.LoadArg(1);
        emitter.LoadLocal(searchFromLocal);
        emitter.LoadLocalAddress(trailingStartLocal);
        emitter.LoadLocalAddress(trailingLengthLocal);
        emitter.Emit(OpCodes.Call, s_tryFindNextPairedTrailingFamilyMethod);
        emitter.Emit(OpCodes.Brfalse, notFoundLabel);

        emitter.LoadArg(0);
        emitter.LoadArg(1);
        emitter.LoadLocal(trailingStartLocal);
        emitter.LoadLocal(trailingLengthLocal);
        emitter.LoadLocalAddress(leadingStartLocal);
        emitter.Emit(OpCodes.Call, s_tryMatchPairedLeadingBeforeTrailingMethod);
        emitter.Emit(OpCodes.Brtrue, successLabel);

        emitter.MarkLabel(searchContinueLabel);
        emitter.EmitStoreSum(searchFromLocal, trailingStartLocal, 1);
        emitter.Emit(OpCodes.Br, searchLoopLabel);

        emitter.MarkLabel(successLabel);
        emitter.LoadLocal(trailingStartLocal);
        emitter.LoadLocal(trailingLengthLocal);
        emitter.Emit(OpCodes.Add);
        emitter.LoadLocal(leadingStartLocal);
        emitter.Emit(OpCodes.Sub);
        emitter.StoreLocal(matchedLengthLocal);

        if (forCount)
        {
            emitter.AddToLocal(countLocal!, 1);
            emitter.LoadLocal(leadingStartLocal);
            emitter.LoadLocal(matchedLengthLocal);
            emitter.Emit(OpCodes.Add);
            emitter.StoreLocal(searchFromLocal);
            emitter.Emit(OpCodes.Br, searchLoopLabel);
        }
        else
        {
            emitter.LoadArg(3);
            emitter.LoadLocal(matchedLengthLocal);
            emitter.Emit(OpCodes.Stind_I4);
            emitter.LoadLocal(leadingStartLocal);
            emitter.Emit(OpCodes.Ret);
        }

        emitter.MarkLabel(notFoundLabel);
        if (forCount)
        {
            emitter.LoadLocal(countLocal!);
            emitter.Emit(OpCodes.Ret);
        }
        else
        {
            emitter.LoadArg(3);
            emitter.LdcI4(0);
            emitter.Emit(OpCodes.Stind_I4);
            emitter.LdcI4(-1);
            emitter.Emit(OpCodes.Ret);
        }
    }

    private static bool IsAsciiWhitespace(byte value)
    {
        return value is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n' or 0x0B or 0x0C;
    }
}
