namespace Sharpy;

public partial struct Optional<T>
{
    public int __Hash__()
    {
        var hashCode = new HashCode();
        hashCode.Add(typeof(Optional<T>).GetHashCode());
        hashCode.Add(_value.GetHashCode());

        return hashCode.ToHashCode();
    }
}
