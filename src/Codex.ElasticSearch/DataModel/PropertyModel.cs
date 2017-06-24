using Codex.Storage.ElasticProviders;
using Nest;

namespace Codex.Storage.DataModel
{
    [ElasticsearchType(Name = ElasticProviders.ElasticProvider.SearchPropertiesTypeName)]
    public class PropertyModel
    {
        /// <summary>
        /// The key of the property
        /// </summary>
        [NormalizedKeyword]
        public string Key { get; set; }

        /// <summary>
        /// The value of the property
        /// </summary>
        [NormalizedKeyword]
        public string Value { get; set; }

        /// <summary>
        /// The id of the object to which the property belongs
        /// </summary>
        [Keyword]
        public string ObjectId { get; set; }
    }
}