using System;
using System.Collections.Generic;
using Codex.Analysis.Files;
using Codex.Import;

namespace Codex.Analysis.External
{
    public class ExternalRepoFileAnalyzer : RepoFileAnalyzer
    {
        private readonly HashSet<string> semanticDataDirectories;

        private static readonly string[] supportedExtensions = new[] {".dsc"};

        public override string[] SupportedExtensions => supportedExtensions;

        public Dictionary<string, CodexFile> m_fileToStoreMap =
            new Dictionary<string, CodexFile>(StringComparer.OrdinalIgnoreCase);

        public ExternalRepoFileAnalyzer(string[] semanticDataDirectories)
        {
            this.semanticDataDirectories = new HashSet<string>(semanticDataDirectories ?? new string[0],
                StringComparer.OrdinalIgnoreCase);
        }

        public override void Initialize(Repo repo)
        {
            var dd = new Dictionary<string, RepoProject>();
            foreach (var semanticDataDirectory in semanticDataDirectories)
            {
                var store = new CodexSemanticStore(semanticDataDirectory);
                store.Load();

                foreach (var project in store.Projects.List)
                {
                    var repoProject = new RepoProject(project.Name, repo)
                    {
                        ProjectDirectory = project.Directory,
                        // $TODO;
                        //ProjectFile = projectFile
                    };

                    dd.Add(project.Name, repoProject);

                    repo.Projects.Add(repoProject);
                }

                foreach (var file in store.Files.List)
                {
                    if (!m_fileToStoreMap.ContainsKey(file.Path))
                    {
                        RepoFile repoFile;
                        if (!repo.FilesByPath.TryGetValue(file.Path, out repoFile))
                        {
                            RepoProject project;
                            if (!dd.TryGetValue(store.Projects[file.Project].Name, out project))
                            {
                                project = repo.DefaultRepoProject;
                            }

                            repoFile = project.AddFile(file.Path);
                        }

                        m_fileToStoreMap[repoFile.RepoRelativePath] = file;
                    }
                }
            }
        }

        protected override void AnnotateFile(BoundSourceFileBuilder binder)
        {
            CodexFile file;
            if (m_fileToStoreMap.TryGetValue(binder.SourceFile.Info.RepoRelativePath, out file))
            {
                file.Load();
                CodexTypeAdapter.Annotate(binder, file);
            }

            base.AnnotateFile(binder);
        }

    }
}