using System.Diagnostics;

namespace Sharpy
{
    public static partial class Builtins
    {
        /// <summary>
        /// Drop into the debugger. No-op when no debugger is attached.
        /// </summary>
        /// <remarks>
        /// Maps to <see cref="System.Diagnostics.Debugger.Break()"/>.
        /// When no debugger is attached, this method does nothing.
        /// </remarks>
        public static void Breakpoint()
        {
            if (Debugger.IsAttached)
            {
                Debugger.Break();
            }
        }
    }
}
