namespace Sharpy
{
    public static partial class ZlibModule
    {
        public static int MAX_WBITS => 15;
        public static int DEFLATED => 8;
        public static int DEF_MEM_LEVEL => 8;
        public static int DEF_BUF_SIZE => 16384;

        public static int Z_DEFAULT_COMPRESSION => -1;
        public static int Z_NO_COMPRESSION => 0;
        public static int Z_BEST_SPEED => 1;
        public static int Z_BEST_COMPRESSION => 9;

        public static int Z_DEFAULT_STRATEGY => 0;
        public static int Z_FILTERED => 1;
        public static int Z_HUFFMAN_ONLY => 2;
        public static int Z_RLE => 3;
        public static int Z_FIXED => 4;

        public static int Z_NO_FLUSH => 0;
        public static int Z_PARTIAL_FLUSH => 1;
        public static int Z_SYNC_FLUSH => 2;
        public static int Z_FULL_FLUSH => 3;
        public static int Z_FINISH => 4;
        public static int Z_BLOCK => 5;
        public static int Z_TREES => 6;
    }
}
