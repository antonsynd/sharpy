#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.GenericClass0005
{
    public static class Program
    {
        public static void Main()
        {
#line 12 "generic_class_0005.spy"
            var intBox = new Box<int>(42);
#line 13 "generic_class_0005.spy"
            global::Sharpy.Core.Exports.Print(intBox.Get());
#line 15 "generic_class_0005.spy"
            var strBox = new Box<string>("hello");
#line 16 "generic_class_0005.spy"
            global::Sharpy.Core.Exports.Print(strBox.Get());
        }
    }

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
}
