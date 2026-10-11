using Lokad.Utf8Regex.Internal.Search;
using Lokad.Utf8Regex.Internal.FrontEnd;
using System.Reflection;
using System.Reflection.Emit;

namespace Lokad.Utf8Regex.Internal.Execution;

internal sealed partial class Utf8EmittedDeterministicMatcher
{
    internal delegate int FindNextDelegate(Utf8EmittedDeterministicMatcher matcher, ReadOnlySpan<byte> input, int startIndex, out int matchedLength);

    private static readonly MethodInfo s_findSearchLiteralMethod =
        typeof(Utf8EmittedDeterministicMatcher).GetMethod(nameof(FindSearchLiteral), BindingFlags.Static | BindingFlags.NonPublic) ??
        throw new MissingMethodException(typeof(Utf8EmittedDeterministicMatcher).FullName, nameof(FindSearchLiteral));

    private static readonly MethodInfo s_matchesCharClassMethod =
        typeof(Utf8EmittedDeterministicMatcher).GetMethod(nameof(MatchesCharClass), BindingFlags.Static | BindingFlags.NonPublic) ??
        throw new MissingMethodException(typeof(Utf8EmittedDeterministicMatcher).FullName, nameof(MatchesCharClass));

    private static readonly MethodInfo s_getInputLengthMethod =
        typeof(Utf8EmittedDeterministicMatcher).GetMethod(nameof(GetInputLength), BindingFlags.Static | BindingFlags.NonPublic) ??
        throw new MissingMethodException(typeof(Utf8EmittedDeterministicMatcher).FullName, nameof(GetInputLength));

    private static readonly MethodInfo s_getInputByteMethod =
        typeof(Utf8EmittedDeterministicMatcher).GetMethod(nameof(GetInputByte), BindingFlags.Static | BindingFlags.NonPublic) ??
        throw new MissingMethodException(typeof(Utf8EmittedDeterministicMatcher).FullName, nameof(GetInputByte));

    private static readonly MethodInfo s_foldCaseMethod =
        typeof(Utf8EmittedDeterministicMatcher).GetMethod(nameof(FoldCase), BindingFlags.Static | BindingFlags.NonPublic) ??
        throw new MissingMethodException(typeof(Utf8EmittedDeterministicMatcher).FullName, nameof(FoldCase));

    private readonly FindNextDelegate _findNext;
    private readonly byte[] _searchLiteral;
    private readonly AsciiCharClass[] _charClasses;
    private readonly bool _ignoreCase;

    private Utf8EmittedDeterministicMatcher(
        FindNextDelegate findNext,
        byte[] searchLiteral,
        AsciiCharClass[] charClasses,
        bool ignoreCase)
    {
        _findNext = findNext;
        _searchLiteral = searchLiteral;
        _charClasses = charClasses;
        _ignoreCase = ignoreCase;
    }

    internal static bool CanCreate(Utf8StructuralLinearProgram program) => Utf8EmitStructuralCompiler.CanCompile(program);

    internal static bool TryCreate(Utf8StructuralLinearProgram program, out Utf8EmittedDeterministicMatcher? matcher)
    {
        matcher = null;
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        budget.EnterDepth();
        try
        {
            if (!Utf8EmitStructuralCompiler.CanCompile(program))
            {
                return false;
            }

            var deterministicProgram = program.DeterministicProgram;
            var charClasses = CollectCharClasses(deterministicProgram);
            var findNext = Utf8EmitStructuralCompiler.CompileFindNext(program, charClasses);
            matcher = budget.AllocateObject((findNext, literal: deterministicProgram.SearchLiterals[0],
                charClasses, ignoreCase: deterministicProgram.IgnoreCase), static state =>
                new Utf8EmittedDeterministicMatcher(state.findNext, state.literal, state.charClasses, state.ignoreCase));
            scope.Complete();
            return true;
        }
        finally { budget.LeaveDepth(); }
    }

    internal int FindNext(ReadOnlySpan<byte> input, int startIndex, out int matchedLength) => _findNext(this, input, startIndex, out matchedLength);

