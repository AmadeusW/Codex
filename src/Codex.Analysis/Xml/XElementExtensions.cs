using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;
using Codex.ObjectModel;
using Codex.Utilities;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Language.Xml;

namespace Codex.Analysis.Xml.Linq
{
    internal static class XElementExtensions
    {
        private const char LessThanAlt = (char)0x02C2;
        private const char GreaterThanAlt = (char)0x02C3;

        public static XElement Element(string name)
        {
            return new XElement(name);
        }

        public static XElement ForEach<T>(this XElement element, IEnumerable<T> collection, Action<XElement, T> invoke)
        {
            foreach (var item in collection)
            {
                invoke(element, item);
            }

            return element;
        }

        public static XElement AddAttribute(this XElement element, string name, object value, XmlSemanticAnnotation annotation = null, bool annotateName = false)
        {
            var attribute = element.Attribute(name);

            value = value.ToString().Replace('<', LessThanAlt).Replace('>', GreaterThanAlt);

            if (attribute == null)
            {
                attribute = new XAttribute(name, value);
                element.Add(attribute);
            }

            if (annotation != null)
            {
                annotation.AppliesToName = annotateName;
                attribute.AddAnnotation(annotation);
            }

            return element;
        }

        public static XElement AddElement(this XElement element, string name, Action<XElement> childModifier)
        {
            CreateChild(element, name, childModifier);

            return element;
        }

        public static XElement CreateChild(this XElement element, string name, Action<XElement> childModifier)
        {
            var child = new XElement(name);
            element.Add(child);
            childModifier(child);
            return child;
        }

        public static XElement AddElement(this XElement element, string name, object value, XmlSemanticAnnotation annotation, bool annotateName = false)
        {
            annotation.AppliesToName = annotateName;
            var child = new XElement(name);
            element.Add(child);

            child.AddAnnotation(annotation);

            return element;
        }

        public static XmlSourceFileBuilder CreateAnnotatedSourceBuilder(this XElement sourceElement, SourceFileInfo info, string projectId)
        {
            var sourceFile = new SourceFile()
            {
                Content = sourceElement.ToString(),
                Info = info
            };

            var binder = new XmlSourceFileBuilder(sourceFile, projectId);

            var elementList = sourceElement.DescendantsAndSelf().ToList();
            var sortedElementList = binder.SortedElementList;
            Debug.Assert(elementList.Count == sortedElementList.Count);

            List<XmlAttributeSyntax> attributeSyntaxBuffer = new List<XmlAttributeSyntax>();
            List<XAttribute> attributeBuffer = new List<XAttribute>();

            for (int i = 0; i < elementList.Count; i++)
            {
                var element = elementList[i];
                var syntaxElement = sortedElementList[i].Value;

                foreach (var annotation in element.Annotations<XmlSemanticAnnotation>())
                {
                    int start = 0;
                    int length = 0;
                    if (annotation.AppliesToName)
                    {
                        var nameNode = syntaxElement.NameNode();
                        start = nameNode.Start;
                        length = nameNode.FullWidth - nameNode.GetTrailingTriviaWidth();
                    }
                    else
                    {
                        var valueNode = syntaxElement.ValueNode();
                        start = valueNode.Start;
                        length = valueNode.FullWidth;
                    }

                    if (annotation.References != null)
                    {
                        binder.AnnotateReferences(start, length, annotation.References);
                    }
                    else
                    {
                        binder.AnnotateDefinition(start, length, annotation.Definition);
                    }
                }

                attributeSyntaxBuffer.Clear();
                attributeBuffer.Clear();

                attributeSyntaxBuffer.AddRange(syntaxElement.AsSyntaxElement.Attributes);
                attributeBuffer.AddRange(element.Attributes());

                Debug.Assert(attributeSyntaxBuffer.Count == attributeBuffer.Count);

                for (int j = 0; j < attributeSyntaxBuffer.Count; j++)
                {
                    var attribute = attributeBuffer[j];
                    var attributeSyntax = attributeSyntaxBuffer[j];

                    foreach (var annotation in attribute.Annotations<XmlSemanticAnnotation>())
                    {
                        int start = 0;
                        int length = 0;
                        if (annotation.AppliesToName)
                        {
                            var nameNode = attributeSyntax.NameNode;
                            start = nameNode.Start;
                            length = nameNode.FullWidth - nameNode.GetTrailingTriviaWidth();
                        }
                        else
                        {
                            var valueNode = attributeSyntax?.ValueNode.As<XmlStringSyntax>()?.TextTokens.Node;
                            if (valueNode != null)
                            {
                                start = valueNode.Start;
                                length = valueNode.FullWidth;
                            }
                        }

                        if (length != 0)
                        {
                            if (annotation.References != null)
                            {
                                binder.AnnotateReferences(start, length, annotation.References);
                            }
                            else
                            {
                                binder.AnnotateDefinition(start, length, annotation.Definition);
                            }
                        }
                    }
                }
            }

            return binder;
        }

        public class XmlSemanticAnnotation
        {
            public bool AppliesToName;
            public DefinitionSymbol Definition;
            public ReferenceSymbol[] References;

            public static implicit operator XmlSemanticAnnotation(ReferenceSymbol[] value)
            {
                return new XmlSemanticAnnotation() { References = value };
            }

            public static implicit operator XmlSemanticAnnotation(ReferenceSymbol value)
            {
                if (value == null)
                {
                    return null;
                }

                return new XmlSemanticAnnotation() { References = new[] { value } };
            }

            public static implicit operator XmlSemanticAnnotation(DefinitionSymbol value)
            {
                if (value == null)
                {
                    return null;
                }

                return new XmlSemanticAnnotation() { Definition = value };
            }
        }
    }
}