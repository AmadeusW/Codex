using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Codex.Storage.Utilities;
using Nest;

namespace Codex.Storage.ElasticProviders
{
    /// <summary>
    /// Helper class with custom analyzers.
    /// </summary>
    internal static class CustomAnalyzers
    {
        /// <summary>
        /// Project name analyzer which lowercases project name
        /// </summary>
        public static CustomAnalyzer LowerCaseKeywordAnalyzer { get; } = new CustomAnalyzer
        {
            Filter = new List<string>
            {
                // (built in) normalize to lowercase
                "lowercase",
            },
            Tokenizer = "keyword",

        };

        /// <summary>
        /// Project name analyzer which lowercases project name
        /// </summary>
        public static CustomNormalizer LowerCaseKeywordNormalizer { get; } = new CustomNormalizer
        {
            Filter = new List<string>
            {
                // (built in) normalize to lowercase
                "lowercase",

                 // (built in) normalize to ascii equivalents
                "asciifolding",
            },
        };

        /// <summary>
        /// NGramAnalyzer is useful for "partial name search".
        /// </summary>
        public static CustomAnalyzer PrefixFilterIdentifierNGramAnalyzer { get; } = new CustomAnalyzer
        {
            Filter = new List<string>
            {
                "name_gram_delimiter_start_inserter",
                "name_gram_delimiter_inserter",
                "name_gram_prefix_delimiter_processor",
                "code_preprocess_gram",
                "name_gram_delimiter_exclusion",
                "unique",
                "name_gram_delimiter_remover",
                // Camel case is superceded by name_gram_delimiter_inserter, which
                // seems to put a delimiter at all the right places
                // does not help with a sequence of digits but that probably doesn't matter
                //"camel_case_filter",
                "name_gram_length_exclusion",

                // (built in) normalize to lowercase
                "lowercase",

                // (built in) Only pass unique tokens to name_ngrams to prevent excessive proliferation of tokens
                "unique",
                "name_ngrams",

                // Remove 3 character strings with the first character '^'
                // which is only the prefix marker character
                // Since that means there are only 2 actual searchable characters (3 searchable characters is required)
                "prefix_min_length_remover",
                "name_gram_length_exclusion",

                // (built in) normalize to ascii equivalents
                "asciifolding",

                // (built in) Finally only index unique tokens
                "unique",
            },
            Tokenizer = "keyword",

        };

        public static CustomAnalyzer PrefixFilterFullNameNGramAnalyzer { get; } = new CustomAnalyzer
        {
            Filter = new List<string>
            {
                "leading_dot_inserter",
                "code_preprocess_gram",
                "leading_dot_full_name_only_exclusion",
                "name_gram_length_exclusion",

                // TODO: All the filters above here could be replaced with path_hierarchy tokenizer with delimiter '.' and reverse=true.

                // (built in) normalize to lowercase
                "lowercase",

                // (built in) normalize to ascii equivalents
                "asciifolding",

                // (built in) Only pass unique tokens to name_ngrams to prevent excessive proliferation of tokens
                "unique",
            },
            Tokenizer = "keyword",

        };

        public static CustomAnalyzer EncodedFullTextAnalyzer { get; } = new CustomAnalyzer
        {
            Filter = new List<string>
            {
                "standard",
                "lowercase",
            },
            Tokenizer = "standard",
            CharFilter = new List<string>
            {
                "punctuation_to_space_replacement",
                "remove_line_encoding"
            }
        };

        public static readonly IDictionary<string, ITokenizer> TokenizersMap = new Dictionary<string, ITokenizer>()
        {
            {
                // Ex. Turns [one.two.three] into [one.two.three, two.three, three]
                "end_edge_dot_hierarchy",
                new PathHierarchyTokenizer()
                {
                    Delimiter = '.',
                    Reverse = true,
                }
            },
            {
                // Ex. Turns [one@two@three] into [one@two@three, two@three, three]
                "end_edge_at_hierarchy",
                new PathHierarchyTokenizer()
                {
                    Delimiter = '@',
                    Reverse = true,
                }
            }
        };

