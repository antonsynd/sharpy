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

    }
}
