using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex.Analysis.FileSystems
{
    public class MultiFileSystemFilter : FileSystemFilter
    {
        protected readonly FileSystemFilter[] InnerFilters;

        public MultiFileSystemFilter(params FileSystemFilter[] innerFilters)
        {
            InnerFilters = innerFilters;
        }

        public override bool IncludeDirectory(FileSystem fileSystem, string directoryPath)
        {
            foreach (var filter in InnerFilters)
            {
                if (!filter.IncludeDirectory(fileSystem, directoryPath))
                {
                    return false;
                }
            }

            return true;
        }

        public override bool IncludeFile(FileSystem fileSystem, string filePath)
        {
            foreach (var filter in InnerFilters)
            {
                if (!filter.IncludeFile(fileSystem, filePath))
                {
                    return false;
                }
            }

            return true;
        }

    }
}
