namespace Sharpy.Core;

public abstract partial class Object
{
    /// <remarks>
    /// By default, calls <see cref="__Repr__()"/>.
    /// </remarks>
    public virtual string __Str__()
    {
        return __Repr__();
    }
}
