using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex
{
    public interface ICommit
    {
        string CommitId { get; }

        IReadOnlyList<IRef<ICommit>> ChildCommits { get; }
    }
}
