using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Codex.ObjectModel
{
    public class SourceFileInfo
    {
        public string Path { get; set; }
        public int Lines { get; set; }
        public int Size { get; set; }
        public string Language { get; set; }
        public string WebAddress { get; set; }
        public string RepoRelativePath { get; set; }

        /// <summary>
        /// Extensible key value properties for the document
        /// </summary>
        public Dictionary<string, string> Properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public interface ISourceFile
    {
        SourceFileInfo Info { get; }
        Task<string> GetContentsAsync();
    }

    public class EncodedString
    {
        public readonly string Value;
        public readonly Encoding Encoding;

        public EncodedString(string value, Encoding encoding)
        {
            Value = value;
            Encoding = encoding;
        }

        public static implicit operator string(EncodedString value)
        {
            return value.Value;
        }
    }

    public class SourceFile : ISourceFile
    {
        public SourceFileInfo Info { get; set; }
        public string Content { get; set; }

        public Task<string> GetContentsAsync()
        {
            return Task.FromResult(Content);
        }
    }

    public class ReferencedProject
    {
        /// <summary>
        /// The identifier of the referenced project
        /// </summary>
        public string ProjectId { get; set; }

        /// <summary>
        /// The display name of the project
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// The properties of the project
        /// </summary>
        public Dictionary<string, string> Properties { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Used definitions for the project. Sorted.
        /// </summary>
        public List<DefinitionSymbol> Definitions { get; set; } = new List<DefinitionSymbol>();

        public override string ToString()
        {
            return DisplayName ?? ProjectId;
        }
    }

    public class Span
    {
        /// <summary>
        /// The absolute character offset of the span within the document
        /// </summary>
        public int Start { get; set; }

        /// <summary>
        /// The length of the span
        /// </summary>
        public int Length { get; set; }

        /// <summary>
        /// The absolute character offset of the end (exclusive) of the span within the document
        /// </summary>
        public int End => Start + Length;

        public bool Contains(Span span)
        {
            if (span == null)
            {
                return false;
            }

            return span.Start >= Start && span.End <= End;
        }

        public bool SpanEquals(Span span)
        {
            if (span == null)
            {
                return false;
            }

            return span.Start == Start && span.End == End;
        }
    }

    public class ClassificationSpan : Span
    {
        /// <summary>
        /// The identifier to the local group of spans which refer to the same common symbol
        /// </summary>
        public int LocalGroupId { get; set; }

        /// <summary>
        /// The classification identifier for the span
        /// </summary>
        public string Classification { get; set; }

        /// <summary>
        /// The default classification color for the span. This is used for
        /// contexts where a mapping from classification id to color is not
        /// available.
        /// </summary>
        public int DefaultClassificationColor { get; set; } = -1;
    }

    public class SymbolSpan : Span
    {
        /// <summary>
        /// The character position where the span starts in the line text
        /// </summary>
        public int LineNumber { get; set; }

        /// <summary>
        /// The character position where the span starts in the line text
        /// </summary>
        public int LineSpanStart { get; set; }

        public int LineSpanEnd => LineSpanStart + Length;

        /// <summary>
        /// The line text
        /// </summary>
        public string LineSpanText { get; set; }

        /// <summary>
        /// If positive, the offset of the line span from the beginning of the line
        /// If negative, the offset of the linespan from the end of the next line
        /// </summary>
        public int LineOffset { get; set; }

        public void Trim()
        {
            var initialLength = LineSpanText.Length;
            LineSpanText = LineSpanText.TrimStart();
            var newLength = LineSpanText.Length;
            LineSpanStart -= (initialLength - newLength);
            LineSpanText = LineSpanText.TrimEnd();
            LineSpanStart = Math.Max(LineSpanStart, 0);
            Length = Math.Min(LineSpanText.Length, Length);
        }

        public ReferenceSpan CreateReference(ReferenceSymbol referenceSymbol, SymbolId relatedDefinition = default(SymbolId))
        {
            return new ReferenceSpan()
            {
                RelatedDefinition = relatedDefinition,
                Start = Start,
                Length = Length,
                LineNumber = LineNumber,
                LineSpanStart = LineSpanStart,
                LineSpanText = LineSpanText,
                Reference = referenceSymbol
            };
        }

        public DefinitionSpan CreateDefinition(DefinitionSymbol definition)
        {
            return new DefinitionSpan()
            {
                Start = Start,
                Length = Length,
                LineNumber = LineNumber,
                LineSpanStart = LineSpanStart,
                LineSpanText = LineSpanText,
                Definition = definition
            };
        }
    }

    public class ReferenceSpan : SymbolSpan
    {
        /// <summary>
        /// The symbol referenced by the span
        /// </summary>
        public ReferenceSymbol Reference { get; set; }

        /// <summary>
        /// Gets the symbol id of the definition which provides this reference 
        /// (i.e. method definition for interface implementation)
        /// </summary>
        public SymbolId RelatedDefinition { get; set; }
    }

    public class DefinitionSpan : SymbolSpan
    {
        /// <summary>
        /// The definition symbol
        /// </summary>
        public DefinitionSymbol Definition { get; set; }
    }

    /// <summary>
    /// Unique identifier for C# class, field or method.
    /// </summary>
    public class Symbol
    {
        /// <summary>
        /// The identifier of the referenced project
        /// </summary>
        public string ProjectId { get; set; }

        /// <summary>
        /// The identifier for the symbol
        /// </summary>
        public SymbolId Id { get; set; }

        /// <summary>
        /// The symbol kind. (i.e. interface, method, field)
        /// </summary>
        public string Kind { get; set; }

        /// <summary>
        /// Indicates if the symbol should be excluded from the definition/find all references search (by default).
        /// Symbol will only be included if kind is explicitly specified
        /// </summary>
        public bool ExcludeFromDefaultSearch { get; set; }

        /// <summary>
        /// Indicates if the symbol should NEVER be included in the definition/find all references search.
        /// </summary>
        public bool ExcludeFromSearch { get; set; }

        /// <summary>
        /// Indicates the corresponding definitions is implicitly declared and therefore this should not be
        /// used for find all references search
        /// </summary>
        public bool IsImplicitlyDeclared { get; set; }

        /// <summary>
        /// Extension data used during analysis/search
        /// </summary>
        public ExtensionData ExtData { get; set; }

        protected bool Equals(Symbol other)
        {
            return string.Equals(ProjectId, other.ProjectId, StringComparison.Ordinal) && string.Equals(Id.Value, other.Id.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((Symbol)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((ProjectId?.GetHashCode() ?? 0) * 397) ^ (Id.Value?.GetHashCode() ?? 0);
            }
        }

        public override string ToString()
        {
            return Id.Value;
        }
    }

    public class DefinitionSymbol : ReferenceSymbol
    {
        /// <summary>
        /// The unique identifier for the symbol
        /// NOTE: This is not applicable to most symbols. Only set for symbols
        /// which are added in a shared context and need this for deduplication)
        /// </summary>
        public string Uid { get; set; }
        public string DisplayName { get; set; }
        private string shortName;
        public string ShortName
        {
            get
            {
                return shortName ?? "";
            }
            set
            {
                shortName = value;
            }
        }


        public string ContainerQualifiedName { get; set; }

        public Glyph Glyph { get; set; } = Glyph.Unknown;
        public int SymbolDepth { get; set; }

        public int ReferenceCount;

        public DefinitionSymbol()
        {
            ReferenceKind = nameof(ObjectModel.ReferenceKind.Definition);
        }

        public void IncrementReferenceCount()
        {
            Interlocked.Increment(ref ReferenceCount);
        }

        public override string ToString()
        {
            return DisplayName;
        }
    }

    /// <summary>
    ///  Allows defining extension data during analysis
    /// </summary>
    public class ExtensionData
    {
    }

    public class ReferenceSymbol : Symbol
    {
        /// <summary>
        /// The kind of reference. This is used to group references.
        /// (i.e. for C#(aka .NET) MethodOverride, InterfaceMemberImplementation, InterfaceImplementation, etc.)
        /// </summary>
        public string ReferenceKind { get; set; }

        public override string ToString()
        {
            return ReferenceKind + " " + base.ToString();
        }
    }

    public class TextReferenceEntry
    {
        public string ReferringProjectId { get; set; }
        public string ReferringFilePath { get; set; }
        public SymbolSpan ReferringSpan { get; set; }

        public string File => ReferringFilePath;
        public SymbolSpan Span => ReferringSpan;
    }

    public class SymbolReferenceEntry
    {
        public string ReferringProjectId { get; set; }
        public string ReferringFilePath { get; set; }
        public ReferenceSpan ReferringSpan { get; set; }

        public string File => ReferringFilePath;
        public ReferenceSpan Span => ReferringSpan;
    }

    public class SymbolSearchResultEntry
    {
        public DefinitionSymbol Symbol => Span.Definition;
        public DefinitionSpan Span { get; set; }
        public string File { get; set; }
        public string Glyph { get; set; }
        public int Rank { get; set; }
        public int KindRank { get; set; }
        public int MatchLevel { get; set; }
        public string ReferenceKind { get; set; } = nameof(ObjectModel.ReferenceKind.Definition);
        public string DisplayName => Symbol?.DisplayName ?? File;
    }

    public class GetDefinitionResult
    {
        public SourceFileInfo File { get; set; }
        public DefinitionSpan Span { get; set; }
    }

    public class ProjectContents
    {
        public string Id { get; set; }
        public DateTime DateUploaded { get; set; }

        public int SourceLineCount { get; set; }
        public int SymbolCount { get; set; }

        public Dictionary<string, string> Properties { get; set; } = new Dictionary<string, string>();

        public List<ReferencedProject> References { get; set; } = new List<ReferencedProject>();
        public List<SourceFileInfo> Files { get; set; } = new List<SourceFileInfo>();
    }

    public class SymbolSearchResult
    {
        public List<SymbolSearchResultEntry> Entries { get; set; }

        public int Total { get; set; }
        public string QueryText { get; set; }

        public string Error { get; set; }
    }

    public class SymbolReferenceResult
    {
        public List<SymbolReferenceEntry> Entries { get; set; }

        public IList<SymbolSearchResultEntry> RelatedDefinitions { get; set; }

        public int Total { get; set; }
        public string SymbolName { get; set; }
        public string ProjectId { get; set; }
        public string SymbolId { get; set; }
    }
}
