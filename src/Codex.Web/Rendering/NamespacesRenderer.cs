using Codex.ObjectModel;

namespace WebUI.Rendering
{
    public class NamespacesRenderer
    {
        private string projectId;
        private IStorage storage;

        public NamespacesRenderer(IStorage storage, string projectId)
        {
            this.storage = storage;
            this.projectId = projectId;
        }

        public string Generate()
        {
            return "Namespaces";
        }
    }
}