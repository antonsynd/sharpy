using System;
using System.IO;
using System.Linq;

namespace Sharpy
{
    /// <summary>
    /// The one classifier of filesystem failures (plan-638e8b D1, #2476): (path state × what the
    /// operation requires) → python's <see cref="OSError"/> subclass, python3's Linux errno rule
    /// spelled once. Every Core and Stdlib filesystem site calls a door of this class and never spells
    /// an <see cref="OSError"/> subclass itself.
    /// </summary>
    /// <remarks>
    /// <para><b>The rule</b> (measured with python3 3.13.18 on Linux, uid 0, over the six path states
    /// of G1 — <c>Sharpy.Stdlib.Tests</c> <c>Conformance/OSErrorPathState</c> — for every requirement):
    /// a missing path or a missing parent → <see cref="FileNotFoundError"/>; a parent that is a file,
    /// or a directory required and a file found → <see cref="NotADirectoryError"/>; a file required and
    /// a directory found → <see cref="IsADirectoryError"/>; a create onto anything existing →
    /// <see cref="FileExistsError"/>; the removal of a non-empty directory → plain
    /// <see cref="OSError"/>; an access refusal with none of those states →
    /// <see cref="PermissionError"/>; any other CLR I/O failure → plain <see cref="OSError"/>, never a
    /// CLR type.</para>
    /// <para><b>Doors.</b> Each <see cref="PathRequirement"/> member has its doors, each stamped with
    /// exactly one requirement (<see cref="PathRequirementAttribute"/>, read by G1's R0 and R2):
    /// a <i>check</i> door (<c>Check…(path)</c>) raising python's class where .NET is silent or lenient
    /// and POSIX refuses; a <i>translate</i> door (<c>Translate…(error, path)</c>) returning the
    /// exception to raise for a caught CLR failure; and, for the two-path rename, the pair door
    /// <see cref="Rename"/>, which performs the rename (D1 predicates 2 and 3). The requirement enum and
    /// the stamp stay internal, so this class reserves no name a Sharpy program could declare (D1
    /// predicate 4). From Sharpy source: <c>from sharpy import PathErrors</c>, then
    /// <c>PathErrors.check_creates_dir(p)</c>, <c>raise PathErrors.translate_removes_dir(e, p)</c>,
    /// <c>PathErrors.rename(src, dst)</c>.</para>
    /// <para><b>Stated limits.</b> The state is read with <see cref="File.Exists"/> and
    /// <see cref="Directory.Exists"/>, which follow symbolic links and answer false on a path the
    /// process may not search; permission bits, symbolic links and path spelling are axes G1 excludes
    /// (plan-638e8b C2). Messages carry python's strerror text without the <c>[Errno N]</c> prefix
    /// (message text is DEVIATION by default).</para>
    /// </remarks>
    public static class PathErrors
    {
        // ------------------------------------------------------------------ check doors

        /// <summary>Raise python's class unless <paramref name="path"/> is an existing file to read (<c>open(p)</c>).</summary>
        [PathRequirement(PathRequirement.NeedsFileToRead)]
        public static void CheckNeedsFileToRead(string path) => Check(path, PathRequirement.NeedsFileToRead);

        /// <summary>Raise python's class unless <paramref name="path"/> can be written as a file (<c>open(p, "w")</c>).</summary>
        [PathRequirement(PathRequirement.NeedsFileToWrite)]
        public static void CheckNeedsFileToWrite(string path) => Check(path, PathRequirement.NeedsFileToWrite);

        /// <summary>Raise python's class unless <paramref name="path"/> can be created as a new file (<c>open(p, "x")</c>).</summary>
        [PathRequirement(PathRequirement.CreatesExclusively)]
        public static void CheckCreatesExclusively(string path) => Check(path, PathRequirement.CreatesExclusively);

        /// <summary>Raise python's class unless <paramref name="path"/> is an existing non-directory to unlink (<c>os.remove</c>).</summary>
        [PathRequirement(PathRequirement.RemovesFile)]
        public static void CheckRemovesFile(string path) => Check(path, PathRequirement.RemovesFile);

        /// <summary>Raise python's class unless <paramref name="path"/> can be created as a directory (<c>os.mkdir</c>).</summary>
        [PathRequirement(PathRequirement.CreatesDir)]
        public static void CheckCreatesDir(string path) => Check(path, PathRequirement.CreatesDir);

