namespace Codex.Storage.DataModel
{
    public class SymbolReferenceModel
    {
        public string ReferringProjectId { get; set; }
        public string ReferringFilePath { get; set; }
        public ReferenceSpanModel ReferringSpan { get; set; }
    }
}