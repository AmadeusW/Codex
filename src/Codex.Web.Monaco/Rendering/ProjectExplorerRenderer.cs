using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using Codex;
using Codex.ObjectModel;
using Codex.Utilities;
using Folder = WebUI.Rendering.Folder<string>;

namespace WebUI.Rendering
{
    public class ProjectExplorerRenderer
    {
        private ProjectContents projectContents;
        private readonly IEnumerable<string> referencingProjects;

        public ProjectExplorerRenderer(ProjectContents projectContents, IEnumerable<string> referencingProjects)
        {
            this.projectContents = projectContents;
            this.referencingProjects = referencingProjects.OrderBy(s => s, StringComparer.OrdinalIgnoreCase);
        }

        public string GenerateProjectExplorer()
        {
            var sb = new StringBuilder();
            sb.Append("<div id=\"projectExplorer\">");
            WriteBody(sb);
            WriteProjectStats(sb);
            return sb.ToString();
        }

        private bool IsCSharp => true;

        private void WriteProjectStats(StringBuilder sb)
        {
            sb.AppendLine("<p class=\"projectInfo\">");

            //sb.AppendLine("Project&nbsp;path:&nbsp;" + ProjectSourcePath + "<br>");
            sb.AppendLine("Files:&nbsp;" + projectContents.Files.Count.WithThousandSeparators() + "<br>");
            //sb.AppendLine("Lines&nbsp;of&nbsp;code:&nbsp;" + projectContents.SourceLineCount.WithThousandSeparators() + "<br>");
            //sb.AppendLine("Bytes:&nbsp;" + BytesOfCode.WithThousandSeparators() + "<br>");
            //sb.AppendLine("Declared&nbsp;symbols:&nbsp;" + projectContents.SymbolCount.WithThousandSeparators() + "<br>");
            //sb.AppendLine("Declared&nbsp;types:&nbsp;" + namedTypes.Count().WithThousandSeparators() + "<br>");
            //sb.AppendLine("Public&nbsp;types:&nbsp;" + namedTypes.Where(t => t.DeclaredAccessibility == Accessibility.Public).Count().WithThousandSeparators() + "<br>");
            if (projectContents.DateUploaded != default(DateTime))
            {
                sb.AppendLine("Indexed&nbsp;on:&nbsp;" + projectContents.DateUploaded.ToLocalTime().ToString("MMMM dd"));
            }

            sb.AppendLine("</p>");
        }

        private void WriteBody(StringBuilder sb)
        {
            Folder root = new Folder();
            root.Name = projectContents.Id;

            foreach (var file in projectContents.Files.Select(f => f.Path))
            {
                var parts = file.Split('\\');
                AddDocumentToFolder(root, file, parts.Take(parts.Length - 1).ToArray());
            }

            root.Sort((l, r) => Path.GetFileName(l).CompareTo(Path.GetFileName(r)));
            WriteRootFolder(root, sb);
        }

        private void WriteRootFolder(Folder folder, StringBuilder sb)
        {
            string className = IsCSharp ?
                "projectCS" :
                "projectVB";
            sb.AppendFormat(
                "<div id=\"rootFolder\" class=\"{0}\">{1}</div>",
                className,
                folder.Name);
            sb.AppendLine("<div>");

            WriteReferences(sb);
            WriteUsedBy(sb);

            WriteFolders(folder, sb);
            WriteDocuments(folder, sb);
            sb.AppendLine("</div>");
        }

        private void WriteReferences(StringBuilder sb)
        {
            var references = projectContents.References;
            WriteReferencesCore(sb, references.Select(r => r.DisplayName ?? r.ProjectId), "References");
        }

        private void WriteUsedBy(StringBuilder sb)
        {
            var trimmed = referencingProjects.Take(100).ToArray();
            var totalCount = referencingProjects.Count();
            var title = $"Used By ({totalCount})";
            if (trimmed.Length < totalCount)
            {
                title = $"Used By (displaying {trimmed.Length} of {totalCount})";
            }

            WriteReferencesCore(sb, trimmed, title);
        }

        private void WriteReferencesCore(StringBuilder sb, IEnumerable<string> references, string title)
        {
            if (references == null || !references.Any())
            {
                return;
            }

            WriteFolderName(title, sb, folderIcon: "192.png");
            WriteFolderHeader(sb);

            foreach (var reference in references)
            {
                string url = "/#" + reference;
                sb.AppendLine($"<a class=\"reference\" href=\"{url}\" onclick=\"LoadProjectExplorer('{reference}');return false;\">{reference}</a>");
            }

            sb.AppendLine("</div>");
        }

        private void WriteFolder(Folder folder, StringBuilder sb)
        {
            WriteFolderName(folder.Name, sb);
            WriteFolderHeader(sb);
            WriteFolders(folder, sb);
            WriteDocuments(folder, sb);
            sb.AppendLine("</div>");
        }

        private static void WriteFolderHeader(StringBuilder sb)
        {
            sb.AppendLine("<div class=\"folder\" style=\"display: none;\">");
        }

        private void WriteFolders(Folder folder, StringBuilder sb)
        {
            if (folder.Folders != null)
            {
                foreach (var subfolder in folder.Folders.Values)
                {
                    WriteFolder(subfolder, sb);
                }
            }
        }

        private void WriteDocuments(Folder folder, StringBuilder sb)
        {
            if (folder.Items != null)
            {
                foreach (var document in folder.Items)
                {
                    WriteDocument(folder, document, sb);
                }
            }
        }

        private void WriteFolderName(string folderName, StringBuilder sb, string folderIcon = "202.png")
        {
            sb.Append($"<div class=\"folderTitle\" onclick=\"ToggleExpandCollapse(this);ToggleFolderIcon(this);\" style=\"background-image:url('../../content/images/plus.png');\"><img src=\"../../content/icons/{folderIcon}\" class=\"imageFolder\" />{folderName}</div>");
        }

        private static readonly HashSet<string> wellKnownExtensions = new HashSet<string>(
            new[] { "cs", "vb", "csproj", "vbproj", "xml", "xaml" }, StringComparer.OrdinalIgnoreCase);

        private void WriteDocument(Folder folder, string document, StringBuilder sb)
        {
            var extension = PathUtilities.GetExtension(document) ?? "";
            if (extension.StartsWith("."))
            {
                extension = extension.Substring(1);
            }

            if (!wellKnownExtensions.Contains(extension))
            {
                extension = "genericfile";
            }

            var classname = extension;
            var fileName = PathUtilities.GetFileName(document);
            var url = $"/?leftProject={projectContents.Id}&file={HttpUtility.UrlEncode(document)}";
            sb.Append(
                $"<a href=\"{url}\" class=\"{extension}\" onclick=\"LoadSourceCode('{projectContents.Id}', '{HttpUtility.JavaScriptStringEncode(document)}'); return false;\">{fileName}</a>");
            sb.AppendLine();
        }

        private void AddDocumentToFolder(Folder folder, string document, string[] subfolders)
        {
            if (subfolders == null || subfolders.Length == 0)
            {
                folder.Add(document);
                return;
            }

            if (subfolders[0].EndsWith(":"))
            {
                return;
            }

            var folderName = Paths.SanitizeFolder(subfolders[0]);
            Folder subfolder = folder.GetOrCreateFolder(folderName);
            AddDocumentToFolder(subfolder, document, subfolders.Skip(1).ToArray());
        }
    }
}