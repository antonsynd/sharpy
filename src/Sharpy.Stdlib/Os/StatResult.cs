namespace Sharpy
{
    /// <summary>
    /// Result of os.stat(), similar to Python's os.stat_result.
    /// </summary>
    public sealed class StatResult
    {
        /// <summary>Size in bytes (0 for directories).</summary>
        public long StSize { get; }

        /// <summary>Time of last modification (Unix timestamp).</summary>
        public double StMtime { get; }

        /// <summary>Time of creation (Unix timestamp).</summary>
        public double StCtime { get; }

        /// <summary>Time of last access (Unix timestamp).</summary>
        public double StAtime { get; }

        /// <summary>File mode / attributes.</summary>
        public int StMode { get; }

        /// <summary>Create a new stat result.</summary>
        public StatResult(long stSize, double stMtime, double stCtime, double stAtime, int stMode)
        {
            StSize = stSize;
            StMtime = stMtime;
            StCtime = stCtime;
            StAtime = stAtime;
            StMode = stMode;
        }
    }
}
