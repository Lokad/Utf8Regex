using Lokad.Utf8Regex.Internal.Search;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;
using System.Reflection;
using System.Reflection.Emit;

namespace Lokad.Utf8Regex.Internal.Execution;

internal sealed class Utf8EmittedSearchGuidedFallback
{
    internal delegate bool IsMatchDelegate(Utf8EmittedSearchGuidedFallback backend, ReadOnlySpan<byte> input);
    internal delegate int CountDelegate(Utf8EmittedSearchGuidedFallback backend, ReadOnlySpan<byte> input);

    private static readonly MethodInfo s_tryFindNextVerifiedMatchMethod =
        typeof(Utf8EmittedSearchGuidedFallback).GetMethod(nameof(TryFindNextVerifiedMatch), BindingFlags.Static | BindingFlags.NonPublic)
        ?? throw new MissingMethodException(typeof(Utf8EmittedSearchGuidedFallback).FullName, nameof(TryFindNextVerifiedMatch));

    private static readonly MethodInfo s_getNextSearchStartMethod =
        typeof(Utf8EmittedSearchGuidedFallback).GetMethod(nameof(GetNextSearchStart), BindingFlags.Static | BindingFlags.NonPublic)
        ?? throw new MissingMethodException(typeof(Utf8EmittedSearchGuidedFallback).FullName, nameof(GetNextSearchStart));

    private readonly Utf8SearchPlan _searchPlan;
    private readonly Utf8VerifierRuntime _verifierRuntime;
    private readonly Utf8SearchOperationPlan _firstMatchProgram;
    private readonly Utf8SearchOperationPlan _countProgram;
    private readonly IsMatchDelegate _isMatch;
    private readonly CountDelegate _count;

    private Utf8EmittedSearchGuidedFallback(
        Utf8SearchPlan searchPlan,
        Utf8VerifierRuntime verifierRuntime,
        Utf8SearchOperationPlan firstMatchProgram,
        Utf8SearchOperationPlan countProgram,
        IsMatchDelegate isMatch,
        CountDelegate count)
    {
        _searchPlan = searchPlan;
        _verifierRuntime = verifierRuntime;
        _firstMatchProgram = firstMatchProgram;
        _countProgram = countProgram;
        _isMatch = isMatch;
        _count = count;
    }

    internal static bool TryCreate(Utf8PreparedRegex regexPlan, Utf8VerifierRuntime verifierRuntime, out Utf8EmittedSearchGuidedFallback? backend)
    {
        backend = null;
        if (!Utf8CompiledBackendCapability.CanUseEmittedSearchGuidedFallback(regexPlan))
        {
            return false;
        }

        if (CanUseBoundaryLiteralFamilyBackend(regexPlan))
        {
            backend = new Utf8EmittedSearchGuidedFallback(
                regexPlan.SearchPlan,
                verifierRuntime,
                regexPlan.SearchPlan.FirstMatchOperation,
                regexPlan.SearchPlan.CountOperation,
                BoundaryLiteralFamilyIsMatch,
                BoundaryLiteralFamilyCount);
            return true;
        }

        backend = new Utf8EmittedSearchGuidedFallback(
            regexPlan.SearchPlan,
            verifierRuntime,
            regexPlan.SearchPlan.FirstMatchOperation,
            regexPlan.SearchPlan.CountOperation,
            CompileIsMatch(),
            CompileCount());
        return true;
    }

    internal bool IsMatch(ReadOnlySpan<byte> input) => _isMatch(this, input);

    internal int Count(ReadOnlySpan<byte> input) => _count(this, input);

    private static bool CanUseBoundaryLiteralFamilyBackend(Utf8PreparedRegex regexPlan)
    {
        var searchPlan = regexPlan.SearchPlan;
        return regexPlan.ExecutionKind == NativeExecutionKind.FallbackRegex &&
            searchPlan.Kind == Utf8SearchKind.ExactAsciiLiterals &&
            searchPlan.HasPreparedSearcher &&
            searchPlan.PreparedSearcher.Kind == PreparedSearcherKind.MultiLiteral &&
            searchPlan.HasBoundaryRequirements &&
            !searchPlan.HasTrailingLiteralRequirement &&
            searchPlan.FirstMatchOperation.Confirmation.Kind == Utf8ConfirmationKind.BoundaryRequirements &&
            searchPlan.CountOperation.Confirmation.Kind == Utf8ConfirmationKind.BoundaryRequirements;
    }

    private static bool TryFindNextVerifiedMatch(
        Utf8EmittedSearchGuidedFallback backend,
        bool useCountOperation,
        ReadOnlySpan<byte> input,
        int startIndex,
        ref Utf8BoundaryMap? boundaryMap,
        ref string? decoded,
        out Utf8FallbackVerificationResult verification)
    {
        var validation = Utf8InputAnalyzer.ValidateOnly(input);
        return Utf8BackendInstructionExecutor.TryFindNextFallbackVerifiedMatch(
            backend._searchPlan,
            useCountOperation ? backend._countProgram : backend._firstMatchProgram,
            backend._verifierRuntime,
            input,
            validation,
            startIndex,
            ref boundaryMap,
            ref decoded,
            out verification);
    }

