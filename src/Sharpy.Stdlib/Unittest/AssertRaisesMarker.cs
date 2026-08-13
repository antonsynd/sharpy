using System;

namespace Sharpy
{
    /// <summary>
    /// Marker type returned by unittest.assert_raises(). Implements IDisposable
    /// so the with-statement type checking passes. The compiler replaces the
    /// entire with-block during codegen with a try/catch guarded by a raised flag.
    /// </summary>
    [SharpyModuleType("unittest")]
    public sealed class AssertRaisesMarker : IDisposable
    {
        public void Dispose()
        {
            // Never called — compiler replaces the with-block
        }
    }
}
