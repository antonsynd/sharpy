using System;
using System.Collections.Generic;
using System.IO;

namespace Sharpy
{
    /// <summary>
    /// Python-compatible configparser module.
    /// Provides ConfigParser for reading and writing INI-style configuration files.
    /// </summary>
    public static partial class Configparser
    {
        /// <summary>
        /// Create a new ConfigParser instance.
        /// </summary>
        /// <returns>A new <see cref="ConfigParserInstance"/>.</returns>
        public static ConfigParserInstance ConfigParser()
        {
            return new ConfigParserInstance();
        }

        /// <summary>
        /// Create a new ConfigParser instance with BasicInterpolation.
        /// </summary>
        /// <param name="interpolation">The interpolation handler to use.</param>
        /// <returns>A new <see cref="ConfigParserInstance"/>.</returns>
        public static ConfigParserInstance ConfigParser(ConfigInterpolation? interpolation)
        {
            return new ConfigParserInstance(interpolation);
        }
    }
}
