using System;
using System.Linq;
using System.Text;
using System.Web;
using Codex.ObjectModel;
using Codex.Storage;

namespace WebUI.Rendering
{
    public class DocumentOutlineRenderer
    {
        private BoundSourceFile boundSourceFile;
        private string projectId;

        public DocumentOutlineRenderer(string projectId, BoundSourceFile boundSourceFile)
        {
            this.projectId = projectId;
            this.boundSourceFile = boundSourceFile;
        }

        public string Generate()
        {
            var sb = new StringBuilder();

            sb.AppendLine("<div id=\"documentOutline\">");

            int current = 0;
            GenerateCore(sb, ref current, -1);

            sb.AppendLine("</div>");

            return sb.ToString();
        }

        public void GenerateCore(StringBuilder sb, ref int current, int parentDepth, string parentPrefix = "")
        {
            for (; current < boundSourceFile.Definitions.Count; current++)
            {
                int nextIndex = current + 1;
                var definition = boundSourceFile.Definitions[current];

                if (string.Equals(definition.Definition.Kind, nameof(SymbolKinds.File), StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var symbol = definition.Definition;
                var depth = symbol.SymbolDepth;

                if (depth <= parentDepth)
                {
                    return;
                }

                var text = symbol.DisplayName;
                if (text.StartsWith(parentPrefix))
                {
                    text = text.Substring(parentPrefix.Length);
                }

                bool hasChildren = nextIndex != boundSourceFile.Definitions.Count &&
                    boundSourceFile.Definitions[nextIndex].Definition.SymbolDepth > depth;

                WriteFolderName(text, sb, definition.Definition.Id.Value, definition.Definition.Kind.ToLowerInvariant(), definition.GetGlyph(boundSourceFile?.SourceFile?.Info?.Path), hasChildren);
                if (hasChildren)
                {
                    WriteFolderChildrenContainer(sb);
                    current++;
                    GenerateCore(sb, ref current, depth, symbol.DisplayName + ".");
                    sb.Append("</div>");
                }
            }
        }

        private void WriteFolderName(string folderName, StringBuilder sb, string symbolId, string kind, string folderIcon = "202.png", bool hasChildren = false)
        {
            folderName = HttpUtility.HtmlEncode(folderName);
            var url = $"/?left=outline&rightProject={projectId}&file={HttpUtility.UrlEncode(boundSourceFile.SourceFile.Info.Path)}&rightSymbol={symbolId}";
            var folderNameText = $"<a href=\"{url}\" onclick=\"event.stopPropagation();S('{symbolId}');return false;\"><span class=\"k\">{kind}</span>&nbsp;{folderName}</a>";
            var icon = $"<img src=\"../../content/icons/{folderIcon}\" class=\"imageFolder\" />";
            if (hasChildren)
            {
                sb.Append($"<div class=\"folderTitle\" onclick=\"ToggleExpandCollapse(this);ToggleFolderIcon(this);\" style=\"background-image:url('../../content/images/minus.png');\">{icon}{folderNameText}</div>");
            }
            else
            {
                sb.Append($"<div class=\"folderTitle\">{icon}{folderNameText}</div>");
            }
        }

        private static void WriteFolderChildrenContainer(StringBuilder sb)
        {
            sb.AppendLine("<div class=\"folder\" style=\"display: block;\">");
        }
    }
}