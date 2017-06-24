using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex.ObjectModel
{
    public class FileSystemSourceFile : ISourceFile
    {
        public SourceFileInfo Info { get; private set; }

        public readonly string BackingFilePath;

        public string BackingText = null;

        public FileSystemSourceFile(string backingFilePath, SourceFileInfo sourceFileInfo)
        {
            BackingFilePath = backingFilePath;
            Info = sourceFileInfo;
        }

        public Task<string> GetContentsAsync()
        {
            var text = BackingText;

            text = text ?? File.ReadAllText(BackingFilePath);
            return Task.FromResult(text);
        }
    }
}
