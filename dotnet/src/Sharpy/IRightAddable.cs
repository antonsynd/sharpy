namespace Sharpy;

public interface IRightAddableWith<TSum, TAddend>
{
    TSum __RAdd__(TAddend other);
}

public interface IRightAddable<TAugend, TAddend, TSum>
    : IRightAddableWith<TSum, TAddend>
      where TAugend
        : IRightAddable<TAugend, TAddend, TSum>
{
    static virtual TSum operator +(TAddend left, TAugend right)
    {
        return right.__RAdd__(left);
    }
}

public interface IRightAddable<T>
    : IRightAddableWith<T, T>,
      IRightAddable<T, T, T>
      where T
        : IRightAddable<T>
{
}
