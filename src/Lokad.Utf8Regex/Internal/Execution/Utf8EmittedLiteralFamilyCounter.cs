using System.Reflection;
using System.Reflection.Emit;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Utilities;
using RuntimeFrontEnd = Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

namespace Lokad.Utf8Regex.Internal.Execution;

internal sealed class Utf8EmittedLiteralFamilyCounter
{
    internal delegate int CountDelegate(Utf8EmittedLiteralFamilyCounter counter, ReadOnlySpan<byte> input);
    internal delegate bool IsMatchDelegate(Utf8EmittedLiteralFamilyCounter counter, ReadOnlySpan<byte> input);
    internal delegate bool TryMatchDelegate(
        Utf8EmittedLiteralFamilyCounter counter,
        ReadOnlySpan<byte> input,
        out int index,
        out int matchedLength);

    private static readonly ConstructorInfo s_scanStateCtor =
        typeof(PreparedMultiLiteralScanState).GetConstructor([typeof(int), typeof(int), typeof(int)])!;

    private static readonly MethodInfo s_tryFindNextNonOverlappingLengthMethod =
        typeof(Utf8EmittedLiteralFamilyCounter).GetMethod(nameof(TryFindNextNonOverlappingLength), BindingFlags.Static | BindingFlags.NonPublic)!;

    private static readonly MethodInfo s_confirmMethod =
        typeof(Utf8EmittedLiteralFamilyCounter).GetMethod(nameof(Confirm), BindingFlags.Static | BindingFlags.NonPublic)!;

    private static readonly MethodInfo s_confirmFirstMatchMethod =
        typeof(Utf8EmittedLiteralFamilyCounter).GetMethod(nameof(ConfirmFirstMatch), BindingFlags.Static | BindingFlags.NonPublic)!;

    private static readonly MethodInfo s_confirmAsciiBoundaryOnlyMethod =
        typeof(Utf8EmittedLiteralFamilyCounter).GetMethod(nameof(ConfirmAsciiBoundaryOnly), BindingFlags.Static | BindingFlags.NonPublic)!;

    private static readonly MethodInfo s_findNextCandidate2Method =
        typeof(Utf8EmittedLiteralFamilyCounter).GetMethod(nameof(FindNextCandidate2), BindingFlags.Static | BindingFlags.NonPublic)!;

    private static readonly MethodInfo s_findNextCandidate3Method =
        typeof(Utf8EmittedLiteralFamilyCounter).GetMethod(nameof(FindNextCandidate3), BindingFlags.Static | BindingFlags.NonPublic)!;

    private static readonly MethodInfo s_findNextCandidate1Method =
        typeof(Utf8EmittedLiteralFamilyCounter).GetMethod(nameof(FindNextCandidate1), BindingFlags.Static | BindingFlags.NonPublic)!;

    private static readonly MethodInfo s_spanLengthMethod =
        typeof(ReadOnlySpan<byte>).GetProperty(nameof(ReadOnlySpan<byte>.Length))!.GetMethod!;

    private static readonly MethodInfo s_spanItemMethod =
        typeof(ReadOnlySpan<byte>).GetProperty("Item")!.GetMethod!;

    private readonly CountDelegate _count;
    private readonly IsMatchDelegate _isMatch;
    private readonly TryMatchDelegate _tryMatch;
    private readonly Utf8SearchPlan _plan;
    private readonly Utf8BackendInstructionProgram _countProgram;
    private readonly Utf8BackendInstructionProgram _firstMatchProgram;
    private readonly bool _canUseAsciiBoundaryOnlyFastConfirmation;

    private Utf8EmittedLiteralFamilyCounter(
        Utf8SearchPlan plan,
        Utf8BackendInstructionProgram countProgram,
        Utf8BackendInstructionProgram firstMatchProgram,
        bool canUseAsciiBoundaryOnlyFastConfirmation,
        CountDelegate count,
        IsMatchDelegate isMatch,
        TryMatchDelegate tryMatch)
    {
        _plan = plan;
        _countProgram = countProgram;
        _firstMatchProgram = firstMatchProgram;
        _canUseAsciiBoundaryOnlyFastConfirmation = canUseAsciiBoundaryOnlyFastConfirmation;
        _count = count;
        _isMatch = isMatch;
        _tryMatch = tryMatch;
    }

    internal static bool CanCreate(Utf8SearchPlan plan, Utf8BackendInstructionProgram countProgram, Utf8BackendInstructionProgram firstMatchProgram)
    {
        return plan.NativeSearch.HasPreparedSearcher &&
            plan.NativeSearch.PreparedSearcher.Kind == PreparedSearcherKind.MultiLiteral &&
            !plan.HasTrailingLiteralRequirement &&
            countProgram.HasValue &&
            firstMatchProgram.HasValue;
    }