        public static readonly IDictionary<string, ICharFilter> CharFiltersMap = new Dictionary<string, ICharFilter>()
        {
            {
                // Replace punctuation (i.e. '.' or ',') characters with a space
                // $ ^      +=`~        <>
                "punctuation_to_space_replacement",
                new PatternReplaceCharFilter()
                {
                    Pattern = "[$^+=`~<>!\"#%&'()*,-./:;?@\\[\\]\\\\_{}]",
                    //Pattern = $"[{Regex.Escape("!\"#%&'()*,-./:;?@[\\]_{}")}]",
                    Replacement = " "
                }
            },
            {
                // Remove encoded line numbers
                "remove_line_encoding",
                new PatternReplaceCharFilter()
                {
                    Pattern = FullTextUtilities.EncodeLineSpecifier("\\d+"),
                    Replacement = ""
                }
            }
        };

        public static readonly IDictionary<string, ITokenFilter> FiltersMap = new Dictionary<string, ITokenFilter>()
        {
            {
                // Add '@^' symbols at the beginning of string
                "name_gram_delimiter_start_inserter",
                new PatternReplaceTokenFilter()
                {
                    Pattern = "^(.*)",
                    Replacement = "@\\^$1^"
                }
            },
            {
                // Add @ symbol before upper to lower case transition
                "name_gram_delimiter_inserter",
                new PatternReplaceTokenFilter()
                {
                    Pattern = "(?:(?<leading>\\p{Lu})(?<trailing>\\p{Ll}))",
                    Replacement = "@${leading}${trailing}"
                }
            },
            {
                // Replace @^@ with @^ so only strict prefix always starts with ^ symbol
                "name_gram_prefix_delimiter_processor",
                new PatternReplaceTokenFilter()
                {
                    Pattern = "@\\^@",
                    Replacement = "@\\^"
                }
            },
            {
                // Split out end aligned ngrams
                "code_preprocess_gram",
                new EdgeNGramTokenFilter()
                {
                    MaxGram = MaxGram,
                    MinGram = MinGram,
                    Side = EdgeNGramSide.Back
                }
            },
            {
                // Add @ symbol before upper to lower case transition
                "name_ngrams",
                new EdgeNGramTokenFilter()
                {
                    MaxGram = MaxGram,
                    MinGram = MinGram,
                }
            },
            {
                // Clear grams not containing @ symbol marker
                // This ensures that random substrings will be removed later that
                // don't mark significant name boundaries.
                // Also, remove grams containing closing angle bracket,
                // without opening angle bracket or starting with angle bracket
                "name_gram_delimiter_exclusion",
                new PatternReplaceTokenFilter()
                {
                    Pattern = "((^[^@]*$)|(^[^@].*$)|(^[^\\<]*\\>$)|(^\\^..$))",
                    Replacement = ""
                }
            },
            {
                // Remove @ symbol marker in preparation for
                // generating final name grams
                "name_gram_delimiter_remover",
                new PatternReplaceTokenFilter()
                {
                    Pattern = "\\@",
                    Replacement = ""
                }
            },
            {
                // Only include full name symbols (which contain a dot other
                // than the starting dot character added due to normalization
                // using leading_dot_inserter)
                "leading_dot_full_name_only_exclusion",
                new PatternReplaceTokenFilter()
                {
                    Pattern = "^(?<empty>)(([^\\.].*)|(\\.(?<value>.*)))$",
                    Replacement = "${empty}${value}"
                }
            },
            {
                // Normalize full name symbols so they all start with
                // leading dot. This allows selecting only n-grams
                // which contain starting dot and inner dot
                // meaning the token represents a valid fragment
                // of the full symbol
                "leading_dot_inserter",
                new PatternReplaceTokenFilter()
                {
                    Pattern = "^.*$",
                    Replacement = ".$0"
                }
            },
            {
                // Remove 3 character strings with the first character '^'
                // which is only the prefix marker character
                // Since that means there are only 2 actual searchable characters
                "prefix_min_length_remover",
                new PatternReplaceTokenFilter()
                {
                    Pattern = "(^\\^..$)",
                    Replacement = ""
                }
            },
            {
                // Capture camel casing groups
                "camel_case_filter",
                CamelCaseFilter
            },
            {
                // Clear grams not containing @ symbol marker
                // This ensures that random substrings will be removed later that
                // don't mark significant name boundaries
                "name_gram_length_exclusion",
                new LengthTokenFilter()
                {
                    Min = 2,
                }
            }
        };

