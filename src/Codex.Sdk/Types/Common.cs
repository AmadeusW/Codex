using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex
{
    public interface IRef<T>
    {
        string Id { get; }
    }

    public interface INested<TContainer>
    {

    }

    public interface IContentAddressable
    {
        string Id { get; }
    }
}
