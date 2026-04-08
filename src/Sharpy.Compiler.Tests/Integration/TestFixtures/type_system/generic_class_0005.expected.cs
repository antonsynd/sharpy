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
        {
#line 9 "generic_class_0005.spy"
            return this.Item;
        }

        public Box(T value)
        {
#line 6 "generic_class_0005.spy"
            this.Item = value;
        }
    }

    public static void Main()
    {
#line 12 "generic_class_0005.spy"
        var intBox = new Box<int>(42);
#line 13 "generic_class_0005.spy"
        global::Sharpy.Builtins.Print(intBox.Get());
#line 15 "generic_class_0005.spy"
        var strBox = new Box<Sharpy.Str>(((Sharpy.Str)"hello"));
#line 16 "generic_class_0005.spy"
        global::Sharpy.Builtins.Print(strBox.Get());
    }
}
