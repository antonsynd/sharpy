namespace Sharpy
{
    /// <summary>Exposes zlib constants and helper functions.</summary>
    public static partial class ZlibModule
    {
        /// <summary>Gets the largest supported window size.</summary>
        public static int MAX_WBITS => 15;

        /// <summary>Gets the DEFLATE compression method identifier.</summary>
        public static int DEFLATED => 8;

        /// <summary>Gets the default memory level for compression.</summary>
        public static int DEF_MEM_LEVEL => 8;

        /// <summary>Gets the default buffer size used by zlib helpers.</summary>
        public static int DEF_BUF_SIZE => 16384;

        /// <summary>Gets the default compression level sentinel.</summary>
        public static int Z_DEFAULT_COMPRESSION => -1;

        /// <summary>Gets the constant for no compression.</summary>
        public static int Z_NO_COMPRESSION => 0;

        /// <summary>Gets the fastest compression level constant.</summary>
        public static int Z_BEST_SPEED => 1;

        /// <summary>Gets the best compression level constant.</summary>
        public static int Z_BEST_COMPRESSION => 9;

        /// <summary>Gets the default compression strategy constant.</summary>
        public static int Z_DEFAULT_STRATEGY => 0;

        /// <summary>Gets the filtered compression strategy constant.</summary>
        public static int Z_FILTERED => 1;

        /// <summary>Gets the Huffman-only compression strategy constant.</summary>
        public static int Z_HUFFMAN_ONLY => 2;

        /// <summary>Gets the run-length encoding strategy constant.</summary>
        public static int Z_RLE => 3;

        /// <summary>Gets the fixed-Huffman compression strategy constant.</summary>
        public static int Z_FIXED => 4;

        /// <summary>Gets the constant for no flush.</summary>
        public static int Z_NO_FLUSH => 0;

        /// <summary>Gets the constant for partial flush.</summary>
        public static int Z_PARTIAL_FLUSH => 1;

        /// <summary>Gets the constant for synchronous flush.</summary>
        public static int Z_SYNC_FLUSH => 2;

        /// <summary>Gets the constant for full flush.</summary>
        public static int Z_FULL_FLUSH => 3;

        /// <summary>Gets the constant for finishing a stream.</summary>
        public static int Z_FINISH => 4;

        /// <summary>Gets the constant for block flush mode.</summary>
        public static int Z_BLOCK => 5;

        /// <summary>Gets the constant for tree flush mode.</summary>
        public static int Z_TREES => 6;
    }
}
