namespace Sharpy.Core;

public interface IInplaceMultipliable<T>
{
    void __IMul__(T other);
}
