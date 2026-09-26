#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace DunderLen
{
    public static partial class DunderLenModule
    {
        public static void Main()
        {
#line (15, 5) - (15, 18) 12 "dunder_len.spy"
            var ml = new global::DunderLen.MyList();
#line (16, 5) - (16, 14) 12 "dunder_len.spy"
            ml.Add(1);
#line (17, 5) - (17, 14) 12 "dunder_len.spy"
            ml.Add(2);
#line (18, 5) - (18, 14) 12 "dunder_len.spy"
            ml.Add(3);
#line (19, 5) - (19, 19) 12 "dunder_len.spy"
            global::Sharpy.Builtins.Print(global::Sharpy.Builtins.Len(ml));
#line (20, 5) - (20, 21) 12 "dunder_len.spy"
            var empty = new global::DunderLen.MyList();
#line (21, 5) - (21, 22) 12 "dunder_len.spy"
            global::Sharpy.Builtins.Print(global::Sharpy.Builtins.Len(empty));
#line hidden
        }
    }

    [global::Sharpy.SharpyModuleType("__main__", "MyList")]
    public class MyList : Sharpy.ISized
    {
        public Sharpy.List<int> Items;
        public void Add(int item)
#line 8 "dunder_len.spy"
        {
#line (9, 9) - (9, 32) 12 "dunder_len.spy"
            this.Items.Append(item);
#line hidden
        }

        public virtual int Count
        {
            get
            {
#line (12, 9) - (12, 32) 16 "dunder_len.spy"
                return global::Sharpy.Builtins.Len(this.Items);
#line hidden
            }
        }

        public MyList()
#line 5 "dunder_len.spy"
        {
#line (6, 9) - (6, 24) 12 "dunder_len.spy"
            this.Items = new Sharpy.List<int>()
#line hidden
            {
            };
        }
    }
}
#line default
