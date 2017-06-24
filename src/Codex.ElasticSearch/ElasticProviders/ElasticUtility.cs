using Codex.Storage.DataModel;
using Nest;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Linq.Expressions;
using System.Collections;
using System.Collections.Generic;

namespace Codex.Storage.ElasticProviders
{
    public class QualifiedNameTerms
    {
        public string ContainerTerm = string.Empty;
        public string NameTerm = string.Empty;
        public string SecondaryNameTerm = string.Empty;
        public string ExactNameTerm => NameTerm + "^";

        public bool HasContainerName
        {
            get
            {
                return !string.IsNullOrEmpty(ContainerTerm);
            }
        }

        public bool HasName
        {
            get
            {
                return !string.IsNullOrEmpty(NameTerm);
            }
        }
    }

    internal static class ElasticUtility
    {
        public static string GetRequestString(byte[] request)
        {
            using (var ms = new MemoryStream(request))
            {
                var textReader = new StreamReader(ms);
                return textReader.ReadToEnd();
            }
        }

        public static QueryContainer CaseTerm<T>(this QueryContainerDescriptor<T> descriptor, Expression<Func<T, object>> field, string term)
            where T : class
        {
            if (string.IsNullOrEmpty(term))
            {
                return descriptor;
            }

            var lowerTerm = term.ToLowerInvariant();
            if (lowerTerm == term)
            {
                return descriptor.Term(field, term);
            }

            return descriptor.Term(field, term) || descriptor.Term(field, term.ToLowerInvariant());
        }

        public static QualifiedNameTerms CreateNameTerm(this string nameTerm)
        {
            string secondaryNameTerm = string.Empty;
            if (!string.IsNullOrEmpty(nameTerm))
            {
                nameTerm = nameTerm.Trim();
                nameTerm = nameTerm.TrimStart('"');
                if (!string.IsNullOrEmpty(nameTerm))
                {
                    if (nameTerm.EndsWith("\""))
                    {
                        nameTerm = nameTerm.TrimEnd('"');
                        nameTerm += "^";
                    }

                    if (!string.IsNullOrEmpty(nameTerm))
                    {
                        if (nameTerm[0] == '*')
                        {
                            nameTerm = nameTerm.TrimStart('*');
                            secondaryNameTerm = nameTerm.Trim();
                            nameTerm = "^" + secondaryNameTerm;
                        }
                        else
                        {
                            nameTerm = "^" + nameTerm;
                        }
                    }
                }
            }

            return new QualifiedNameTerms() { NameTerm = nameTerm, SecondaryNameTerm = secondaryNameTerm };
        }

        public static QualifiedNameTerms ParseContainerAndName(string fullyQualifiedTerm)
        {
            QualifiedNameTerms terms = new QualifiedNameTerms();
            int indexOfLastDot = fullyQualifiedTerm.LastIndexOf('.');
            if (indexOfLastDot >= 0)
            {
                terms.ContainerTerm = fullyQualifiedTerm.Substring(0, indexOfLastDot);
            }

            terms.NameTerm = fullyQualifiedTerm.Substring(indexOfLastDot + 1);
            if (terms.NameTerm.Length > 0)
            {
                terms.NameTerm = "^" + terms.NameTerm;
            }
            return terms;
        }

        public static string SubstringAfterFirstOccurrence(this string s, char c)
        {
            var index = s.IndexOf(c);
            return s.Substring(index + 1);
        }

        public static TRequest ForEach<TRequest, T>(this TRequest request, IEnumerable<T> collection, Action<TRequest, T> action)
        {
            foreach (var item in collection)
            {
                action(request, item);
            }

            return request;
        }

        public static TResult ConfigureIf<T, TResult>(this T requestDescriptor, bool condition, Func<T, TResult> configure)
            where T : TResult, IDescriptor
        {
            if (condition)
            {
                return configure(requestDescriptor);
            }

            return requestDescriptor;
        }

        public static TResult ConfigureIfElse<T, TResult>(this T requestDescriptor, bool condition,
            Func<T, TResult> thenConfigure,
            Func<T, TResult> elseConfigure)
            where T : TResult, IDescriptor
        {
            if (condition)
            {
                return thenConfigure(requestDescriptor);
            }
            else
            {
                return elseConfigure(requestDescriptor);
            }
        }

        public static T CaptureRequest<T>(this T requestDescriptor, ElasticClient client, string[] requestHolder)
            where T : IDescriptor
        {
            if (requestHolder.Length != 0)
            {
                using (var ms = new MemoryStream())
                {
                    client.Serializer.Serialize(requestDescriptor, ms);
                    ms.Position = 0;
                    var textReader = new StreamReader(ms);
                    requestHolder[0] = textReader.ReadToEnd();
                }
            }

            return requestDescriptor;
        }

        public static TypeMappingDescriptor<T> AutoMapEx<T>(this TypeMappingDescriptor<T> mappingDescriptor)
            where T : class
        {
            return mappingDescriptor.AutoMap().AllField(all => all.Enabled(false));
        }
    }
}