namespace Sharpy
{
    public static partial class __Exports
    {
        public static Optional<T> None<T>() where T : notnull
        {
            return new Optional<T>();
        }
    }
}
