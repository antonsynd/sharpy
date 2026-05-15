using System.Collections.Generic;

namespace Sharpy.Generators
{
    /// <summary>Read-only description of a decorator (or bracket attribute) applied to a declaration.</summary>
    public sealed class DecoratorInfo
    {
        public string Name { get; }
        public List<object> Arguments { get; }
        public Dictionary<string, object> KeywordArguments { get; }

        /// <summary>True if applied via <c>@[name]</c> bracket attribute syntax.</summary>
        public bool IsBracketAttribute { get; }

        public DecoratorInfo(
            string name,
            List<object> arguments,
            Dictionary<string, object> keywordArguments,
            bool isBracketAttribute)
        {
            Name = name;
            Arguments = arguments;
            KeywordArguments = keywordArguments;
            IsBracketAttribute = isBracketAttribute;
        }
    }
}
