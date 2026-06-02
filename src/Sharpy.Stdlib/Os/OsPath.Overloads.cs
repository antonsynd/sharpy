using System;
using System.IO;

namespace Sharpy
{
    /// <summary>Common pathname manipulations.</summary>
    public static partial class OsPathModule
    {
        /// <summary>Join three pathname components, inserting '/' as needed.</summary>
        public static string Join(string a, string b, string c)
        {
            return System.IO.Path.Combine(a, b, c);
        }

        /// <summary>Join four pathname components, inserting '/' as needed.</summary>
        public static string Join(string a, string b, string c, string d)
        {
            return System.IO.Path.Combine(a, b, c, d);
        }

    }
}
