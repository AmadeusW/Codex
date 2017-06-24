using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex
{
    public interface IProject : IProjectScopeEntity
    {
        /// <summary>
        /// References to files in the project
        /// </summary>
        IReadOnlyList<IFileLink> Files { get; }

        IReadOnlyList<IProjectReference> ProjectReferences { get; }
    }

    public interface IProjectReference : IProjectScopeEntity
    {
        string ReferencedProjectId { get; }

        IReadOnlyList<IDefinitionSymbol> Definitions { get; }
    }

    public interface IFileLink
    {
        /// <summary>
        /// The virtual path to the file inside the project
        /// </summary>
        string Path { get; }

        /// <summary>
        /// Unique identifer for file
        /// </summary>
        string FileId { get; }

        /// <summary>
        /// Unique identifier of project
        /// </summary>
        string ProjectId { get; }
    }
}
