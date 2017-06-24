using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Text;

namespace Codex.Utilities
{
    /// <summary>
    /// Represents a parsed value and its corresponding span
    /// </summary>
    public struct ParsedValue<T>
    {
        public T Value { get; }

        public TextSpan Span { get; }

        public ParsedValue(T value, TextSpan span)
        {
            Value = value;
            Span = span;
        }
    }

    public static class ParsedValueExtensions
    {
        public static ParsedValue<T> Parse<T>(this SubText text, Func<string, T> parse)
        {
            return new ParsedValue<T>(parse(text.ToString()), text.UnderlyingSpan);
        }

        public static ParsedValue<string> Parse(this SubText text)
        {
            return Parse(text, s => s);
        }
    }
}
