// Generated from src/Sharpy.Stdlib/spy/os_module.spy — do not edit directly.
// To regenerate: sharpyc emit csharp src/Sharpy.Stdlib/spy/os_module.spy -t library -n Sharpy
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace Sharpy.OsModule
{
    /// <summary>
    /// Miscellaneous operating system interfaces.
    /// </summary>
    public static partial class OsModuleModule
    {
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static string Sep__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte Sep__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void Sep__init()
        {
            Sep__field = global::Sharpy.Builtins.Str(global::System.IO.Path.DirectorySeparatorChar.ToString());
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void Sep__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref Sep__state, "sep", Sep__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref string Sep
        {
            get
            {
                if (!__Swept.Done)
                    Sep__slow();
                return ref Sep__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void Sep__ensure()
        {
            if (!__Swept.Done)
                Sep__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static string Sep__set(string value)
        {
            if (__Swept.Done)
                Sep__field = value;
            else
                global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref Sep__state, ref Sep__field, value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<string>? Sep__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref Sep__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<string>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref string Sep__out(out global::System.Runtime.CompilerServices.StrongBox<string>? spill)
        {
            spill = Sep__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref Sep__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R Sep__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<string>? spill)
        {
            if (spill != null)
                Sep__set(spill.Value!);
            return result;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static string Linesep__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte Linesep__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void Linesep__init()
        {
            Linesep__field = global::System.Environment.NewLine;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void Linesep__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref Linesep__state, "linesep", Linesep__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref string Linesep
        {
            get
            {
                if (!__Swept.Done)
                    Linesep__slow();
                return ref Linesep__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void Linesep__ensure()
        {
            if (!__Swept.Done)
                Linesep__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static string Linesep__set(string value)
        {
            if (__Swept.Done)
                Linesep__field = value;
            else
                global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref Linesep__state, ref Linesep__field, value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<string>? Linesep__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref Linesep__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<string>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref string Linesep__out(out global::System.Runtime.CompilerServices.StrongBox<string>? spill)
        {
            spill = Linesep__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref Linesep__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R Linesep__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<string>? spill)
        {
            if (spill != null)
                Linesep__set(spill.Value!);
            return result;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static string Name__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte Name__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void Name__init()
        {
            Name__field = global::Sharpy.OsModule.OsModuleModule.Sep == "\\" ? "nt" : "posix";
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void Name__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref Name__state, "name", Name__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref string Name
        {
            get
            {
                if (!__Swept.Done)
                    Name__slow();
                return ref Name__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void Name__ensure()
        {
            if (!__Swept.Done)
                Name__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static string Name__set(string value)
        {
            if (__Swept.Done)
                Name__field = value;
            else
                global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref Name__state, ref Name__field, value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<string>? Name__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref Name__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<string>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref string Name__out(out global::System.Runtime.CompilerServices.StrongBox<string>? spill)
        {
            spill = Name__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref Name__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R Name__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<string>? spill)
        {
            if (spill != null)
                Name__set(spill.Value!);
            return result;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static string Pathsep__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte Pathsep__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void Pathsep__init()
        {
            Pathsep__field = global::Sharpy.Builtins.Str(global::System.IO.Path.PathSeparator.ToString());
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void Pathsep__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref Pathsep__state, "pathsep", Pathsep__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref string Pathsep
        {
            get
            {
                if (!__Swept.Done)
                    Pathsep__slow();
                return ref Pathsep__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void Pathsep__ensure()
        {
            if (!__Swept.Done)
                Pathsep__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static string Pathsep__set(string value)
        {
            if (__Swept.Done)
                Pathsep__field = value;
            else
                global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref Pathsep__state, ref Pathsep__field, value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<string>? Pathsep__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref Pathsep__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<string>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref string Pathsep__out(out global::System.Runtime.CompilerServices.StrongBox<string>? spill)
        {
            spill = Pathsep__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref Pathsep__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R Pathsep__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<string>? spill)
        {
            if (spill != null)
                Pathsep__set(spill.Value!);
            return result;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static string Altsep__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte Altsep__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void Altsep__init()
        {
            Altsep__field = global::System.IO.Path.AltDirectorySeparatorChar.ToString() == global::System.IO.Path.DirectorySeparatorChar.ToString() ? "" : global::Sharpy.Builtins.Str(global::System.IO.Path.AltDirectorySeparatorChar.ToString());
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void Altsep__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref Altsep__state, "altsep", Altsep__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref string Altsep
        {
            get
            {
                if (!__Swept.Done)
                    Altsep__slow();
                return ref Altsep__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void Altsep__ensure()
        {
            if (!__Swept.Done)
                Altsep__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static string Altsep__set(string value)
        {
            if (__Swept.Done)
                Altsep__field = value;
            else
                global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref Altsep__state, ref Altsep__field, value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<string>? Altsep__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref Altsep__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<string>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref string Altsep__out(out global::System.Runtime.CompilerServices.StrongBox<string>? spill)
        {
            spill = Altsep__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref Altsep__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R Altsep__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<string>? spill)
        {
            if (spill != null)
                Altsep__set(spill.Value!);
            return result;
        }

        /// <summary>
        /// Remove a file (same as unlink).
        /// </summary>
        public static void Remove(string path)
        {
            global::Sharpy.PathErrors.CheckRemovesFile(path);
            try
            {
                global::System.IO.File.Delete(path);
            }
            catch (global::System.Exception e)
            {
                throw global::Sharpy.PathErrors.TranslateRemovesFile(e, path);
            }
        }

        /// <summary>
        /// Rename a file or directory.
        /// </summary>
        public static void Rename(string src, string dst)
        {
            global::Sharpy.PathErrors.Rename(src, dst);
        }

        /// <summary>
        /// Create a directory.
        /// </summary>
        public static void Mkdir(string path)
        {
            global::Sharpy.PathErrors.CheckCreatesDir(path);
            try
            {
                global::System.IO.Directory.CreateDirectory(path);
            }
            catch (global::System.Exception e)
            {
                throw global::Sharpy.PathErrors.TranslateCreatesDir(e, path);
            }
        }

        /// <summary>
        /// Super-mkdir; create a leaf directory and all intermediate ones.
        /// </summary>
        public static void Makedirs(string path, bool existOk = false)
        {
            string name = global::Sharpy.OsModule.OsModuleModule._StripTrailingSeparators(path);
            string? head = global::System.IO.Path.GetDirectoryName(name);
            if (head is not null)
            {
                if (head!.Length > 0 && !global::Sharpy.OsModule.OsModuleModule.PathExists(head!))
                {
                    global::Sharpy.OsModule.OsModuleModule.Makedirs(head!, existOk);
                    if (global::System.IO.Path.GetFileName(name) == ".")
                    {
                        return;
                    }
                }
            }

            if (existOk && global::System.IO.Directory.Exists(name))
            {
                return;
            }

            global::Sharpy.OsModule.OsModuleModule.Mkdir(name);
        }

        /// <summary>
        /// Remove a directory.
        /// </summary>
        public static void Rmdir(string path)
        {
            global::Sharpy.PathErrors.CheckRemovesDir(path);
            try
            {
                global::System.IO.Directory.Delete(path, false);
            }
            catch (global::System.Exception e)
            {
                throw global::Sharpy.PathErrors.TranslateRemovesDir(e, path);
            }
        }

        /// <summary>
        /// Return a list containing the names of the entries in the directory.
        /// </summary>
        public static Sharpy.List<string> Listdir(string path = ".")
        {
            Sharpy.List<string> entries = new Sharpy.List<string>()
            {
            };
            try
            {
                foreach (var __loopVar_0 in global::System.IO.Directory.GetFileSystemEntries(path))
                {
                    var entry = __loopVar_0;
                    entries.Append(entry);
                }
            }
            catch (global::System.Exception e)
            {
                throw global::Sharpy.PathErrors.TranslateNeedsDir(e, path);
            }

            Sharpy.List<string> result = new Sharpy.List<string>()
            {
            };
            foreach (var __loopVar_1 in entries)
            {
                var full = __loopVar_1;
                result.Append(global::System.IO.Path.GetFileName(full));
            }

            return result;
        }

        /// <summary>
        /// Return a string representing the current working directory.
        /// </summary>
        public static string Getcwd()
        {
            return global::System.IO.Directory.GetCurrentDirectory();
        }

        /// <summary>
        /// Change the current working directory to the specified path.
        /// </summary>
        public static void Chdir(string path)
        {
            try
            {
                global::System.IO.Directory.SetCurrentDirectory(path);
            }
            catch (global::System.Exception e)
            {
                throw global::Sharpy.PathErrors.TranslateNeedsDir(e, path);
            }
        }

        /// <summary>
        /// Get an environment variable, return None if it doesn't exist.
        /// </summary>
        public static string? Getenv(string key)
        {
            var result = global::System.Environment.GetEnvironmentVariable(key);
            if (result is null)
            {
                return null;
            }

            return result!;
        }

        /// <summary>
        /// Get an environment variable, return default if it doesn't exist.
        /// </summary>
        public static string Getenv(string key, string @default)
        {
            var result = global::System.Environment.GetEnvironmentVariable(key);
            if (result is null)
            {
                return @default;
            }

            return result!;
        }

        /// <summary>
        /// Change or add an environment variable.
        /// </summary>
        public static void Putenv(string key, string value)
        {
            global::System.Environment.SetEnvironmentVariable(key, value);
        }

        /// <summary>
        /// Return a mapping object representing the string environment.
        /// </summary>
        public static Sharpy.Dict<string, string> GetEnviron()
        {
            Sharpy.Dict<string, string> result = new Sharpy.Dict<string, string>()
            {
            };
            foreach (var __loopVar_2 in global::System.Environment.GetEnvironmentVariables())
            {
                var entry = __loopVar_2;
                if (entry is global::System.Collections.DictionaryEntry)
                {
                    var k = ((global::System.Collections.DictionaryEntry)entry!).Key;
                    var v = ((global::System.Collections.DictionaryEntry)entry!).Value;
                    if ((object?)k is string && v is string)
                    {
                        result[((string)k!)] = ((string)v!);
                    }
                }
            }

            return result;
        }

        public static Sharpy.Dict<string, string> Environ
        {
            get
            {
                _ = "A mapping object representing the string environment.";
                return global::Sharpy.OsModule.OsModuleModule.GetEnviron();
            }
        }

        /// <summary>
        /// Test whether a path exists.
        /// </summary>
        public static bool PathExists(string path)
        {
            return global::System.IO.File.Exists(path) || global::System.IO.Directory.Exists(path);
        }

        /// <summary>
        /// The path without trailing separators (python's path.split skips an empty tail); a root stays itself.
        /// </summary>
        internal static string _StripTrailingSeparators(string path)
        {
            string stripped = path.TrimEnd(global::System.IO.Path.DirectorySeparatorChar, global::System.IO.Path.AltDirectorySeparatorChar);
            if (stripped.Length == 0)
            {
                return path;
            }

            return stripped;
        }

        /// <summary>
        /// Perform the equivalent of a stat() system call on the given path.
        /// </summary>
        public static global::Sharpy.OsModule.StatResult Stat(string path)
        {
            global::Sharpy.PathErrors.CheckNeedsAny(path);
            if (global::System.IO.File.Exists(path))
            {
                global::System.IO.FileInfo finfo = new global::System.IO.FileInfo(path);
                global::System.DateTimeOffset fw = new global::System.DateTimeOffset(finfo.LastWriteTimeUtc);
                global::System.DateTimeOffset fc = new global::System.DateTimeOffset(finfo.CreationTimeUtc);
                global::System.DateTimeOffset fa = new global::System.DateTimeOffset(finfo.LastAccessTimeUtc);
                return new global::Sharpy.OsModule.StatResult(finfo.Length, fw.ToUnixTimeSeconds() + fw.Millisecond / 1000.0d, fc.ToUnixTimeSeconds() + fc.Millisecond / 1000.0d, fa.ToUnixTimeSeconds() + fa.Millisecond / 1000.0d, global::System.Convert.ToInt32(finfo.Attributes));
            }

            global::System.IO.DirectoryInfo dinfo = new global::System.IO.DirectoryInfo(path);
            global::System.DateTimeOffset dw = new global::System.DateTimeOffset(dinfo.LastWriteTimeUtc);
            global::System.DateTimeOffset dc = new global::System.DateTimeOffset(dinfo.CreationTimeUtc);
            global::System.DateTimeOffset da = new global::System.DateTimeOffset(dinfo.LastAccessTimeUtc);
            return new global::Sharpy.OsModule.StatResult(0, dw.ToUnixTimeSeconds() + dw.Millisecond / 1000.0d, dc.ToUnixTimeSeconds() + dc.Millisecond / 1000.0d, da.ToUnixTimeSeconds() + da.Millisecond / 1000.0d, global::System.Convert.ToInt32(dinfo.Attributes));
        }

        [global::Sharpy.SharpyGenerator("_walk_core")]
        internal static System.Collections.Generic.IEnumerable<global::System.ValueTuple<string, Sharpy.List<string>, Sharpy.List<string>>> _WalkCore(string top)
        {
            if (!global::System.IO.Directory.Exists(top))
            {
                yield break;
            }

            Sharpy.List<string> pending = new Sharpy.List<string>()
            {
                top
            };
            while (global::Sharpy.Builtins.Len(pending) > 0)
            {
                string current = pending.Pop();
                Sharpy.List<string> dirnames = new Sharpy.List<string>()
                {
                };
                Sharpy.List<string> filenames = new Sharpy.List<string>()
                {
                };
                foreach (var __loopVar_3 in global::System.IO.Directory.GetDirectories(current))
                {
                    var dirEntry = __loopVar_3;
                    dirnames.Append(global::System.IO.Path.GetFileName(dirEntry));
                }

                foreach (var __loopVar_4 in global::System.IO.Directory.GetFiles(current))
                {
                    var fileEntry = __loopVar_4;
                    filenames.Append(global::System.IO.Path.GetFileName(fileEntry));
                }

                yield return (current, dirnames, filenames);
                int i = global::Sharpy.Builtins.Len(dirnames) - 1;
                while (i >= 0)
                {
                    pending.Append(global::System.IO.Path.Combine(current, dirnames[i]));
                    i = i - 1;
                }
            }
        }

        /// <summary>
        /// Directory tree generator yielding (dirpath, dirnames, filenames) for each directory in the tree rooted at top.
        /// </summary>
        public static global::Sharpy.Iterator<global::System.ValueTuple<string, Sharpy.List<string>, Sharpy.List<string>>> Walk(string top)
        {
            return global::Sharpy.Builtins.Iter<global::System.ValueTuple<string, Sharpy.List<string>, Sharpy.List<string>>>(global::Sharpy.OsModule.OsModuleModule._WalkCore(top));
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static global::Sharpy.StaticInitHost __host;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void __Sweep()
        {
            global::Sharpy.StaticInit.Run(ref Sep__state, "sep", Sep__init);
            global::Sharpy.StaticInit.Run(ref Linesep__state, "linesep", Linesep__init);
            global::Sharpy.StaticInit.Run(ref Name__state, "name", Name__init);
            global::Sharpy.StaticInit.Run(ref Pathsep__state, "pathsep", Pathsep__init);
            global::Sharpy.StaticInit.Run(ref Altsep__state, "altsep", Altsep__init);
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static class __Swept
        {
            internal static readonly bool Done;
            static __Swept()
            {
                Done = global::Sharpy.StaticInit.TryLoad(ref __host, __Sweep);
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        [global::Sharpy.SharpyLoadEntry(true)]
        public static void __Load()
        {
            if (!__Swept.Done)
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
        }
    }

    /// <summary>
    /// Result of os.stat(), similar to Python's os.stat_result.
    /// </summary>
    [global::Sharpy.SharpyModuleType("os", "StatResult")]
    public class StatResult
    {
        public long StSize { get; }
        public double StMtime { get; }
        public double StCtime { get; }
        public double StAtime { get; }
        public int StMode { get; }

        /// <summary>
        /// Create a new stat result.
        /// </summary>
        public StatResult(long stSize, double stMtime, double stCtime, double stAtime, int stMode)
        {
            this.StSize = stSize;
            this.StMtime = stMtime;
            this.StCtime = stCtime;
            this.StAtime = stAtime;
            this.StMode = stMode;
        }
    }
}
