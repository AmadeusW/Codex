using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex
{
    [RequiredFor(ObjectStage.Upload)]
    public interface IRepoScopeEntity
    {
        string RepositoryName { get; }

        [Restricted(ObjectStage.Upload)]
        string CommitId { get; }

        [Restricted(ObjectStage.Upload)]
        int StableId { get; }
    }

    [RequiredFor(ObjectStage.Upload)]
    public interface IProjectScopeEntity : IRepoScopeEntity
    {
        string ProjectId { get; }
    }

    [RequiredFor(ObjectStage.Upload)]
    public interface IFileScopeEntity : IProjectScopeEntity
    {
        /// <summary>
        /// The language of the file
        /// </summary>
        string Language { get; }

        /// <summary>
        /// The project relative path of the file
        /// </summary>
        string FilePath { get; }

        /// <summary>
        /// The unique identifier for the file
        /// </summary>
        string FileId { get; }
    }
}
