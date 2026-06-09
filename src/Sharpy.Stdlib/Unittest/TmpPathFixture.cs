namespace Sharpy
{
    /// <summary>
    /// A per-test temporary directory fixture, modelled on pytest's <c>tmp_path</c>.
    /// </summary>
    /// <remarks>
    /// The compiler recognizes a module-level <c>@test</c> function parameter named
    /// <c>tmp_path</c> (when no user fixture of that name exists) and injects an instance
    /// of this type per test method. A fresh directory is created on construction and
    /// best-effort recursively deleted on <see cref="Dispose"/>; cleanup failures are
    /// swallowed so they cannot fail an otherwise-passing test.
    /// </remarks>
    public sealed class TmpPathFixture : System.IDisposable
    {
        private bool _disposed;

        /// <summary>
        /// The absolute path to this fixture's unique temporary directory.
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// Create a unique temporary directory under the system temp path.
        /// </summary>
        public TmpPathFixture()
        {
            string path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "sharpy-test-" + System.Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(path);
            this.Value = path;
        }

        /// <summary>
        /// Best-effort recursive deletion of the temporary directory. Safe to call
        /// multiple times; swallows IO and access errors.
        /// </summary>
        public void Dispose()
        {
            if (this._disposed)
            {
                return;
            }

            this._disposed = true;

            try
            {
                if (System.IO.Directory.Exists(this.Value))
                {
                    System.IO.Directory.Delete(this.Value, true);
                }
            }
            catch (System.IO.IOException)
            {
                // Cleanup failure must not fail the test.
            }
            catch (System.UnauthorizedAccessException)
            {
                // Cleanup failure must not fail the test.
            }
        }
    }
}
