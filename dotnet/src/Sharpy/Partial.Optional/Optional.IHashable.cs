namespace Sharpy;

public partial struct Optional<T>
{
    public int __Hash__()
    {
        var hashCode = new HashCode();
        hashCode.Add(typeof(Optional<T>).GetHashCode());

        if (HasValue())
        {
            hashCode.Add(true.GetHashCode());
            hashCode.Add(_value.GetHashCode());
        }
        else
        {
            hashCode.Add(false.GetHashCode());
        }

        return hashCode.ToHashCode();
    }

    public override int GetHashCode()
    {
        return __Hash__();
    }
}
