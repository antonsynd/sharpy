namespace Sharpy
{
    public static partial class Builtins
    {
        public static bool Bool(Object? obj)
        {
            return obj?.__Bool__() ?? false;
        }

        public static bool Bool(object? obj)
        {
            if (obj is null)
            {
                return false;
            }

            return (bool)obj;
        }
    }
}
