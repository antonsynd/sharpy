namespace Sharpy
{
    /// <summary>Provides ZIP compression constants and helpers.</summary>
    public static partial class ZipfileModule
    {
        /// <summary>Stores entries without compression.</summary>
        public static int ZIP_STORED => 0;

        /// <summary>Compresses entries with the deflate method.</summary>
        public static int ZIP_DEFLATED => 8;
    }
}