    internal static bool TryCreate(
        Utf8SearchPlan plan,
        Utf8BackendInstructionProgram countProgram,
        Utf8BackendInstructionProgram firstMatchProgram,
        out Utf8EmittedLiteralFamilyCounter? counter)
    {
        counter = null;
        if (!CanCreate(plan, countProgram, firstMatchProgram))
        {
            return false;
        }

        var canUseFastConfirmation = CanUseAsciiBoundaryOnlyFastConfirmation(plan, countProgram, firstMatchProgram);
        var singleLiteralBuckets = TryGetSmallSingleLiteralBuckets(plan, out var loweredLiterals) ? loweredLiterals : null;
        var singleBucketPrefixFamily = TryGetSmallSingleBucketPrefixFamily(plan, out var commonPrefix, out var prefixLiterals)
            ? (CommonPrefix: commonPrefix, Literals: prefixLiterals)
            : ((byte[] CommonPrefix, byte[][] Literals)?)null;
        counter = new Utf8EmittedLiteralFamilyCounter(
            plan,
            countProgram,
            firstMatchProgram,
            canUseFastConfirmation,
            CompileCount(canUseFastConfirmation, singleLiteralBuckets, singleBucketPrefixFamily),
            CompileIsMatch(canUseFastConfirmation, singleLiteralBuckets, singleBucketPrefixFamily),
            CompileTryMatch(canUseFastConfirmation, singleLiteralBuckets, singleBucketPrefixFamily));
        return true;
    }

    internal int Count(ReadOnlySpan<byte> input) => _count(this, input);

    internal bool IsMatch(ReadOnlySpan<byte> input) => _isMatch(this, input);

    internal bool TryMatch(ReadOnlySpan<byte> input, out int index, out int matchedLength) => _tryMatch(this, input, out index, out matchedLength);

    private static bool TryFindNextNonOverlappingLength(
        Utf8EmittedLiteralFamilyCounter counter,
        ReadOnlySpan<byte> input,
        ref PreparedMultiLiteralScanState state,
        out int index,
        out int matchedLength)
    {
        return counter._plan.NativeSearch.PreparedSearcher.TryFindNextNonOverlappingLength(input, ref state, out index, out matchedLength);
    }

    private static bool Confirm(Utf8EmittedLiteralFamilyCounter counter, ReadOnlySpan<byte> input, int index, int matchedLength)
    {
        return Utf8ConfirmationExecutor.IsMatch(counter._plan, counter._countProgram.Confirmation, input, index, matchedLength);
    }

    private static bool ConfirmFirstMatch(Utf8EmittedLiteralFamilyCounter counter, ReadOnlySpan<byte> input, int index, int matchedLength)
    {
        return Utf8ConfirmationExecutor.IsMatch(counter._plan, counter._firstMatchProgram.Confirmation, input, index, matchedLength);
    }

    private static bool ConfirmAsciiBoundaryOnly(Utf8EmittedLiteralFamilyCounter counter, ReadOnlySpan<byte> input, int index, int matchedLength)
    {
        if (!TryMatchesBoundaryRequirementAsciiOnly(counter._plan.LeadingBoundary, input, index, out var leadingMatch) ||
            !TryMatchesBoundaryRequirementAsciiOnly(counter._plan.TrailingBoundary, input, index + matchedLength, out var trailingMatch))
        {
            return Utf8ConfirmationExecutor.IsMatch(counter._plan, counter._firstMatchProgram.Confirmation, input, index, matchedLength);
        }

        return leadingMatch && trailingMatch;
    }

    private static int FindNextCandidate2(ReadOnlySpan<byte> input, int startIndex, byte firstByte0, byte firstByte1)
    {
        if ((uint)startIndex >= (uint)input.Length)
        {
            return -1;
        }

        var relative = input[startIndex..].IndexOfAny(firstByte0, firstByte1);
        return relative < 0 ? -1 : startIndex + relative;
    }

    private static int FindNextCandidate3(ReadOnlySpan<byte> input, int startIndex, byte firstByte0, byte firstByte1, byte firstByte2)
    {
        if ((uint)startIndex >= (uint)input.Length)
        {
            return -1;
        }

        var relative = input[startIndex..].IndexOfAny(firstByte0, firstByte1, firstByte2);
        return relative < 0 ? -1 : startIndex + relative;
    }

    private static int FindNextCandidate1(ReadOnlySpan<byte> input, int startIndex, byte firstByte0)
    {
        if ((uint)startIndex >= (uint)input.Length)
        {
            return -1;
        }

        var relative = input[startIndex..].IndexOf(firstByte0);
        return relative < 0 ? -1 : startIndex + relative;
    }

    private static bool TryMatchesBoundaryRequirementAsciiOnly(Utf8BoundaryRequirement requirement, ReadOnlySpan<byte> input, int byteOffset, out bool isMatch)
    {
        isMatch = false;
        if (requirement == Utf8BoundaryRequirement.None)
        {
            isMatch = true;
            return true;
        }

        if (!TryGetAsciiWordBoundary(input, byteOffset, out var isBoundary))
        {
            return false;
        }

        isMatch = requirement switch
        {
            Utf8BoundaryRequirement.Boundary => isBoundary,
            Utf8BoundaryRequirement.NonBoundary => !isBoundary,
            _ => false,
        };

        return true;
    }

    private static bool TryGetAsciiWordBoundary(ReadOnlySpan<byte> input, int byteOffset, out bool isBoundary)
    {
        var lookup = RuntimeFrontEnd.RegexCharClass.WordCharAsciiLookup;

        bool previousIsWord;
        if (byteOffset <= 0)
        {
            previousIsWord = false;
        }
        else
        {
            var previous = input[byteOffset - 1];
            if (previous >= 128)
            {
                isBoundary = false;
                return false;
            }

            previousIsWord = lookup[previous] != 0;
        }

        bool nextIsWord;
        if (byteOffset >= input.Length)
        {
            nextIsWord = false;
        }
        else
        {
            var next = input[byteOffset];
            if (next >= 128)
            {
                isBoundary = false;
                return false;
            }

            nextIsWord = lookup[next] != 0;
        }

        isBoundary = previousIsWord != nextIsWord;
        return true;
    }

