using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Xml;
using Microsoft.Extensions.DependencyInjection;
using Sitecore.ContentSearch.Linq;
using Sitecore.ContentSearch.Linq.Utilities;
using Sitecore.ContentSearch.SearchTypes;
using Sitecore.Data.Items;
using Sitecore.XA.Foundation.Search;
using Sitecore.XA.Foundation.Search.Models;
using Sitecore.XA.Foundation.Search.Services;
using Sitecore.XA.Foundation.Search.Spatial;
using Sitecore.XA.Foundation.SitecoreExtensions.Extensions;

namespace Sitecore.Support.XA.Foundation.Search.Extensions
{
    public static class FacetExtensions
    {
        public static IQueryable<ContentPage> ApplyFacetFilters(this IQueryable<ContentPage> query, NameValueCollection queryString, Coordinates center, string siteName)
        {
            var facilityService = DependencyInjection.ServiceLocator.ServiceProvider.GetService<IFacetService>();
            List<Item> facets = facilityService.GetFacetItems(queryString.AllKeys, siteName);

            foreach (Item facet in facets)
            {
                string key = facet[Buckets.Util.Constants.FacetParameters];
                string value = queryString[facet.Name];
                if (key != null && key.Contains(','))
                {
                    string[] keys = key.Split(',').Select(i => i.Trim()).ToArray();
                    string[] values = (value ?? string.Empty).Split(new[] { '/' }).Select(i => i.Trim()).ToArray();
                    for (int i = 0; i < Math.Min(keys.Length, values.Length); i++)
                    {
                        query = query.ApplyFacetFilter(keys[i], values[i], facet);
                    }
                }
                else
                {
                    query = query.ApplyFacetFilter(key, value, facet, center);
                }
            }

            return query;
        }

        private static IQueryable<ContentPage> ApplyFacetFilter(this IQueryable<ContentPage> query, string key, string value, Item facetItem, Coordinates center = null)
        {
            if (facetItem.DoesItemInheritFrom(Templates.DistanceFacet.ID))
            {
                Distance radius = Distance.Parse(value, facetItem[Templates.DistanceFacet.Fields.Unit]);
                if (center != null && radius != null)
                {
                    query = query.WithinDistance(i => i.Location, center, radius);
                }
            }
            else if (key != null)
            {
                Expression<Func<ContentPage, bool>> predicate = PredicateBuilder.False<ContentPage>();
                foreach (string s in value.Split(',').TrimAndRemoveEmpty())
                {
                    predicate = predicate.Or(BuildFacetPredicate(key, s == "_empty_" ? string.Empty : s, facetItem));
                }
                query = query.Where(predicate);
            }

            return query;
        }

