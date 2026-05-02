// Snapshot: Generic class with type parameter
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class GenericClass0005
{
    public class Box<T>
    {
        public T Item;
        public T Get()
#line 8 "generic_class_0005.spy"
        {
#line (9, 9) - (9, 26) 1 "generic_class_0005.spy"
            return this.Item;
        }

        public Box(T value)
#line 5 "generic_class_0005.spy"
        {
#line (6, 9) - (6, 26) 1 "generic_class_0005.spy"
            this.Item = value;
        }
    }

    public static void Main()
    {
#line (12, 5) - (12, 27) 1 "generic_class_0005.spy"
        var intBox = new Box<int>(42);
#line (13, 5) - (13, 25) 1 "generic_class_0005.spy"
        global::Sharpy.Builtins.Print(intBox.Get());
#line (15, 5) - (15, 32) 1 "generic_class_0005.spy"
        var strBox = new Box<string>("hello");
#line (16, 5) - (16, 25) 1 "generic_class_0005.spy"
        global::Sharpy.Builtins.Print(strBox.Get());
    }
}
