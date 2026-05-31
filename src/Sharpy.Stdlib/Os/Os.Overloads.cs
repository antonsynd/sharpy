using System;
using System.Collections.Generic;
using System.IO;

namespace Sharpy
{
    /// <summary>Miscellaneous operating system interfaces.</summary>
    public static partial class OsModule
    {
        /// <summary>Get an environment variable, return <paramref name="default_"/> if it doesn't exist.</summary>
        public static string Getenv(string key, string default_)
        {
            return Environment.GetEnvironmentVariable(key) ?? default_;
        }

        /// <summary>A mapping object representing the string environment.</summary>
        public static Dict<string, string> Environ
        {
            get
            {
                var dict = new Dict<string, string>();
                foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
                {
                    if (entry.Key is string k && entry.Value is string v)
                    {
                        dict[k] = v;
                    }
                }
                return dict;
            }
        }

        /// <summary>Perform a stat system call on the given path.</summary>
        public static StatResult Stat(string path)
        {
            if (File.Exists(path))
            {
                var info = new FileInfo(path);
                return new StatResult(
                    stSize: info.Length,
                    stMtime: new DateTimeOffset(info.LastWriteTimeUtc).ToUnixTimeSeconds() + (double)new DateTimeOffset(info.LastWriteTimeUtc).Millisecond / 1000.0,
                    stCtime: new DateTimeOffset(info.CreationTimeUtc).ToUnixTimeSeconds() + (double)new DateTimeOffset(info.CreationTimeUtc).Millisecond / 1000.0,
                    stAtime: new DateTimeOffset(info.LastAccessTimeUtc).ToUnixTimeSeconds() + (double)new DateTimeOffset(info.LastAccessTimeUtc).Millisecond / 1000.0,
                    stMode: (int)info.Attributes
                );
            }
            if (Directory.Exists(path))
            {
                var info = new DirectoryInfo(path);
                return new StatResult(
                    stSize: 0,
                    stMtime: new DateTimeOffset(info.LastWriteTimeUtc).ToUnixTimeSeconds() + (double)new DateTimeOffset(info.LastWriteTimeUtc).Millisecond / 1000.0,
                    stCtime: new DateTimeOffset(info.CreationTimeUtc).ToUnixTimeSeconds() + (double)new DateTimeOffset(info.CreationTimeUtc).Millisecond / 1000.0,
                    stAtime: new DateTimeOffset(info.LastAccessTimeUtc).ToUnixTimeSeconds() + (double)new DateTimeOffset(info.LastAccessTimeUtc).Millisecond / 1000.0,
                    stMode: (int)info.Attributes
                );
            }
            throw new FileNotFoundError("No such file or directory: '" + path + "'");
        }

        /// <summary>Directory tree generator. For each directory in the tree rooted at top, yields a 3-tuple (dirpath, dirnames, filenames).</summary>
        public static IEnumerable<(string dirpath, System.Collections.Generic.List<string> dirnames, System.Collections.Generic.List<string> filenames)> Walk(string top)
        {
            if (!Directory.Exists(top))
            {
                yield break;
            }

            var dirnames = new System.Collections.Generic.List<string>();
            var filenames = new System.Collections.Generic.List<string>();

            foreach (var dir in Directory.GetDirectories(top))
            {
                dirnames.Add(System.IO.Path.GetFileName(dir));
            }
            foreach (var file in Directory.GetFiles(top))
            {
                filenames.Add(System.IO.Path.GetFileName(file));
            }

            yield return (top, dirnames, filenames);

            foreach (string dirname in dirnames)
            {
                var subdir = System.IO.Path.Combine(top, dirname);
                foreach (var entry in Walk(subdir))
                {
                    yield return entry;
                }
            }
        }
    }
}
