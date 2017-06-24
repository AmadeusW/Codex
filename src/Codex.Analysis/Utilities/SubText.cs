// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Text;

namespace Microsoft.CodeAnalysis.Text
{
    /// <summary>
    /// An <see cref="SourceText"/> that represents a subrange of another <see cref="SourceText"/>.
    /// </summary>
    public sealed class SubText : SourceText
    {
        public SubText(SourceText text, TextSpan span)
            : base(checksumAlgorithm: text.ChecksumAlgorithm)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            if (span.Start < 0
                || span.Start >= text.Length
                || span.End < 0
                || span.End > text.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(span));
            }

            UnderlyingText = text;
            UnderlyingSpan = span;
        }

        public override Encoding Encoding => UnderlyingText.Encoding;

        public SourceText UnderlyingText { get; }

        public TextSpan UnderlyingSpan { get; }

        public override int Length => UnderlyingSpan.Length;

        public override char this[int position]
        {
            get
            {
                if (position < 0 || position > this.Length)
                {
                    throw new ArgumentOutOfRangeException(nameof(position));
                }

                return UnderlyingText[UnderlyingSpan.Start + position];
            }
        }

        public override string ToString(TextSpan span)
        {
            CheckSubSpan(span);

            return UnderlyingText.ToString(GetCompositeSpan(span.Start, span.Length));
        }

        public override SourceText GetSubText(TextSpan span)
        {
            return new SubText(UnderlyingText, GetCompositeSpan(span.Start, span.Length));
        }

        public SubText Substring(TextSpan span)
        {
            CheckSubSpan(span);

            return new SubText(UnderlyingText, GetCompositeSpan(span.Start, span.Length));
        }

        public SubText Substring(int start)
        {
            return Substring(new TextSpan(start, Length - start));
        }

        public SubText Substring(int start, int length)
        {
            return Substring(new TextSpan(start, length));
        }

        public override void CopyTo(int sourceIndex, char[] destination, int destinationIndex, int count)
        {
            var span = GetCompositeSpan(sourceIndex, count);
            UnderlyingText.CopyTo(span.Start, destination, destinationIndex, span.Length);
        }

        private TextSpan GetCompositeSpan(int start, int length)
        {
            int compositeStart = Math.Min(UnderlyingText.Length, UnderlyingSpan.Start + start);
            int compositeEnd = Math.Min(UnderlyingText.Length, compositeStart + length);
            return new TextSpan(compositeStart, compositeEnd - compositeStart);
        }

        internal void CheckSubSpan(TextSpan span)
        {
            if (span.Start < 0 || span.Start > Length || span.End > Length)
            {
                throw new ArgumentOutOfRangeException(nameof(span));
            }
        }

        /// <inheritdoc />
        public static bool operator ==(SubText left, string right)
        {
            return left?.Equals(right) ?? right == null;
        }

        /// <inheritdoc />
        public static bool operator !=(SubText left, string right)
        {
            return !(left?.Equals(right) ?? right == null);
        }

        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        /// <inheritdoc />
        public bool Equals(string other)
        {
            if (other == null)
            {
                return false;
            }

            var length = Length;
            if (length != other.Length)
            {
                return false;
            }

            for (int i = 0; i < length; i++)
            {
                if (this[i] != other[i])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Returns the index of the first occurrence of a string within the segment, or -1 if the string doesn't occur in the segment.
        /// </summary>
        public int IndexOf(string value, int start = 0)
        {
            var length = Length;
            if (value.Length == 0)
            {
                return 0;
            }

            for (int index = start; index < length; index++)
            {
                if (value.Length > (length - index))
                {
                    return -1;
                }

                bool found = true;
                for (int subIndex = 0; subIndex < value.Length; subIndex++)
                {
                    if (this[index + subIndex] != value[subIndex])
                    {
                        found = false;
                        break;
                    }
                }

                if (found)
                {
                    return index;
                }
            }

            return -1;
        }
    }

    public static class SubTextExtensions
    {
        public static SubText TrimEnd(this SubText text)
        {
            return Trim(text, trimStart: false, trimEnd: true);
        }

        public static SubText TrimStart(this SubText text)
        {
            return Trim(text, trimStart: true, trimEnd: false);
        }

        public static SubText Trim(this SubText text, bool trimStart = true, bool trimEnd = true)
        {
            if (text.Length == 0)
            {
                return text;
            }

            int start = 0;
            int length = text.Length;
            int end = text.Length - 1;

            if (trimStart)
            {
                for (start = 0; start < length; start++)
                {
                    if (!char.IsWhiteSpace(text[start]))
                    {
                        break;
                    }
                }
            }

            if (trimEnd)
            {
                for (end = length - 1; end >= start; end--)
                {
                    if (!char.IsWhiteSpace(text[end]))
                    {
                        break;
                    }
                }
            }

            return text.Substring(TextSpan.FromBounds(start, end + 1));
        }
    }
}
