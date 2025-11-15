namespace Sharpy.Core;

public sealed partial class List<T>
{
    /// <inheritdoc/>
    public List<T> __Add__(List<T> other)
    {
        if (other is null)
        {
            throw TypeError.CanOnlyNot("concatenate", $"List<{typeof(T).Name}>", "NoneType", "to", $"List<{typeof(T).Name}>");
        }

        var res = Copy();
        res.Extend(other);

        return res;
    }
}
