using System.Collections.Generic;
using System.Linq;
using Codex.ObjectModel;
using Codex.Utilities;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Language.Xml;

namespace Codex.Analysis
{
    public static class XmlSyntaxExtensions
    {
        public static IEnumerable<IXmlElement> Elements(this IXmlElement element, string name)
        {
            return element.Elements.Where(el => el.Name == name);
        }

        public static XmlNameSyntax NameNode(this IXmlElement element)
        {
            return element.AsSyntaxElement.Name;
        }

        public static SyntaxNode ValueNode(this IXmlElement element)
        {
            return element.As<XmlElementSyntax>()?.Content;
        }

        public static XmlAttributeSyntax Attribute(this IXmlElement element, string name)
        {
            return element.AsSyntaxElement.Attributes.Where(att => att.Name == name).FirstOrDefault();
        }

        public static int End(this XmlNodeSyntax node)
        {
            return node.Start + node.FullWidth;
        }

        public static T As<T>(this IXmlElement node) where T : XmlNodeSyntax
        {
            return node as T;
        }

        public static T As<T>(this XmlNodeSyntax node) where T : XmlNodeSyntax
        {
            return node as T;
        }

        public static void AddAttributeValueReferences(
            this BoundSourceFileBuilder binder,
            XmlAttributeSyntax attribute,
            params ReferenceSymbol[] references)
        {
            var node = attribute?.ValueNode.As<XmlStringSyntax>()?.TextTokens.Node;
            if (node != null)
            {
                binder.AnnotateReferences(node.Start, node.FullWidth, references);
            }
        }

        public static void AddAttributeNameReferences(
            this BoundSourceFileBuilder binder,
            XmlAttributeSyntax attribute,
            params ReferenceSymbol[] references)
        {
            var node = attribute?.NameNode;
            if (node != null)
            {
                binder.AnnotateReferences(node.Start, node.FullWidth, references);
            }
        }

        public static void AddAttributeValueDefinition(
            this BoundSourceFileBuilder binder,
            XmlAttributeSyntax attribute,
            DefinitionSymbol definition)
        {
            var node = attribute?.ValueNode.As<XmlStringSyntax>()?.TextTokens.Node;
            if (node != null)
            {
                binder.AnnotateDefinition(node.Start, node.FullWidth, definition);
            }
        }

        public static void AddElementNameReferences(
            this BoundSourceFileBuilder binder,
            IXmlElement element,
            params ReferenceSymbol[] references)
        {
            var nameNode = element.NameNode();
            binder.AnnotateReferences(nameNode.Start, nameNode.FullWidth - nameNode.GetTrailingTriviaWidth(), references);
        }
    }
}