using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex
{
    public interface ILanguage : IContentAddressable
    {
        string DisplayName { get; }

        IReadOnlyList<IClassificationType> ClassificationTypes { get; }

        IReadOnlyList<IReferenceType> ReferenceTypes { get; }
    }

    // TODO: Maybe use this to establish groups in UI for find all references
    public interface IReferenceType
    {
        string Name { get; }

        string DisplayName { get; }
    }

    public interface IClassificationType
    {
        string Name { get; }

        int DefaultClassificationColor { get; }
    }
}
