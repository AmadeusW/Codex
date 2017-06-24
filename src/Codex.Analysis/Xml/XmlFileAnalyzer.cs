using Codex.Analysis.Files;
using Codex.Import;
using Codex.ObjectModel;
using Microsoft.Language.Xml;

namespace Codex.Analysis.Xml
{
    public class XmlFileAnalyzer : RepoFileAnalyzer
    {
        private string[] supportedExtensions;

        public override string[] SupportedExtensions => supportedExtensions;

        public XmlFileAnalyzer(params string[] supportedExtensions)
        {
            this.supportedExtensions = supportedExtensions;
        }

        protected override void AnnotateFile(BoundSourceFileBuilder binder)
        {
            XmlAnalyzer.Analyze(binder);
        }

        protected override BoundSourceFileBuilder CreateBuilderCore(SourceFile sourceFile, string projectId)
        {
            return new XmlSourceFileBuilder(sourceFile, projectId);
        }

        protected override void AnnotateFile(AnalysisServices services, RepoFile file, BoundSourceFileBuilder binder)
        {
            var document = XmlAnalyzer.Analyze(binder);

            AnnotateFile(services, file, (XmlSourceFileBuilder)binder, document);
        }

        protected virtual void AnnotateFile(
            AnalysisServices services, 
            RepoFile file,
            XmlSourceFileBuilder binder,
            XmlDocumentSyntax document)
        {
        }

        public override SourceFileInfo AugmentSourceFileInfo(SourceFileInfo info)
        {
            info.Language = "xml";
            return info;
        }
    }
}
