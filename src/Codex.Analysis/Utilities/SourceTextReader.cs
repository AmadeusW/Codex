using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Text;

namespace Codex.Utilities
{
    public class SourceTextReader
    {
        private SourceText _s;
        private int _pos;
        private int _length;
        public SourceText SourceText => _s;
        public int Position => _pos;

        public SourceTextReader(SourceText sourceText)
        {
            _s = sourceText;
            _length = sourceText.Length;
        }

        public int Peek()
        {
            // TODO: Should this be just Position
            var peekPosition = Position;
            if (peekPosition >= SourceText.Length)
            {
                return -1;
            }

            return SourceText[peekPosition];
        }

        private SubText GetSubText(int start, int length)
        {
            return new SubText(_s, new TextSpan(start, length));
        }

        // Copied from 
        // Reads a line. A line is defined as a sequence of characters followed by
        // a carriage return ('\r'), a line feed ('\n'), or a carriage return
        // immediately followed by a line feed. The resulting string does not
        // contain the terminating carriage return and/or line feed. The returned
        // value is null if the end of the underlying string has been reached.
        public SubText ReadLine()
        {
            int i = _pos;
            while (i < _length)
            {
                char ch = _s[i];
                if (ch == '\r' || ch == '\n')
                {
                    var result = GetSubText(_pos, i - _pos);
                    _pos = i + 1;
                    if (ch == '\r' && _pos < _length && _s[_pos] == '\n') _pos++;
                    return result;
                }
                i++;
            }
            if (i > _pos)
            {
                var result = GetSubText(_pos, i - _pos);
                _pos = i;
                return result;
            }

            return null;
        }
    }
}
