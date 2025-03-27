namespace Sharpy
{
    public interface InplaceMultipliable<T>
    {
        void __IMul__(T other);
    }
}
