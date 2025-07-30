namespace Sharpy;

public partial struct Result<T, E>
{
    public int __Hash__()
    {
        var hashCode = new HashCode();
        hashCode.Add(typeof(Result<T, E>).GetHashCode());

        if (IsOk())
        {
            hashCode.Add(true.GetHashCode());
            hashCode.Add(_value.GetHashCode());
        }
        else
        {
            hashCode.Add(false.GetHashCode());
            hashCode.Add(_error.GetHashCode());
        }

        return hashCode.ToHashCode();
    }
}
