#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Xunit;

public static partial class TmpPathOverride
{
    public static void Main()
    {
#line (10, 5) - (10, 16) 1 "tmp_path_override.spy"
        global::Sharpy.Builtins.Print("ok");
    }
}

public class TmpPathFixture
{
    public string Value { get; private set; } = default!;

    public TmpPathFixture()
    {
        Value = "/custom/path";
    }
}

public partial class TmpPathOverrideTests : Xunit.IClassFixture<TmpPathFixture>
{
    private readonly TmpPathFixture _tmpPathFixture;
    public TmpPathOverrideTests(TmpPathFixture tmpPathFixture)
    {
        _tmpPathFixture = tmpPathFixture;
    }

    [Xunit.FactAttribute]
    public void TestOverride()
    {
        string tmpPath = _tmpPathFixture.Value;
#line (7, 5) - (7, 39) 1 "tmp_path_override.spy"
        Xunit.Assert.Equal("/custom/path", tmpPath);
    }
}
