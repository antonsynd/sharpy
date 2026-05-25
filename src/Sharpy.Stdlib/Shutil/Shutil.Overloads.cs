using System;
using System.IO;

namespace Sharpy
{
    public static partial class ShutilModule
    {
        public static string? Which(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            if (name.IndexOf(System.IO.Path.DirectorySeparatorChar) >= 0 ||
                name.IndexOf(System.IO.Path.AltDirectorySeparatorChar) >= 0)
            {
                if (File.Exists(name))
                {
                    return System.IO.Path.GetFullPath(name);
                }
                return null;
            }

            string? pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(pathEnv))
            {
                return null;
            }

            string[] pathDirs = pathEnv.Split(System.IO.Path.PathSeparator);

            bool isWindows = System.IO.Path.DirectorySeparatorChar == '\\';
            string[] extensions;
            if (isWindows)
            {
                string? pathExt = Environment.GetEnvironmentVariable("PATHEXT");
                extensions = !string.IsNullOrEmpty(pathExt)
                    ? pathExt.Split(';')
                    : new[] { ".COM", ".EXE", ".BAT", ".CMD" };
            }
            else
            {
                extensions = new[] { "" };
            }

            foreach (string dir in pathDirs)
            {
                if (string.IsNullOrEmpty(dir))
                {
                    continue;
                }

                foreach (string ext in extensions)
                {
                    string candidate = System.IO.Path.Combine(dir, name + ext);
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
            }

            return null;
        }

        public static (long, long, long) DiskUsage(string path)
        {
            try
            {
                string fullPath = System.IO.Path.GetFullPath(path);
                string root = System.IO.Path.GetPathRoot(fullPath) ?? fullPath;

                DriveInfo driveInfo = new DriveInfo(root);
                long total = driveInfo.TotalSize;
                long free = driveInfo.AvailableFreeSpace;
                long used = total - free;

                return (total, used, free);
            }
            catch (Exception ex) when (!(ex is OSError))
            {
                throw new OSError("Failed to get disk usage for '" + path + "': " + ex.Message, ex);
            }
        }
    }
}
