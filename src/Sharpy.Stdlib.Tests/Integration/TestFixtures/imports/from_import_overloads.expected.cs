#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace FromImportOverloads
{
    public static partial class FromImportOverloadsModule
    {
        public static void Main()
        {
            __Load();
#line (6, 5) - (6, 35) 12 "from_import_overloads.spy"
            string result1 = global::Sharpy.OsPathModule.OsPathModuleModule.Join("a", "b");
#line (7, 5) - (7, 40) 12 "from_import_overloads.spy"
            string result2 = global::Sharpy.OsPathModule.OsPathModuleModule.Join("a", "b", "c");
#line (8, 5) - (8, 45) 12 "from_import_overloads.spy"
            string result3 = global::Sharpy.OsPathModule.OsPathModuleModule.Join("a", "b", "c", "d");
#line (9, 5) - (9, 19) 12 "from_import_overloads.spy"
            global::Sharpy.Builtins.Print(result1);
#line (10, 5) - (10, 19) 12 "from_import_overloads.spy"
            global::Sharpy.Builtins.Print(result2);
#line (11, 5) - (11, 19) 12 "from_import_overloads.spy"
            global::Sharpy.Builtins.Print(result3);
#line hidden
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static global::Sharpy.StaticInitHost __host;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void __Sweep()
        {
            global::Sharpy.OsModule.OsModuleModule.__Load();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static class __Swept
        {
            internal static readonly bool Done;
            static __Swept()
            {
                Done = global::Sharpy.StaticInit.TryLoad(ref __host, __Sweep);
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        [global::Sharpy.SharpyLoadEntry(true)]
        public static void __Load()
        {
            if (!__Swept.Done)
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
        }
    }
}
#line default
