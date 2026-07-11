using Sharpy.Compiler.Shared;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Stdlib.Tests.Integration;

/// <summary>
/// End-to-end runtime coverage for the <c>@</c> matrix-multiplication operator (PEP 465,
/// #989) against numpy's <c>NdArray</c>. Exercises the CLR named-instance-method dispatch
/// path (<c>__matmul__</c> → <c>NdArray.MatMul</c>) through semantic analysis, code
/// generation, and execution.
/// </summary>
/// <remarks>
/// Since #1038 the programmatic harness drives the production project pipeline, so the
/// semantic feature gate runs here too — these tests enable the <c>matmul</c> feature
/// explicitly, exactly as a real consumer would. The gate's rejection path is covered
/// separately by <c>MatMulGatingTests</c> and <c>FeatureFlagsProjectTests</c>.
/// </remarks>
public class NumpyMatMulTests : StdlibIntegrationTestBase
{
    public NumpyMatMulTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void MatMul_TwoMatrices_ComputesMatrixProduct()
    {
        // [[1,2],[3,4]] @ [[5,6],[7,8]] = [[19,22],[43,50]]
        var result = CompileAndExecute(@"
import numpy as np

def main() -> None:
    a = np.array([[1.0, 2.0], [3.0, 4.0]])
    b = np.array([[5.0, 6.0], [7.0, 8.0]])
    c = a @ b
    print(c[0, 0])
    print(c[0, 1])
    print(c[1, 0])
    print(c[1, 1])
", features: FeatureFlags.None.Enable("matmul"));

        Assert.True(result.Success,
            "matrix multiplication should compile and run. Errors: "
            + string.Join("\n", result.CompilationErrors));
        Assert.Equal("19.0\n22.0\n43.0\n50.0\n", result.StandardOutput);
    }

    [Fact]
    public void MatMulAssign_InPlace_ComputesMatrixProduct()
    {
        // a @= b desugars to a = a.MatMul(b); same product as above.
        var result = CompileAndExecute(@"
import numpy as np

def main() -> None:
    a = np.array([[1.0, 2.0], [3.0, 4.0]])
    b = np.array([[5.0, 6.0], [7.0, 8.0]])
    a @= b
    print(a[0, 0])
    print(a[1, 1])
", features: FeatureFlags.None.Enable("matmul"));

        Assert.True(result.Success,
            "in-place matrix multiplication should compile and run. Errors: "
            + string.Join("\n", result.CompilationErrors));
        Assert.Equal("19.0\n50.0\n", result.StandardOutput);
    }
}
