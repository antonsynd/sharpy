namespace Sharpy
{
    public sealed partial class Set<T>
    {
        public static bool operator ==(Set<T> left, Set<T> right)
        {
            return left.__Eq__(right);
        }

        public static bool operator !=(Set<T> left, Set<T> right)
        {
            return !(left == right);
        }

        public static Set<T> operator |(Set<T> left, Set<T> right)
        {
            throw new NotImplementedException();
        }
    }
}
