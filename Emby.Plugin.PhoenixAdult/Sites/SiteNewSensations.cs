using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using PhoenixAdult.Helpers;
using PhoenixAdult.Helpers.Utils;

namespace PhoenixAdult.Sites
{
    public class SiteNewSensations : IProviderBase
    {
        private enum Site
        {
            Default,
            FamilyXXX,
            HotWifeXXX,
        }

        public async Task<List<RemoteSearchResult>> Search(int[] siteNum, string searchTitle, DateTime? searchDate, CancellationToken cancellationToken)
        {
            Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): ***** Starting - searchTitle: {searchTitle}");

            var result = new List<RemoteSearchResult>();
            if (siteNum == null || string.IsNullOrEmpty(searchTitle))
            {
                Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): ***** Leaving early empty search title");
                return result;
            }

            Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Searching for results");

            var rootUrl = Helper.GetSearchSearchURL(siteNum);
            var searchResultsURLs = new List<string>();

            var searchResults = await GoogleSearch.GetSearchResults(searchTitle, siteNum, cancellationToken).ConfigureAwait(false);
            Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Found results {searchResults.Count}");

            foreach (var searchResult in searchResults)
            {
                if (searchResult.StartsWith(rootUrl))
                {
                    searchResultsURLs.Add(searchResult);
                }
            }

            foreach (var url in searchResultsURLs)
            {
                var sceneURL = new Uri(url);
                var sceneID = new List<string> { Helper.Encode(sceneURL.AbsolutePath) };

                if (searchDate.HasValue)
                {
                    sceneID.Add(searchDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                }

                var searchResult = await Helper.GetSearchResultsFromUpdate(this, siteNum, sceneID.ToArray(), searchDate, cancellationToken).ConfigureAwait(false);
                if (searchResult.Any())
                {
                    result.AddRange(searchResult);
                }
            }

            Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): **** Leaving - Search results: Found {result.Count} results for searchTitle: {searchTitle}");

