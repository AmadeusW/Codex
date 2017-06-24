using System;
using System.Threading;
using Codex.Utilities;
using Microsoft.CodeAnalysis;

using CS = Microsoft.CodeAnalysis.CSharp;
using VB = Microsoft.CodeAnalysis.VisualBasic;

namespace Codex.Analysis
{
    /// <summary>
    /// Reference types from CS.Workspaces and VB.Workspaces to make RAR copy the assemblies
    /// </summary>
    public class DummyReferences
    {
        private CS.Formatting.BinaryOperatorSpacingOptions csbs;
        private Microsoft.CodeAnalysis.Completion.CompletionService cacs;
    }
}