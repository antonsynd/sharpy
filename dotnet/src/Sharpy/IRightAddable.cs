namespace Sharpy;

public interface IRightAddable<TSum, TAugend>
{
    TSum __RAdd__(TAugend other);
}

public interface IRightAddable<T> : IRightAddable<T, T>
{
}
