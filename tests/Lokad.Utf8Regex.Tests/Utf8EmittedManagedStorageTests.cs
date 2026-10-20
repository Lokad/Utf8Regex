using System.Reflection;
using System.Reflection.Emit;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Caching;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

// Forced collections must not pause parallel deadline-sensitive operations.
[Collection(Utf8RuntimeAllocationCollection.Name)]
public sealed class Utf8EmittedManagedStorageTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(19)]
    public void FinalReceiptMatchesActualCopiesAfterBuilderDisposal(int locals)
    {
        Utf8EmittedDelegate<Func<int,int>> emitted;
        using (var builder=Utf8DynamicIlBuilder.Create("RetainedCopy",typeof(int),typeof(Utf8EmittedManagedStorageTests),typeof(int)))
        {
            for(var index=0;index<locals;index++) builder.DeclareLocal(typeof(int));
            builder.Emit(OpCodes.Ldarg_0); builder.Emit(OpCodes.Ret);
            emitted=builder.CreateDelegate<Func<int,int>>();
            Assert.Throws<InvalidOperationException>(()=>builder.CreateDelegate<Func<int,int>>());
        }
        GC.Collect(); GC.WaitForPendingFinalizers();
        Assert.Equal(37,emitted.Callback(37));
        Assert.Equal(InspectManagedCopies(emitted.Callback),emitted.ManagedArrayBytes);
        Assert.True(emitted.ManagedArrayBytes>0);
    }







    internal static ulong InspectManagedCopies(Delegate callback)
    {
        // Independent qualification only. Production uses final emission receipts
        // and does not depend on private CLR members or reflection enumeration.
        var method=Assert.IsType<DynamicMethod>(callback.Method);
        var parameters=Assert.IsAssignableFrom<Type[]>(Field(method,"_parameterTypes"));
        var info=Assert.IsType<DynamicILInfo>(Field(method,"_dynamicILInfo"));
        var code=Assert.IsType<byte[]>(Field(info,"m_code"));
        var locals=Assert.IsType<byte[]>(Field(info,"m_localSignature"));
        Assert.NotSame(code,locals);
        return checked(Utf8ConstructionBudget.ArrayBytes<Type>(parameters.Length)+
            Utf8ConstructionBudget.ArrayBytes<byte>(code.Length)+Utf8ConstructionBudget.ArrayBytes<byte>(locals.Length));
    }

    private static object? Field(object value,string name)
    {
        var field=value.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic);
        Assert.NotNull(field);return field.GetValue(value);
    }
}
