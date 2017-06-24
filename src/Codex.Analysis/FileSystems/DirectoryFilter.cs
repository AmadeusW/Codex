using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Codex.Utilities;

namespace Codex.Analysis.FileSystems
{
    public class DirectoryFileSystemFilter : FileSystemFilter
    {
        public readonly string[] ExcludedSegments;

        public DirectoryFileSystemFilter(params string[] excludedSegments)
        {
            ExcludedSegments = excludedSegments;
        }

        public override bool IncludeDirectory(FileSystem fileSystem, string directoryPath)
        {
            directoryPath = directoryPath.EnsureTrailingSlash();

            foreach (var excludedSegment in ExcludedSegments)
            {
                if (directoryPath.IndexOf(excludedSegment, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return false;
                }
            }

            if ((new DirectoryInfo(directoryPath).Attributes & FileAttributes.Hidden) == FileAttributes.Hidden)
            {
                return false;
            }

            return true;
        }
    }
}