    private static bool CanUseAsciiBoundaryOnlyFastConfirmation(
        Utf8SearchPlan plan,
        Utf8BackendInstructionProgram countProgram,
        Utf8BackendInstructionProgram firstMatchProgram)
    {
        return plan.HasBoundaryRequirements &&
            !plan.HasTrailingLiteralRequirement &&
            countProgram.Confirmation.Kind == Utf8ConfirmationKind.BoundaryRequirements &&
            firstMatchProgram.Confirmation.Kind == Utf8ConfirmationKind.BoundaryRequirements;
    }

    private static bool TryGetSmallSingleLiteralBuckets(Utf8SearchPlan plan, out byte[][] literals)
    {
        literals = [];

        if (plan.Kind != Utf8SearchKind.ExactUtf8Literals ||
            !plan.AlternateLiteralSearch.HasValue ||
            plan.MultiLiteralSearch.Kind is not (PreparedMultiLiteralKind.ExactDirect or PreparedMultiLiteralKind.ExactEarliest))
        {
            return false;
        }

        var search = plan.AlternateLiteralSearch.Value;
        if (search.Strategy != PreparedLiteralSetStrategy.SingleLiteralBuckets)
        {
            return false;
        }

        var buckets = search.SearchData.Buckets;
        if (buckets.Length is < 2 or > 3)
        {
            return false;
        }

        literals = new byte[buckets.Length][];
        for (var i = 0; i < buckets.Length; i++)
        {
            if (buckets[i].Literals.Length != 1)
            {
                literals = [];
                return false;
            }

            literals[i] = buckets[i].Literals[0];
        }

        return true;
    }

    private static bool TryGetSmallSingleBucketPrefixFamily(Utf8SearchPlan plan, out byte[] commonPrefix, out byte[][] literals)
    {
        commonPrefix = [];
        literals = [];

        if (plan.Kind != Utf8SearchKind.ExactUtf8Literals || !plan.AlternateLiteralSearch.HasValue)
        {
            return false;
        }

        var search = plan.AlternateLiteralSearch.Value;
        if (search.Strategy != PreparedLiteralSetStrategy.SingleBucketPrefix)
        {
            return false;
        }

        var buckets = search.SearchData.Buckets;
        if (buckets.Length != 1)
        {
            return false;
        }

        var bucket = buckets[0];
        if (bucket.CommonPrefix.Length < 2 || !bucket.PrefixDiscriminator.HasValue || bucket.Literals.Length is < 2 or > 6)
        {
            return false;
        }

        commonPrefix = bucket.CommonPrefix;
        literals = bucket.Literals;
        return true;
    }

    private static CountDelegate CompileCount(
        bool useAsciiBoundaryOnlyFastConfirmation,
        byte[][]? singleLiteralBuckets,
        (byte[] CommonPrefix, byte[][] Literals)? singleBucketPrefixFamily)
    {
        if (singleLiteralBuckets is { Length: >= 2 and <= 3 })
        {
            return CompileSingleLiteralBucketCount(useAsciiBoundaryOnlyFastConfirmation, singleLiteralBuckets);
        }

        if (singleBucketPrefixFamily is { } prefixFamily)
        {
            return CompileSingleBucketPrefixCount(useAsciiBoundaryOnlyFastConfirmation, prefixFamily.CommonPrefix, prefixFamily.Literals);
        }

        return CompileGenericCount(useAsciiBoundaryOnlyFastConfirmation);
    }

    private static IsMatchDelegate CompileIsMatch(
        bool useAsciiBoundaryOnlyFastConfirmation,
        byte[][]? singleLiteralBuckets,
        (byte[] CommonPrefix, byte[][] Literals)? singleBucketPrefixFamily)
    {
        if (singleLiteralBuckets is { Length: >= 2 and <= 3 })
        {
            return CompileSingleLiteralBucketIsMatch(useAsciiBoundaryOnlyFastConfirmation, singleLiteralBuckets);
        }

        if (singleBucketPrefixFamily is { } prefixFamily)
        {
            return CompileSingleBucketPrefixIsMatch(useAsciiBoundaryOnlyFastConfirmation, prefixFamily.CommonPrefix, prefixFamily.Literals);
        }

        return CompileGenericIsMatch(useAsciiBoundaryOnlyFastConfirmation);
    }

    private static TryMatchDelegate CompileTryMatch(
        bool useAsciiBoundaryOnlyFastConfirmation,
        byte[][]? singleLiteralBuckets,
        (byte[] CommonPrefix, byte[][] Literals)? singleBucketPrefixFamily)
    {
        if (singleLiteralBuckets is { Length: >= 2 and <= 3 })
        {
            return CompileSingleLiteralBucketTryMatch(useAsciiBoundaryOnlyFastConfirmation, singleLiteralBuckets);
        }

        if (singleBucketPrefixFamily is { } prefixFamily)
        {
            return CompileSingleBucketPrefixTryMatch(useAsciiBoundaryOnlyFastConfirmation, prefixFamily.CommonPrefix, prefixFamily.Literals);
        }

        return CompileGenericTryMatch(useAsciiBoundaryOnlyFastConfirmation);
    }