        /// <summary>Raise python's class unless <paramref name="path"/> is an existing empty directory (<c>os.rmdir</c>).</summary>
        [PathRequirement(PathRequirement.RemovesDir)]
        public static void CheckRemovesDir(string path) => Check(path, PathRequirement.RemovesDir);

        /// <summary>Raise python's class unless <paramref name="path"/> is an existing directory (<c>os.listdir</c>).</summary>
        [PathRequirement(PathRequirement.NeedsDir)]
        public static void CheckNeedsDir(string path) => Check(path, PathRequirement.NeedsDir);

        /// <summary>Raise python's class unless <paramref name="path"/> exists (<c>os.stat</c>).</summary>
        [PathRequirement(PathRequirement.NeedsAny)]
        public static void CheckNeedsAny(string path) => Check(path, PathRequirement.NeedsAny);

        // ------------------------------------------------------------------ translate doors

        /// <summary>The exception to raise for <paramref name="error"/>, caught reading <paramref name="path"/> as a file.</summary>
        [PathRequirement(PathRequirement.NeedsFileToRead)]
        public static Exception TranslateNeedsFileToRead(Exception error, string path) => Translate(error, path, PathRequirement.NeedsFileToRead);

        /// <summary>The exception to raise for <paramref name="error"/>, caught writing <paramref name="path"/> as a file.</summary>
        [PathRequirement(PathRequirement.NeedsFileToWrite)]
        public static Exception TranslateNeedsFileToWrite(Exception error, string path) => Translate(error, path, PathRequirement.NeedsFileToWrite);

        /// <summary>The exception to raise for <paramref name="error"/>, caught creating <paramref name="path"/> exclusively.</summary>
        [PathRequirement(PathRequirement.CreatesExclusively)]
        public static Exception TranslateCreatesExclusively(Exception error, string path) => Translate(error, path, PathRequirement.CreatesExclusively);

        /// <summary>The exception to raise for <paramref name="error"/>, caught unlinking <paramref name="path"/>.</summary>
        [PathRequirement(PathRequirement.RemovesFile)]
        public static Exception TranslateRemovesFile(Exception error, string path) => Translate(error, path, PathRequirement.RemovesFile);

        /// <summary>The exception to raise for <paramref name="error"/>, caught creating the directory <paramref name="path"/>.</summary>
        [PathRequirement(PathRequirement.CreatesDir)]
        public static Exception TranslateCreatesDir(Exception error, string path) => Translate(error, path, PathRequirement.CreatesDir);

        /// <summary>The exception to raise for <paramref name="error"/>, caught removing the directory <paramref name="path"/>.</summary>
        [PathRequirement(PathRequirement.RemovesDir)]
        public static Exception TranslateRemovesDir(Exception error, string path) => Translate(error, path, PathRequirement.RemovesDir);

        /// <summary>The exception to raise for <paramref name="error"/>, caught listing or entering the directory <paramref name="path"/>.</summary>
        [PathRequirement(PathRequirement.NeedsDir)]
        public static Exception TranslateNeedsDir(Exception error, string path) => Translate(error, path, PathRequirement.NeedsDir);

        /// <summary>The exception to raise for <paramref name="error"/>, caught reading the entry <paramref name="path"/>.</summary>
        [PathRequirement(PathRequirement.NeedsAny)]
        public static Exception TranslateNeedsAny(Exception error, string path) => Translate(error, path, PathRequirement.NeedsAny);

        // ------------------------------------------------------------------ the pair door

