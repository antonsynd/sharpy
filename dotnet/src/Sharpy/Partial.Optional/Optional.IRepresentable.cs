namespace Sharpy;

public partial struct Optional<T>
{
    public string __Repr__()
    {
        if (_value is null)
        {
            return "None";
        }

        return Repr(_value);
    }
}