    private static int GetNextSearchStart(Utf8FallbackVerificationResult verification)
    {
        return verification.IndexInBytes + Math.Max(verification.LengthInBytes, 1);
    }

    private static bool BoundaryLiteralFamilyIsMatch(Utf8EmittedSearchGuidedFallback backend, ReadOnlySpan<byte> input)
    {
        var state = new PreparedSearchScanState(0, default);
        while (backend._searchPlan.PreparedSearcher.TryFindNextOverlappingMatch(input, ref state, out var match))
        {
            if (Utf8ConfirmationExecutor.IsMatch(
                backend._searchPlan,
                backend._firstMatchProgram.Confirmation,
                input,
                match.Index,
                match.Length))
            {
                return true;
            }
        }

        return false;
    }

    private static int BoundaryLiteralFamilyCount(Utf8EmittedSearchGuidedFallback backend, ReadOnlySpan<byte> input)
    {
        var count = 0;
        var state = new PreparedMultiLiteralScanState(0, 0, 0);
        while (backend._searchPlan.PreparedSearcher.TryFindNextNonOverlappingLength(input, ref state, out var index, out var matchedLength))
        {
            if (Utf8ConfirmationExecutor.IsMatch(
                backend._searchPlan,
                backend._countProgram.Confirmation,
                input,
                index,
                matchedLength))
            {
                count++;
            }
        }

        return count;
    }

    private static IsMatchDelegate CompileIsMatch()
    {
        var dynamicMethod = new DynamicMethod(
            "Utf8Regex_EmitSearchGuidedFallbackIsMatch",
            typeof(bool),
            [typeof(Utf8EmittedSearchGuidedFallback), typeof(ReadOnlySpan<byte>)],
            typeof(Utf8EmittedSearchGuidedFallback),
            skipVisibility: false);

        var il = dynamicMethod.GetILGenerator();
        var boundaryMapLocal = il.DeclareLocal(typeof(Utf8BoundaryMap));
        var decodedLocal = il.DeclareLocal(typeof(string));
        var verificationLocal = il.DeclareLocal(typeof(Utf8FallbackVerificationResult));

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Ldloca_S, boundaryMapLocal);
        il.Emit(OpCodes.Ldloca_S, decodedLocal);
        il.Emit(OpCodes.Ldloca_S, verificationLocal);
        il.Emit(OpCodes.Call, s_tryFindNextVerifiedMatchMethod);
        il.Emit(OpCodes.Ret);

        return dynamicMethod.CreateDelegate<IsMatchDelegate>();
    }

    private static CountDelegate CompileCount()
    {
        var dynamicMethod = new DynamicMethod(
            "Utf8Regex_EmitSearchGuidedFallbackCount",
            typeof(int),
            [typeof(Utf8EmittedSearchGuidedFallback), typeof(ReadOnlySpan<byte>)],
            typeof(Utf8EmittedSearchGuidedFallback),
            skipVisibility: false);

        var il = dynamicMethod.GetILGenerator();
        var boundaryMapLocal = il.DeclareLocal(typeof(Utf8BoundaryMap));
        var decodedLocal = il.DeclareLocal(typeof(string));
        var verificationLocal = il.DeclareLocal(typeof(Utf8FallbackVerificationResult));
        var countLocal = il.DeclareLocal(typeof(int));
        var startIndexLocal = il.DeclareLocal(typeof(int));

        var loopLabel = il.DefineLabel();
        var advanceLabel = il.DefineLabel();
        var returnLabel = il.DefineLabel();

        il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Stloc, countLocal);
        il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Stloc, startIndexLocal);

        il.MarkLabel(loopLabel);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Ldloc, startIndexLocal);
        il.Emit(OpCodes.Ldloca_S, boundaryMapLocal);
        il.Emit(OpCodes.Ldloca_S, decodedLocal);
        il.Emit(OpCodes.Ldloca_S, verificationLocal);
        il.Emit(OpCodes.Call, s_tryFindNextVerifiedMatchMethod);
        il.Emit(OpCodes.Brfalse, returnLabel);

        il.Emit(OpCodes.Ldloc, countLocal);
        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.Add);
        il.Emit(OpCodes.Stloc, countLocal);

        il.MarkLabel(advanceLabel);
        il.Emit(OpCodes.Ldloc, verificationLocal);
        il.Emit(OpCodes.Call, s_getNextSearchStartMethod);
        il.Emit(OpCodes.Stloc, startIndexLocal);
        il.Emit(OpCodes.Br, loopLabel);

        il.MarkLabel(returnLabel);
        il.Emit(OpCodes.Ldloc, countLocal);
        il.Emit(OpCodes.Ret);

        return dynamicMethod.CreateDelegate<CountDelegate>();
    }
}
