#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class DunderLen
{
    public class MyList : Sharpy.ISized
    {
        public Sharpy.List<int> Items;
        public void Add(int item)
        {
#line 9 "dunder_len.spy"
            this.Items.Append(item);
        }

        public int Count
        {
            get
            {
#line 12 "dunder_len.spy"
                return global::Sharpy.Builtins.Len(this.Items);
            }
        }

        public MyList()
        {
#line 6 "dunder_len.spy"
            this.Items = new Sharpy.List<int>()
            {
            };
        }
    }

    public static void Main()
    {
#line 15 "dunder_len.spy"
        var ml = new MyList();
#line 16 "dunder_len.spy"
        ml.Add(1);
#line 17 "dunder_len.spy"
        ml.Add(2);
#line 18 "dunder_len.spy"
        ml.Add(3);
#line 19 "dunder_len.spy"
        global::Sharpy.Builtins.Print(global::Sharpy.Builtins.Len(ml));
#line 20 "dunder_len.spy"
        var empty = new MyList();
#line 21 "dunder_len.spy"
        global::Sharpy.Builtins.Print(global::Sharpy.Builtins.Len(empty));
    }
}
