#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class DataclassDefaultListPerInstance1684
{
    public class Bag
    {
        public Sharpy.List<int> Xs { get; set; } = new Sharpy.List<int>()
        {
            1
        };

        public Bag(Sharpy.List<int>? xs = null)
        {
            this.Xs = xs ?? new Sharpy.List<int>()
            {
                1
            };
        }

        public override bool Equals(object? obj)
        {
            if (obj is not Bag other)
                return false;
            return Equals(Xs, other.Xs);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Xs);
        }

        public static bool operator ==(Bag? left, Bag? right) => Equals(left, right);
        public static bool operator !=(Bag? left, Bag? right) => !Equals(left, right);
        public override string ToString()
        {
            return $"Bag(xs={Xs})";
        }
    }

    public static void Main()
    {
#line (10, 5) - (10, 14) 8 "dataclass_default_list_per_instance_1684.spy"
        var a = new global::DataclassDefaultListPerInstance1684.Bag();
#line (11, 5) - (11, 14) 8 "dataclass_default_list_per_instance_1684.spy"
        var b = new global::DataclassDefaultListPerInstance1684.Bag();
#line (12, 5) - (12, 19) 8 "dataclass_default_list_per_instance_1684.spy"
        a.Xs.Append(2);
#line (13, 5) - (13, 16) 8 "dataclass_default_list_per_instance_1684.spy"
        global::Sharpy.Builtins.Print(a.Xs);
#line (14, 5) - (14, 16) 8 "dataclass_default_list_per_instance_1684.spy"
        global::Sharpy.Builtins.Print(b.Xs);
#line (15, 5) - (15, 20) 8 "dataclass_default_list_per_instance_1684.spy"
        var c = new global::DataclassDefaultListPerInstance1684.Bag(xs: new Sharpy.List<int>() { 9 });
#line (16, 5) - (16, 16) 8 "dataclass_default_list_per_instance_1684.spy"
        global::Sharpy.Builtins.Print(c.Xs);
#line hidden
    }
}
#line default
