namespace Sharpy;

public sealed partial class List<T>
{
    /// <inheritdoc/>
    public void __IAdd__(List<T> other)
    {
        if (other is null)
        {
            throw TypeError.CanOnlyNot("concatenate", $"List<{typeof(T).Name}>", "NoneType", "to", $"List<{typeof(T).Name}>");
        }

        Extend(other);
    }
}
