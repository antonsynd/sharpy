namespace Sharpy;

public interface IRightAddable<T, U>
{
    T __RAdd__(U other);
}

public interface IRightAddable<T> : IRightAddable<T, T>
{
}
