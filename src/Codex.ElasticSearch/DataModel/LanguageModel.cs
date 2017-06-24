using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Nest;
using System;
using System.Linq;

namespace Codex.Storage.DataModel
{
    public class LanguageModel
    {
        [Keyword]
        public string Id { get; set; }

        [Keyword]
        public string Name { get; set; }

        /// <summary>
        /// The reference kinds
        /// </summary>
        public List<string> ReferenceKinds { get; set; }
    }
}