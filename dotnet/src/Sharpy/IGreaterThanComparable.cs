namespace Sharpy
{
    public interface IGreaterThanComparable<T> : System.IComparable<T>
    {
        bool __Gt__(T other);
    }
}
