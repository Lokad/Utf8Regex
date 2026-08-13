namespace Lokad.Utf8Regex.Internal.FrontEnd;

/// <summary>Checked budget shared by speculative, semantics-neutral optimizers.</summary>
internal sealed class Utf8PreparationBudget
{
    private int _remainingWork;
    private int _remainingOutput;

    public Utf8PreparationBudget(int workLimit, int outputLimit)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(workLimit);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(outputLimit);
        _remainingWork = workLimit;
        _remainingOutput = outputLimit;
    }

    public bool TryReserve(int work, int output)
    {
        if (work < 0 || output < 0 || work > _remainingWork || output > _remainingOutput)
        {
            return false;
        }

        _remainingWork -= work;
        _remainingOutput -= output;
        return true;
    }

    public bool TryReserveProduct(int left, int right, int outputLimit, out int product)
    {
        product = 0;
        if (left <= 0 || right <= 0)
        {
            return false;
        }

        var checkedProduct = (long)left * right;
        if (checkedProduct > outputLimit || checkedProduct > int.MaxValue)
        {
            return false;
        }

        product = (int)checkedProduct;
        return TryReserve(product, product);
    }
}
