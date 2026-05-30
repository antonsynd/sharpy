using System;

namespace Sharpy
{
    [SharpyModuleType("zipfile")]
    public class BadZipFile : Exception
    {
        public BadZipFile(string message) : base(message) { }
    }

    [SharpyModuleType("zipfile")]
    public class LargeZipFile : Exception
    {
        public LargeZipFile(string message) : base(message) { }
    }
}
