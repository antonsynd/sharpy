using System;

namespace Sharpy
{
    [SharpyModuleType("json")]
    public class JSONDecoder
    {
        private readonly Func<Dict<string, object?>, object?>? _objectHook;

        public JSONDecoder(Func<Dict<string, object?>, object?>? objectHook = null)
        {
            _objectHook = objectHook;
        }

        public virtual object? Decode(string s)
        {
            return JsonParser.Parse(s, _objectHook);
        }

        public virtual (object?, int) RawDecode(string s, int idx = 0)
        {
            var result = JsonParser.Parse(s.Substring(idx));
            return (result, s.Length);
        }
    }
}
