namespace Sharpy
{
    /// <summary>Miscellaneous operating system interfaces.</summary>
    public static partial class OsModule
    {
        /// <summary>A mapping object representing the string environment.</summary>
        public static Dict<string, string> Environ => GetEnviron();
    }
}
