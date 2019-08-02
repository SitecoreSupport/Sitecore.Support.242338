using System.Collections.Generic;
using System.Linq;
using Sitecore.ContentSearch;
using Sitecore.ContentSearch.Utilities;
using Sitecore.Support.XA.Foundation.Search.Extensions;
using Sitecore.XA.Foundation.Search.Models;
using Sitecore.XA.Foundation.Search.Services;
using Sitecore.XA.Foundation.SitecoreExtensions.Utils;

namespace Sitecore.Support.XA.Foundation.Search.Services
{
    public class SearchService : Sitecore.XA.Foundation.Search.Services.SearchService, ISearchService
    {
        IQueryable<ContentPage> ISearchService.GetQuery(string query, string scope, string language, Coordinates center, string siteName, out string indexName)
        {
            ISearchIndex searchIndex = IndexResolver.ResolveIndex();

            indexName = searchIndex.Name;

            IEnumerable<SearchStringModel> model = ItemUtils.Lookup(scope, Context.Database).Select(i => i[Sitecore.XA.Foundation.Search.Constants.ScopeQuery]).SelectMany(SearchStringModel.ParseDatasourceString);
            IQueryable<ContentPage> queryable = LinqHelper.CreateQuery<ContentPage>(searchIndex.CreateSearchContext(), model);

            queryable = queryable.Where(IsGeolocationRequest ? GeolocationPredicate(siteName) : PageOrMediaPredicate(siteName));
            queryable = queryable.Where(ContentPredicate(query));
            queryable = queryable.Where(LanguagePredicate(language));
            queryable = queryable.Where(LatestVersionPredicate());
            queryable = queryable.ApplyFacetFilters(Context.Request.QueryString, center, siteName);
            return queryable;
        }

        protected override IQueryable<ContentPage> GetQuery(string query, string scope, string language, Coordinates center, string siteName)
        {
            string indexName;
            return (this as ISearchService).GetQuery(query, scope, language, center, siteName, out indexName);
        }
    }
}