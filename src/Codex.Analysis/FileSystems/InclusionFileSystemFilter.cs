using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex.Analysis.FileSystems
{
    public class InclusionFileSystemFilter : FileSystemFilter
    {
        public readonly Dictionary<string, bool> InclusionByExtension = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        public InclusionFileSystemFilter(IEnumerable<string> includedExtensions)
        {
            foreach (var extension in includedExtensions)
            {
                InclusionByExtension[extension] = true;
            }
        }

        public override bool IncludeFile(FileSystem fileSystem, string filePath)
        {
            var extension = Path.GetExtension(filePath);
            bool inclusion;
            if (InclusionByExtension.TryGetValue(extension, out inclusion))
            {
                return inclusion;
            }

            return false;
        }
    }
}
