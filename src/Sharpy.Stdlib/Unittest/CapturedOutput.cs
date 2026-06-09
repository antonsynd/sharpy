namespace Sharpy
{
    /// <summary>
    /// A context manager that captures everything written to the console while it
    /// is active, mirroring Python's <c>contextlib.redirect_stdout(io.StringIO())</c>
    /// idiom used in unit tests.
    /// </summary>
    /// <remarks>
    /// On construction the current <see cref="System.Console.Out"/> writer is saved
    /// and replaced with an internal <see cref="System.IO.StringWriter"/>. Disposing
    /// restores the original writer. Because <see cref="System.Console.Out"/> is
    /// process-global, captures nest in LIFO order and must not be shared across
    /// tests that run in parallel.
    /// </remarks>
    public sealed class CapturedOutput : System.IDisposable
    {
        private readonly System.IO.TextWriter _original;
        private readonly System.IO.StringWriter _buffer;
        private bool _disposed;

        /// <summary>
        /// Redirect <see cref="System.Console.Out"/> to an internal buffer.
        /// </summary>
        public CapturedOutput()
        {
            this._original = System.Console.Out;
            this._buffer = new System.IO.StringWriter();
            System.Console.SetOut(this._buffer);
        }

        /// <summary>
        /// Return the text captured so far, mirroring <c>io.StringIO.getvalue()</c>.
        /// </summary>
        public string Getvalue()
        {
            return this._buffer.ToString();
        }

        /// <summary>
        /// Restore the original console writer. Safe to call multiple times.
        /// </summary>
        public void Dispose()
        {
            if (this._disposed)
            {
                return;
            }

            this._disposed = true;
            System.Console.SetOut(this._original);
            this._buffer.Dispose();
        }
    }
}
