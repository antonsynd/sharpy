#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class BytesFromIterable
{
    public static void Main()
    {
#line (4, 5) - (4, 41) 1 "bytes_from_iterable.spy"
        Sharpy.Bytes data = new global::Sharpy.Bytes(new Sharpy.List<int>() { 104, 105, 33 });
#line (5, 5) - (5, 16) 1 "bytes_from_iterable.spy"
        global::Sharpy.Builtins.Print(data);
#line (6, 5) - (6, 21) 1 "bytes_from_iterable.spy"
        global::Sharpy.Builtins.Print(global::Sharpy.Builtins.Len(data));
#line (7, 5) - (7, 19) 1 "bytes_from_iterable.spy"
        global::Sharpy.Builtins.Print(data[0]);
#line (10, 5) - (10, 28) 1 "bytes_from_iterable.spy"
        Sharpy.Bytes empty = new global::Sharpy.Bytes();
#line (11, 5) - (11, 17) 1 "bytes_from_iterable.spy"
        global::Sharpy.Builtins.Print(empty);
#line (12, 5) - (12, 22) 1 "bytes_from_iterable.spy"
        global::Sharpy.Builtins.Print(global::Sharpy.Builtins.Len(empty));
#line (15, 5) - (15, 29) 1 "bytes_from_iterable.spy"
        Sharpy.Bytes zeros = new global::Sharpy.Bytes(4);
#line (16, 5) - (16, 17) 1 "bytes_from_iterable.spy"
        global::Sharpy.Builtins.Print(zeros);
#line (17, 5) - (17, 22) 1 "bytes_from_iterable.spy"
        global::Sharpy.Builtins.Print(global::Sharpy.Builtins.Len(zeros));
#line (20, 5) - (20, 36) 1 "bytes_from_iterable.spy"
        Sharpy.List<int> nums = new Sharpy.List<int>()
        {
            97,
            98,
            99
        };
#line (21, 5) - (21, 35) 1 "bytes_from_iterable.spy"
        Sharpy.Bytes fromVar = new global::Sharpy.Bytes(nums);
#line (22, 5) - (22, 20) 1 "bytes_from_iterable.spy"
        global::Sharpy.Builtins.Print(fromVar);
    }
}
