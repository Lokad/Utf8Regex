using System.Reflection;
using System.Reflection.Emit;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8IlEmitterTests
{
    private delegate int DummySpanDelegate(object? _, ReadOnlySpan<byte> input);
    private delegate int DummySpanIndexDelegate(object? _, ReadOnlySpan<byte> input, int index);
    private delegate int DummySpanIndexLengthDelegate(object? _, ReadOnlySpan<byte> input, int index, int length);

    private static readonly MethodInfo s_getSpanLengthMethod =
        typeof(ReadOnlySpan<byte>).GetProperty(nameof(ReadOnlySpan<byte>.Length))!.GetMethod!;

    private static readonly MethodInfo s_getSpanItemMethod =
        typeof(ReadOnlySpan<byte>).GetProperty("Item")!.GetMethod!;

    [Fact]
    public void Utf8IlEmitterCanLoadSpanLengthAndConstants()
    {
        var dynamicMethod = new DynamicMethod(
            "Utf8Regex_Test_LengthPlusConstant",
            typeof(int),
            [typeof(object), typeof(ReadOnlySpan<byte>)],
            typeof(Utf8IlEmitterTests),
            skipVisibility: false);
        var il = new Utf8IlEmitter(dynamicMethod.GetILGenerator(), s_getSpanLengthMethod, s_getSpanItemMethod);

        il.LoadInputLength();
        il.LdcI4(7);
        il.Emit(OpCodes.Add);
        il.Emit(OpCodes.Ret);

        var del = dynamicMethod.CreateDelegate<DummySpanDelegate>();
        Assert.Equal(12, del(null, "hello"u8));
    }

    [Fact]
    public void Utf8IlEmitterCanLoadInputByteWithOffset()
    {
        var dynamicMethod = new DynamicMethod(
            "Utf8Regex_Test_LoadInputByteOffset",
            typeof(int),
            [typeof(object), typeof(ReadOnlySpan<byte>), typeof(int)],
            typeof(Utf8IlEmitterTests),
            skipVisibility: false);
        var il = new Utf8IlEmitter(dynamicMethod.GetILGenerator(), s_getSpanLengthMethod, s_getSpanItemMethod);
        var indexLocal = il.DeclareLocal<int>();

        il.LoadArg(2);
        il.StoreLocal(indexLocal);
        il.LoadInputByte(indexLocal, 1);
        il.Emit(OpCodes.Ret);

        var del = dynamicMethod.CreateDelegate<DummySpanIndexDelegate>();
        Assert.Equal((int)(byte)'b', del(null, "zabc"u8, 1));
    }

    [Fact]
    public void Utf8IlEmitterCanBranchOnAsciiClassifications()
    {
        var dynamicMethod = new DynamicMethod(
            "Utf8Regex_Test_ClassifyAsciiByte",
            typeof(int),
            [typeof(object), typeof(ReadOnlySpan<byte>), typeof(int)],
            typeof(Utf8IlEmitterTests),
            skipVisibility: false);
        var il = new Utf8IlEmitter(dynamicMethod.GetILGenerator(), s_getSpanLengthMethod, s_getSpanItemMethod);
        var indexLocal = il.DeclareLocal<int>();
        var valueLocal = il.DeclareLocal<byte>();
        var whitespaceLabel = il.DefineLabel();
        var nonWhitespaceLabel = il.DefineLabel();
        var upperLabel = il.DefineLabel();
        var nonUpperLabel = il.DefineLabel();
        var wordLabel = il.DefineLabel();
        var otherLabel = il.DefineLabel();

        il.LoadArg(2);
        il.StoreLocal(indexLocal);
        il.LoadInputByte(indexLocal);
        il.StoreLocal(valueLocal);
        il.EmitAsciiWhitespaceBranch(valueLocal, whitespaceLabel, nonWhitespaceLabel);

        il.MarkLabel(nonWhitespaceLabel);
        il.EmitAsciiUpperBranch(valueLocal, upperLabel, nonUpperLabel);

        il.MarkLabel(upperLabel);
        il.LdcI4(2);
        il.Emit(OpCodes.Ret);

        il.MarkLabel(whitespaceLabel);
        il.LdcI4(1);
        il.Emit(OpCodes.Ret);

        il.MarkLabel(nonUpperLabel);
        il.EmitAsciiWordBranch(valueLocal, wordLabel, otherLabel);

        il.MarkLabel(wordLabel);
        il.LdcI4(3);
        il.Emit(OpCodes.Ret);

        il.MarkLabel(otherLabel);
        il.LdcI4(0);
        il.Emit(OpCodes.Ret);

        var del = dynamicMethod.CreateDelegate<DummySpanIndexDelegate>();
        Assert.Equal(1, del(null, " \t"u8, 0));
        Assert.Equal(2, del(null, "Az"u8, 0));
        Assert.Equal(3, del(null, "_z"u8, 0));
        Assert.Equal(0, del(null, "-z"u8, 0));
    }

    [Fact]
    public void Utf8IlEmitterCanEnsureAvailableAndCompareLiteral()
    {
        var dynamicMethod = new DynamicMethod(
            "Utf8Regex_Test_EnsureAvailableLiteral",
            typeof(int),
            [typeof(object), typeof(ReadOnlySpan<byte>), typeof(int), typeof(int)],
            typeof(Utf8IlEmitterTests),
            skipVisibility: false);
        var il = new Utf8IlEmitter(dynamicMethod.GetILGenerator(), s_getSpanLengthMethod, s_getSpanItemMethod);
        var indexLocal = il.DeclareLocal<int>();
        var lengthLocal = il.DeclareLocal<int>();
        var enoughLabel = il.DefineLabel();
        var matchLabel = il.DefineLabel();
        var failLabel = il.DefineLabel();

        il.LoadArg(2);
        il.StoreLocal(indexLocal);
        il.LoadArg(3);
        il.StoreLocal(lengthLocal);
        il.EmitEnsureAvailable(indexLocal, lengthLocal, 3, enoughLabel, failLabel);

        il.MarkLabel(enoughLabel);
        il.EmitSpanLiteralEquals(indexLocal, "abc"u8, skipPrefixCount: 1, matchLabel, failLabel);

        il.MarkLabel(matchLabel);
        il.LdcI4(1);
        il.Emit(OpCodes.Ret);

        il.MarkLabel(failLabel);
        il.LdcI4(0);
        il.Emit(OpCodes.Ret);

        var del = dynamicMethod.CreateDelegate<DummySpanIndexLengthDelegate>();
        Assert.Equal(1, del(null, "zabc"u8, 1, 4));
        Assert.Equal(0, del(null, "zaxc"u8, 1, 4));
        Assert.Equal(0, del(null, "za"u8, 1, 2));
    }

    [Fact]
    public void Utf8IlEmitterCanAddToLocalAndStoreDifference()
    {
        var dynamicMethod = new DynamicMethod(
            "Utf8Regex_Test_AddToLocalDifference",
            typeof(int),
            [typeof(object), typeof(ReadOnlySpan<byte>), typeof(int), typeof(int)],
            typeof(Utf8IlEmitterTests),
            skipVisibility: false);
        var il = new Utf8IlEmitter(dynamicMethod.GetILGenerator(), s_getSpanLengthMethod, s_getSpanItemMethod);
        var leftLocal = il.DeclareLocal<int>();
        var rightLocal = il.DeclareLocal<int>();
        var diffLocal = il.DeclareLocal<int>();

        il.LoadArg(2);
        il.StoreLocal(leftLocal);
        il.LoadArg(3);
        il.StoreLocal(rightLocal);
        il.AddToLocal(leftLocal, 5);
        il.EmitStoreDifference(diffLocal, leftLocal, rightLocal);
        il.LoadLocal(diffLocal);
        il.Emit(OpCodes.Ret);

        var del = dynamicMethod.CreateDelegate<DummySpanIndexLengthDelegate>();
        Assert.Equal(6, del(null, [] , 4, 3));
    }

    [Fact]
    public void Utf8IlEmitterCanDispatchOnByteValues()
    {
        var dynamicMethod = new DynamicMethod(
            "Utf8Regex_Test_ByteDispatch",
            typeof(int),
            [typeof(object), typeof(ReadOnlySpan<byte>), typeof(int)],
            typeof(Utf8IlEmitterTests),
            skipVisibility: false);
        var il = new Utf8IlEmitter(dynamicMethod.GetILGenerator(), s_getSpanLengthMethod, s_getSpanItemMethod);
        var indexLocal = il.DeclareLocal<int>();
        var valueLocal = il.DeclareLocal<byte>();
        var aLabel = il.DefineLabel();
        var zLabel = il.DefineLabel();
        var defaultLabel = il.DefineLabel();

        il.LoadArg(2);
        il.StoreLocal(indexLocal);
        il.LoadInputByte(indexLocal);
        il.StoreLocal(valueLocal);
        il.EmitByteDispatch(valueLocal, [(byte)'a', (byte)'z'], [aLabel, zLabel], defaultLabel);

        il.MarkLabel(aLabel);
        il.LdcI4(1);
        il.Emit(OpCodes.Ret);

        il.MarkLabel(zLabel);
        il.LdcI4(2);
        il.Emit(OpCodes.Ret);

        il.MarkLabel(defaultLabel);
        il.LdcI4(0);
        il.Emit(OpCodes.Ret);

        var del = dynamicMethod.CreateDelegate<DummySpanIndexDelegate>();
        Assert.Equal(1, del(null, "abc"u8, 0));
        Assert.Equal(2, del(null, "za"u8, 0));
        Assert.Equal(0, del(null, "mx"u8, 0));
    }

    [Fact]
    public void Utf8IlEmitterCanClampAndStoreDerivedLocals()
    {
        var dynamicMethod = new DynamicMethod(
            "Utf8Regex_Test_ClampAndDerivedLocals",
            typeof(int),
            [typeof(object), typeof(ReadOnlySpan<byte>), typeof(int)],
            typeof(Utf8IlEmitterTests),
            skipVisibility: false);
        var il = new Utf8IlEmitter(dynamicMethod.GetILGenerator(), s_getSpanLengthMethod, s_getSpanItemMethod);
        var sourceLocal = il.DeclareLocal<int>();
        var leftLocal = il.DeclareLocal<int>();
        var rightLocal = il.DeclareLocal<int>();

        il.LoadArg(2);
        il.StoreLocal(sourceLocal);
        il.EmitStoreDifference(leftLocal, sourceLocal, 5);
        il.EmitClampLocalMinZero(leftLocal);
        il.EmitStoreSum(rightLocal, leftLocal, 3);
        il.EmitStoreDifference(sourceLocal, rightLocal, leftLocal);
        il.LoadLocal(sourceLocal);
        il.Emit(OpCodes.Ret);

        var del = dynamicMethod.CreateDelegate<DummySpanIndexDelegate>();
        Assert.Equal(3, del(null, [] , 2));
        Assert.Equal(3, del(null, [] , 9));
    }

    [Fact]
    public void Utf8IlEmitterCanBranchOnLocalComparisons()
    {
        var dynamicMethod = new DynamicMethod(
            "Utf8Regex_Test_LocalComparisonBranch",
            typeof(int),
            [typeof(object), typeof(ReadOnlySpan<byte>), typeof(int), typeof(int)],
            typeof(Utf8IlEmitterTests),
            skipVisibility: false);
        var il = new Utf8IlEmitter(dynamicMethod.GetILGenerator(), s_getSpanLengthMethod, s_getSpanItemMethod);
        var leftLocal = il.DeclareLocal<int>();
        var rightLocal = il.DeclareLocal<int>();
        var lessLabel = il.DefineLabel();
        var greaterLabel = il.DefineLabel();
        var equalLabel = il.DefineLabel();
        var finalLabel = il.DefineLabel();

        il.LoadArg(2);
        il.StoreLocal(leftLocal);
        il.LoadArg(3);
        il.StoreLocal(rightLocal);
        il.EmitLocalLessThanLocalBranch(leftLocal, rightLocal, lessLabel, equalLabel);

        il.MarkLabel(equalLabel);
        il.EmitLocalGreaterThanLocalBranch(leftLocal, rightLocal, greaterLabel, finalLabel);

        il.MarkLabel(lessLabel);
        il.LdcI4(-1);
        il.Emit(OpCodes.Ret);

        il.MarkLabel(greaterLabel);
        il.LdcI4(1);
        il.Emit(OpCodes.Ret);

        il.MarkLabel(finalLabel);
        il.LdcI4(0);
        il.Emit(OpCodes.Ret);

        var del = dynamicMethod.CreateDelegate<DummySpanIndexLengthDelegate>();
        Assert.Equal(-1, del(null, [] , 2, 5));
        Assert.Equal(1, del(null, [] , 5, 2));
        Assert.Equal(0, del(null, [] , 3, 3));
    }

    [Fact]
    public void Utf8IlEmitterCanClassifyAsciiNewlines()
    {
        var dynamicMethod = new DynamicMethod(
            "Utf8Regex_Test_NewlineClassification",
            typeof(int),
            [typeof(object), typeof(ReadOnlySpan<byte>), typeof(int)],
            typeof(Utf8IlEmitterTests),
            skipVisibility: false);
        var il = new Utf8IlEmitter(dynamicMethod.GetILGenerator(), s_getSpanLengthMethod, s_getSpanItemMethod);
        var indexLocal = il.DeclareLocal<int>();
        var valueLocal = il.DeclareLocal<byte>();
        var newlineLabel = il.DefineLabel();
        var otherLabel = il.DefineLabel();

        il.LoadArg(2);
        il.StoreLocal(indexLocal);
        il.LoadInputByte(indexLocal);
        il.StoreLocal(valueLocal);
        il.EmitAsciiNewlineBranch(valueLocal, newlineLabel, otherLabel);

        il.MarkLabel(newlineLabel);
        il.LdcI4(1);
        il.Emit(OpCodes.Ret);

        il.MarkLabel(otherLabel);
        il.LdcI4(0);
        il.Emit(OpCodes.Ret);

        var del = dynamicMethod.CreateDelegate<DummySpanIndexDelegate>();
        Assert.Equal(1, del(null, "\r"u8, 0));
        Assert.Equal(1, del(null, "\n"u8, 0));
        Assert.Equal(0, del(null, " "u8, 0));
    }
}
