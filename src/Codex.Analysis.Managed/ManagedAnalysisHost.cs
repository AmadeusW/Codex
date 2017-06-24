namespace Codex.Analysis
{
    public class ManagedAnalysisHost
    {
        public static readonly ManagedAnalysisHost Default = new ManagedAnalysisHost();

        public virtual bool IncludeDocument(string projectId, string documentPath)
        {
            return true;
        }
    }
}
