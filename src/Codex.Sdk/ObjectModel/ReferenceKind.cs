namespace Codex.ObjectModel
{
    /// <summary>
    /// Defines standard set of reference kinds
    /// </summary>
    public enum ReferenceKind
    {
        Definition,

        /// <summary>
        /// This represents a constructor declaration for the given type. This is different than
        /// instantiation which actually represents a call to the constructor
        /// </summary>
        Constructor,

        /// <summary>
        /// A call to the constructor of the type referenced by the symbol. This is different than
        /// constructor which is the actual declaration for a constructor for the type symbol.
        /// </summary>
        Instantiation,

        DerivedType,
        InterfaceInheritance,
        InterfaceImplementation,
        Override,
        InterfaceMemberImplementation,

        Write,
        Read,
        GuidUsage,
        UsingDispose,

        EmptyArrayAllocation,
        MSBuildPropertyAssignment,
        MSBuildPropertyUsage,
        MSBuildItemAssignment,
        MSBuildItemUsage,
        MSBuildTargetDeclaration,
        MSBuildTargetUsage,
        MSBuildTaskDeclaration,
        MSBuildTaskUsage,

        Text, // full-text-search result
        
        ProjectLevelReference,

        /// <summary>
        /// Catch-all reference comes after more specific reference kinds
        /// </summary>
        Reference,
    }
}
