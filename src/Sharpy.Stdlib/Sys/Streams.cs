using System;
using System.IO;

namespace Sharpy
{
    public sealed partial class Sys
    {
        /// <summary>The standard output stream.</summary>
        public static TextWriter Stdout => Console.Out;

        /// <summary>The standard error stream.</summary>
        public static TextWriter Stderr => Console.Error;
    }
}
