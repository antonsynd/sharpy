namespace Sharpy;

public static partial class Exports
{
    public static T Next<T>(Iterator<T> iterator)
    {
        if (iterator is null)
        {
            throw TypeError.ArgNone("next", "iterator");
        }

        return iterator.__Next__();
    }
}
