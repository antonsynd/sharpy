namespace Sharpy
{
    public static partial class Builtins {
        public static bool Bool(Object obj) {
            return obj.__Bool__();
        }

        public static bool Bool(object x)
        {
            return (bool)x;
        }
    }
}
