namespace Sharpy
{
    public interface RightAddable<T, U>
    {
        T __RAdd__(U other);
    }

    public interface RightAddable<T> : RightAddable<T, T>
    {
    }
}
