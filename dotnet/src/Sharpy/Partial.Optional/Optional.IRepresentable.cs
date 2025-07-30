namespace Sharpy;

public partial struct Optional<T>
{
    public string __Repr__()
    {
        if (!HasValue())
        {
            return $"Optional<{typeof(T).Name}>(None)";
        }

        return $"Optional<{typeof(T).Name}>({Repr(_value)})";
    }
}
