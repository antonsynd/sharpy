namespace Sharpy
{
    public static partial class Builtins
    {
        public static string Str(Object obj)
        {
            return obj.__Str__();
        }
    }
}
