using System;
using System.IO;

namespace Sharpy
{
    /// <summary>
    /// Temporary file and directory creation, similar to Python's tempfile module.
    /// </summary>
    public static partial class Tempfile
    {
        /// <summary>
        /// Return the name of the directory used for temporary files.
        /// Similar to Python's <c>tempfile.gettempdir()</c>.
        /// </summary>
        /// <returns>The path to the system temporary directory, without a trailing separator.</returns>
        /// <example>
        /// <code>
        /// tempfile.gettempdir()    # "/tmp" on Unix, "C:\Users\...\Temp" on Windows
        /// </code>
        /// </example>
        public static string Gettempdir()
        {
            return System.IO.Path.GetTempPath().TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
        }

        /// <summary>
        /// Create a temporary directory and return its absolute pathname.
        /// Similar to Python's <c>tempfile.mkdtemp()</c>.
        /// </summary>
        /// <param name="prefix">Prefix for the directory name. Defaults to "tmp".</param>
        /// <returns>The absolute path of the created temporary directory.</returns>
        /// <exception cref="OSError">Thrown if the directory could not be created.</exception>
        /// <example>
        /// <code>
        /// tempfile.mkdtemp()           # "/tmp/tmpabcdefgh"
        /// tempfile.mkdtemp("myapp_")   # "/tmp/myapp_abcdefgh"
        /// </code>
        /// </example>
        public static string Mkdtemp(string prefix = "tmp")
        {
            try
            {
                string randomPart = System.IO.Path.GetRandomFileName().Replace(".", "");
                string dirName = prefix + randomPart;
                string fullPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), dirName);
                Directory.CreateDirectory(fullPath);
                return fullPath;
            }
            catch (Exception ex) when (!(ex is OSError))
            {
                throw new OSError("Failed to create temporary directory: " + ex.Message, ex);
            }
        }

        /// <summary>
        /// Create a temporary file and return a tuple of (fd, name).
        /// Similar to Python's <c>tempfile.mkstemp()</c>.
        /// Note: The file descriptor is always 0 as .NET does not use POSIX file descriptors.
        /// </summary>
        /// <param name="prefix">Prefix for the file name. Defaults to "tmp".</param>
        /// <param name="suffix">Suffix for the file name. Defaults to "".</param>
        /// <returns>A tuple of (0, absolute_path) where the file has been created.</returns>
        /// <exception cref="OSError">Thrown if the file could not be created.</exception>
        /// <example>
        /// <code>
        /// tempfile.mkstemp()                    # (0, "/tmp/tmpabcdefgh")
        /// tempfile.mkstemp("myapp_", ".dat")    # (0, "/tmp/myapp_abcdefgh.dat")
        /// </code>
        /// </example>
        public static (int, string) Mkstemp(string prefix = "tmp", string suffix = "")
        {
            try
            {
                string randomPart = System.IO.Path.GetRandomFileName().Replace(".", "");
                string fileName = prefix + randomPart + suffix;
                string fullPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), fileName);
                // Create the file (and immediately close it, matching Python's mkstemp behavior
                // where the caller is responsible for the file handle)
                using (File.Create(fullPath))
                {
                }
                return (0, fullPath);
            }
            catch (Exception ex) when (!(ex is OSError))
            {
                throw new OSError("Failed to create temporary file: " + ex.Message, ex);
            }
        }
    }
}
