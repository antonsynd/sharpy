#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Xunit;
using static TmpPathOverride;

public static partial class TmpPathOverride
{
    public static void Main()
    {
#line (13, 5) - (13, 16) 1 "tmp_path_override.spy"
        global::Sharpy.Builtins.Print("ok");
    }
}

public class TmpPathFixture
{
    public string Value { get; private set; } = default!;

    public TmpPathFixture()
    {
        Value = "/custom/override";
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
    public void TestUsesOverride()
    {
        string tmpPath = _tmpPathFixture.Value;
#line (10, 5) - (10, 43) 1 "tmp_path_override.spy"
        Xunit.Assert.Equal("/custom/override", tmpPath);
    }
}
