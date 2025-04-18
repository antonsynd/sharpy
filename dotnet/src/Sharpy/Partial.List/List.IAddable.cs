namespace Sharpy;

public sealed partial class List<T>
{
    /// <inheritdoc/>
    public List<T> __Add__(List<T> other)
    {
        if (other is null)
        {
            throw new TypeError($"can only concatenate List<${typeof(T).Name}> (not \"NoneType\") to List<${typeof(T).Name}");
        }

        var res = Copy();
        res.Extend(other);

        return res;
    }
}
