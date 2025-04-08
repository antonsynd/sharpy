namespace Sharpy
{
    public interface ILessThanComparable<T> : System.IComparable<T>
    {
        bool __Lt__(T other);
    }
}
