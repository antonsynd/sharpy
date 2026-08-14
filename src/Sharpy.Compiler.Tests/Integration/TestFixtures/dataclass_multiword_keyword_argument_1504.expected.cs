#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class DataclassMultiwordKeywordArgument1504
{
    public class TwoWord
    {
        public string Name { get; set; }
        public int MaxConnections { get; set; }

        public TwoWord(string name, int maxConnections)
        {
            this.Name = name;
            this.MaxConnections = maxConnections;
        }

        public override bool Equals(object? obj)
        {
            if (obj is not TwoWord other)
                return false;
            return Equals(Name, other.Name) && Equals(MaxConnections, other.MaxConnections);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Name, MaxConnections);
        }

        public static bool operator ==(TwoWord? left, TwoWord? right) => Equals(left, right);
        public static bool operator !=(TwoWord? left, TwoWord? right) => !Equals(left, right);
        public override string ToString()
        {
            return $"TwoWord(name={Name}, max_connections={MaxConnections})";
        }
    }

    public static void Main()
    {
#line (16, 5) - (16, 58) 8 "dataclass_multiword_keyword_argument_1504.spy"
        TwoWord t = new TwoWord(name: "web", maxConnections: 10);
#line (17, 5) - (17, 18) 8 "dataclass_multiword_keyword_argument_1504.spy"
        global::Sharpy.Builtins.Print(t.Name);
#line (18, 5) - (18, 29) 8 "dataclass_multiword_keyword_argument_1504.spy"
        global::Sharpy.Builtins.Print(t.MaxConnections);
#line (19, 5) - (19, 36) 8 "dataclass_multiword_keyword_argument_1504.spy"
        TwoWord u = new TwoWord("db", 20);
#line (20, 5) - (20, 29) 8 "dataclass_multiword_keyword_argument_1504.spy"
        global::Sharpy.Builtins.Print(u.MaxConnections);
#line hidden
    }
}
#line default
