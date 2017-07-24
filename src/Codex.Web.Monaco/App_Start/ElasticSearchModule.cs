using Autofac;
using Codex.ObjectModel;
using Codex.Storage;

namespace WebUI
{
    internal class ElasticSearchModule : Module
    {
        public string Endpoint { get; set; }

        protected override void Load(ContainerBuilder builder)
        {
            builder.Register(_ => new ElasticsearchStorage(Endpoint))
                .As<IStorage>()
                .SingleInstance();
        }
    }
}
