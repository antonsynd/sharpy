namespace Sharpy
{
    public interface IAddable<T, U>
    {
        T __Add__(U other);
    }

    public interface IAddable<T> : IAddable<T, T>
    {
    }
}
