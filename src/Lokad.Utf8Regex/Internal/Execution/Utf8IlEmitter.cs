using System.Reflection;
using System.Reflection.Emit;

namespace Lokad.Utf8Regex.Internal.Execution;

internal sealed class Utf8IlEmitter
{
    private readonly ILGenerator _il;
    private readonly MethodInfo _getSpanLengthMethod;
    private readonly MethodInfo _getSpanItemMethod;
    private readonly byte _inputArgIndex;

    public Utf8IlEmitter(ILGenerator il, MethodInfo getSpanLengthMethod, MethodInfo getSpanItemMethod, byte inputArgIndex = 1)
    {
        _il = il;
        _getSpanLengthMethod = getSpanLengthMethod;
        _getSpanItemMethod = getSpanItemMethod;
        _inputArgIndex = inputArgIndex;
    }

    public LocalBuilder DeclareLocal<T>() => _il.DeclareLocal(typeof(T));

    public LocalBuilder DeclareLocal(Type type) => _il.DeclareLocal(type);

    public Label DefineLabel() => _il.DefineLabel();

    public void MarkLabel(Label label) => _il.MarkLabel(label);

    public void Emit(OpCode opcode) => _il.Emit(opcode);

    public void Emit(OpCode opcode, Label label) => _il.Emit(opcode, label);

    public void Emit(OpCode opcode, LocalBuilder local) => _il.Emit(opcode, local);

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

    public void LoadLocal(LocalBuilder local) => _il.Emit(OpCodes.Ldloc, local);

    public void LoadLocalAddress(LocalBuilder local) => _il.Emit(OpCodes.Ldloca_S, local);

    public void StoreLocal(LocalBuilder local) => _il.Emit(OpCodes.Stloc, local);

    public void LoadInputLength()
    {
        LoadArgAddress(_inputArgIndex);
        Emit(OpCodes.Call, _getSpanLengthMethod);
    }

    public void LoadInputByte(LocalBuilder indexLocal, int offset = 0)
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

    public void AddToLocal(LocalBuilder local, int delta)
    {
        LoadLocal(local);
        LdcI4(delta);
        Emit(OpCodes.Add);
        StoreLocal(local);
    }

    public void EmitStoreDifference(LocalBuilder targetLocal, LocalBuilder leftLocal, LocalBuilder rightLocal)
    {
        LoadLocal(leftLocal);
        LoadLocal(rightLocal);
        Emit(OpCodes.Sub);
        StoreLocal(targetLocal);
    }

    public void EmitStoreDifference(LocalBuilder targetLocal, LocalBuilder leftLocal, int rightConstant)
    {
        LoadLocal(leftLocal);
        LdcI4(rightConstant);
        Emit(OpCodes.Sub);
        StoreLocal(targetLocal);
    }

    public void EmitStoreSum(LocalBuilder targetLocal, LocalBuilder leftLocal, int rightConstant)
    {
        LoadLocal(leftLocal);
        LdcI4(rightConstant);
        Emit(OpCodes.Add);
        StoreLocal(targetLocal);
    }

    public void EmitClampLocalMinZero(LocalBuilder local)
    {
        var nonNegativeLabel = DefineLabel();
        LoadLocal(local);
        LdcI4(0);
        Emit(OpCodes.Bge, nonNegativeLabel);
        LdcI4(0);
        StoreLocal(local);
        MarkLabel(nonNegativeLabel);
    }

    public void EmitLocalLessThanLocalBranch(LocalBuilder leftLocal, LocalBuilder rightLocal, Label successLabel, Label failureLabel)
    {
        LoadLocal(leftLocal);
        LoadLocal(rightLocal);
        Emit(OpCodes.Blt, successLabel);
        Emit(OpCodes.Br, failureLabel);
    }

    public void EmitLocalGreaterThanLocalBranch(LocalBuilder leftLocal, LocalBuilder rightLocal, Label successLabel, Label failureLabel)
    {
        LoadLocal(leftLocal);
        LoadLocal(rightLocal);
        Emit(OpCodes.Bgt, successLabel);
        Emit(OpCodes.Br, failureLabel);
    }

    public void EmitEnsureAvailable(LocalBuilder startLocal, LocalBuilder inputLengthLocal, int requiredLength, Label successLabel, Label failureLabel)
    {
        LoadLocal(inputLengthLocal);
        LoadLocal(startLocal);
        Emit(OpCodes.Sub);
        LdcI4(requiredLength);
        Emit(OpCodes.Bge, successLabel);
        Emit(OpCodes.Br, failureLabel);
    }

