namespace Sharpy;

using static Sharpy.Exports;

public partial struct Result<T, E>
{
    public string __Repr__()
    {
        if (!IsOk())
        {
            return $"Result<{typeof(T).Name}, {typeof(E).Name}>({Repr(_error)})";
        }

        return $"Result<{typeof(T).Name}, {typeof(E).Name}>({Repr(_value)})";
    }
}
