namespace Sharpy
{
    public static partial class Builtins
    {
        public static string Str(Object obj)
        {
            return obj.__Str__();
        }

        public static string Str<T>(T x) where T : struct
        {
            return Repr(x);
        }
    }
}
