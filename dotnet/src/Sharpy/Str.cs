namespace Sharpy
{
    public static partial class Builtins
    {
        /// <remarks>
        /// <see cref="Object.ToString"/> calls <see cref="Object.__Str__"/>
        /// so this implementation covers all native C# objects and Sharpy
        /// objects.
        /// </remarks>
        public static string Str(object x)
        {
            return x.ToString() ?? "";
        }
    }
}