        /// <summary>
        /// Rename <paramref name="src"/> to <paramref name="dst"/> as POSIX <c>rename(2)</c> does
        /// (<c>os.rename</c>, <c>os.replace</c>): python's verdict is a function of the PAIR of states
        /// (D1 predicate 2), and python's successes succeed — an existing file is replaced, and so is an
        /// empty directory, which is removed first because neither target has a directory overwrite
        /// (D1 predicate 3; that sequence is not atomic where <c>rename(2)</c> is).
        /// </summary>
        [PathRequirement(PathRequirement.Rename)]
        public static void Rename(string src, string dst)
        {
            var refusal = ClassifyRename(src, dst);
            if (refusal != Verdict.Ok)
            {
                throw Create(refusal, RenameSubject(src, dst), null);
            }
            try
            {
                if (Directory.Exists(src))
                {
                    if (Directory.Exists(dst))
                    {
                        Directory.Delete(dst);
                    }
                    Directory.Move(src, dst);
                }
                else
                {
#if NET10_0_OR_GREATER
                    File.Move(src, dst, true);
#else
                    if (File.Exists(dst))
                    {
                        File.Replace(src, dst, null);
                    }
                    else
                    {
                        File.Move(src, dst);
                    }
#endif
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                throw TranslateClassified(ex, RenameSubject(src, dst), () => ClassifyRename(src, dst));
            }
        }

        // ------------------------------------------------------------------ the table

        /// <summary>python's outcome for one path state; <see cref="Verdict.Ok"/> is no exception.</summary>
        internal enum Verdict
        {
            Ok,
            NotFound,
            NotADirectory,
            IsADirectory,
            Exists,
            NotEmpty,
        }

        /// <summary>
        /// The table: per requirement, python's verdict when the path is missing, a file, an empty
        /// directory, a non-empty directory. A missing parent (<see cref="Verdict.NotFound"/>) and a
        /// parent that is a file (<see cref="Verdict.NotADirectory"/>) answer before it, for every
        /// requirement alike (<see cref="ParentVerdict"/>).
        /// </summary>
        private static Verdict[] VerdictRow(PathRequirement requirement)
        {
            switch (requirement)
            {
                //                                                     Missing           File                  EmptyDir              NonEmptyDir
                case PathRequirement.NeedsFileToRead:
                    return new[] { Verdict.NotFound, Verdict.Ok, Verdict.IsADirectory, Verdict.IsADirectory };
                case PathRequirement.NeedsFileToWrite:
                    return new[] { Verdict.Ok, Verdict.Ok, Verdict.IsADirectory, Verdict.IsADirectory };
                case PathRequirement.CreatesExclusively:
                    return new[] { Verdict.Ok, Verdict.Exists, Verdict.Exists, Verdict.Exists };
                case PathRequirement.RemovesFile:
                    return new[] { Verdict.NotFound, Verdict.Ok, Verdict.IsADirectory, Verdict.IsADirectory };
                case PathRequirement.CreatesDir:
                    return new[] { Verdict.Ok, Verdict.Exists, Verdict.Exists, Verdict.Exists };
                case PathRequirement.RemovesDir:
                    return new[] { Verdict.NotFound, Verdict.NotADirectory, Verdict.Ok, Verdict.NotEmpty };
                case PathRequirement.NeedsDir:
                    return new[] { Verdict.NotFound, Verdict.NotADirectory, Verdict.Ok, Verdict.Ok };
                case PathRequirement.NeedsAny:
                    return new[] { Verdict.NotFound, Verdict.Ok, Verdict.Ok, Verdict.Ok };
                default:
                    throw new ArgumentOutOfRangeException(nameof(requirement), requirement,
                        "a requirement with no single-path row (Rename is the pair door's)");
            }
        }

        private const int Missing = 0;
        private const int AFile = 1;
        private const int EmptyDir = 2;
        private const int NonEmptyDir = 3;

        /// <summary>python's verdict for <paramref name="requirement"/> on <paramref name="path"/> as it is now.</summary>
        internal static Verdict Classify(string path, PathRequirement requirement)
        {
            var row = VerdictRow(requirement);
            var parent = ParentVerdict(path);
            if (parent != Verdict.Ok)
            {
                return parent;
            }
            return row[StateIndex(path, row)];
        }

        /// <summary>
        /// The pair rule of <c>rename(2)</c> (measured over the 36 state pairs): the source's parent,
        /// then the destination's parent, then a missing source, then the kinds — a file onto a
        /// directory is <see cref="Verdict.IsADirectory"/>, a directory onto a file
        /// <see cref="Verdict.NotADirectory"/>, a directory onto a non-empty directory
        /// <see cref="Verdict.NotEmpty"/>; everything else succeeds.
        /// </summary>
        internal static Verdict ClassifyRename(string src, string dst)
        {
            var verdict = ParentVerdict(src);
            if (verdict != Verdict.Ok)
            {
                return verdict;
            }
            verdict = ParentVerdict(dst);
            if (verdict != Verdict.Ok)
            {
                return verdict;
            }
            if (Directory.Exists(src))
            {
                if (File.Exists(dst))
                {
                    return Verdict.NotADirectory;
                }
                return Directory.Exists(dst) && !IsEmptyDirectory(dst) ? Verdict.NotEmpty : Verdict.Ok;
            }
            if (!File.Exists(src))
            {
                return Verdict.NotFound;
            }
            return Directory.Exists(dst) ? Verdict.IsADirectory : Verdict.Ok;
        }

        /// <summary>
        /// The verdict of the path's ancestors, walked as the kernel resolves them: the nearest
        /// existing ancestor being a file is <see cref="Verdict.NotADirectory"/>; an existing parent
        /// directory is <see cref="Verdict.Ok"/>; a missing parent is <see cref="Verdict.NotFound"/>.
        /// </summary>
        private static Verdict ParentVerdict(string path)
        {
            var ancestor = System.IO.Path.GetDirectoryName(path);
            var immediate = true;
            while (!string.IsNullOrEmpty(ancestor))
            {
                if (Directory.Exists(ancestor))
                {
                    return immediate ? Verdict.Ok : Verdict.NotFound;
                }
                if (File.Exists(ancestor))
                {
                    return Verdict.NotADirectory;
                }
                ancestor = System.IO.Path.GetDirectoryName(ancestor);
                immediate = false;
            }
            return immediate ? Verdict.Ok : Verdict.NotFound;
        }

        /// <summary>The column of <paramref name="row"/> for the path; emptiness is read only where the row tells the two apart.</summary>
        private static int StateIndex(string path, Verdict[] row)
        {
            if (Directory.Exists(path))
            {
                if (row[EmptyDir] == row[NonEmptyDir])
                {
                    return EmptyDir;
                }
                return IsEmptyDirectory(path) ? EmptyDir : NonEmptyDir;
            }
            return File.Exists(path) ? AFile : Missing;
        }

        private static bool IsEmptyDirectory(string path) => !Directory.EnumerateFileSystemEntries(path).Any();

        // ------------------------------------------------------------------ raising and translating

        private static void Check(string path, PathRequirement requirement)
        {
            var verdict = Classify(path, requirement);
            if (verdict != Verdict.Ok)
            {
                throw Create(verdict, Quote(path), null);
            }
        }

        internal static Exception Translate(Exception error, string path, PathRequirement requirement)
            => TranslateClassified(error, Quote(path), () => Classify(path, requirement));

        /// <summary>
        /// Translate a caught exception: an <see cref="OSError"/> is returned unchanged (D1 predicate 1 —
        /// Core <see cref="OSError"/> derives from <see cref="IOException"/>, so an outer catch receives
        /// exceptions already classified), and so is anything that is not a CLR I/O failure; an
        /// <see cref="IOException"/> or <see cref="UnauthorizedAccessException"/> becomes python's class,
        /// the path's state (read now, after the failure) deciding first.
        /// </summary>
        private static Exception TranslateClassified(Exception error, string subject, Func<Verdict> classify)
        {
            if (error is OSError || !(error is IOException || error is UnauthorizedAccessException))
            {
                return error;
            }
            var verdict = classify();
            if (verdict != Verdict.Ok)
            {
                return Create(verdict, subject, error);
            }
            // The state shows no refusal the table names: the CLR's own report decides.
            if (error is UnauthorizedAccessException)
            {
                return new PermissionError("Permission denied: " + subject, error);
            }
            if (error is FileNotFoundException || error is DirectoryNotFoundException)
            {
                return new FileNotFoundError("No such file or directory: " + subject, error);
            }
            return new OSError(error.Message + ": " + subject, error);
        }

        /// <summary>The one place a verdict becomes python's class (strerror text, no <c>[Errno N]</c>).</summary>
        private static OSError Create(Verdict verdict, string subject, Exception? inner)
        {
            switch (verdict)
            {
                case Verdict.NotFound:
                    return inner == null
                        ? new FileNotFoundError("No such file or directory: " + subject)
                        : new FileNotFoundError("No such file or directory: " + subject, inner);
                case Verdict.NotADirectory:
                    return inner == null
                        ? new NotADirectoryError("Not a directory: " + subject)
                        : new NotADirectoryError("Not a directory: " + subject, inner);
                case Verdict.IsADirectory:
                    return inner == null
                        ? new IsADirectoryError("Is a directory: " + subject)
                        : new IsADirectoryError("Is a directory: " + subject, inner);
                case Verdict.Exists:
                    return inner == null
                        ? new FileExistsError("File exists: " + subject)
                        : new FileExistsError("File exists: " + subject, inner);
                case Verdict.NotEmpty:
                    return inner == null
                        ? new OSError("Directory not empty: " + subject)
                        : new OSError("Directory not empty: " + subject, inner);
                default:
                    throw new ArgumentOutOfRangeException(nameof(verdict), verdict, "Ok raises nothing");
            }
        }

        private static string Quote(string path) => "'" + path + "'";

        private static string RenameSubject(string src, string dst) => Quote(src) + " -> " + Quote(dst);
    }
}
