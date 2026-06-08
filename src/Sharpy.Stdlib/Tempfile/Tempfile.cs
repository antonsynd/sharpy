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
        /// Return the filename prefix used to create temporary files.
        /// </summary>
        public static string Gettempprefix()
        {
            return "tmp";
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

        /// <summary>
        /// A temporary file with a visible name, deleted on close by default.
        /// </summary>
        public class NamedTemporaryFile
        {
            public string Name;
            public string Mode;
            public bool Delete;
            protected bool _Closed;
            /// <summary>
            /// Write a string to the file, returning the number of characters written.
            /// </summary>
            public int Write(string data)
            {
                global::System.IO.File.AppendAllText(this.Name, data);
                return data.Length;
            }

            /// <summary>
            /// Read the entire contents of the file.
            /// </summary>
            public string Read()
            {
                return global::System.IO.File.ReadAllText(this.Name);
            }

            /// <summary>
            /// Close the file, deleting it if delete is True.
            /// </summary>
            public void Close()
            {
                if (this._Closed)
                {
                    return;
                }

                this._Closed = true;
                if (this.Delete)
                {
                    if (global::System.IO.File.Exists(this.Name))
                    {
                        global::System.IO.File.Delete(this.Name);
                    }
                }
            }

            public NamedTemporaryFile Enter()
            {
                return this;
            }

            public void Exit()
            {
                this.Close();
            }

            /// <summary>
            /// Create a named temporary file in the default temporary directory.
            /// </summary>
            public NamedTemporaryFile(string mode = "w+b", string suffix = "", string prefix = "tmp", bool delete = true)
            {
                string randomPart = global::System.IO.Path.GetRandomFileName().Replace(".", "");
                this.Name = global::System.IO.Path.Combine(global::System.IO.Path.GetTempPath(), prefix + randomPart + suffix);
                global::System.IO.File.WriteAllText(this.Name, "");
                this.Mode = mode;
                this.Delete = delete;
                this._Closed = false;
            }
        }

        /// <summary>
        /// A temporary directory, recursively deleted on cleanup or context exit.
        /// </summary>
        public class TemporaryDirectory
        {
            public string Name;
            /// <summary>
            /// Recursively delete the temporary directory and its contents.
            /// </summary>
            public void Cleanup()
            {
                if (global::System.IO.Directory.Exists(this.Name))
                {
                    global::System.IO.Directory.Delete(this.Name, true);
                }
            }

            public string Enter()
            {
                return this.Name;
            }

            public void Exit()
            {
                this.Cleanup();
            }

            /// <summary>
            /// Create a temporary directory in the default temporary directory.
            /// </summary>
            public TemporaryDirectory(string suffix = "", string prefix = "tmp")
            {
                string randomPart = global::System.IO.Path.GetRandomFileName().Replace(".", "");
                this.Name = global::System.IO.Path.Combine(global::System.IO.Path.GetTempPath(), prefix + randomPart + suffix);
                global::System.IO.Directory.CreateDirectory(this.Name);
            }
        }

        /// <summary>
        /// A temporary file kept in memory until it exceeds max_size, then written to disk.
        /// </summary>
        public class SpooledTemporaryFile
        {
            public int MaxSize;
            public string Mode;
            public string? Name;
            protected string _Buffer;
            protected bool _Rolled;
            protected bool _Closed;
            /// <summary>
            /// Write the in-memory buffer to a real temporary file on disk.
            /// </summary>
            public void Rollover()
            {
                if (this._Rolled)
                {
                    return;
                }

                string randomPart = global::System.IO.Path.GetRandomFileName().Replace(".", "");
                string path = global::System.IO.Path.Combine(global::System.IO.Path.GetTempPath(), "tmp" + randomPart);
                global::System.IO.File.WriteAllText(path, this._Buffer);
                this.Name = path;
                this._Buffer = "";
                this._Rolled = true;
            }

            /// <summary>
            /// Write a string to the spooled file, rolling over to disk if max_size is exceeded.
            /// </summary>
            public int Write(string data)
            {
                if (this._Rolled)
                {
                    string? path = this.Name;
                    if (path != null)
                    {
                        global::System.IO.File.AppendAllText(path!, data);
                    }

                    return data.Length;
                }

                this._Buffer = this._Buffer + data;
                if (this.MaxSize > 0)
                {
                    if (this._Buffer.Length > this.MaxSize)
                    {
                        this.Rollover();
                    }
                }

                return data.Length;
            }

            /// <summary>
            /// Read the entire contents of the spooled file.
            /// </summary>
            public string Read()
            {
                if (this._Rolled)
                {
                    string? path = this.Name;
                    if (path != null)
                    {
                        return global::System.IO.File.ReadAllText(path!);
                    }

                    return "";
                }

                return this._Buffer;
            }

            /// <summary>
            /// Close the spooled file, deleting any on-disk file.
            /// </summary>
            public void Close()
            {
                if (this._Closed)
                {
                    return;
                }

                this._Closed = true;
                if (this._Rolled)
                {
                    string? path = this.Name;
                    if (path != null)
                    {
                        if (global::System.IO.File.Exists(path!))
                        {
                            global::System.IO.File.Delete(path!);
                        }
                    }
                }
            }

            public SpooledTemporaryFile Enter()
            {
                return this;
            }

            public void Exit()
            {
                this.Close();
            }

            /// <summary>
            /// Create a spooled temporary file that rolls over to disk when max_size is exceeded.
            /// </summary>
            public SpooledTemporaryFile(int maxSize = 0, string mode = "w+b")
            {
                this.MaxSize = maxSize;
                this.Mode = mode;
                this.Name = default;
                this._Buffer = "";
                this._Rolled = false;
                this._Closed = false;
            }
        }
    }
}
