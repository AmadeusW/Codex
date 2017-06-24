using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Codex.Types
{
    class SearchTypes
    {
        public SearchType DefinitionSearch = SearchType.Create<IDefinitionSearchModel>()
            .CopyTo(ds => ds.Definition.Modifiers, ds => ds.Keywords)
            .CopyTo(ds => ds.Definition.Kind, ds => ds.Keywords)
            .CopyTo(ds => ds.Language, ds => ds.Keywords)
            .CopyTo(ds => ds.ProjectId, ds => ds.PrefixTerms);

        public SearchType ReferenceSearch = SearchType.Create<IReferenceSearchModel>()
            .CopyTo(rs => rs.References.First().Symbol.Kind, rs => rs.Symbol.Kind)
            .CopyTo(rs => rs.References.First().Symbol, rs => rs.Symbol);
    }

    public class SearchType
    {
        public static SearchType<T> Create<T>([CallerMemberName]string name = null)
        {
            return new SearchType<T>();
        }
    }

    public class SearchType<TSearchType> : SearchType
    {
        public SearchType<TSearchType> Inherit<TPRovider, T>(
            Expression<Func<TPRovider, T>> providerField,
            Expression<Func<TSearchType, T>> searchField)
        {
            return this;
        }

        public SearchType<TSearchType> CopyTo(
            Expression<Func<TSearchType, object>> sourceField,
            Expression<Func<TSearchType, object>> targetField)
        {
            return this;
        }

        public SearchType<TSearchType> SearchAs<T>(
            Expression<Func<TSearchType, T>> searchField,
            SearchBehavior behavior)
        {
            return this;
        }
    }

    public interface IDefinitionSearchModel : IFileScopeEntity
    {
        [Inline]
        IDefinitionSymbol Definition { get; }

        [SearchBehavior(SearchBehavior.Prefix)]
        string PrefixTerms { get; }

        [SearchBehavior(SearchBehavior.NormalizedKeyword)]
        string Keywords { get; }
    }

    public interface IReferenceSearchModel : IFileScopeEntity
    {
        [Inline]
        IReferenceSymbol Symbol { get; }

        [SearchBehavior(SearchBehavior.None)]
        IReadOnlyList<IReferenceSpan> References { get; }
    }

    public interface ISourceSearchModel : IFileScopeEntity
    {
        [Inline]
        IBoundSourceFile File { get; }
    }

    public interface IRepositorySearchModel : IRepoScopeEntity
    {
        [Inline]
        IRepository Repository { get; }
    }

    public interface IProjectSearchModel : IProjectScopeEntity
    {
        [Inline]
        IProject Project { get; }
    }

    public interface IProjectReferenceSearchModel : IProjectScopeEntity
    {
        [Inline]
        IProjectReference ProjectReference { get; }
    }
}

