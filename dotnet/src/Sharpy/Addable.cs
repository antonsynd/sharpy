namespace Sharpy
{
    public interface Addable<T, U>
    {
        T __Add__(U other);
    }

    public interface Addable<T> : Addable<T, T>
    {
    }
}
