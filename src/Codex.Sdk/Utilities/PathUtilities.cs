using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;

namespace Codex.Utilities
{
    /// <summary>
    /// Utility methods for paths and Uris (including relativizing and derelativizing)
    /// </summary>
    public static class PathUtilities
    {
        /// <summary>
        /// Indicates that a relative path is same as the base path
        /// </summary>
        public const string CurrentDirectoryRelativePath = @"./";

        public static string GetRelativePath(string directory, string path)
        {
            string result = null;
            if (!string.IsNullOrEmpty(directory) && path.StartsWith(directory, StringComparison.OrdinalIgnoreCase))
            {
                result = path.Substring(directory.Length).TrimStart('/', '\\');
            }

            return result;
        }

        public static string GetExtension(string path)
        {
            for (int i = path.Length - 1; i >= 0; i--)
            {
                if (path[i] == '\\')
                {
                    return string.Empty;
                }

                if (path[i] == '.')
                {
                    return path.Substring(i);
                }
            }

            return string.Empty;
        }

        public static string GetFileName(string path)
        {
            return path.Substring(path.LastIndexOf('\\') + 1);
        }

        public static string GetDirectoryName(string path)
        {
            var index = path.LastIndexOf('\\');
            return path.Substring(0, index > 0 ? index : 0);
        }

        /// <summary>
        /// Ensures that the path has a trailing slash at the end
        /// </summary>
        /// <param name="path">the path</param>
        /// <returns>the path ending with a trailing slash</returns>
        public static string EnsureTrailingSlash(string path)
        {
            return PathUtilities.EnsureTrailingSlash(path, Path.DirectorySeparatorChar);
        }

        /// <summary>
        /// Ensures that the path has a trailing slash at the end
        /// </summary>
        /// <param name="path">the path</param>
        /// <param name="separatorChar">the trailing separator char</param>
        /// <returns>the path ending with a trailing slash</returns>
        public static string EnsureTrailingSlash(string path, char separatorChar)
        {
            if (path[path.Length - 1] != separatorChar)
            {
                return path + separatorChar;
            }

            return path;
        }
    }
}