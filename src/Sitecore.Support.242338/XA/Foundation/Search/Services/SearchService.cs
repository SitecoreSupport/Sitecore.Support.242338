using System.Collections.Generic;
using System.Linq;
using Sitecore.ContentSearch;
using Sitecore.ContentSearch.Utilities;
using Sitecore.Data.Items;
using Sitecore.Sites;
using Sitecore.Support.XA.Foundation.Search.Extensions;
using Sitecore.XA.Foundation.Search.Models;
using Sitecore.XA.Foundation.Search.Services;
using Sitecore.XA.Foundation.SitecoreExtensions.Utils;

namespace Sitecore.Support.XA.Foundation.Search.Services
{
    public class SearchService : Sitecore.XA.Foundation.Search.Services.SearchService, ISearchService
    {
        IQueryable<ContentPage> ISearchService.GetQuery(string query, string scope, string language, Coordinates center, string site, string itemid, out string indexName)
        {
            Item contextItem = GetContextItem(itemid);
            ISearchIndex searchIndex = IndexResolver.ResolveIndex(contextItem);
            IList<Item> scopeItems = ItemUtils.Lookup(scope, Context.Database);
            IQueryable<ContentPage> queryable;

            indexName = searchIndex.Name;

            IEnumerable<SearchStringModel> model = scopeItems.Select(i => i[Sitecore.XA.Foundation.Search.Constants.ScopeQuery]).SelectMany(SearchStringModel.ParseDatasourceString);
            model = ResolveSearchQueryTokens(contextItem, model);

            using (new SiteContextSwitcher(SiteContextFactory.GetSiteContext("shell")))
            {
                queryable = LinqHelper.CreateQuery<ContentPage>(searchIndex.CreateSearchContext(), model);
            }

            queryable = queryable.Where(IsGeolocationRequest ? GeolocationPredicate(site) : PageOrMediaPredicate(site));
            queryable = queryable.Where(ContentPredicate(query));
            queryable = queryable.Where(LanguagePredicate(language));
            queryable = queryable.Where(LatestVersionPredicate());
            queryable = queryable.ApplyFacetFilters(Context.Request.QueryString, center, site);
            queryable = BoostingService.BoostQuery(scopeItems, query, contextItem, queryable);

            return queryable;
        }
    }
}