#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using static global::Sharpy.OsPathModule;

public static partial class FromImportOverloads
{
    public static void Main()
    {
#line (6, 5) - (6, 35) 1 "from_import_overloads.spy"
        string result1 = Join("a", "b");
#line (7, 5) - (7, 40) 1 "from_import_overloads.spy"
        string result2 = Join("a", "b", "c");
#line (8, 5) - (8, 45) 1 "from_import_overloads.spy"
        string result3 = Join("a", "b", "c", "d");
#line (9, 5) - (9, 19) 1 "from_import_overloads.spy"
        global::Sharpy.Builtins.Print(result1);
#line (10, 5) - (10, 19) 1 "from_import_overloads.spy"
        global::Sharpy.Builtins.Print(result2);
#line (11, 5) - (11, 19) 1 "from_import_overloads.spy"
        global::Sharpy.Builtins.Print(result3);
    }
}
