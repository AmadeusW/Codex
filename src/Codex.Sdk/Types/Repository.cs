using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex
{
    public interface IRepository
    {
        string Name { get; }

        string WebAddress { get; }

        IReadOnlyList<IRepositoryReference> RepositoryReferences { get; }
    }

    public interface IRepositoryReference
    {

    }
}