        public static TokenFiltersDescriptor AddTokenFilters(this TokenFiltersDescriptor descriptor)
        {
            foreach (var tokenFilterEntry in FiltersMap)
            {
                descriptor = descriptor.UserDefined(tokenFilterEntry.Key, tokenFilterEntry.Value);
            }

            return descriptor;
        }

        public static CharFiltersDescriptor AddCharFilters(this CharFiltersDescriptor descriptor)
        {
            foreach (var charFilterEntry in CharFiltersMap)
            {
                descriptor = descriptor.UserDefined(charFilterEntry.Key, charFilterEntry.Value);
            }

            return descriptor;
        }

        public const string LowerCaseKeywordNormalizerName = "lowercase_keyword_norm";
        public const string LowerCaseKeywordAnalyzerName = "lowercase_keyword";
        public const string PrefixFilterFullNameNGramAnalyzerName = "full_name";
        public const string PrefixFilterPartialNameNGramAnalyzerName = "partial_name";
        public const string EncodedFullTextAnalyzerName = "encoded_full_text";

        /// <summary>
        /// Capture filter splits incoming sequence into the tokens that would be used by the following analysis.
        /// This means that uploading the "StringBuilder" we'll get following set of indices: "str", "stri", ... "stringbuilder", "bui", "build", ... "builder.
        /// To test tokenizer, you can use following query in sense:
        /// <code>
        /// GET testsources/_analyze?tokenizer=keyword&analyzer=partial_name { "StringBuilder"}
        /// </code>
        /// where 'testsources' is an index with an uploaded source.
        /// </summary>
        public static TokenFilterBase CamelCaseFilter => new PatternCaptureTokenFilter()
        {
            PreserveOriginal = true,
            Patterns = new[]
            {
                  // Lowercase sequence with at least three lower case characters
                  "(\\p{Lu}\\p{Lu}\\p{Lu}+)",

                  // Uppercase followed by lowercase then rest of word characters (NOTE: word characters include underscore '_')
                  "(\\p{Lu}\\p{Ll}\\w+)",

                  // Non-alphanumeric char (not captured) followed by series of word characters (NOTE: word characters include underscore '_')
                  "[^\\p{L}\\d]+(\\w+)",

                  // Alphanumeric char (not captured) followed by series of at least one alpha-number then series of word characters
                  // (NOTE: word characters include underscore '_')
                  "[\\p{L}\\d]([^\\p{L}\\d]+\\w+)",

                  // Sequence of digits
                  "(\\d\\d+)"
            },
        };

        public const int MinGram = 2;
        public const int MaxGram = 70;

        /// <summary>
        /// Func that can be used with <see cref="CreateIndexExtensions.CreateIndexAsync"/>.
        /// </summary>
        public static Func<CreateIndexDescriptor, CreateIndexDescriptor> AddNGramAnalyzerFunc { get; } =
            c => c.Settings(isd => isd.Analysis(descriptor => descriptor
                            .TokenFilters(tfd => AddTokenFilters(tfd))
                            .CharFilters(cfd => AddCharFilters(cfd))
                            .Normalizers(bases => bases
                                .UserDefined(LowerCaseKeywordNormalizerName, LowerCaseKeywordNormalizer))
                            .Analyzers(bases => bases
                                .UserDefined(PrefixFilterPartialNameNGramAnalyzerName, PrefixFilterIdentifierNGramAnalyzer)
                                .UserDefined(PrefixFilterFullNameNGramAnalyzerName, PrefixFilterFullNameNGramAnalyzer)
                                .UserDefined(LowerCaseKeywordAnalyzerName, LowerCaseKeywordAnalyzer)
                                .UserDefined(EncodedFullTextAnalyzerName, EncodedFullTextAnalyzer))));
    }
}