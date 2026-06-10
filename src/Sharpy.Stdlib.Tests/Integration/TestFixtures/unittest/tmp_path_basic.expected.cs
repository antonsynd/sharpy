#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using os_path = global::Sharpy.OsPathModule;
using Xunit;
using static TmpPathBasic;

public static partial class TmpPathBasic
{
    public static void Main()
    {
#line (13, 5) - (13, 16) 1 "tmp_path_basic.spy"
        global::Sharpy.Builtins.Print("ok");
    }
}

public partial class TmpPathBasicTests : global::System.IDisposable
{
    private readonly global::Sharpy.TmpPathFixture _tmpPathFixture = new global::Sharpy.TmpPathFixture();
    [Xunit.FactAttribute]
    public void TestWritesFile()
    {
        string tmpPath = _tmpPathFixture.Value;
#line (7, 5) - (7, 54) 1 "tmp_path_basic.spy"
        string target = os_path.Join(tmpPath, "data.txt");
#line (8, 5) - (10, 1) 1 "tmp_path_basic.spy"
        using (var f = global::Sharpy.Builtins.Open(target, "w"))
        {
#line (9, 9) - (9, 27) 1 "tmp_path_basic.spy"
            f.Write("content");
        }

#line (10, 5) - (10, 35) 1 "tmp_path_basic.spy"
        Xunit.Assert.True(os_path.Exists(target));
    }

    public void Dispose()
    {
        _tmpPathFixture.Dispose();
    }
}
