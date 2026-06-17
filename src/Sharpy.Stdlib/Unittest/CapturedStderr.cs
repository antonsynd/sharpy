namespace Sharpy
{
    /// <summary>
    /// A context manager that captures everything written to the standard error
    /// stream while it is active, mirroring Python's
    /// <c>contextlib.redirect_stderr(io.StringIO())</c> idiom used in unit tests.
    /// </summary>
    /// <remarks>
    /// On construction the current <see cref="System.Console.Error"/> writer is saved
    /// and replaced with an internal <see cref="System.IO.StringWriter"/>. Disposing
    /// restores the original writer. Because <see cref="System.Console.Error"/> is
    /// process-global, captures nest in LIFO order and must not be shared across
    /// tests that run in parallel.
    /// </remarks>
    public sealed class CapturedStderr : System.IDisposable
    {
        private readonly System.IO.TextWriter _original;
        private readonly System.IO.StringWriter _buffer;
        private bool _disposed;

        /// <summary>
        /// Redirect <see cref="System.Console.Error"/> to an internal buffer.
        /// </summary>
        public CapturedStderr()
        {
            this._original = System.Console.Error;
            this._buffer = new System.IO.StringWriter();
            System.Console.SetError(this._buffer);
        }

        /// <summary>
        /// Return the text captured so far, mirroring <c>io.StringIO.getvalue()</c>.
        /// </summary>
        public string Getvalue()
        {
            return this._buffer.ToString();
        }

        /// <summary>
        /// Restore the original error writer. Safe to call multiple times.
        /// </summary>
        public void Dispose()
        {
            if (this._disposed)
            {
                return;
            }

            this._disposed = true;
            System.Console.SetError(this._original);
            this._buffer.Dispose();
        }
    }
}
