#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using os_path = global::Sharpy.OsPathModule.OsPathModuleModule;
using Xunit;

namespace TmpPathBasic
{
    public static partial class TmpPathBasicModule
    {
        public static void Main()
        {
#line (13, 5) - (13, 16) 12 "tmp_path_basic.spy"
            global::Sharpy.Builtins.Print("ok");
#line hidden
        }
    }

    public partial class TmpPathBasicModuleTests : global::System.IDisposable
    {
        private readonly global::Sharpy.TmpPathFixture _tmpPathFixture = new global::Sharpy.TmpPathFixture();
        [Xunit.FactAttribute]
        public void TestWritesFile()
        {
            string tmpPath = _tmpPathFixture.Value;
#line (7, 5) - (7, 54) 12 "tmp_path_basic.spy"
            string target = global::Sharpy.OsPathModule.OsPathModuleModule.Join(tmpPath, "data.txt");
#line (8, 5) - (9, 27) 12 "tmp_path_basic.spy"
            using (var f = global::Sharpy.Builtins.Open(target, "w"))
#line hidden
            {
#line (9, 9) - (9, 27) 16 "tmp_path_basic.spy"
                f.Write("content");
#line hidden
            }

#line (10, 5) - (10, 35) 12 "tmp_path_basic.spy"
            Xunit.Assert.True(global::Sharpy.OsPathModule.OsPathModuleModule.Exists(target));
#line hidden
        }

        public void Dispose()
        {
            _tmpPathFixture.Dispose();
        }
    }
}
#line default
