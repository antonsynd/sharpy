namespace Sharpy
{
    public static partial class Builtins
    {
        public static string Repr(Object obj)
        {
            return obj.__Repr__();
        }

        public static string Repr<T>(T i) where T : struct
        {
            return i.ToString() ?? "";
        }
    }
}
