using System;
using Nest;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Codex.Storage.DataModel
{
    public class VersionedSearchModelBase
    {
        [DataString]
        public string FileMergeId { get; set; }

        [Sortword]
        public string FilePath { get; set; }

        [Sortword]
        public string Language { get; set; }
    }
}