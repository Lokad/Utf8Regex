using System.Reflection.Emit;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8DynamicIlParameterTests
{
    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void ParameterArityAndOrderingSurviveBuilderDisposal(int count)
    {
        Delegate result;
        using (var builder = Create(count, typeof(int)))
        {
            builder.Emit(OpCodes.Ldarg, count - 1);
            builder.Emit(OpCodes.Ret);
            result = count switch
            {
                1 => builder.CreateDelegate<Func<int, int>>().Callback,
                2 => builder.CreateDelegate<Func<int, int, int>>().Callback,
                3 => builder.CreateDelegate<Func<int, int, int, int>>().Callback,
                _ => builder.CreateDelegate<Func<int, int, int, int, int>>().Callback,
            };
        }
        var arguments = Enumerable.Range(1, count).Select(value => (object)value).ToArray();
        Assert.Equal(count, result.DynamicInvoke(arguments));
        Assert.Equal(count, result.Method.GetParameters().Length);
    }

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void NullSuppliedParameterStillRejectsAndRestoresParent(int count)
    {
        using var owner = Utf8ConstructionScope.Enter();
        var loan = owner.Budget.AllocateArray<byte>(1, false);
        loan[0] = 37;
        var error = Assert.Throws<ArgumentException>(() => Create(count, null!));
        Assert.Null(error.ParamName);
        Assert.Equal(48ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Reserved);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Equal(37, loan[0]);
    }

    private static Utf8DynamicIlBuilder Create(int count, Type last)
    {
        const string name = "OwnedParameterOrder";
        var owner = typeof(Utf8DynamicIlParameterTests);
        return count switch
        {
            1 => Utf8DynamicIlBuilder.Create(name, typeof(int), owner, last),
            2 => Utf8DynamicIlBuilder.Create(name, typeof(int), owner, typeof(int), last),
            3 => Utf8DynamicIlBuilder.Create(name, typeof(int), owner, typeof(int), typeof(int), last),
            _ => Utf8DynamicIlBuilder.Create(name, typeof(int), owner, typeof(int), typeof(int), typeof(int), last),
        };
    }
}
