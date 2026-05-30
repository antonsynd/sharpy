namespace Sharpy
{
    public static partial class GzipModule
    {
        public static GzipFile Open(string filename, string mode = "rb", int compresslevel = 9)
        {
            if (mode == "rt" || mode == "wt" || mode == "at")
            {
                throw new ValueError("text mode not supported");
            }

            return new GzipFile(filename, mode, compresslevel);
        }
    }
}
