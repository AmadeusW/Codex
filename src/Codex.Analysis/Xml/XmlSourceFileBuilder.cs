using System.Collections.Generic;
using System.Linq;
using Codex.ObjectModel;
using Codex.Utilities;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Language.Xml;

namespace Codex.Analysis
{
    public class XmlSourceFileBuilder : BoundSourceFileBuilder
    {
        public XmlDocumentSyntax Document { get; }

        private List<KeyValuePair<int, IXmlElement>> sortedElementList;
        public List<KeyValuePair<int, IXmlElement>> SortedElementList
        {
            get
            {
                if (sortedElementList == null)
                {
                    sortedElementList = CreateSortedElementList();
                }

                return sortedElementList;
            }
        }

        private static readonly IComparer<KeyValuePair<int, IXmlElement>> Comparer
            = Comparer<KeyValuePair<int, IXmlElement>>.Create((e1, e2) => e1.Key.CompareTo(e2.Key));

        public XmlSourceFileBuilder(SourceFile sourceFile, string projectId)
            : base(sourceFile, projectId)
        {
            Document = Parser.ParseText(sourceFile.Content);
        }

        public IXmlElement TryGetElement(int lineNumber, int column)
        {
            var position = SourceText.Lines.GetPosition(new LinePosition(lineNumber, column));

            var kvp = new KeyValuePair<int, IXmlElement>(
                position, null);

            var index = SortedElementList.BinarySearch(kvp, Comparer);
            if (index < 0)
            {
                index = ~index - 1;
            }

            if (index < 0 || index >= SortedElementList.Count)
            {
                return null;
            }

            var result = SortedElementList[index].Value;

            return result;
        }

        private int GetContainingElementMin(int position, IXmlElement element)
        {
            var node = element.As<XmlNodeSyntax>();
            if (position == node.Start)
            {
                return RangeHelper.FirstEqualSecond;
            }
            else if (position < node.Start)
            {
                return RangeHelper.FirstLessThanSecond;
            }
            else if (position > (node.Start + node.FullWidth))
            {
                return RangeHelper.FirstGreaterThanSecond;
            }
            else
            {
                return RangeHelper.FirstGreaterThanSecond;
            }
        }

        private List<KeyValuePair<int, IXmlElement>> CreateSortedElementList()
        {
            return Descendants(Document.Root).ToList();
        }

        private IEnumerable<KeyValuePair<int, IXmlElement>> Descendants(IXmlElement element)
        {
            yield return new KeyValuePair<int, IXmlElement>(
                element.As<XmlNodeSyntax>().Start,
                element);

            foreach (var child in element.Elements)
            {
                foreach (var descendant in Descendants(child))
                {
                    yield return descendant;
                }
            }
        }
    }
}