using System;
using System.Linq;
using System.Threading;
using Codex.Utilities;
using Microsoft.CodeAnalysis;

using CS = Microsoft.CodeAnalysis.CSharp;
using VB = Microsoft.CodeAnalysis.VisualBasic;

namespace Codex.Analysis
{
    public class CompilationServices
    {
        public Lazy<IMethodSymbol> IDisposable_Dispose;
        public Compilation Compilation { get; }

        public CompilationServices(Compilation compilation)
        {
            Compilation = compilation;
            IDisposable_Dispose = new Lazy<IMethodSymbol>(() => compilation.GetTypeByMetadataName(typeof(IDisposable).FullName)?.GetMembers()
                .Where(s => s.Kind == SymbolKind.Method)
                .OfType<IMethodSymbol>()
                .Where(m => m.Name == nameof(IDisposable.Dispose))
                .FirstOrDefault());
        }

    }
}
