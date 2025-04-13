namespace Sharpy;

public sealed partial class List<T>
{
    /// <inheritdoc/>
    public List<T> __Mul__(int i)
    {
        var res = new List<T>();

        if (i <= 0)
        {
            return res;
        }

        res._list.EnsureCapacity(_list.Count * i);

        for (; i > 0; --i)
        {
            res.Extend(this);
        }

        return res;
    }
}
