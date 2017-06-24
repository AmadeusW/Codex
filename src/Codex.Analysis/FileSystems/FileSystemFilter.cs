using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex.Analysis.FileSystems
{
    public class FileSystemFilter
    {
        public virtual bool IncludeDirectory(FileSystem fileSystem, string directoryPath) => true;

        public virtual bool IncludeFile(FileSystem fileSystem, string filePath) => true;
    }
}
