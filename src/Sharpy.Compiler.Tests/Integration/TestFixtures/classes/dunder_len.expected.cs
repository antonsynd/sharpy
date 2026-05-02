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
#line 8 "dunder_len.spy"
        {
#line (9, 9) - (9, 32) 1 "dunder_len.spy"
            this.Items.Append(item);
        }

        public virtual int Count
        {
            get
            {
#line (12, 9) - (12, 32) 1 "dunder_len.spy"
                return global::Sharpy.Builtins.Len(this.Items);
            }
        }

        public MyList()
#line 5 "dunder_len.spy"
        {
#line (6, 9) - (6, 24) 1 "dunder_len.spy"
            this.Items = new Sharpy.List<int>()
            {
            };
        }
    }

    public static void Main()
    {
#line (15, 5) - (15, 18) 1 "dunder_len.spy"
        var ml = new MyList();
#line (16, 5) - (16, 14) 1 "dunder_len.spy"
        ml.Add(1);
#line (17, 5) - (17, 14) 1 "dunder_len.spy"
        ml.Add(2);
#line (18, 5) - (18, 14) 1 "dunder_len.spy"
        ml.Add(3);
#line (19, 5) - (19, 19) 1 "dunder_len.spy"
        global::Sharpy.Builtins.Print(global::Sharpy.Builtins.Len(ml));
#line (20, 5) - (20, 21) 1 "dunder_len.spy"
        var empty = new MyList();
#line (21, 5) - (21, 22) 1 "dunder_len.spy"
        global::Sharpy.Builtins.Print(global::Sharpy.Builtins.Len(empty));
    }
}
