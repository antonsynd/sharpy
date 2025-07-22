namespace Sharpy;

public partial struct Optional<T>
{
    public bool __Bool__()
    {
        return HasValue();
    }
}
