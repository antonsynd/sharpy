namespace Sharpy.Core;

public abstract partial class Object
{
    /// <summary>
    /// By default, returns a string containing the result of
    /// <see cref="__Id__()"/> in the form <c>"&lt;Object object with id {__Id__()}&gt;"</c>.
    /// </summary>
    /// <returns></returns>
    public virtual string __Repr__()
    {
        return $"<Object object with id {__Id__()}>";
    }
}
