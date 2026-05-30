namespace Sharpy
{
    [SharpyModuleType("gzip")]
    public class BadGzipFile : OSError
    {
        public BadGzipFile(string message) : base(message) { }
    }
}