    internal int Count(ReadOnlySpan<byte> input)
    {
        var count = 0;
        var startIndex = 0;
        while ((uint)startIndex <= (uint)input.Length)
        {
            var matchIndex = FindNext(input, startIndex, out var matchedLength);
            if (matchIndex < 0)
            {
                return count;
            }

            count++;
            startIndex = matchIndex + Math.Max(matchedLength, 1);
        }

        return count;
    }

    private static AsciiCharClass[] CollectCharClasses(Utf8AsciiDeterministicProgram program)
    {
        var budget = Utf8ConstructionScope.CurrentBudget;
        using var classes = Utf8ConstructionBuffer<AsciiCharClass>.Create(budget);
        using var unique = Utf8ConstructionMap<AsciiCharClass, bool>.Create(budget, EqualityComparer<AsciiCharClass>.Default);
        foreach (var check in program.FixedWidthChecks)
        {
            budget.ChargeWork(1);
            if (!check.CharClass.IsEmpty && !unique.ContainsKey(check.CharClass))
            {
                unique.Add(check.CharClass, true);
                classes.Add(check.CharClass);
            }
        }

        foreach (var step in program.Steps)
        {
            budget.ChargeWork(1);
            if (!step.CharClass.IsEmpty && !unique.ContainsKey(step.CharClass))
            {
                unique.Add(step.CharClass, true);
                classes.Add(step.CharClass);
            }
        }

        return classes.Take();
    }

    private static int FindSearchLiteral(Utf8EmittedDeterministicMatcher matcher, ReadOnlySpan<byte> input, int searchFrom)
    {
        if ((uint)searchFrom > (uint)input.Length)
        {
            return -1;
        }

        return matcher._ignoreCase
            ? AsciiSearch.IndexOfIgnoreCase(input[searchFrom..], matcher._searchLiteral)
            : AsciiSearch.IndexOfExact(input[searchFrom..], matcher._searchLiteral);
    }

    private static bool MatchesCharClass(Utf8EmittedDeterministicMatcher matcher, int classIndex, byte value)
    {
        return matcher._charClasses[classIndex].Contains(matcher._ignoreCase ? AsciiSearch.FoldCase(value) : value);
    }

    private static int GetInputLength(ReadOnlySpan<byte> input) => input.Length;

    private static byte GetInputByte(ReadOnlySpan<byte> input, int index) => input[index];

    private static byte FoldCase(byte value) => AsciiSearch.FoldCase(value);

    private static class Utf8EmitStructuralCompiler
    {
        internal static bool CanCompile(Utf8StructuralLinearProgram program)
        {
            if (!program.DeterministicProgram.HasValue ||
                program.DeterministicProgram.SearchLiterals.Length != 1 ||
                program.InstructionProgram.IsStartAnchored ||
                program.DeterministicProgram.SearchLiteralOffset < 0 ||
                program.DeterministicProgram.FixedWidthLength <= 0)
            {
                return false;
            }

            foreach (var check in program.DeterministicProgram.FixedWidthChecks)
            {
                Utf8ConstructionScope.ChargeWork(1);
                if (check.Kind is not
                    (Utf8AsciiDeterministicFixedWidthCheckKind.Literal or
                     Utf8AsciiDeterministicFixedWidthCheckKind.AnyByte or
                     Utf8AsciiDeterministicFixedWidthCheckKind.CharClass))
                {
                    return false;
                }
            }

            return true;
        }

