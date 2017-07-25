using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Codex.Web.Monaco.Models
{
    public class Span
    {
        public int lineNumber { get; set; }
        public int column { get; set; }
        public int length { get; set; }
    }

    public class ResultModel
    {
        public string url { get; set; }
        public string symbolId { get; set; }
        public string projectId { get; set; }
    }

    public class SourceFileContentsModel
    {
        public string contents { get; set; }

        public Span position { get; set; }

        public string view { get; set; }
    }
}