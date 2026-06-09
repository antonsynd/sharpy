#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Xunit;

public static partial class TmpPathBasic
{
    public static void Main()
    {
#line (10, 5) - (10, 16) 1 "tmp_path_basic.spy"
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
#line (3, 5) - (3, 42) 1 "tmp_path_basic.spy"
        string target = tmpPath + "/data.txt";
#line (4, 5) - (6, 1) 1 "tmp_path_basic.spy"
        using (var f = global::Sharpy.Builtins.Open(target, "w"))
        {
#line (5, 9) - (5, 27) 1 "tmp_path_basic.spy"
            f.Write("content");
        }

#line (6, 5) - (9, 1) 1 "tmp_path_basic.spy"
        using (var g = global::Sharpy.Builtins.Open(target, "r"))
        {
#line (7, 9) - (7, 38) 1 "tmp_path_basic.spy"
            Xunit.Assert.Equal("content", g.Read());
        }
    }

    public void Dispose()
    {
        _tmpPathFixture.Dispose();
    }
}
