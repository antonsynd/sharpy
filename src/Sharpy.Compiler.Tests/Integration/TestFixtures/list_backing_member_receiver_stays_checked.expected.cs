#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class ListBackingMemberReceiverStaysChecked
{
    public class Bag
    {
        public Sharpy.List<int> Items;
        public int First()
#line 10 "list_backing_member_receiver_stays_checked.spy"
        {
#line (11, 9) - (11, 30) 12 "list_backing_member_receiver_stays_checked.spy"
            return this.Items[0];
#line hidden
        }

        public Bag()
#line 7 "list_backing_member_receiver_stays_checked.spy"
        {
#line (8, 9) - (8, 31) 12 "list_backing_member_receiver_stays_checked.spy"
            this.Items = new Sharpy.List<int>()
#line hidden
            {
                1,
                2,
                3
            };
        }
    }

    public static void Main()
    {
#line (14, 5) - (14, 20) 8 "list_backing_member_receiver_stays_checked.spy"
        Bag b = new Bag();
#line (15, 5) - (15, 21) 8 "list_backing_member_receiver_stays_checked.spy"
        global::Sharpy.Builtins.Print(b.First());
#line hidden
    }
}
#line default
