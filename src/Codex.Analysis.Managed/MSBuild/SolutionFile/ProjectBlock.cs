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
    public sealed partial class ProjectBlock
    {
        public readonly ParsedValue<Guid> ParsedProjectTypeGuid;
        public readonly ParsedValue<string> ParsedProjectName;
        public readonly ParsedValue<string> ParsedProjectPath;
        public readonly ParsedValue<Guid> ParsedProjectGuid;
        private readonly IEnumerable<SectionBlock> _projectSections;

        public ProjectBlock(ParsedValue<Guid> projectTypeGuid, ParsedValue<string> projectName, ParsedValue<string> projectPath, ParsedValue<Guid> projectGuid, IEnumerable<SectionBlock> projectSections)
        {
            if (string.IsNullOrEmpty(projectName.Value))
            {
                throw new ArgumentException(string.Format(WorkspacesResources.StringIsNullOrEmpty, "projectName"));
            }

            if (string.IsNullOrEmpty(projectPath.Value))
            {
                throw new ArgumentException(string.Format(WorkspacesResources.StringIsNullOrEmpty, "projectPath"));
            }

            ParsedProjectTypeGuid = projectTypeGuid;
            ParsedProjectName = projectName;
            ParsedProjectPath = projectPath;
            ParsedProjectGuid = projectGuid;
            _projectSections = projectSections.ToList().AsReadOnly();
        }

        public Guid ProjectTypeGuid
        {
            get { return ParsedProjectTypeGuid.Value; }
        }

        public string ProjectName
        {
            get { return ParsedProjectName.Value; }
        }

        public string ProjectPath
        {
            get { return ParsedProjectPath.Value; }
        }

        public Guid ProjectGuid
        {
            get { return ParsedProjectGuid.Value; }
        }

        public IEnumerable<SectionBlock> ProjectSections
        {
            get { return _projectSections; }
        }

        internal string GetText()
        {
            var builder = new StringBuilder();

            builder.AppendFormat("Project(\"{0}\") = \"{1}\", \"{2}\", \"{3}\"", ProjectTypeGuid.ToString("B").ToUpper(), ProjectName, ProjectPath, ProjectGuid.ToString("B").ToUpper());
            builder.AppendLine();

            foreach (var block in _projectSections)
            {
                builder.Append(block.GetText(indent: 1));
            }

            builder.AppendLine("EndProject");

            return builder.ToString();
        }

        internal static ProjectBlock Parse(SourceTextReader reader)
        {
            var startLine = reader.ReadLine().TrimStart();
            var scanner = new LineScanner(startLine);

            if (scanner.ReadUpToAndEat("(\"") != "Project")
            {
                throw new Exception(string.Format(WorkspacesResources.InvalidProjectBlockInSolutionFile4, "Project"));
            }

            var projectTypeGuid = scanner.ReadUpToAndEat("\")").Parse(s => Guid.Parse(s));

            // Read chars upto next quote, must contain "=" with optional leading/trailing whitespaces.
            if (scanner.ReadUpToAndEat("\"").Trim() != "=")
            {
                throw new Exception(WorkspacesResources.InvalidProjectBlockInSolutionFile);
            }

            var projectName = scanner.ReadUpToAndEat("\"");

            // Read chars upto next quote, must contain "," with optional leading/trailing whitespaces.
            if (scanner.ReadUpToAndEat("\"").Trim() != ",")
            {
                throw new Exception(WorkspacesResources.InvalidProjectBlockInSolutionFile2);
            }

            var projectPath = scanner.ReadUpToAndEat("\"");

            // Read chars upto next quote, must contain "," with optional leading/trailing whitespaces.
            if (scanner.ReadUpToAndEat("\"").Trim() != ",")
            {
                throw new Exception(WorkspacesResources.InvalidProjectBlockInSolutionFile3);
            }

            var projectGuid = scanner.ReadUpToAndEat("\"").Parse(s => Guid.Parse(s));

            var projectSections = new List<SectionBlock>();

            while (char.IsWhiteSpace((char)reader.Peek()))
            {
                projectSections.Add(SectionBlock.Parse(reader));
            }

            // Expect to see "EndProject" but be tolerant with missing tags as in Dev12.
            // Instead, we may see either P' for "Project" or 'G' for "Global", which will be handled next.
            if (reader.Peek() != 'P' && reader.Peek() != 'G')
            {
                if (reader.ReadLine() != "EndProject")
                {
                    throw new Exception(string.Format(WorkspacesResources.InvalidProjectBlockInSolutionFile4, "EndProject"));
                }
            }

            return new ProjectBlock(projectTypeGuid, projectName.Parse(), projectPath.Parse(), projectGuid, projectSections);
        }
    }
}
