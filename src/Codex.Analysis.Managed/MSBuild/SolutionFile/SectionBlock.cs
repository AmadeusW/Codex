// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Codex.Utilities;
using Microsoft.CodeAnalysis.Text;

namespace Codex.MSBuild
{
    /// <summary>
    /// Represents a SectionBlock in a .sln file. Section blocks are of the form:
    ///
    /// Type(ParenthesizedName) = Value
    ///     Key = Value
    ///     [more keys/values]
    /// EndType
    /// </summary>
    public sealed partial class SectionBlock
    {
        private readonly ParsedValue<string> _type;
        private readonly ParsedValue<string> _parenthesizedName;
        private readonly ParsedValue<string> _value;
        private readonly IEnumerable<KeyValuePair<ParsedValue<string>, ParsedValue<string>>> _keyValuePairs;

        public SectionBlock(ParsedValue<string> type, ParsedValue<string> parenthesizedName, ParsedValue<string> value, IEnumerable<KeyValuePair<ParsedValue<string>, ParsedValue<string>>> keyValuePairs)
        {
            if (string.IsNullOrEmpty(type.Value))
            {
                throw new ArgumentException(string.Format(WorkspacesResources.StringIsNullOrEmpty, "type"));
            }

            if (string.IsNullOrEmpty(parenthesizedName.Value))
            {
                throw new ArgumentException(string.Format(WorkspacesResources.StringIsNullOrEmpty, "parenthesizedName"));
            }

            if (string.IsNullOrEmpty(value.Value))
            {
                throw new ArgumentException(string.Format(WorkspacesResources.StringIsNullOrEmpty, "value"));
            }

            _type = type;
            _parenthesizedName = parenthesizedName;
            _value = value;
            _keyValuePairs = keyValuePairs.ToList().AsReadOnly();
        }

        public string Type
        {
            get { return _type.Value; }
        }

        public string ParenthesizedName
        {
            get { return _parenthesizedName.Value; }
        }

        public string Value
        {
            get { return _value.Value; }
        }

        public IEnumerable<KeyValuePair<string, string>> KeyValuePairs
        {
            get
            {
                foreach (var kvp in _keyValuePairs)
                {
                    yield return new KeyValuePair<string, string>(kvp.Key.Value, kvp.Value.Value);
                }
            }
        }

        internal string GetText(int indent)
        {
            var builder = new StringBuilder();

            builder.Append('\t', indent);
            builder.AppendFormat("{0}({1}) = ", Type, ParenthesizedName);
            builder.AppendLine(Value);

            foreach (var pair in KeyValuePairs)
            {
                builder.Append('\t', indent + 1);
                builder.Append(pair.Key);
                builder.Append(" = ");
                builder.AppendLine(pair.Value);
            }

            builder.Append('\t', indent);
            builder.AppendFormat("End{0}", Type);
            builder.AppendLine();

            return builder.ToString();
        }

        internal static SectionBlock Parse(SourceTextReader reader)
        {
            SubText startLine;
            while ((startLine = reader.ReadLine()) != null)
            {
                startLine = startLine.TrimStart();
                if (startLine != string.Empty)
                {
                    break;
                }
            }

            var scanner = new LineScanner(startLine);

            var type = scanner.ReadUpToAndEat("(");
            var parenthesizedName = scanner.ReadUpToAndEat(") = ");
            var sectionValue = scanner.ReadRest();

            var keyValuePairs = new List<KeyValuePair<ParsedValue<string>, ParsedValue<string>>>();
            SubText line;
            while ((line = reader.ReadLine()) != null)
            {
                line = line.TrimStart();

                // ignore empty lines
                if (line == string.Empty)
                {
                    continue;
                }

                if (line == "End" + type)
                {
                    break;
                }

                scanner = new LineScanner(line);
                var key = scanner.ReadUpToAndEat(" = ");
                var value = scanner.ReadRest();

                keyValuePairs.Add(new KeyValuePair<ParsedValue<string>, ParsedValue<string>>(key.Parse(), value.Parse()));
            }

            return new SectionBlock(type.Parse(), parenthesizedName.Parse(), sectionValue.Parse(), keyValuePairs);
        }
    }
}
