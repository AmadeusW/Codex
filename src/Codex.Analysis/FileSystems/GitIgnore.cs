using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Codex
{
    public class GitIgnore
    {
        public readonly string InclusionPattern;
        public readonly string ExclusionPattern;

        private Regex m_inclusionRegex;
        private Regex m_exclusionRegex;

        public Regex InclusionRegex
        {
            get
            {
                if (m_inclusionRegex == null)
                {
                    m_inclusionRegex = new Regex(InclusionPattern);
                }

                return m_inclusionRegex;
            }
        }

        public Regex ExclusionRegex
        {
            get
            {
                if (m_exclusionRegex == null)
                {
                    m_exclusionRegex = new Regex(ExclusionPattern);
                }

                return m_exclusionRegex;
            }
        }

        public GitIgnore(string exclusionPattern, string inclusionPattern)
        {
            ExclusionPattern = exclusionPattern;
            InclusionPattern = inclusionPattern;
        }

        public bool Includes(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return true;
            }

            path = path.Replace('\\', '/');

            if (path[0] == '/')
            {
                path = path.Substring(1);
            }

            return !ExclusionRegex.IsMatch(path) || InclusionRegex.IsMatch(path);
        }

        public bool Excludes(string path)
        {
            return !Includes(path);
        }

        public static GitIgnore Parse(TextReader reader, bool tfIgnore = false)
        {
            List<string> negatives = new List<string>();
            List<string> positives = new List<string>();

            string line = null;
            while ((line = reader.ReadLine()) != null)
            {
                line = line.Trim();

                if (line.Length == 0 || line[0] == '#')
                {
                    continue;
                }

                var isNegative = line[0] == '!';
                if (isNegative)
                {
                    line = line.Substring(1);
                }

                if (line.Length == 0)
                {
                    continue;
                }

                var list = isNegative ? negatives : positives;
                list.Add(PrepareRegexPattern(line, tfIgnore));
            }

            return new GitIgnore(Combine(positives), Combine(negatives));
        }

        public static GitIgnore Parse(string filePath, bool tfIgnore = false)
        {
            using (StreamReader reader = new StreamReader(filePath))
            {
                return Parse(reader, tfIgnore);
            }
        }

        private static string Combine(List<string> expressions)
        {
            if (expressions.Count == 0)
            {
                return "$^";
            }
            else
            {
                return "^((" + string.Join(")|(", expressions) + "))";
            }
        }

        private static string PrepareRegexPattern(string line, bool tfIgnore)
        {
            if (tfIgnore)
            {
                line = line.Replace("\\", "/");
            }

            bool prefixMatch = false;
            if (line[0] == '/')
            {
                line = line.Substring(1);
                prefixMatch = true;
            }

            bool matchFileOrDirectory = line[line.Length - 1] != '/';

            line = Regex.Replace(line, @"[\-\/\{\}\(\)\+\.\\\^\$\|]", "\\$0");

            if (tfIgnore)
            {
                line = line.Replace("?", ".?");
                line = line.Replace("*", "(.+)");
            }
            else
            {
                line = line.Replace("?", "\\?");
                line = line.Replace("**", "(.+)").Replace("*", "[^\\/]+");
            }


            if (!prefixMatch)
            {
                line = "((.+)\\/)?" + line;
            }

            if (matchFileOrDirectory)
            {
                line += "(/|$)";
            }

            return line;
        }
    }
}
