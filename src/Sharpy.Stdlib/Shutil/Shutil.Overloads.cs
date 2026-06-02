using System;
using System.IO;

namespace Sharpy
{
    /// <summary>High-level file operations (copy, move, remove trees).</summary>
    public static partial class ShutilModule
    {
        /// <summary>Return disk usage statistics about the given path as a (total, used, free) tuple.</summary>
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