        internal static FindNextDelegate CompileFindNext(
            Utf8StructuralLinearProgram program,
            AsciiCharClass[] charClasses)
        {
            var deterministicProgram = program.DeterministicProgram;
            var fixedWidthChecks = deterministicProgram.FixedWidthChecks;
            using var charClassIndexes = CreateCharClassIndexMap(charClasses);
            using var dynamicMethod = Utf8DynamicIlBuilder.Create("Utf8Regex_EmitDeterministicFindNext", typeof(int), typeof(Utf8EmittedDeterministicMatcher), typeof(Utf8EmittedDeterministicMatcher), typeof(ReadOnlySpan<byte>), typeof(int), typeof(int).MakeByRefType());

            var il = dynamicMethod;
            var searchFromLocal = il.DeclareLocal(typeof(int));
            var relativeLocal = il.DeclareLocal(typeof(int));
            var absoluteAnchorLocal = il.DeclareLocal(typeof(int));
            var candidateLocal = il.DeclareLocal(typeof(int));
            var endIndexLocal = il.DeclareLocal(typeof(int));
            var valueLocal = il.DeclareLocal(typeof(byte));

            var searchLoopLabel = il.DefineLabel();
            var advanceSearchLabel = il.DefineLabel();
            var notFoundLabel = il.DefineLabel();
            var successLabel = il.DefineLabel();

            il.Emit(OpCodes.Ldarg_2);
            EmitLdcI4(il, deterministicProgram.SearchLiteralOffset);
            il.Emit(OpCodes.Add);
            il.Emit(OpCodes.Stloc, searchFromLocal);

            il.MarkLabel(searchLoopLabel);
            il.Emit(OpCodes.Ldloc, searchFromLocal);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Call, s_getInputLengthMethod);
            il.Emit(OpCodes.Bgt, notFoundLabel);

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldloc, searchFromLocal);
            il.Emit(OpCodes.Call, s_findSearchLiteralMethod);
            il.Emit(OpCodes.Stloc, relativeLocal);

            il.Emit(OpCodes.Ldloc, relativeLocal);
            il.Emit(OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Blt, notFoundLabel);

            il.Emit(OpCodes.Ldloc, searchFromLocal);
            il.Emit(OpCodes.Ldloc, relativeLocal);
            il.Emit(OpCodes.Add);
            il.Emit(OpCodes.Stloc, absoluteAnchorLocal);

            il.Emit(OpCodes.Ldloc, absoluteAnchorLocal);
            EmitLdcI4(il, deterministicProgram.SearchLiteralOffset);
            il.Emit(OpCodes.Sub);
            il.Emit(OpCodes.Stloc, candidateLocal);

            il.Emit(OpCodes.Ldloc, candidateLocal);
            il.Emit(OpCodes.Ldarg_2);
            il.Emit(OpCodes.Blt, advanceSearchLabel);

            il.Emit(OpCodes.Ldloc, candidateLocal);
            EmitLdcI4(il, deterministicProgram.FixedWidthLength);
            il.Emit(OpCodes.Add);
            il.Emit(OpCodes.Stloc, endIndexLocal);

