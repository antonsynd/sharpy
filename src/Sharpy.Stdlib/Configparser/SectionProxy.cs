namespace Sharpy
{
    /// <summary>Provides mapping-style access to a single configparser section.</summary>
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

        /// <summary>Gets or sets an option value in the proxied section.</summary>
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

        /// <summary>Gets an option from the section, returning the fallback when it is missing.</summary>
        public string? Get(string key, string? fallback = null)
        {
            return _parser.Get(_section, key, fallback: fallback);
        }

        /// <summary>Determines whether the section contains an option.</summary>
        public bool ContainsKey(string key)
        {
            return _parser.HasOption(_section, key);
        }

        /// <summary>Returns the option names available in the section.</summary>
        public List<string> Keys()
        {
            return _parser.Options(_section);
        }

        /// <summary>Returns the section items with defaults merged in.</summary>
        public Dict<string, string> Items()
        {
            return _parser.Items(_section);
        }
    }
}
