using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using Codex.ObjectModel;

namespace Codex.Storage.Utilities
{
    public static class FullTextUtilities
    {
        public const char StartOfLineSpecifierChar = '\u0011';
        public const char EndOfLineSpecifierChar = '\u0012';
        public const char HighlightStartTagChar = '\u0001';
        public const string HighlightStartTagCharString = "\u0001";
        public const char HighlightEndTagChar = '\u0002';
        public const string HighlightEndTagCharString = "\u0002";
        private const string HighlightStartTag = "<em>";
        private const string HighlightEndTag = "</em>";
        private static readonly char[] NewLineChars = new[] { '\n', '\r' };

        public static string Capitalize(this string s)
        {
            StringBuilder sb = new StringBuilder();

            bool lastCharacterWasSeparator = true;
            foreach (var character in s)
            {
                var processedCharacter = character;

                if (character == '.')
                {
                    lastCharacterWasSeparator = true;
                }
                else if (lastCharacterWasSeparator)
                {
                    processedCharacter = char.ToUpperInvariant(character);
                    lastCharacterWasSeparator = false;
                }

                sb.Append(processedCharacter);
            }

            return sb.ToString();
        }

        public static string EncodeLineSpecifier(string lineSpecifier)
        {
            return StartOfLineSpecifierChar + lineSpecifier + EndOfLineSpecifierChar;
        }

        public static IEnumerable<SymbolSpan> ParseHighlightSpans(string highlight)
        {
            highlight = highlight.Replace(HighlightStartTag, HighlightStartTagCharString);
            highlight = highlight.Replace(HighlightEndTag, HighlightEndTagCharString);
            highlight += "\n";

            List<SymbolSpan> spans = new List<SymbolSpan>(1);
            StringBuilder builder = new StringBuilder();
            SymbolSpan currentSpan = new SymbolSpan();

            for (int i = 0; i < highlight.Length; i++)
            {
                var ch = highlight[i];
                switch (ch)
                {
                    case StartOfLineSpecifierChar:
                        var endOfLineSpecifierIndex = highlight.IndexOf(EndOfLineSpecifierChar, i + 1);
                        if (endOfLineSpecifierIndex >= 0)
                        {
                            int lineNumber = 0;
                            var lineNumberString = highlight.Substring(i + 1, endOfLineSpecifierIndex - (i + 1));
                            if (int.TryParse(lineNumberString, out lineNumber))
                            {
                                currentSpan.LineNumber = lineNumber;
                            }

                            i = endOfLineSpecifierIndex;
                        }
                        else
                        {
                            i = highlight.Length;
                        }

                        continue;
                    case HighlightStartTagChar:
                        if (currentSpan.Length == 0)
                        {
                            currentSpan.LineSpanStart = builder.Length;
                        }

                        break;
                    case HighlightEndTagChar:
                        currentSpan.Length = (builder.Length - currentSpan.LineSpanStart);
                        break;
                    case EndOfLineSpecifierChar:
                        // This is only encountered if this character appears before
                        // a start of line specifier character. Truncate in that case.
                        builder.Clear();
                        break;
                    case '\r':
                        // Just skip carriage return.
                        break;
                    case '\n':
                        if (spans.Count != 0)
                        {
                            var priorSpan = spans[spans.Count - 1];
                            if (currentSpan.LineNumber != 0)
                            {
                                priorSpan.LineNumber = currentSpan.LineNumber - 1;
                            }
                            else
                            {
                                currentSpan.LineNumber = priorSpan.LineNumber + 1;
                            }
                        }

                        currentSpan.LineSpanText = builder.ToString();
                        currentSpan.LineSpanText = currentSpan.LineSpanText.Trim();
                        spans.Add(currentSpan);
                        currentSpan = new SymbolSpan();
                        builder.Clear();
                        break;
                    default:
                        if (char.IsWhiteSpace(ch) && builder.Length == 0)
                        {
                            currentSpan.LineOffset++;

                            // Skip leading whitespace
                            continue;
                        }

                        builder.Append(ch);
                        break;
                }
            }

            return spans.Where(s => s.Length != 0);
        }

        public static SymbolSpan ParseHighlightSpan(string highlight)
        {
            return ParseHighlightSpan(highlight, new StringBuilder());
        }

