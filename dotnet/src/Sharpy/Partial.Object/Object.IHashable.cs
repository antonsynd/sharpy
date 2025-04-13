namespace Sharpy;

public abstract partial class Object
{
    /// <remarks>
    /// By default, returns <see cref="object.GetHashCode()"/>.
    /// </remarks>
    public virtual int __Hash__()
    {
        return base.GetHashCode();
    }
}
