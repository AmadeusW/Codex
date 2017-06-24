using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex.Analysis
{
    public struct NamedRoot
    {
        public readonly string Name;
        public readonly string Path;

        public NamedRoot(string name, string path)
        {
            Name = name;
            Path = path;
        }
    }
}
