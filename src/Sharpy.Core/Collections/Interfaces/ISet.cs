using Sharpy.Core;

namespace Sharpy.Collections.Interfaces;

/// <summary>
/// Interface for read-only sets.
/// </summary>
public interface ISet<T>
    : ICollection<T>,
      Sharpy.Core.IEquatable<ISet<T>>,
      Sharpy.Core.IInequatable<ISet<T>>
{
    ISet<T> __And__(ISet<T> other);

    ISet<T> __Or__(ISet<T> other);

    ISet<T> __Sub__(ISet<T> other);

    ISet<T> __RSub__(ISet<T> other);

    ISet<T> __XOr__(ISet<T> other);

    ISet<T> __ROr__(ISet<T> other);

    bool IsDisjoint(ISet<T> other);
}

public interface ISet<TSet, TElement>
    : ISet<TElement>
      where TSet
        : ISet<TSet, TElement>,
          ILessThanOrEquatable<TSet>,
          IGreaterThanOrEquatable<TSet>
{
    TSet __And__(TSet other);

    TSet __Or__(TSet other);

    TSet __Sub__(TSet other);

    TSet __RSub__(TSet other);

    TSet __XOr__(TSet other);

    TSet __ROr__(TSet other);

    bool IsDisjoint(TSet other);

    static virtual TSet operator &(TSet left, TSet right)
    {
        return left.__And__(right);
    }

    static virtual TSet operator |(TSet left, TSet right)
    {
        return left.__Or__(right);
    }

    static virtual TSet operator -(TSet left, TSet right)
    {
        return left.__Sub__(right);
    }

    static virtual TSet operator ^(TSet left, TSet right)
    {
        return left.__XOr__(right);
    }
}
