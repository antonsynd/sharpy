#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class DataclassDefaults
{
    public class Config
    {
        public string Name { get; set; }
        public bool Debug { get; set; } = false;
        public int Retries { get; set; } = 3;

        public Config(string name, bool debug = false, int retries = 3)
        {
            this.Name = name;
            this.Debug = debug;
            this.Retries = retries;
        }

        public override bool Equals(object? obj)
        {
            if (obj is not Config other)
                return false;
            return Equals(Name, other.Name) && Equals(Debug, other.Debug) && Equals(Retries, other.Retries);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Name, Debug, Retries);
        }

        public static bool operator ==(Config? left, Config? right) => Equals(left, right);
        public static bool operator !=(Config? left, Config? right) => !Equals(left, right);
        public override string ToString()
        {
            return $"Config(name={Name}, debug={Debug}, retries={Retries})";
        }
    }

    public static void Main()
    {
#line (8, 5) - (8, 23) 8 "dataclass_defaults.spy"
        var c1 = new global::DataclassDefaults.Config("app");
#line (9, 5) - (9, 14) 8 "dataclass_defaults.spy"
        global::Sharpy.Builtins.Print(c1);
#line (10, 5) - (10, 19) 8 "dataclass_defaults.spy"
        global::Sharpy.Builtins.Print(c1.Name);
#line (11, 5) - (11, 20) 8 "dataclass_defaults.spy"
        global::Sharpy.Builtins.Print(c1.Debug);
#line (12, 5) - (12, 22) 8 "dataclass_defaults.spy"
        global::Sharpy.Builtins.Print(c1.Retries);
#line (13, 5) - (13, 32) 8 "dataclass_defaults.spy"
        var c2 = new global::DataclassDefaults.Config("app", true, 5);
#line (14, 5) - (14, 14) 8 "dataclass_defaults.spy"
        global::Sharpy.Builtins.Print(c2);
#line hidden
    }
}
#line default
