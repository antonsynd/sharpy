namespace Sharpy
{
    public static partial class Builtins
    {
        public static T Next<T>(Iterator<T> iterator) where T : notnull
        {
            return iterator.__Next__();
        }
    }
}
