namespace WebUI
{
    public class EditorModel
    {
        public string ProjectId { get; set; }
        public string FilePath { get; set; }
        public string RepoRelativePath { get; set; }
        public string WebLink { get; set; }
        public string Text { get; set; }
        public string LineNumberText { get; set; }
        public string Error { get; set; }
    }
}