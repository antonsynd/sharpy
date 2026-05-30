using SCG = System.Collections.Generic;

namespace Sharpy
{
    [SharpyModuleType("configparser")]
    public sealed class SectionProxy
    {
        private readonly ConfigParser _parser;
        private readonly string _section;

        internal SectionProxy(ConfigParser parser, string section)
        {
            _parser = parser;
            _section = section;
        }

        public string this[string key]
        {
            get
            {
                string? value = _parser.Get(_section, key);
                if (value == null)
                {
                    throw new NoOptionError(key, _section);
                }
                return value;
            }
            set
            {
                _parser.Set(_section, key, value);
            }
        }

        public string? Get(string key, string? fallback = null)
        {
            return _parser.Get(_section, key, fallback: fallback);
        }

        public bool ContainsKey(string key)
        {
            return _parser.HasOption(_section, key);
        }

        public SCG.List<string> Keys()
        {
            return _parser.Options(_section);
        }

        public SCG.Dictionary<string, string> Items()
        {
            return _parser.Items(_section);
        }
    }
}
