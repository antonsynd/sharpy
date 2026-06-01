#nullable enable

namespace Sharpy
{
    public interface ITzinfo
    {
        Timedelta Utcoffset(DateTime? dt = null);
        string Tzname(DateTime? dt = null);
        Timedelta Dst(DateTime? dt = null);
    }
}
