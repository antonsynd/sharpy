#nullable enable

namespace Sharpy
{
    [SharpyModuleType("zoneinfo", "ZoneInfoNotFoundError")]
    public class ZoneInfoNotFoundError : KeyError
    {
        public ZoneInfoNotFoundError(string message) : base(message)
        {
        }
    }
}
