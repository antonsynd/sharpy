namespace Sharpy;

public partial struct Result<T, E>
{
    public bool __Bool__()
    {
        return IsOk();
    }
}