            return result;
        }

        public async Task<MetadataResult<BaseItem>> Update(int[] siteNum, string[] sceneID, CancellationToken cancellationToken)
        {
            Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): **** Starting");

            var result = new MetadataResult<BaseItem>()
            {
                Item = new Movie(),
                People = new List<PersonInfo>(),
            };

            if (sceneID == null || siteNum == null)
            {
                Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Leaving early empty sceneID");
                return result;
            }

            string sceneURL = Helper.Decode(sceneID[0]),
                sceneDate = string.Empty;

            if (!sceneURL.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                sceneURL = Helper.GetSearchBaseURL(siteNum) + sceneURL;
            }

            Logger.Info($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Loading scene: {sceneURL}");

            var searchSite = Helper.GetSearchSiteName(siteNum);
            Logger.Info($"Search site: {searchSite}");
            var site = searchSite switch
            {
                "Family XXX" => Site.FamilyXXX,
                "HotwifeXXX" => Site.HotWifeXXX,
                _ => Site.Default
            };
            Logger.Info(site.ToString());

            if (sceneID.Length > 1)
            {
                sceneDate = sceneID[1];
            }

            result.Item.ExternalId = sceneURL;
            if (Plugin.Instance.Configuration.EnableDebugging)
            {
                Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): externalID: {result.Item.ExternalId}");
            }

            result.Item.AddStudio("New Sensations");
            if (Plugin.Instance.Configuration.EnableDebugging)
            {
                Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): studio: {result.Item.Studios[0]}");
            }

            var sceneData = await HTML.ElementFromURL(sceneURL, cancellationToken).ConfigureAwait(false);

            switch (site)
            {
                case Site.FamilyXXX:
                    {
                        result.Item.Name = sceneData.SelectSingleText("//div[@class='sceneRight']//div[@class='indScene']//h2");
                        Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): title: {result.Item.Name}");

                        var descriptionNode = sceneData.SelectNodesSafe("//div[@class='description']//p");
                        var overview = descriptionNode[0].InnerText.Trim();

                        // remove <span>Description:</span> from beginning
                        overview = overview.Substring(13);
                        result.Item.Overview = overview;
                        if (Plugin.Instance.Configuration.EnableDebugging)
                        {
                            Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): overview: {result.Item.Overview}");
                        }

                        result.Item.AddStudio("Family XXX");
                        if (Plugin.Instance.Configuration.EnableDebugging)
                        {
                            Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): studio: {result.Item.Studios[0]}");
                        }

                        if (Plugin.Instance.Configuration.EnableDebugging)
                        {
                            Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Finding Premier date");
                        }

                        var dateNode = sceneData.SelectSingleText("//div[@class='sceneRight']//div[@class='indScene']//div[@class='sceneDateP']//span").TrimEnd(',');
                        if (DateTime.TryParseExact(dateNode, "MM/dd/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var sceneDateObj))
                        {
                            result.Item.PremiereDate = sceneDateObj;
                            if (Plugin.Instance.Configuration.EnableDebugging)
                            {
                                Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Premier date added {result.Item.PremiereDate}");
                            }
                        }

                        // performers
                        if (Plugin.Instance.Configuration.EnableDebugging)
                        {
                            Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Processing Actors");
                        }

                        var performerNodes = sceneData.SelectNodesSafe("//div[@class='sceneRight']//div[@class='indScene']//div[@class='sceneTextLink']//p//span//a");

                        foreach (var performerNode in performerNodes)
                        {
                            var performerUrl = performerNode.Attributes["href"].Value;
                            var performerPage = await HTML.ElementFromURL(performerUrl, cancellationToken).ConfigureAwait(false);
                            var performerImg = performerPage.SelectSingleNode("//div[contains(@class, 'modelBioPic')]/img");

                            var actor = new PersonInfo
                            {
                                Name = performerNode.InnerText,
                                ImageUrl = performerImg.Attributes["src0_1x"].Value,
                            };

                            if (Plugin.Instance.Configuration.EnableDebugging)
                            {
                                Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Found actor: {actor.Name}");
                                Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Found actor photoURL: {actor.ImageUrl}");
                            }

                            result.People.Add(actor);
                        }

                        break;
                    }

                case Site.HotWifeXXX:
                    {
                        result.Item.Name = sceneData.SelectSingleText("//div[@class='trailerInfo']/h2");
                        Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): title: {result.Item.Name}");

                        var descriptionNode = sceneData.SelectNodesSafe("//div[@class='dvdDescription']//p");
                        var overview = descriptionNode[0].InnerText.Trim();

                        // remove <span>Description:</span> from beginning
                        overview = overview.Substring(13);
                        result.Item.Overview = overview;
                        if (Plugin.Instance.Configuration.EnableDebugging)
                        {
                            Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): overview: {result.Item.Overview}");
                        }

                        result.Item.AddStudio("HotwifeXXX");
                        if (Plugin.Instance.Configuration.EnableDebugging)
                        {
                            Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): studio: {result.Item.Studios[0]}");
                        }

                        if (Plugin.Instance.Configuration.EnableDebugging)
                        {
                            Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Finding Premier date");
                        }

                        var dateNodeText = sceneData.SelectSingleText("//div[@class='trailerInfo']//div[contains(@class, 'released2')]");
                        var date = dateNodeText.Substring(0, dateNodeText.IndexOf(','));
                        if (DateTime.TryParseExact(date, "MM/dd/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var sceneDateObj))
                        {
                            result.Item.PremiereDate = sceneDateObj;
                            if (Plugin.Instance.Configuration.EnableDebugging)
                            {
                                Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Premier date added {result.Item.PremiereDate}");
                            }
                        }

                        // performers
                        if (Plugin.Instance.Configuration.EnableDebugging)
                        {
                            Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Processing Actors");
                        }

                        var performerNodes = sceneData.SelectNodesSafe("//div[@class='trailerInfo']//span[@class='tour_update_models']/a");

                        foreach (var performerNode in performerNodes)
                        {
                            var actor = new PersonInfo
                            {
                                Name = performerNode.InnerText,
                            };

                            if (Plugin.Instance.Configuration.EnableDebugging)
                            {
                                Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Found actor: {actor.Name}");
                            }

                            result.People.Add(actor);
                        }

                        break;
                    }

                default:
                    {
                        result.Item.Name = sceneData.SelectSingleText("//div[@class='sceneRight']//div[@class='indScene']//h1");
                        Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): title: {result.Item.Name}");

                        var descriptionNodes = sceneData.SelectNodesSafe("//div[@class='description']//h2");
                        var overview = descriptionNodes[0].InnerText.Trim();
                        result.Item.Overview = overview;
                        if (Plugin.Instance.Configuration.EnableDebugging)
                        {
                            Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): overview: {result.Item.Overview}");
                        }

                        if (Plugin.Instance.Configuration.EnableDebugging)
                        {
                            Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Finding Premier date");
                        }

                        var dateNode = sceneData.SelectSingleText("//div[@class='sceneRight']//div[@class='indScene']//div[@class='sceneDateP']//span").TrimEnd(',');
                        if (DateTime.TryParseExact(dateNode, "MM/dd/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var sceneDateObj))
                        {
                            result.Item.PremiereDate = sceneDateObj;
                            if (Plugin.Instance.Configuration.EnableDebugging)
                            {
                                Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Premier date added {result.Item.PremiereDate}");
                            }
                        }

                        // performers
                        if (Plugin.Instance.Configuration.EnableDebugging)
                        {
                            Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Processing Actors");
                        }

                        var performerNodes = sceneData.SelectNodesSafe("//div[@class='sceneRight']//div[@class='indScene']//div[@class='sceneTextLink']//p//span//a");

                        foreach (var performerNode in performerNodes)
                        {
                            var actor = new PersonInfo
                            {
                                Name = performerNode.InnerText,
                            };

                            if (Plugin.Instance.Configuration.EnableDebugging)
                            {
                                Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Found actor: {actor.Name}");
                            }

                            result.People.Add(actor);
                        }

                        break;
                    }
            }

            Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): **** Leaving - Updated title: {result.Item.Name}");

            return result;
        }

        public async Task<IEnumerable<RemoteImageInfo>> GetImages(int[] siteNum, string[] sceneID, BaseItem item, CancellationToken cancellationToken)
        {
            Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): **** Starting");

            var result = new List<RemoteImageInfo>();

            if (sceneID == null)
            {
                Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Leaving early empty sceneID");
                return result;
            }

            var sceneURL = Helper.Decode(sceneID[0]);
            if (!sceneURL.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                sceneURL = Helper.GetSearchBaseURL(siteNum) + sceneURL;
            }

            var sceneData = await HTML.ElementFromURL(sceneURL, cancellationToken).ConfigureAwait(false);
            if (Plugin.Instance.Configuration.EnableDebugging)
            {
                Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Processing image");
            }

            var posterNode = sceneData.SelectSingleNode("//span[@id='trailer_thumb']//span//img");
            result.Add(new RemoteImageInfo
            {
                Url = posterNode.Attributes["src"].Value,
                Type = ImageType.Primary,
            });
            result.Add(new RemoteImageInfo
            {
                Url = posterNode.Attributes["src"].Value,
                Type = ImageType.Backdrop,
            });

            Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): **** Leaving - Found {result.Count} images");

            return result;
        }
    }
}
