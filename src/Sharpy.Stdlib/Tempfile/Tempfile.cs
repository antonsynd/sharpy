// Generated from src/Sharpy.Stdlib/spy/tempfile_module.spy — do not edit directly.
// To regenerate: sharpyc emit csharp src/Sharpy.Stdlib/spy/tempfile_module.spy -t library -n Sharpy
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace Sharpy
{
    /// <summary>
    /// Generate temporary files and directories.
    /// </summary>
    public static partial class TempfileModule
    {
        /// <summary>
        /// Return the name of the directory used for temporary files.
        /// </summary>
        public static string Gettempdir()
        {
            string temp = global::System.IO.Path.GetTempPath();
            return temp.TrimEnd(global::System.IO.Path.DirectorySeparatorChar, global::System.IO.Path.AltDirectorySeparatorChar);
        }

        /// <summary>
        /// Create and return a unique temporary directory.
        /// </summary>
        public static string Mkdtemp(string prefix = "tmp")
        {
            try
            {
                string randomPart = global::System.IO.Path.GetRandomFileName().Replace(".", "");
                string dirName = prefix + randomPart;
                string fullPath = global::System.IO.Path.Combine(global::System.IO.Path.GetTempPath(), dirName);
                global::System.IO.Directory.CreateDirectory(fullPath);
                return fullPath;
            }
            catch (Exception ex)
            {
                throw new global::Sharpy.OSError("Failed to create temporary directory: " + ex.Message);
            }
        }

        /// <summary>
        /// Create and return a unique temporary file.
        /// </summary>
        public static global::System.ValueTuple<int, string> Mkstemp(string prefix = "tmp", string suffix = "")
        {
            try
            {
                string randomPart = global::System.IO.Path.GetRandomFileName().Replace(".", "");
                string fileName = prefix + randomPart + suffix;
                string fullPath = global::System.IO.Path.Combine(global::System.IO.Path.GetTempPath(), fileName);
                global::System.IO.File.WriteAllText(fullPath, "");
                return (0, fullPath);
            }
            catch (Exception ex)
            {
                throw new global::Sharpy.OSError("Failed to create temporary file: " + ex.Message);
            }
        }
    }
}
