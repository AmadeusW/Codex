using System;
using System.IO;
using System.Security;

namespace Codex.Utilities
{
    static class FileUtilities
    {
        /// <summary>
        /// A variation on File.Delete that will throw ExceptionHandling.NotExpectedException exceptions
        /// </summary>
        internal static void DeleteNoThrow(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception ex)
            {
                if (NotExpectedException(ex))
                {
                    throw;
                }

                // Otherwise eat it.
            }
        }

        public static void CreateFileDirectory(string filePath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
        }

        /// <summary>
        /// If the given exception is file IO related or expected return false.
        /// Otherwise, return true.
        /// </summary>
        /// <param name="e">The exception to check.</param>
        /// <returns> True if exception is not IO related or expected otherwise. </returns>
        internal static bool NotExpectedException(Exception e)
        {
            // These all derive from IOException
            //     DirectoryNotFoundException
            //     DriveNotFoundException
            //     EndOfStreamException
            //     FileLoadException
            //     FileNotFoundException
            //     PathTooLongException
            //     PipeException
            if (e is UnauthorizedAccessException
             || e is NotSupportedException
             || (e is ArgumentException && !(e is ArgumentNullException))
             || e is SecurityException
             || e is IOException)
            {
                return false;
            }

            return true;
        }
    }
}
