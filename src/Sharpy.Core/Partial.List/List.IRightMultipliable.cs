namespace Sharpy.Core;

public sealed partial class List<T>
{
    /// <inheritdoc/>
    public List<T> __RMul__(int i)
    {
        return __Mul__(i);
    }
}
