using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex.Analysis.FileSystems
{
    public class FileSystemWrapper : FileSystem
    {
        protected readonly FileSystem InnerFileSystem;

        public FileSystemWrapper(FileSystem innerFileSystem)
        {
            InnerFileSystem = innerFileSystem;
        }

        public override IEnumerable<string> GetFiles()
        {
            return InnerFileSystem.GetFiles();
        }

        public override Stream OpenFile(string filePath)
        {
            return InnerFileSystem.OpenFile(filePath);
        }
    }

    public class CachingFileSystem : FileSystemWrapper
    {
        public List<string> Files;

        public CachingFileSystem(FileSystem innerFileSystem)
            : base (innerFileSystem)
        {
        }

        public override IEnumerable<string> GetFiles()
        {
            if (Files == null)
            {
                return GetAndPopulateFiles();
            }

            return Files;
        }

        public IEnumerable<string> GetAndPopulateFiles()
        {
            var files = new List<string>();
            foreach (var file in base.GetFiles())
            {
                files.Add(file);
                yield return file;
            }

            Files = files;
        }
    }
}
