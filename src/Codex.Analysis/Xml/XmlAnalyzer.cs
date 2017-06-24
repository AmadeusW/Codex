using Microsoft.Language.Xml;

namespace Codex.Analysis
{
    public static class XmlAnalyzer
    {
        private static string[] ClassificationTypeNamesLookup = new string[]
        {
            "text",
            ClassificationTypeNames.XmlAttributeName,
            ClassificationTypeNames.XmlAttributeQuotes,
            ClassificationTypeNames.XmlAttributeValue,
            ClassificationTypeNames.XmlCDataSection,
            ClassificationTypeNames.XmlComment,
            ClassificationTypeNames.XmlDelimiter,
            ClassificationTypeNames.XmlEntityReference,
            ClassificationTypeNames.XmlName,
            ClassificationTypeNames.XmlProcessingInstruction,
            ClassificationTypeNames.XmlText,
        };

        public static XmlDocumentSyntax Analyze(BoundSourceFileBuilder binder)
        {
            var text = binder.SourceFile.Content;
            XmlDocumentSyntax parsedXml = Parser.ParseText(text);
            Classify(binder, parsedXml);
            return parsedXml;
        }

        private static void Classify(BoundSourceFileBuilder binder, XmlDocumentSyntax parsedXml)
        {
            ClassifierVisitor.Visit(parsedXml, 0, parsedXml.FullWidth, (start, length, node, classification) =>
            {
                var leadingTriviaWidth = node.GetLeadingTriviaWidth();
                var trailingTriviaWidth = node.GetTrailingTriviaWidth();
                start += leadingTriviaWidth;
                length -= (leadingTriviaWidth + trailingTriviaWidth);

                binder.AnnotateClassification(start, length, ClassificationTypeNamesLookup[(int)classification]);
            });
        }
    }
}
