using System.Reflection;
using System.Reflection.Emit;

namespace Lokad.Utf8Regex.Internal.Execution;

internal readonly struct Utf8IlEmitter
{
    private readonly Utf8DynamicIlBuilder _il;
    private readonly MethodInfo _getSpanLengthMethod;
    private readonly MethodInfo _getSpanItemMethod;
    private readonly byte _inputArgIndex;

    public Utf8IlEmitter(Utf8DynamicIlBuilder il, MethodInfo getSpanLengthMethod, MethodInfo getSpanItemMethod)
        : this(il, getSpanLengthMethod, getSpanItemMethod, 1)
    {
    }

    public Utf8IlEmitter(Utf8DynamicIlBuilder il, MethodInfo getSpanLengthMethod, MethodInfo getSpanItemMethod, byte inputArgIndex)
    {
        _il = il;
        _getSpanLengthMethod = getSpanLengthMethod;
        _getSpanItemMethod = getSpanItemMethod;
        _inputArgIndex = inputArgIndex;
    }

    public Utf8IlLocal DeclareLocal<T>() => _il.DeclareLocal(typeof(T));

    internal T[] AllocateScratch<T>(int count) => _il.AllocateScratch<T>(count);

    internal void ChargeWork(ulong steps) => _il.Budget.ChargeWork(steps);

    public Utf8IlLocal DeclareLocal(Type type) => _il.DeclareLocal(type);

    public Utf8IlLabel DefineLabel() => _il.DefineLabel();

    public void MarkLabel(Utf8IlLabel label) => _il.MarkLabel(label);

    public void Emit(OpCode opcode) => _il.Emit(opcode);

    public void Emit(OpCode opcode, Utf8IlLabel label) => _il.Emit(opcode, label);

    public void Emit(OpCode opcode, Utf8IlLocal local) => _il.Emit(opcode, local);

    public void Emit(OpCode opcode, MethodInfo method) => _il.Emit(opcode, method);

    public void Emit(OpCode opcode, int value) => _il.Emit(opcode, value);

    public void LoadArg(byte index)
    {
        switch (index)
        {
            case 0:
                Emit(OpCodes.Ldarg_0);
                break;
            case 1:
                Emit(OpCodes.Ldarg_1);
                break;
            case 2:
                Emit(OpCodes.Ldarg_2);
                break;
            case 3:
                Emit(OpCodes.Ldarg_3);
                break;
            default:
                _il.Emit(OpCodes.Ldarg_S, index);
                break;
        }
    }

    public void LoadArgAddress(byte index) => _il.Emit(OpCodes.Ldarga_S, index);

    public void LoadLocal(Utf8IlLocal local) => _il.Emit(OpCodes.Ldloc, local);

    public void LoadLocalAddress(Utf8IlLocal local) => _il.Emit(OpCodes.Ldloca_S, local);

    public void StoreLocal(Utf8IlLocal local) => _il.Emit(OpCodes.Stloc, local);

    public void LoadInputLength()
    {
        LoadArgAddress(_inputArgIndex);
        Emit(OpCodes.Call, _getSpanLengthMethod);
    }

    public void LoadInputByte(Utf8IlLocal indexLocal) => LoadInputByte(indexLocal, 0);

    public void LoadInputByte(Utf8IlLocal indexLocal, int offset)
    {
        LoadArgAddress(_inputArgIndex);
        LoadLocal(indexLocal);
        if (offset != 0)
        {
            LdcI4(offset);
            Emit(OpCodes.Add);
        }

        Emit(OpCodes.Call, _getSpanItemMethod);
        Emit(OpCodes.Ldind_U1);
    }

    public void AddToLocal(Utf8IlLocal local, int delta)
    {
        LoadLocal(local);
        LdcI4(delta);
        Emit(OpCodes.Add);
        StoreLocal(local);
    }

    public void EmitStoreDifference(Utf8IlLocal targetLocal, Utf8IlLocal leftLocal, Utf8IlLocal rightLocal)
    {
        LoadLocal(leftLocal);
        LoadLocal(rightLocal);
        Emit(OpCodes.Sub);
        StoreLocal(targetLocal);
    }

    public void EmitStoreDifference(Utf8IlLocal targetLocal, Utf8IlLocal leftLocal, int rightConstant)
    {
        LoadLocal(leftLocal);
        LdcI4(rightConstant);
        Emit(OpCodes.Sub);
        StoreLocal(targetLocal);
    }

    public void EmitStoreSum(Utf8IlLocal targetLocal, Utf8IlLocal leftLocal, int rightConstant)
    {
        LoadLocal(leftLocal);
        LdcI4(rightConstant);
        Emit(OpCodes.Add);
        StoreLocal(targetLocal);
    }

    public void EmitClampLocalMinZero(Utf8IlLocal local)
    {
        var nonNegativeLabel = DefineLabel();
        LoadLocal(local);
        LdcI4(0);
        Emit(OpCodes.Bge, nonNegativeLabel);
        LdcI4(0);
        StoreLocal(local);
        MarkLabel(nonNegativeLabel);
    }

    public void EmitLocalLessThanLocalBranch(Utf8IlLocal leftLocal, Utf8IlLocal rightLocal, Utf8IlLabel successLabel, Utf8IlLabel failureLabel)
    {
        LoadLocal(leftLocal);
        LoadLocal(rightLocal);
        Emit(OpCodes.Blt, successLabel);
        Emit(OpCodes.Br, failureLabel);
    }

    public void EmitLocalGreaterThanLocalBranch(Utf8IlLocal leftLocal, Utf8IlLocal rightLocal, Utf8IlLabel successLabel, Utf8IlLabel failureLabel)
    {
        LoadLocal(leftLocal);
        LoadLocal(rightLocal);
        Emit(OpCodes.Bgt, successLabel);
        Emit(OpCodes.Br, failureLabel);
    }

    public void EmitEnsureAvailable(Utf8IlLocal startLocal, Utf8IlLocal inputLengthLocal, int requiredLength, Utf8IlLabel successLabel, Utf8IlLabel failureLabel)
    {
        LoadLocal(inputLengthLocal);
        LoadLocal(startLocal);
        Emit(OpCodes.Sub);
        LdcI4(requiredLength);
        Emit(OpCodes.Bge, successLabel);
        Emit(OpCodes.Br, failureLabel);
    }

    public void EmitLocalsEqualBranch(Utf8IlLocal leftLocal, Utf8IlLocal rightLocal, Utf8IlLabel successLabel, Utf8IlLabel failureLabel)
    {
        LoadLocal(leftLocal);
        LoadLocal(rightLocal);
        Emit(OpCodes.Beq, successLabel);
        Emit(OpCodes.Br, failureLabel);
    }

    public void EmitSpanLiteralEquals(Utf8IlLocal startLocal, ReadOnlySpan<byte> literal, int skipPrefixCount, Utf8IlLabel successLabel, Utf8IlLabel failureLabel)
    {
        for (var i = skipPrefixCount; i < literal.Length; i++)
        {
            LoadInputByte(startLocal, i);
            LdcI4(literal[i]);
            Emit(OpCodes.Bne_Un, failureLabel);
        }

        Emit(OpCodes.Br, successLabel);
    }

    public void EmitSpanLiteralEqualsIgnoreCase(Utf8IlLocal startLocal, ReadOnlySpan<byte> literal, int skipPrefixCount, Utf8IlLocal valueLocal, Utf8IlLabel successLabel, Utf8IlLabel failureLabel)
    {
        for (var i = skipPrefixCount; i < literal.Length; i++)
        {
            LoadInputByte(startLocal, i);
            StoreLocal(valueLocal);
            EmitAsciiCaseInsensitiveByteEqualsOrBranchFailure(valueLocal, literal[i], failureLabel);
        }

        Emit(OpCodes.Br, successLabel);
    }

    public void EmitByteDispatch(Utf8IlLocal valueLocal, ReadOnlySpan<byte> keys, ReadOnlySpan<Utf8IlLabel> labels, Utf8IlLabel defaultLabel)
    {
        if (keys.Length != labels.Length)
        {
            throw new ArgumentException("Dispatch keys and labels must have the same length.");
        }

        for (var i = 0; i < keys.Length; i++)
        {
            EmitEqualityBranch(valueLocal, keys[i], labels[i]);
        }

        Emit(OpCodes.Br, defaultLabel);
    }

    public void LdcI4(int value)
    {
        switch (value)
        {
            case -1:
                Emit(OpCodes.Ldc_I4_M1);
                break;
            case 0:
                Emit(OpCodes.Ldc_I4_0);
                break;
            case 1:
                Emit(OpCodes.Ldc_I4_1);
                break;
            case 2:
                Emit(OpCodes.Ldc_I4_2);
                break;
            case 3:
                Emit(OpCodes.Ldc_I4_3);
                break;
            case 4:
                Emit(OpCodes.Ldc_I4_4);
                break;
            case 5:
                Emit(OpCodes.Ldc_I4_5);
                break;
            case 6:
                Emit(OpCodes.Ldc_I4_6);
                break;
            case 7:
                Emit(OpCodes.Ldc_I4_7);
                break;
            case 8:
                Emit(OpCodes.Ldc_I4_8);
                break;
            default:
                if (value is >= sbyte.MinValue and <= sbyte.MaxValue)
                {
                    _il.Emit(OpCodes.Ldc_I4_S, (sbyte)value);
                }
                else
                {
                    Emit(OpCodes.Ldc_I4, value);
                }

                break;
        }
    }

    public void EmitEqualityBranch(Utf8IlLocal valueLocal, int expected, Utf8IlLabel successLabel)
    {
        LoadLocal(valueLocal);
        LdcI4(expected);
        Emit(OpCodes.Beq, successLabel);
    }

    public void EmitEqualityBranch(Utf8IlLocal valueLocal, int expected, Utf8IlLabel successLabel, Utf8IlLabel failureLabel)
    {
        LoadLocal(valueLocal);
        LdcI4(expected);
        Emit(OpCodes.Beq, successLabel);
        Emit(OpCodes.Br, failureLabel);
    }

    public void EmitAsciiCaseInsensitiveByteEqualsOrBranchFailure(Utf8IlLocal valueLocal, int expected, Utf8IlLabel failureLabel)
    {
        if (expected is >= 'a' and <= 'z')
        {
            var matchedLabel = DefineLabel();
            EmitEqualityBranch(valueLocal, expected, matchedLabel);
            EmitEqualityBranch(valueLocal, expected - 32, matchedLabel);
            Emit(OpCodes.Br, failureLabel);
            MarkLabel(matchedLabel);
            return;
        }

        if (expected is >= 'A' and <= 'Z')
        {
            var matchedLabel = DefineLabel();
            EmitEqualityBranch(valueLocal, expected, matchedLabel);
            EmitEqualityBranch(valueLocal, expected + 32, matchedLabel);
            Emit(OpCodes.Br, failureLabel);
            MarkLabel(matchedLabel);
            return;
        }

        var nextLabel = DefineLabel();
        EmitEqualityBranch(valueLocal, expected, nextLabel, failureLabel);
        MarkLabel(nextLabel);
    }

    public void EmitRangeBranch(Utf8IlLocal valueLocal, int lowerInclusive, int upperInclusive, Utf8IlLabel successLabel)
    {
        var doneLabel = DefineLabel();
        LoadLocal(valueLocal);
        LdcI4(lowerInclusive);
        Emit(OpCodes.Blt_Un, doneLabel);
        LoadLocal(valueLocal);
        LdcI4(upperInclusive);
        Emit(OpCodes.Ble_Un, successLabel);
        MarkLabel(doneLabel);
    }

    public void EmitAsciiWhitespaceBranch(Utf8IlLocal valueLocal, Utf8IlLabel successLabel, Utf8IlLabel failureLabel)
    {
        EmitEqualityBranch(valueLocal, ' ', successLabel);
        EmitEqualityBranch(valueLocal, '\t', successLabel);
        EmitEqualityBranch(valueLocal, '\r', successLabel);
        EmitEqualityBranch(valueLocal, '\n', successLabel);
        EmitEqualityBranch(valueLocal, 0x0B, successLabel);
        EmitEqualityBranch(valueLocal, 0x0C, successLabel);
        Emit(OpCodes.Br, failureLabel);
    }

    public void EmitAsciiNewlineBranch(Utf8IlLocal valueLocal, Utf8IlLabel successLabel, Utf8IlLabel failureLabel)
    {
        EmitEqualityBranch(valueLocal, '\r', successLabel);
        EmitEqualityBranch(valueLocal, '\n', successLabel);
        Emit(OpCodes.Br, failureLabel);
    }

    public void EmitAsciiUpperBranch(Utf8IlLocal valueLocal, Utf8IlLabel? successLabel, Utf8IlLabel failureLabel)
    {
        var continueLabel = successLabel ?? DefineLabel();

        LoadLocal(valueLocal);
        _il.Emit(OpCodes.Ldc_I4_S, (sbyte)'A');
        Emit(OpCodes.Blt_Un, failureLabel);
        LoadLocal(valueLocal);
        _il.Emit(OpCodes.Ldc_I4_S, (sbyte)'Z');
        Emit(OpCodes.Ble_Un, continueLabel);
        Emit(OpCodes.Br, failureLabel);

        if (successLabel is null)
        {
            MarkLabel(continueLabel);
        }
    }

    public void EmitAsciiWordBranch(Utf8IlLocal valueLocal, Utf8IlLabel? successLabel, Utf8IlLabel failureLabel)
    {
        var success = successLabel ?? DefineLabel();

        EmitRangeBranch(valueLocal, 'A', 'Z', success);
        EmitRangeBranch(valueLocal, 'a', 'z', success);
        EmitRangeBranch(valueLocal, '0', '9', success);
        EmitEqualityBranch(valueLocal, '_', success);
        Emit(OpCodes.Br, failureLabel);

        if (successLabel is null)
        {
            MarkLabel(success);
        }
    }

    public void EmitPredicateBranch(AsciiCharClassPredicateKind predicateKind, Utf8IlLocal valueLocal, Utf8IlLabel successLabel, Utf8IlLabel failureLabel)
    {
        switch (predicateKind)
        {
            case AsciiCharClassPredicateKind.Digit:
                EmitRangeBranch(valueLocal, '0', '9', successLabel);
                Emit(OpCodes.Br, failureLabel);
                return;

            case AsciiCharClassPredicateKind.AsciiLetter:
                EmitRangeBranch(valueLocal, 'A', 'Z', successLabel);
                EmitRangeBranch(valueLocal, 'a', 'z', successLabel);
                Emit(OpCodes.Br, failureLabel);
                return;

            case AsciiCharClassPredicateKind.AsciiLetterOrDigit:
                EmitRangeBranch(valueLocal, 'A', 'Z', successLabel);
                EmitRangeBranch(valueLocal, 'a', 'z', successLabel);
                EmitRangeBranch(valueLocal, '0', '9', successLabel);
                Emit(OpCodes.Br, failureLabel);
                return;

            case AsciiCharClassPredicateKind.AsciiLetterDigitUnderscore:
                EmitRangeBranch(valueLocal, 'A', 'Z', successLabel);
                EmitRangeBranch(valueLocal, 'a', 'z', successLabel);
                EmitRangeBranch(valueLocal, '0', '9', successLabel);
                EmitEqualityBranch(valueLocal, '_', successLabel, failureLabel);
                return;

            case AsciiCharClassPredicateKind.AsciiHexDigit:
                EmitRangeBranch(valueLocal, '0', '9', successLabel);
                EmitRangeBranch(valueLocal, 'A', 'F', successLabel);
                EmitRangeBranch(valueLocal, 'a', 'f', successLabel);
                Emit(OpCodes.Br, failureLabel);
                return;

            default:
                Emit(OpCodes.Br, failureLabel);
                return;
        }
    }

    public void EmitSmallPositiveSetBranch(ReadOnlySpan<byte> allowed, Utf8IlLocal valueLocal, Utf8IlLabel successLabel, Utf8IlLabel failureLabel)
    {
        if (allowed.Length == 0)
        {
            Emit(OpCodes.Br, failureLabel);
            return;
        }

        for (var i = 0; i < allowed.Length; i++)
        {
            EmitEqualityBranch(valueLocal, allowed[i], successLabel);
        }

        Emit(OpCodes.Br, failureLabel);
    }

    public void EmitSmallPositiveSetBranch(AsciiCharClass allowed, Utf8IlLocal valueLocal, Utf8IlLabel successLabel, Utf8IlLabel failureLabel)
    {
        if (!allowed.Negated)
        {
            // Borrow the masks instead of materializing another class byte array.
            // Preserve the ascending order of GetPositiveMatchBytes.
            ChargeWork(128);
            for (var value = 0; value < 128; value++)
                if (allowed.Contains((byte)value)) EmitEqualityBranch(valueLocal, value, successLabel);
        }
        Emit(OpCodes.Br, failureLabel);
    }

    public void EmitTrimSingleTrailingLf(Utf8IlLocal inputLengthLocal, Utf8IlLocal effectiveLengthLocal, Utf8IlLocal valueLocal)
    {
        var hasContentLabel = DefineLabel();
        var trimLabel = DefineLabel();
        var afterTrimLabel = DefineLabel();

        LoadLocal(inputLengthLocal);
        Emit(OpCodes.Brtrue, hasContentLabel);
        Emit(OpCodes.Br, afterTrimLabel);

        MarkLabel(hasContentLabel);
        LoadInputByte(inputLengthLocal, -1);
        StoreLocal(valueLocal);
        EmitEqualityBranch(valueLocal, '\n', trimLabel, afterTrimLabel);

        MarkLabel(trimLabel);
        LoadLocal(inputLengthLocal);
        LdcI4(1);
        Emit(OpCodes.Sub);
        StoreLocal(effectiveLengthLocal);

        MarkLabel(afterTrimLabel);
    }

    public void EmitReturnInt(int value)
    {
        LdcI4(value);
        Emit(OpCodes.Ret);
    }

    public void EmitReturnLocal(Utf8IlLocal local)
    {
        LoadLocal(local);
        Emit(OpCodes.Ret);
    }
}
