using Microsoft.Extensions.DependencyInjection;
using Sitecore.DependencyInjection;
using Sitecore.XA.Foundation.Search.Services;

namespace Sitecore.Support.XA.Foundation.Search.Pipelines.IoC
{
    [UsedImplicitly]
    public class RegisterSearchServices : IServicesConfigurator
    {
        public void Configure(IServiceCollection serviceCollection)
        {
            serviceCollection.AddSingleton<ISearchService, Sitecore.Support.XA.Foundation.Search.Services.SearchService>();
        }
    }
}