            il.Emit(OpCodes.Ldloc, candidateLocal);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Call, s_getInputLengthMethod);
            il.Emit(OpCodes.Bgt, advanceSearchLabel);

            il.Emit(OpCodes.Ldloc, endIndexLocal);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Call, s_getInputLengthMethod);
            il.Emit(OpCodes.Bgt, advanceSearchLabel);

            for (var i = 0; i < fixedWidthChecks.Length; i++)
            {
                Utf8ConstructionScope.ChargeWork(1);
                var check = fixedWidthChecks[i];
                var continueLabel = il.DefineLabel();

                if (check.Kind == Utf8AsciiDeterministicFixedWidthCheckKind.AnyByte)
                {
                    continue;
                }

                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Ldloc, candidateLocal);
                EmitLdcI4(il, i);
                il.Emit(OpCodes.Add);
                il.Emit(OpCodes.Call, s_getInputByteMethod);
                il.Emit(OpCodes.Stloc, valueLocal);

                switch (check.Kind)
                {
                    case Utf8AsciiDeterministicFixedWidthCheckKind.Literal:
                        if (deterministicProgram.IgnoreCase)
                        {
                            il.Emit(OpCodes.Ldloc, valueLocal);
                            il.Emit(OpCodes.Call, s_foldCaseMethod);
                            il.Emit(OpCodes.Stloc, valueLocal);
                        }

                        il.Emit(OpCodes.Ldloc, valueLocal);
                        EmitLdcI4(il, check.Literal);
                        il.Emit(OpCodes.Beq, continueLabel);
                        il.Emit(OpCodes.Br, advanceSearchLabel);
                        il.MarkLabel(continueLabel);
                        break;

                    case Utf8AsciiDeterministicFixedWidthCheckKind.CharClass when !check.CharClass.IsEmpty:
                        il.Emit(OpCodes.Ldarg_0);
                        EmitLdcI4(il, charClassIndexes[check.CharClass]);
                        il.Emit(OpCodes.Ldloc, valueLocal);
                        il.Emit(OpCodes.Call, s_matchesCharClassMethod);
                        il.Emit(OpCodes.Brtrue, continueLabel);
                        il.Emit(OpCodes.Br, advanceSearchLabel);
                        il.MarkLabel(continueLabel);
                        break;

                    default:
                        throw new InvalidOperationException($"Unsupported fixed-width check '{check.Kind}' for emitted deterministic matcher.");
                }
            }

            if (deterministicProgram.IsEndAnchored)
            {
                il.Emit(OpCodes.Ldloc, endIndexLocal);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Call, s_getInputLengthMethod);
                il.Emit(OpCodes.Beq, successLabel);
                il.Emit(OpCodes.Br, advanceSearchLabel);
            }
            else
            {
                il.Emit(OpCodes.Br, successLabel);
            }

            il.MarkLabel(advanceSearchLabel);
            il.Emit(OpCodes.Ldloc, absoluteAnchorLocal);
            il.Emit(OpCodes.Ldc_I4_1);
            il.Emit(OpCodes.Add);
            il.Emit(OpCodes.Stloc, searchFromLocal);
            il.Emit(OpCodes.Br, searchLoopLabel);

            il.MarkLabel(successLabel);
            il.Emit(OpCodes.Ldarg_3);
            EmitLdcI4(il, deterministicProgram.FixedWidthLength);
            il.Emit(OpCodes.Stind_I4);
            il.Emit(OpCodes.Ldloc, candidateLocal);
            il.Emit(OpCodes.Ret);

            il.MarkLabel(notFoundLabel);
            il.Emit(OpCodes.Ldarg_3);
            il.Emit(OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Stind_I4);
            il.Emit(OpCodes.Ldc_I4_M1);
            il.Emit(OpCodes.Ret);

            return dynamicMethod.CreateDelegate<FindNextDelegate>();
        }

        private static Utf8ConstructionMap<AsciiCharClass, int> CreateCharClassIndexMap(AsciiCharClass[] charClasses)
        {
            var budget = Utf8ConstructionScope.CurrentBudget;
            var map = Utf8ConstructionMap<AsciiCharClass, int>.Create(budget, EqualityComparer<AsciiCharClass>.Default);
            try
            {
                for (var i = 0; i < charClasses.Length; i++)
                {
                    budget.ChargeWork(1);
                    map[charClasses[i]] = i;
                }
                return map;
            }
            catch { map.Dispose(); throw; }
        }

        private static void EmitLdcI4(Utf8DynamicIlBuilder il, int value)
        {
            switch (value)
            {
                case -1:
                    il.Emit(OpCodes.Ldc_I4_M1);
                    break;
                case 0:
                    il.Emit(OpCodes.Ldc_I4_0);
                    break;
                case 1:
                    il.Emit(OpCodes.Ldc_I4_1);
                    break;
                case 2:
                    il.Emit(OpCodes.Ldc_I4_2);
                    break;
                case 3:
                    il.Emit(OpCodes.Ldc_I4_3);
                    break;
                case 4:
                    il.Emit(OpCodes.Ldc_I4_4);
                    break;
                case 5:
                    il.Emit(OpCodes.Ldc_I4_5);
                    break;
                case 6:
                    il.Emit(OpCodes.Ldc_I4_6);
                    break;
                case 7:
                    il.Emit(OpCodes.Ldc_I4_7);
                    break;
                case 8:
                    il.Emit(OpCodes.Ldc_I4_8);
                    break;
                default:
                    if (value is >= sbyte.MinValue and <= sbyte.MaxValue)
                    {
                        il.Emit(OpCodes.Ldc_I4_S, (sbyte)value);
                    }
                    else
                    {
                        il.Emit(OpCodes.Ldc_I4, value);
                    }

                    break;
            }
        }
    }
}
