using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex.Analysis.FileSystems
{
    public class BinaryFileSystemFilter : FileSystemFilter
    {
        public readonly ConcurrentDictionary<string, bool> InclusionByExtension = new ConcurrentDictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        public BinaryFileSystemFilter(IEnumerable<string> excludedExtensions)
        {
            foreach (var extension in excludedExtensions)
            {
                InclusionByExtension[extension] = false;
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

            char[] buffer = new char[4096];
            using (var stream = fileSystem.OpenFile(filePath))
            using (var reader = new StreamReader(stream))
            {
                if (stream.Length == 0)
                {
                    return false;
                }
                
                while (true)
                {
                    int readCount = reader.Read(buffer, 0, buffer.Length);
                    for (int i = 0; i < readCount; i++)
                    {
                        if (buffer[i] == default(char))
                        {
                            InclusionByExtension[extension] = false;
                            return false;
                        }
                    }

                    if (readCount < buffer.Length)
                    {
                        break;
                    }
                }
            }

            InclusionByExtension[extension] = true;
            return true;
        }
    }
}
