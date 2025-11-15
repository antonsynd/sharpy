namespace Sharpy.Core;

public sealed partial class List<T>
{
    /// <inheritdoc/>
    public List<T> __RAdd__(List<T> other)
    {
        if (other is null)
        {
            throw TypeError.CanOnlyNot("concatenate", $"List<{typeof(T).Name}>", "NoneType", "to", $"List<{typeof(T).Name}>");
        }

        var res = other.Copy();
        res.Extend(this);

        return res;
    }
}