        private static SymbolSpan ParseHighlightSpan(string highlight, StringBuilder builder)
        {
            builder.Clear();
            int lineNumber = -1;
            int lineStart = highlight.IndexOf(HighlightStartTag);

            for (int i = 0; i < highlight.Length; i++)
            {
                if (highlight[i] == StartOfLineSpecifierChar)
                {
                    int lineSpecifierStartIndex = i + 1;
                    int lineSpecifierLength = 0;
                    for (i = i + 1; i < highlight.Length; i++, lineSpecifierLength++)
                    {
                        if (highlight[i] == EndOfLineSpecifierChar)
                        {
                            if (lineNumber == -1 || i < lineStart)
                            {
                                if (!int.TryParse(highlight.Substring(lineSpecifierStartIndex, lineSpecifierLength), out lineNumber))
                                {
                                    lineNumber = -1;
                                }
                            }
                            break;
                        }
                    }
                }
                else
                {
                    builder.Append(highlight[i]);
                }
            }

            highlight = builder.ToString();
            lineStart = highlight.IndexOf(HighlightStartTag);

            int lastNewLineBeforeStartTag = highlight.LastIndexOfAny(NewLineChars, lineStart) + 1;
            lineStart -= lastNewLineBeforeStartTag;
            highlight = highlight.Substring(lastNewLineBeforeStartTag);

            int length = highlight.LastIndexOf(HighlightEndTag) - lineStart;
            var lineSpanText = highlight.Replace(HighlightStartTag, string.Empty).Replace(HighlightEndTag, string.Empty);
            length -= ((highlight.Length - lineSpanText.Length) - HighlightEndTag.Length);

            var firstNewLineAfterEndTag = lineSpanText.IndexOfAny(NewLineChars, lineStart + length);
            if (firstNewLineAfterEndTag > 0)
            {
                lineSpanText = lineSpanText.Substring(0, firstNewLineAfterEndTag);
            }

            return new SymbolSpan()
            {
                LineNumber = lineNumber >= 0 ? lineNumber : 0,
                LineSpanText = lineSpanText,
                LineSpanStart = lineStart,
                Length = length,
            };
        }

        public static string DecodeFullTextString(string str)
        {
            if (string.IsNullOrEmpty(str))
            {
                return str;
            }

            var stringBuilder = new StringBuilder();
            for (int i = 0; i < str.Length; i++)
            {
                if (str[i] == StartOfLineSpecifierChar)
                {
                    for (i = i + 1; i < str.Length; i++)
                    {
                        if (str[i] == EndOfLineSpecifierChar)
                        {
                            break;
                        }
                    }
                }
                else
                {
                    stringBuilder.Append(str[i]);
                }
            }

            return stringBuilder.ToString();
        }

        public static string EncodeFullTextString(string str)
        {
            if (string.IsNullOrEmpty(str))
            {
                return str;
            }

            int lineNumber = 0;
            int lineEncodingDistance = 0;
            var stringBuilder = new StringBuilder();
            EncodeAndIncrementLineNumber(ref lineNumber, stringBuilder);
            for (int i = 0; i < str.Length; i++, lineEncodingDistance++)
            {
                stringBuilder.Append(str[i]);
                if (str[i] == '\n')
                {
                    EncodeAndIncrementLineNumber(ref lineNumber, stringBuilder);
                    lineEncodingDistance = 0;
                }
                else if (lineEncodingDistance > 40 && char.IsWhiteSpace(str[i]))
                {
                    EncodeLineNumber(lineNumber - 1, stringBuilder);
                    lineEncodingDistance = 0;
                }
            }

            return stringBuilder.ToString();
        }

        private static void EncodeAndIncrementLineNumber(ref int lineNumber, StringBuilder stringBuilder)
        {
            EncodeLineNumber(lineNumber, stringBuilder);
            lineNumber++;
        }

        private static void EncodeLineNumber(int lineNumber, StringBuilder stringBuilder)
        {
            stringBuilder.Append(StartOfLineSpecifierChar);
            stringBuilder.Append(lineNumber);
            stringBuilder.Append(EndOfLineSpecifierChar);
        }
    }
}