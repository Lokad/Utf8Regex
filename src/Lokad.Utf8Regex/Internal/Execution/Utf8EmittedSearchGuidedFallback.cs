using Lokad.Utf8Regex.Internal.Search;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;
using System.Reflection;
using System.Reflection.Emit;

namespace Lokad.Utf8Regex.Internal.Execution;

internal sealed class Utf8EmittedSearchGuidedFallback
{
    internal delegate bool IsMatchDelegate(Utf8EmittedSearchGuidedFallback backend, ReadOnlySpan<byte> input, Utf8ValidationResult validation, ref Utf8SliceInvocation invocation);
    internal delegate int CountDelegate(Utf8EmittedSearchGuidedFallback backend, ReadOnlySpan<byte> input, Utf8ValidationResult validation, ref Utf8SliceInvocation invocation);

    private static readonly MethodInfo s_tryFindNextVerifiedMatchMethod =
        typeof(Utf8EmittedSearchGuidedFallback).GetMethod(nameof(TryFindNextVerifiedMatch), BindingFlags.Static | BindingFlags.NonPublic)
        ?? throw new MissingMethodException(typeof(Utf8EmittedSearchGuidedFallback).FullName, nameof(TryFindNextVerifiedMatch));

    private static readonly MethodInfo s_getNextSearchStartMethod =
        typeof(Utf8EmittedSearchGuidedFallback).GetMethod(nameof(GetNextSearchStart), BindingFlags.Static | BindingFlags.NonPublic)
        ?? throw new MissingMethodException(typeof(Utf8EmittedSearchGuidedFallback).FullName, nameof(GetNextSearchStart));

    private readonly Utf8SearchPlan _searchPlan;
    private readonly Utf8VerifierRuntime _verifierRuntime;
    private readonly IsMatchDelegate _isMatch;
    private readonly CountDelegate _count;

    private Utf8EmittedSearchGuidedFallback(
        Utf8SearchPlan searchPlan,
        Utf8VerifierRuntime verifierRuntime,
        IsMatchDelegate isMatch,
        CountDelegate count)
    {
        _searchPlan = searchPlan;
        _verifierRuntime = verifierRuntime;
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

        backend = new Utf8EmittedSearchGuidedFallback(
            regexPlan.SearchPlan,
            verifierRuntime,
            CompileIsMatch(),
            CompileCount());
        return true;
    }

    internal bool IsMatch(ReadOnlySpan<byte> input, Utf8ValidationResult validation, ref Utf8SliceInvocation invocation) => _isMatch(this, input, validation, ref invocation);

    internal int Count(ReadOnlySpan<byte> input, Utf8ValidationResult validation, ref Utf8SliceInvocation invocation) => _count(this, input, validation, ref invocation);

    private static bool TryFindNextVerifiedMatch(
        Utf8EmittedSearchGuidedFallback backend,
        ReadOnlySpan<byte> input,
        Utf8ValidationResult validation,
        int startIndex,
        ref Utf8SliceInvocation invocation,
        out Utf8ScalarVerificationResult verification)
    {
        return Utf8FallbackSearchExecutor.TryFindNextVerifiedMatch(
            backend._searchPlan.FallbackCandidatePlans ?? [],
            backend._verifierRuntime,
            input,
            validation,
            startIndex,
            ref invocation,
            out _,
            out verification);
    }

    private static int GetNextSearchStart(ReadOnlySpan<byte> input, Utf8ScalarVerificationResult verification, ref Utf8SliceInvocation invocation)
    {
        invocation.AnchorOriginByte = verification.IndexInBytes + verification.LengthInBytes;
        return verification.LengthInBytes == 0
            ? Utf8SliceRunner.AdvancePastScalar(input, verification.IndexInBytes, false)
            : verification.IndexInBytes + verification.LengthInBytes;
    }

    private static IsMatchDelegate CompileIsMatch()
    {
        var dynamicMethod = new DynamicMethod(
            "Utf8Regex_EmitSearchGuidedFallbackIsMatch",
            typeof(bool),
            [typeof(Utf8EmittedSearchGuidedFallback), typeof(ReadOnlySpan<byte>), typeof(Utf8ValidationResult), typeof(Utf8SliceInvocation).MakeByRefType()],
            typeof(Utf8EmittedSearchGuidedFallback),
            skipVisibility: false);

        var il = dynamicMethod.GetILGenerator();
        var verificationLocal = il.DeclareLocal(typeof(Utf8ScalarVerificationResult));

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Ldarg_2);
        il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Ldarg_3);
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
            [typeof(Utf8EmittedSearchGuidedFallback), typeof(ReadOnlySpan<byte>), typeof(Utf8ValidationResult), typeof(Utf8SliceInvocation).MakeByRefType()],
            typeof(Utf8EmittedSearchGuidedFallback),
            skipVisibility: false);

        var il = dynamicMethod.GetILGenerator();
        var verificationLocal = il.DeclareLocal(typeof(Utf8ScalarVerificationResult));
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
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Ldarg_2);
        il.Emit(OpCodes.Ldloc, startIndexLocal);
        il.Emit(OpCodes.Ldarg_3);
        il.Emit(OpCodes.Ldloca_S, verificationLocal);
        il.Emit(OpCodes.Call, s_tryFindNextVerifiedMatchMethod);
        il.Emit(OpCodes.Brfalse, returnLabel);

        il.Emit(OpCodes.Ldloc, countLocal);
        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.Add);
        il.Emit(OpCodes.Stloc, countLocal);

        il.MarkLabel(advanceLabel);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Ldloc, verificationLocal);
        il.Emit(OpCodes.Ldarg_3);
        il.Emit(OpCodes.Call, s_getNextSearchStartMethod);
        il.Emit(OpCodes.Stloc, startIndexLocal);
        il.Emit(OpCodes.Br, loopLabel);

        il.MarkLabel(returnLabel);
        il.Emit(OpCodes.Ldloc, countLocal);
        il.Emit(OpCodes.Ret);

        return dynamicMethod.CreateDelegate<CountDelegate>();
    }
}