    public void EmitLocalsEqualBranch(LocalBuilder leftLocal, LocalBuilder rightLocal, Label successLabel, Label failureLabel)
    {
        LoadLocal(leftLocal);
        LoadLocal(rightLocal);
        Emit(OpCodes.Beq, successLabel);
        Emit(OpCodes.Br, failureLabel);
    }

    public void EmitSpanLiteralEquals(LocalBuilder startLocal, ReadOnlySpan<byte> literal, int skipPrefixCount, Label successLabel, Label failureLabel)
    {
        for (var i = skipPrefixCount; i < literal.Length; i++)
        {
            LoadInputByte(startLocal, i);
            LdcI4(literal[i]);
            Emit(OpCodes.Bne_Un, failureLabel);
        }

        Emit(OpCodes.Br, successLabel);
    }

    public void EmitSpanLiteralEqualsIgnoreCase(LocalBuilder startLocal, ReadOnlySpan<byte> literal, int skipPrefixCount, LocalBuilder valueLocal, Label successLabel, Label failureLabel)
    {
        for (var i = skipPrefixCount; i < literal.Length; i++)
        {
            LoadInputByte(startLocal, i);
            StoreLocal(valueLocal);
            EmitAsciiCaseInsensitiveByteEqualsOrBranchFailure(valueLocal, literal[i], failureLabel);
        }

        Emit(OpCodes.Br, successLabel);
    }

    public void EmitByteDispatch(LocalBuilder valueLocal, ReadOnlySpan<byte> keys, ReadOnlySpan<Label> labels, Label defaultLabel)
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

    public void EmitEqualityBranch(LocalBuilder valueLocal, int expected, Label successLabel)
    {
        LoadLocal(valueLocal);
        LdcI4(expected);
        Emit(OpCodes.Beq, successLabel);
    }

    public void EmitEqualityBranch(LocalBuilder valueLocal, int expected, Label successLabel, Label failureLabel)
    {
        LoadLocal(valueLocal);
        LdcI4(expected);
        Emit(OpCodes.Beq, successLabel);
        Emit(OpCodes.Br, failureLabel);
    }

    public void EmitAsciiCaseInsensitiveByteEqualsOrBranchFailure(LocalBuilder valueLocal, int expected, Label failureLabel)
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

    public void EmitRangeBranch(LocalBuilder valueLocal, int lowerInclusive, int upperInclusive, Label successLabel)
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

    public void EmitAsciiWhitespaceBranch(LocalBuilder valueLocal, Label successLabel, Label failureLabel)
    {
        EmitEqualityBranch(valueLocal, ' ', successLabel);
        EmitEqualityBranch(valueLocal, '\t', successLabel);
        EmitEqualityBranch(valueLocal, '\r', successLabel);
        EmitEqualityBranch(valueLocal, '\n', successLabel);
        EmitEqualityBranch(valueLocal, 0x0B, successLabel);
        EmitEqualityBranch(valueLocal, 0x0C, successLabel);
        Emit(OpCodes.Br, failureLabel);
    }

    public void EmitAsciiNewlineBranch(LocalBuilder valueLocal, Label successLabel, Label failureLabel)
    {
        EmitEqualityBranch(valueLocal, '\r', successLabel);
        EmitEqualityBranch(valueLocal, '\n', successLabel);
        Emit(OpCodes.Br, failureLabel);
    }

    public void EmitAsciiUpperBranch(LocalBuilder valueLocal, Label? successLabel, Label failureLabel)
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

    public void EmitAsciiWordBranch(LocalBuilder valueLocal, Label? successLabel, Label failureLabel)
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

    public void EmitPredicateBranch(AsciiCharClassPredicateKind predicateKind, LocalBuilder valueLocal, Label successLabel, Label failureLabel)
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

    public void EmitSmallPositiveSetBranch(ReadOnlySpan<byte> allowed, LocalBuilder valueLocal, Label successLabel, Label failureLabel)
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

    public void EmitTrimSingleTrailingLf(LocalBuilder inputLengthLocal, LocalBuilder effectiveLengthLocal, LocalBuilder valueLocal)
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

    public void EmitReturnLocal(LocalBuilder local)
    {
        LoadLocal(local);
        Emit(OpCodes.Ret);
    }
}
