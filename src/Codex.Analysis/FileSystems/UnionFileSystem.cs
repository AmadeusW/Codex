using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex.Analysis.FileSystems
{
    public class UnionFileSystem : FileSystemWrapper
    {
        public HashSet<string> Files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public UnionFileSystem(IEnumerable<string> files, FileSystem innerFileSystem)
            : base (innerFileSystem)
        {
            Files.UnionWith(files);
        }

        public override IEnumerable<string> GetFiles()
        {
            foreach (var file in Files)
            {
                yield return file;
            }

            foreach (var file in base.GetFiles())
            {
                if (!Files.Contains(file))
                {
                    yield return file;
                }
            }
        }
    }
}