        private static Expression<Func<ContentPage, bool>> BuildFacetPredicate(string key, string value, Item facetItem)
        {
            ObjectIndexerKey index = key;
            if (value.Contains("|"))
            {
                string[] values = value.Split('|').ToArray();

                if (values[0].Length > 0 && values[1].Length > 0)
                {
                    if (facetItem.DoesItemInheritFrom(Templates.FloatFacet.ID))
                    {
                        double lower = double.TryParse(values[0], NumberStyles.Any, CultureInfo.InvariantCulture, out lower) ? lower : double.MinValue;
                        double upper = double.TryParse(values[1], NumberStyles.Any, CultureInfo.InvariantCulture, out upper) ? upper : double.MaxValue;
                        return i => i.get_Item<double>(index).Between(lower, upper, Inclusion.Both);
                    }
                    if (facetItem.DoesItemInheritFrom(Templates.IntegerFacet.ID))
                    {
                        long lower = long.TryParse(values[0], out lower) ? lower : long.MinValue;
                        long upper = long.TryParse(values[1], out upper) ? upper : long.MaxValue;
                        return i => i.get_Item<long>(index).Between(lower, upper, Inclusion.Both);
                    }
                    if (facetItem.DoesItemInheritFrom(Templates.DateFacet.ID))
                    {
                        var dateFormat = GetDateTimeFieldFormat(key);
                        if (!String.IsNullOrEmpty(dateFormat))
                        {
                            DateTime? startDate = ParseToDateTime(values[0]);
                            DateTime? endDate = ParseToDateTime(values[1]);

                            string startValue = startDate?.ToString(dateFormat) ?? string.Empty;
                            string endValue = endDate?.ToString(dateFormat) ?? string.Empty;

                            return i => i.get_Item<string>(index).Between(startValue, endValue, Inclusion.Both);
                        }
                    }
                    string from = values[0];
                    string to = values[1];
                    return i => i.get_Item<string>(index).Between(from, to, Inclusion.Both);
                }
                if (values[0].Length > 0)
                {
                    if (facetItem.DoesItemInheritFrom(Templates.FloatFacet.ID))
                    {
                        double lower = double.TryParse(values[0], NumberStyles.Any, CultureInfo.InvariantCulture, out lower) ? lower : double.MinValue;
                        return i => i.get_Item<double>(index) >= lower;
                    }
                    if (facetItem.DoesItemInheritFrom(Templates.IntegerFacet.ID))
                    {
                        long lower = long.TryParse(values[0], out lower) ? lower : long.MinValue;
                        return i => i.get_Item<long>(index) >= lower;
                    }
                    if (facetItem.DoesItemInheritFrom(Templates.DateFacet.ID))
                    {
                        var dateFormat = GetDateTimeFieldFormat(key);
                        if (!String.IsNullOrEmpty(dateFormat))
                        {
                            DateTime? startDate = ParseToDateTime(values[0]);
                            string startValue = startDate?.ToString(dateFormat) ?? string.Empty;
                            return i => string.Compare(i.get_Item<string>(index), startValue, StringComparison.Ordinal) >= 0;
                        }
                    }
                    string from = values[0];
                    return i => string.Compare(i.get_Item<string>(index), from, StringComparison.Ordinal) >= 0;
                }
                if (values[1].Length > 0)
                {
                    if (facetItem.DoesItemInheritFrom(Templates.FloatFacet.ID))
                    {
                        double upper = double.TryParse(values[1], NumberStyles.Any, CultureInfo.InvariantCulture, out upper) ? upper : double.MaxValue;
                        return i => i.get_Item<double>(index) <= upper;
                    }
                    if (facetItem.DoesItemInheritFrom(Templates.IntegerFacet.ID))
                    {
                        long upper = long.TryParse(values[1], out upper) ? upper : long.MaxValue;
                        return i => i.get_Item<long>(index) <= upper;
                    }
                    if (facetItem.DoesItemInheritFrom(Templates.DateFacet.ID))
                    {
                        var dateFormat = GetDateTimeFieldFormat(key);
                        if (!String.IsNullOrEmpty(dateFormat))
                        {
                            DateTime? endDate = ParseToDateTime(values[1]);
                            string endValue = endDate?.ToString(dateFormat) ?? string.Empty;
                            return i => string.Compare(i.get_Item<string>(index), endValue, StringComparison.Ordinal) <= 0;
                        }
                    }
                    string to = values[1];
                    return i => string.Compare(i.get_Item<string>(index), to, StringComparison.Ordinal) <= 0;
                }
                else
                {
                    return i => i[key] == value;
                }
            }
            else
            {
                if (facetItem.DoesItemInheritFrom(Templates.FloatFacet.ID))
                {
                    double bound = double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out bound) ? bound : 0;
                    return i => i.get_Item<double>(index).Between(bound, bound, Inclusion.Both);
                }
                if (facetItem.DoesItemInheritFrom(Templates.IntegerFacet.ID))
                {
                    long bound = long.TryParse(value, out bound) ? bound : long.MinValue;
                    return i => i.get_Item<long>(index).Between(bound, bound, Inclusion.Both);
                }
            }

            return i => i[key] == value;
        }

        private static DateTime? ParseToDateTime(string value)
        {
            return DateUtil.ToUniversalTime(DateUtil.IsoDateToDateTime(value, DateTime.MinValue));
        }

        private static string GetDateTimeFieldFormat(string fieldName)
        {
            var fieldNodes = Configuration.Factory.GetConfigNodes("contentSearch/indexConfigurations/*/fieldMap/fieldNames/field").Cast<XmlNode>();
            var fieldNode = fieldNodes.FirstOrDefault(node => Sitecore.Xml.XmlUtil.GetAttribute("fieldName", node) == fieldName);
            if (fieldNode?.Attributes != null)
            {
                return fieldNode.Attributes["format"].Value;
            }
            return Configuration.Settings.GetSetting("ContentSearch.DateFormat", "yyyy-MM-dd'T'HH:mm:ss'Z'");
        }
    }
}