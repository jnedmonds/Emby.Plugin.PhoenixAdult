using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Emby.Media.Common.Extensions;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Providers;
using Newtonsoft.Json.Linq;
using PhoenixAdult.Helpers;
using PhoenixAdult.Helpers.Utils;

namespace PhoenixAdult.Sites
{
    public class SiteNaughtyAmerica : IProviderBase
    {
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

            var searchURL = Helper.GetSearchSearchURL(siteNum) + searchTitle;
            var searchData = await HTML.ElementFromURL(searchURL, cancellationToken).ConfigureAwait(false);

            var searchResultNodes = searchData.SelectNodesSafe("//div[@class='scene-grid-item']/a[@class='contain-img']");

            Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Found results {searchResultNodes.Count} now processing");

            foreach (var nodsearchResultNode in searchResultNodes)
            {
                var sceneUrl = nodsearchResultNode.Attributes["href"].Value;
                Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Processing {sceneUrl}");

                var sceneID = new List<string> { Helper.Encode(sceneUrl) };

                if (searchDate.HasValue)
                {
                    sceneID.Add(searchDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                }

                var searchResult = await Helper.GetSearchResultsFromUpdate(this, siteNum, sceneID.ToArray(), searchDate, cancellationToken).ConfigureAwait(false);
                if (searchResult.Any())
                {
                    Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Found title: {searchResult[0].Name}");
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

            Logger.Info($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Loading scene: {sceneURL}");

            var sceneData = await HTML.ElementFromURL(sceneURL, cancellationToken).ConfigureAwait(false);

            result.Item.ExternalId = sceneURL;

            if (Plugin.Instance.Configuration.EnableDebugging)
            {
                Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): externalID: {result.Item.ExternalId}");
            }

            result.Item.Name = sceneData.SelectSingleText("//div[@class='scene-info']/h1");

            Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): title: {result.Item.Name}");

            string tmpOverview = sceneData.SelectSingleText("//div[contains(@class, 'synopsis')]");
            if (!string.IsNullOrEmpty(tmpOverview))
            {
                result.Item.Overview = tmpOverview.Substring("Synopsis".Length);
            }

            if (Plugin.Instance.Configuration.EnableDebugging)
            {
                Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): overview: {result.Item.Overview}");
            }

            result.Item.AddStudio("Naughty America");

            if (Plugin.Instance.Configuration.EnableDebugging)
            {
                Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): studio: Naughty America");
            }

            var subSites = sceneData.SelectNodesSafe("//div[@class='scene-info']//h2/a");
            foreach (var subSite in subSites)
            {
                if (Plugin.Instance.Configuration.EnableDebugging)
                {
                    Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): sub-studio: {subSite.InnerText}");
                }

                result.Item.AddStudio(subSite.InnerText);
            }

            if (Plugin.Instance.Configuration.EnableDebugging)
            {
                Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Finding Premier date");
            }

            var date = sceneData.SelectSingleText("//span[contains(@class, 'entry-date')]/text()");
            if (DateTime.TryParseExact(date, "MMM d, yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var sceneDateObj))
            {
                if (Plugin.Instance.Configuration.EnableDebugging)
                {
                    Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Premier date added {sceneDateObj.ToString()}");
                }

                result.Item.PremiereDate = sceneDateObj;
            }

            if (Plugin.Instance.Configuration.EnableDebugging)
            {
                Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Processing Genres");
            }

            var genreNode = sceneData.SelectNodesSafe("//div[contains(@class, 'categories')]/a");
            foreach (var genreLink in genreNode)
            {
                if (Plugin.Instance.Configuration.EnableDebugging)
                {
                    Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Found genre: {genreLink.InnerText}");
                }

                result.Item.AddGenre(genreLink.InnerText);
            }

            if (Plugin.Instance.Configuration.EnableDebugging)
            {
                Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Processing Actors");
            }

            var actorsNode = sceneData.SelectNodesSafe("//div[@class='performer-list']/a");
            foreach (var actorLink in actorsNode)
            {
                var actorName = actorLink.InnerText;
                string actorsPageURL = actorName.ToLowerInvariant()
                    .Replace(" ", "-", StringComparison.OrdinalIgnoreCase)
                    .Replace("'", string.Empty, StringComparison.OrdinalIgnoreCase);
                var actorURL = $"https://www.naughtyamerica.com/pornstar/{actorsPageURL}";
                var actorData = await HTML.ElementFromURL(actorURL, cancellationToken).ConfigureAwait(false);
                var actorPhotoURL = actorData.SelectSingleText("//img[contains(@class, 'performer-pic')]/@data-src");

                if (Plugin.Instance.Configuration.EnableDebugging)
                {
                    Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Found actor: {actorName}");
                }

                var res = new PersonInfo
                {
                    Name = actorName,
                };

                if (!string.IsNullOrEmpty(actorPhotoURL))
                {
                    res.ImageUrl = $"https:" + actorPhotoURL;

                    if (Plugin.Instance.Configuration.EnableDebugging)
                    {
                        Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Found actor photoURL: {res.ImageUrl.ToString()}");
                    }
                }

                result.People.Add(res);
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

            var galleryImages = sceneData.SelectNodesSafe("//div[@class='contain-scene-images desktop-only']/a");
            foreach (var image in galleryImages)
            {
                Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Processing image");

                var imageUrl = "https:" + image.Attributes["href"].Value;

                result.Add(new RemoteImageInfo
                {
                    Url = imageUrl,
                    Type = ImageType.Primary,
                });
                result.Add(new RemoteImageInfo
                {
                    Url = imageUrl,
                    Type = ImageType.Backdrop,
                });
            }

            Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): **** Leaving - Found {result.Count} images");

            return result;
        }
    }
}