    private static CountDelegate CompileGenericCount(bool useAsciiBoundaryOnlyFastConfirmation)
    {
        var dynamicMethod = new DynamicMethod(
            "Utf8Regex_EmitLiteralFamilyCount",
            typeof(int),
            [typeof(Utf8EmittedLiteralFamilyCounter), typeof(ReadOnlySpan<byte>)],
            typeof(Utf8EmittedLiteralFamilyCounter),
            skipVisibility: false);

        var il = dynamicMethod.GetILGenerator();
        var stateLocal = il.DeclareLocal(typeof(PreparedMultiLiteralScanState));
        var indexLocal = il.DeclareLocal(typeof(int));
        var matchedLengthLocal = il.DeclareLocal(typeof(int));
        var countLocal = il.DeclareLocal(typeof(int));

        var loopLabel = il.DefineLabel();
        var incrementLabel = il.DefineLabel();
        var returnLabel = il.DefineLabel();

        il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Newobj, s_scanStateCtor);
        il.Emit(OpCodes.Stloc, stateLocal);

        il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Stloc, countLocal);

        il.MarkLabel(loopLabel);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Ldloca_S, stateLocal);
        il.Emit(OpCodes.Ldloca_S, indexLocal);
        il.Emit(OpCodes.Ldloca_S, matchedLengthLocal);
        il.Emit(OpCodes.Call, s_tryFindNextNonOverlappingLengthMethod);
        il.Emit(OpCodes.Brfalse, returnLabel);

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Ldloc, indexLocal);
        il.Emit(OpCodes.Ldloc, matchedLengthLocal);
        il.Emit(OpCodes.Call, useAsciiBoundaryOnlyFastConfirmation ? s_confirmAsciiBoundaryOnlyMethod : s_confirmFirstMatchMethod);
        il.Emit(OpCodes.Brtrue, incrementLabel);
        il.Emit(OpCodes.Br, loopLabel);

        il.MarkLabel(incrementLabel);
        il.Emit(OpCodes.Ldloc, countLocal);
        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.Add);
        il.Emit(OpCodes.Stloc, countLocal);
        il.Emit(OpCodes.Br, loopLabel);

        il.MarkLabel(returnLabel);
        il.Emit(OpCodes.Ldloc, countLocal);
        il.Emit(OpCodes.Ret);

        return dynamicMethod.CreateDelegate<CountDelegate>();
    }

    private static IsMatchDelegate CompileGenericIsMatch(bool useAsciiBoundaryOnlyFastConfirmation)
    {
        var dynamicMethod = new DynamicMethod(
            "Utf8Regex_EmitLiteralFamilyIsMatch",
            typeof(bool),
            [typeof(Utf8EmittedLiteralFamilyCounter), typeof(ReadOnlySpan<byte>)],
            typeof(Utf8EmittedLiteralFamilyCounter),
            skipVisibility: false);

        var il = dynamicMethod.GetILGenerator();
        var stateLocal = il.DeclareLocal(typeof(PreparedMultiLiteralScanState));
        var indexLocal = il.DeclareLocal(typeof(int));
        var matchedLengthLocal = il.DeclareLocal(typeof(int));

        var loopLabel = il.DefineLabel();
        var successLabel = il.DefineLabel();
        var returnFalseLabel = il.DefineLabel();

        il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Newobj, s_scanStateCtor);
        il.Emit(OpCodes.Stloc, stateLocal);

        il.MarkLabel(loopLabel);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Ldloca_S, stateLocal);
        il.Emit(OpCodes.Ldloca_S, indexLocal);
        il.Emit(OpCodes.Ldloca_S, matchedLengthLocal);
        il.Emit(OpCodes.Call, s_tryFindNextNonOverlappingLengthMethod);
        il.Emit(OpCodes.Brfalse, returnFalseLabel);

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Ldloc, indexLocal);
        il.Emit(OpCodes.Ldloc, matchedLengthLocal);
        il.Emit(OpCodes.Call, useAsciiBoundaryOnlyFastConfirmation ? s_confirmAsciiBoundaryOnlyMethod : s_confirmMethod);
        il.Emit(OpCodes.Brtrue, successLabel);
        il.Emit(OpCodes.Br, loopLabel);

        il.MarkLabel(successLabel);
        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.Ret);

        il.MarkLabel(returnFalseLabel);
        il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Ret);

        return dynamicMethod.CreateDelegate<IsMatchDelegate>();
    }

    private static TryMatchDelegate CompileGenericTryMatch(bool useAsciiBoundaryOnlyFastConfirmation)
    {
        var dynamicMethod = new DynamicMethod(
            "Utf8Regex_EmitLiteralFamilyTryMatch",
            typeof(bool),
            [typeof(Utf8EmittedLiteralFamilyCounter), typeof(ReadOnlySpan<byte>), typeof(int).MakeByRefType(), typeof(int).MakeByRefType()],
            typeof(Utf8EmittedLiteralFamilyCounter),
            skipVisibility: false);

        var il = dynamicMethod.GetILGenerator();
        var stateLocal = il.DeclareLocal(typeof(PreparedMultiLiteralScanState));
        var indexLocal = il.DeclareLocal(typeof(int));
        var matchedLengthLocal = il.DeclareLocal(typeof(int));

        var loopLabel = il.DefineLabel();
        var successLabel = il.DefineLabel();
        var returnFalseLabel = il.DefineLabel();

        il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Newobj, s_scanStateCtor);
        il.Emit(OpCodes.Stloc, stateLocal);

        il.MarkLabel(loopLabel);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Ldloca_S, stateLocal);
        il.Emit(OpCodes.Ldloca_S, indexLocal);
        il.Emit(OpCodes.Ldloca_S, matchedLengthLocal);
        il.Emit(OpCodes.Call, s_tryFindNextNonOverlappingLengthMethod);
        il.Emit(OpCodes.Brfalse, returnFalseLabel);

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Ldloc, indexLocal);
        il.Emit(OpCodes.Ldloc, matchedLengthLocal);
        il.Emit(OpCodes.Call, useAsciiBoundaryOnlyFastConfirmation ? s_confirmAsciiBoundaryOnlyMethod : s_confirmFirstMatchMethod);
        il.Emit(OpCodes.Brtrue, successLabel);
        il.Emit(OpCodes.Br, loopLabel);

        il.MarkLabel(successLabel);
        il.Emit(OpCodes.Ldarg_2);
        il.Emit(OpCodes.Ldloc, indexLocal);
        il.Emit(OpCodes.Stind_I4);
        il.Emit(OpCodes.Ldarg_3);
        il.Emit(OpCodes.Ldloc, matchedLengthLocal);
        il.Emit(OpCodes.Stind_I4);
        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.Ret);

        il.MarkLabel(returnFalseLabel);
        il.Emit(OpCodes.Ldarg_2);
        il.Emit(OpCodes.Ldc_I4_M1);
        il.Emit(OpCodes.Stind_I4);
        il.Emit(OpCodes.Ldarg_3);
        il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Stind_I4);
        il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Ret);

        return dynamicMethod.CreateDelegate<TryMatchDelegate>();
    }

    private static CountDelegate CompileSingleLiteralBucketCount(bool useAsciiBoundaryOnlyFastConfirmation, byte[][] literals)
    {
        var dynamicMethod = new DynamicMethod(
            "Utf8Regex_EmitLiteralFamilyBucketCount",
            typeof(int),
            [typeof(Utf8EmittedLiteralFamilyCounter), typeof(ReadOnlySpan<byte>)],
            typeof(Utf8EmittedLiteralFamilyCounter),
            skipVisibility: false);

        var il = dynamicMethod.GetILGenerator();
        var e = new Utf8IlEmitter(il, s_spanLengthMethod, s_spanItemMethod);
        EmitSingleLiteralBucketBody(
            e,
            literals,
            useAsciiBoundaryOnlyFastConfirmation ? s_confirmAsciiBoundaryOnlyMethod : s_confirmFirstMatchMethod,
            returnOnFirstMatch: false,
            countMatches: true,
            storeMatchResult: false);
        return dynamicMethod.CreateDelegate<CountDelegate>();
    }

    private static IsMatchDelegate CompileSingleLiteralBucketIsMatch(bool useAsciiBoundaryOnlyFastConfirmation, byte[][] literals)
    {
        var dynamicMethod = new DynamicMethod(
            "Utf8Regex_EmitLiteralFamilyBucketIsMatch",
            typeof(bool),
            [typeof(Utf8EmittedLiteralFamilyCounter), typeof(ReadOnlySpan<byte>)],
            typeof(Utf8EmittedLiteralFamilyCounter),
            skipVisibility: false);

        var il = dynamicMethod.GetILGenerator();
        var e = new Utf8IlEmitter(il, s_spanLengthMethod, s_spanItemMethod);
        EmitSingleLiteralBucketBody(
            e,
            literals,
            useAsciiBoundaryOnlyFastConfirmation ? s_confirmAsciiBoundaryOnlyMethod : s_confirmMethod,
            returnOnFirstMatch: true,
            countMatches: false,
            storeMatchResult: false);
        return dynamicMethod.CreateDelegate<IsMatchDelegate>();
    }

    private static TryMatchDelegate CompileSingleLiteralBucketTryMatch(bool useAsciiBoundaryOnlyFastConfirmation, byte[][] literals)
    {
        var dynamicMethod = new DynamicMethod(
            "Utf8Regex_EmitLiteralFamilyBucketTryMatch",
            typeof(bool),
            [typeof(Utf8EmittedLiteralFamilyCounter), typeof(ReadOnlySpan<byte>), typeof(int).MakeByRefType(), typeof(int).MakeByRefType()],
            typeof(Utf8EmittedLiteralFamilyCounter),
            skipVisibility: false);

        var il = dynamicMethod.GetILGenerator();
        var e = new Utf8IlEmitter(il, s_spanLengthMethod, s_spanItemMethod);
        EmitSingleLiteralBucketBody(
            e,
            literals,
            useAsciiBoundaryOnlyFastConfirmation ? s_confirmAsciiBoundaryOnlyMethod : s_confirmFirstMatchMethod,
            returnOnFirstMatch: true,
            countMatches: false,
            storeMatchResult: true);
        return dynamicMethod.CreateDelegate<TryMatchDelegate>();
    }

    private static CountDelegate CompileSingleBucketPrefixCount(bool useAsciiBoundaryOnlyFastConfirmation, byte[] commonPrefix, byte[][] literals)
    {
        var dynamicMethod = new DynamicMethod(
            "Utf8Regex_EmitLiteralFamilyPrefixCount",
            typeof(int),
            [typeof(Utf8EmittedLiteralFamilyCounter), typeof(ReadOnlySpan<byte>)],
            typeof(Utf8EmittedLiteralFamilyCounter),
            skipVisibility: false);

        var e = new Utf8IlEmitter(dynamicMethod.GetILGenerator(), s_spanLengthMethod, s_spanItemMethod);
        EmitSingleBucketPrefixBody(
            e,
            commonPrefix,
            literals,
            useAsciiBoundaryOnlyFastConfirmation ? s_confirmAsciiBoundaryOnlyMethod : s_confirmFirstMatchMethod,
            returnOnFirstMatch: false,
            countMatches: true,
            storeMatchResult: false);
        return dynamicMethod.CreateDelegate<CountDelegate>();
    }

    private static IsMatchDelegate CompileSingleBucketPrefixIsMatch(bool useAsciiBoundaryOnlyFastConfirmation, byte[] commonPrefix, byte[][] literals)
    {
        var dynamicMethod = new DynamicMethod(
            "Utf8Regex_EmitLiteralFamilyPrefixIsMatch",
            typeof(bool),
            [typeof(Utf8EmittedLiteralFamilyCounter), typeof(ReadOnlySpan<byte>)],
            typeof(Utf8EmittedLiteralFamilyCounter),
            skipVisibility: false);

        var e = new Utf8IlEmitter(dynamicMethod.GetILGenerator(), s_spanLengthMethod, s_spanItemMethod);
        EmitSingleBucketPrefixBody(
            e,
            commonPrefix,
            literals,
            useAsciiBoundaryOnlyFastConfirmation ? s_confirmAsciiBoundaryOnlyMethod : s_confirmMethod,
            returnOnFirstMatch: true,
            countMatches: false,
            storeMatchResult: false);
        return dynamicMethod.CreateDelegate<IsMatchDelegate>();
    }

    private static TryMatchDelegate CompileSingleBucketPrefixTryMatch(bool useAsciiBoundaryOnlyFastConfirmation, byte[] commonPrefix, byte[][] literals)
    {
        var dynamicMethod = new DynamicMethod(
            "Utf8Regex_EmitLiteralFamilyPrefixTryMatch",
            typeof(bool),
            [typeof(Utf8EmittedLiteralFamilyCounter), typeof(ReadOnlySpan<byte>), typeof(int).MakeByRefType(), typeof(int).MakeByRefType()],
            typeof(Utf8EmittedLiteralFamilyCounter),
            skipVisibility: false);

        var e = new Utf8IlEmitter(dynamicMethod.GetILGenerator(), s_spanLengthMethod, s_spanItemMethod);
        EmitSingleBucketPrefixBody(
            e,
            commonPrefix,
            literals,
            useAsciiBoundaryOnlyFastConfirmation ? s_confirmAsciiBoundaryOnlyMethod : s_confirmFirstMatchMethod,
            returnOnFirstMatch: true,
            countMatches: false,
            storeMatchResult: true);
        return dynamicMethod.CreateDelegate<TryMatchDelegate>();
    }

    private static void EmitSingleLiteralBucketBody(
        Utf8IlEmitter e,
        byte[][] literals,
        MethodInfo confirmMethod,
        bool returnOnFirstMatch,
        bool countMatches,
        bool storeMatchResult)
    {
        var searchFromLocal = e.DeclareLocal<int>();
        var inputLengthLocal = e.DeclareLocal<int>();
        var candidateLocal = e.DeclareLocal<int>();
        var currentByteLocal = e.DeclareLocal<int>();
        var countLocal = countMatches ? e.DeclareLocal<int>() : null;

        e.LoadInputLength();
        e.StoreLocal(inputLengthLocal);
        e.LdcI4(0);
        e.StoreLocal(searchFromLocal);
        if (countMatches)
        {
            e.LdcI4(0);
            e.StoreLocal(countLocal!);
        }

        var loopLabel = e.DefineLabel();
        var returnLabel = e.DefineLabel();
        var retryLabel = e.DefineLabel();
        var successLabel = e.DefineLabel();
        var failureReturnLabel = e.DefineLabel();
        var nextCandidateLabel = e.DefineLabel();
        var perLiteralLabels = new Label[literals.Length];
        var confirmLabels = new Label[literals.Length];
        for (var i = 0; i < perLiteralLabels.Length; i++)
        {
            perLiteralLabels[i] = e.DefineLabel();
            confirmLabels[i] = e.DefineLabel();
        }

        e.MarkLabel(loopLabel);
        e.LoadArg(1);
        e.LoadLocal(searchFromLocal);
        e.LdcI4(literals[0][0]);
        e.LdcI4(literals[1][0]);
        if (literals.Length == 2)
        {
            e.Emit(OpCodes.Call, s_findNextCandidate2Method);
        }
        else
        {
            e.LdcI4(literals[2][0]);
            e.Emit(OpCodes.Call, s_findNextCandidate3Method);
        }

        e.StoreLocal(candidateLocal);
        e.LoadLocal(candidateLocal);
        e.LdcI4(0);
        e.Emit(OpCodes.Blt, returnLabel);

        e.LoadInputByte(candidateLocal);
        e.StoreLocal(currentByteLocal);
        e.EmitByteDispatch(currentByteLocal, literals.Select(static literal => literal[0]).ToArray(), perLiteralLabels, retryLabel);

        for (var i = 0; i < literals.Length; i++)
        {
            var literal = literals[i];
            var literalMatchedLabel = e.DefineLabel();
            e.MarkLabel(perLiteralLabels[i]);

            e.EmitEnsureAvailable(candidateLocal, inputLengthLocal, literal.Length, literalMatchedLabel, retryLabel);
            e.MarkLabel(literalMatchedLabel);
            e.EmitSpanLiteralEquals(candidateLocal, literal, 1, successLabel, retryLabel);
        }

        e.MarkLabel(retryLabel);
        e.LoadLocal(candidateLocal);
        e.LdcI4(1);
        e.Emit(OpCodes.Add);
        e.StoreLocal(searchFromLocal);
        e.Emit(OpCodes.Br, nextCandidateLabel);

        e.MarkLabel(successLabel);
        EmitMatchedLengthDispatch(e, literals, candidateLocal, currentByteLocal, confirmMethod, returnOnFirstMatch, countMatches, storeMatchResult, countLocal, searchFromLocal, retryLabel, failureReturnLabel, nextCandidateLabel);

        e.MarkLabel(nextCandidateLabel);
        e.Emit(OpCodes.Br, loopLabel);

        e.MarkLabel(failureReturnLabel);
        if (storeMatchResult)
        {
            e.LoadArg(2);
            e.LdcI4(-1);
            e.Emit(OpCodes.Stind_I4);
            e.LoadArg(3);
            e.LdcI4(0);
            e.Emit(OpCodes.Stind_I4);
        }

        e.LdcI4(0);
        e.Emit(OpCodes.Ret);

        e.MarkLabel(returnLabel);
        if (countMatches)
        {
            e.LoadLocal(countLocal!);
            e.Emit(OpCodes.Ret);
        }

        if (returnOnFirstMatch)
        {
            if (storeMatchResult)
            {
                e.LoadArg(2);
                e.LdcI4(-1);
                e.Emit(OpCodes.Stind_I4);
                e.LoadArg(3);
                e.LdcI4(0);
                e.Emit(OpCodes.Stind_I4);
            }

            e.LdcI4(0);
            e.Emit(OpCodes.Ret);
        }
    }

    private static void EmitMatchedLengthDispatch(
        Utf8IlEmitter e,
        byte[][] literals,
        LocalBuilder candidateLocal,
        LocalBuilder currentByteLocal,
        MethodInfo confirmMethod,
        bool returnOnFirstMatch,
        bool countMatches,
        bool storeMatchResult,
        LocalBuilder? countLocal,
        LocalBuilder searchFromLocal,
        Label retryLabel,
        Label failureReturnLabel,
        Label continueLabel)
    {
        var confirmedLabels = new Label[literals.Length];
        for (var i = 0; i < confirmedLabels.Length; i++)
        {
            confirmedLabels[i] = e.DefineLabel();
            e.EmitEqualityBranch(currentByteLocal, literals[i][0], confirmedLabels[i]);
        }

        e.Emit(OpCodes.Br, retryLabel);

        for (var i = 0; i < literals.Length; i++)
        {
            var literal = literals[i];
            e.MarkLabel(confirmedLabels[i]);
            e.LoadArg(0);
            e.LoadArg(1);
            e.LoadLocal(candidateLocal);
            e.LdcI4(literal.Length);
            e.Emit(OpCodes.Call, confirmMethod);
            e.Emit(OpCodes.Brfalse, retryLabel);
            if (countMatches)
            {
                e.LoadLocal(countLocal!);
                e.LdcI4(1);
                e.Emit(OpCodes.Add);
                e.StoreLocal(countLocal!);
                e.LoadLocal(candidateLocal);
                e.LdcI4(literal.Length);
                e.Emit(OpCodes.Add);
                e.StoreLocal(searchFromLocal);
                e.Emit(OpCodes.Br, continueLabel);
            }

            if (returnOnFirstMatch)
            {
                if (!storeMatchResult)
                {
                    e.LdcI4(1);
                    e.Emit(OpCodes.Ret);
                }
                else
                {
                    e.LoadArg(2);
                    e.LoadLocal(candidateLocal);
                    e.Emit(OpCodes.Stind_I4);
                    e.LoadArg(3);
                    e.LdcI4(literal.Length);
                    e.Emit(OpCodes.Stind_I4);
                    e.LdcI4(1);
                    e.Emit(OpCodes.Ret);
                }
            }

            e.Emit(OpCodes.Br, failureReturnLabel);
        }
    }

    private static void EmitSingleBucketPrefixBody(
        Utf8IlEmitter e,
        byte[] commonPrefix,
        byte[][] literals,
        MethodInfo confirmMethod,
        bool returnOnFirstMatch,
        bool countMatches,
        bool storeMatchResult)
    {
        var searchFromLocal = e.DeclareLocal<int>();
        var inputLengthLocal = e.DeclareLocal<int>();
        var candidateLocal = e.DeclareLocal<int>();
        var currentByteLocal = e.DeclareLocal<int>();
        var countLocal = countMatches ? e.DeclareLocal<int>() : null;

        e.LoadInputLength();
        e.StoreLocal(inputLengthLocal);
        e.LdcI4(0);
        e.StoreLocal(searchFromLocal);
        if (countMatches)
        {
            e.LdcI4(0);
            e.StoreLocal(countLocal!);
        }

        var loopLabel = e.DefineLabel();
        var returnLabel = e.DefineLabel();
        var retryLabel = e.DefineLabel();
        var prefixMatchedLabel = e.DefineLabel();
        var successLabel = e.DefineLabel();
        var failureReturnLabel = e.DefineLabel();
        var perLiteralLabels = new Label[literals.Length];
        var confirmLabels = new Label[literals.Length];
        for (var i = 0; i < perLiteralLabels.Length; i++)
        {
            perLiteralLabels[i] = e.DefineLabel();
            confirmLabels[i] = e.DefineLabel();
        }

        e.MarkLabel(loopLabel);
        e.LoadArg(1);
        e.LoadLocal(searchFromLocal);
        e.LdcI4(commonPrefix[0]);
        e.Emit(OpCodes.Call, s_findNextCandidate1Method);
        e.StoreLocal(candidateLocal);
        e.LoadLocal(candidateLocal);
        e.LdcI4(0);
        e.Emit(OpCodes.Blt, returnLabel);

        e.EmitEnsureAvailable(candidateLocal, inputLengthLocal, commonPrefix.Length, prefixMatchedLabel, retryLabel);
        e.MarkLabel(prefixMatchedLabel);
        e.EmitSpanLiteralEquals(candidateLocal, commonPrefix, 1, successLabel, retryLabel);

        e.MarkLabel(successLabel);
        e.LoadInputByte(candidateLocal, commonPrefix.Length);
        e.StoreLocal(currentByteLocal);
        EmitPrefixDiscriminatorDispatch(e, commonPrefix, literals, currentByteLocal, perLiteralLabels, retryLabel);

        for (var i = 0; i < literals.Length; i++)
        {
            var literal = literals[i];
            var literalMatchedLabel = e.DefineLabel();
            e.MarkLabel(perLiteralLabels[i]);
            e.EmitEnsureAvailable(candidateLocal, inputLengthLocal, literal.Length, literalMatchedLabel, retryLabel);
            e.MarkLabel(literalMatchedLabel);
            e.EmitSpanLiteralEquals(candidateLocal, literal, commonPrefix.Length + 1, successLabel: confirmLabels[i], failureLabel: retryLabel);

            e.MarkLabel(confirmLabels[i]);
            e.LoadArg(0);
            e.LoadArg(1);
            e.LoadLocal(candidateLocal);
            e.LdcI4(literal.Length);
            e.Emit(OpCodes.Call, confirmMethod);
            e.Emit(OpCodes.Brfalse, retryLabel);

            if (countMatches)
            {
                e.LoadLocal(countLocal!);
                e.LdcI4(1);
                e.Emit(OpCodes.Add);
                e.StoreLocal(countLocal!);
                e.LoadLocal(candidateLocal);
                e.LdcI4(literal.Length);
                e.Emit(OpCodes.Add);
                e.StoreLocal(searchFromLocal);
                e.Emit(OpCodes.Br, loopLabel);
            }

            if (!storeMatchResult)
            {
                e.LdcI4(1);
                e.Emit(OpCodes.Ret);
            }

            e.LoadArg(2);
            e.LoadLocal(candidateLocal);
            e.Emit(OpCodes.Stind_I4);
            e.LoadArg(3);
            e.LdcI4(literal.Length);
            e.Emit(OpCodes.Stind_I4);
            e.LdcI4(1);
            e.Emit(OpCodes.Ret);
        }

        e.MarkLabel(retryLabel);
        e.LoadLocal(candidateLocal);
        e.LdcI4(1);
        e.Emit(OpCodes.Add);
        e.StoreLocal(searchFromLocal);
        e.Emit(OpCodes.Br, loopLabel);

        e.MarkLabel(failureReturnLabel);
        if (storeMatchResult)
        {
            e.LoadArg(2);
            e.LdcI4(-1);
            e.Emit(OpCodes.Stind_I4);
            e.LoadArg(3);
            e.LdcI4(0);
            e.Emit(OpCodes.Stind_I4);
        }

        e.LdcI4(0);
        e.Emit(OpCodes.Ret);

        e.MarkLabel(returnLabel);
        if (countMatches)
        {
            e.LoadLocal(countLocal!);
            e.Emit(OpCodes.Ret);
            return;
        }

        if (returnOnFirstMatch)
        {
            if (storeMatchResult)
            {
                e.LoadArg(2);
                e.LdcI4(-1);
                e.Emit(OpCodes.Stind_I4);
                e.LoadArg(3);
                e.LdcI4(0);
                e.Emit(OpCodes.Stind_I4);
            }

            e.LdcI4(0);
            e.Emit(OpCodes.Ret);
            return;
        }

        e.Emit(OpCodes.Br, failureReturnLabel);
    }

    private static void EmitPrefixDiscriminatorDispatch(
        Utf8IlEmitter e,
        byte[] commonPrefix,
        byte[][] literals,
        LocalBuilder currentByteLocal,
        ReadOnlySpan<Label> labels,
        Label retryLabel)
    {
        for (var i = 0; i < literals.Length; i++)
        {
            if (literals[i].Length <= commonPrefix.Length)
            {
                continue;
            }

            e.EmitEqualityBranch(currentByteLocal, literals[i][commonPrefix.Length], labels[i]);
        }

        e.Emit(OpCodes.Br, retryLabel);
    }
}
