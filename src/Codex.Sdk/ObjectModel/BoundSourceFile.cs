using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Contracts;

namespace Codex.ObjectModel
{
    public class BoundSourceFile
    {
        /// <summary>
        /// The unique identifier for the file
        /// NOTE: This is not applicable to most files. Only set for files
        /// which are added in a shared context and need this for deduplication)
        /// </summary>
        public string Uid { get; set; }

        public string ProjectId { get; set; }

        public ISourceFile SourceFile { get; set; }

        private int? m_referenceCount;
        public int ReferenceCount
        {
            get
            {
                return References.Count;
            }
            set
            {
                m_referenceCount = value;
            }
        }

        private int? m_definitionCount;
        public int DefinitionCount
        {
            get
            {
                return m_definitionCount ?? Definitions.Count;
            }
            set
            {
                m_definitionCount = value;
            }
        }

        public int Lines;

        public bool ExcludeFromSearch { get; set; }

        /// <summary>
        /// Classifications for the document. Sorted by start index. No overlap.
        /// </summary>
        public IReadOnlyList<ClassificationSpan> ClassificationSpans { get; set; } = new List<ClassificationSpan>();

        /// <summary>
        /// References for the document. Sorted. May overlap.
        /// </summary>
        public IReadOnlyList<ReferenceSpan> References { get; set; } = new List<ReferenceSpan>();

        /// <summary>
        /// Definitions for the document. Sorted. No overlap?
        /// </summary>
        public List<DefinitionSpan> Definitions { get; set; } = new List<DefinitionSpan>();

        public BoundSourceFile(ISourceFile sourceFile)
        {
            Contract.Requires(sourceFile != null);
            SourceFile = sourceFile;
        }

        public BoundSourceFile()
        { }

        public void MakeSingleton()
        {
            Uid = SymbolId.CreateFromId($"{ProjectId}|{SourceFile.Info.Path}").Value;
        }
    }
}