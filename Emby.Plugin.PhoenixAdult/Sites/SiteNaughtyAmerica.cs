using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
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
            Logger.Debug($"SiteNaughtyAmerica-Search(): **** Starting with searchTitle: {searchTitle}");

            var result = new List<RemoteSearchResult>();
            if (siteNum == null || string.IsNullOrEmpty(searchTitle))
            {
                Logger.Debug($"SiteNaughtyAmerica-Search(): **** Leaving early empty search title");
                return result;
            }

            Logger.Debug($"SiteNaughtyAmerica-Search(): Searching for results");

            var searchURL = Helper.GetSearchSearchURL(siteNum) + searchTitle;
            var searchData = await HTML.ElementFromURL(searchURL, cancellationToken).ConfigureAwait(false);

            var searchResultNodes = searchData.SelectNodesSafe("//div[@class='scene-grid-item']/a[@class='contain-img']");

            Logger.Debug($"SiteNaughtyAmerica-Search(): Found results {searchResultNodes.Count} now processing");

            foreach (var nodsearchResultNode in searchResultNodes)
            {
                var sceneUrl = nodsearchResultNode.Attributes["href"].Value;
                Logger.Debug($"SiteNaughtyAmerica-Search(): Processing {sceneUrl}");

                var sceneID = new List<string> { Helper.Encode(sceneUrl) };

                if (searchDate.HasValue)
                {
                    sceneID.Add(searchDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                }

                var searchResult = await Helper.GetSearchResultsFromUpdate(this, siteNum, sceneID.ToArray(), searchDate, cancellationToken).ConfigureAwait(false);
                if (searchResult.Any())
                {
                    Logger.Debug($"SiteNaughtyAmerica-Search(): Found title: {searchResult[0].Name}");
                    result.AddRange(searchResult);
                }
            }

            Logger.Debug($"SiteNaughtyAmerica-Search(): **** Leaving - Search results: Found {result.Count} results for searchTitle: {searchTitle}");

            return result;
        }

        public async Task<MetadataResult<BaseItem>> Update(int[] siteNum, string[] sceneID, CancellationToken cancellationToken)
        {
            Logger.Debug($"SiteNaughtyAmerica-Update(): **** Starting");

            var result = new MetadataResult<BaseItem>()
            {
                Item = new Movie(),
                People = new List<PersonInfo>(),
            };

            if (sceneID == null)
            {
                Logger.Debug($"SiteNaughtyAmerica-Update(): Leaving early empty sceneID");
                return result;
            }

            var sceneURL = Helper.Decode(sceneID[0]);
            if (!sceneURL.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                sceneURL = Helper.GetSearchBaseURL(siteNum) + sceneURL;
            }

            Logger.Info($"SitePornhub-Update(): Loading scene: {sceneURL}");

            var sceneData = await HTML.ElementFromURL(sceneURL, cancellationToken).ConfigureAwait(false);

            result.Item.ExternalId = sceneURL;

            if (Plugin.Instance.Configuration.EnableDebugging)
            {
                Logger.Debug($"SiteNaughtyAmerica-Update(): externalID: {result.Item.ExternalId}");
            }

            result.Item.Name = sceneData.SelectSingleText("//div[@class='scene-info']/h1");

            Logger.Debug($"SiteNaughtyAmerica-Update(): title: {result.Item.Name}");

            result.Item.AddStudio("Naughty America");

            if (Plugin.Instance.Configuration.EnableDebugging)
            {
                Logger.Debug($"SiteNaughtyAmerica-Update(): studio: Naughty America");
            }

            var subSite = sceneData.SelectSingleNode("//div[@class='scene-info']//h2/a");
            if (subSite != null)
            {
                if (Plugin.Instance.Configuration.EnableDebugging)
                {
                    Logger.Debug($"SiteNaughtyAmerica-Update(): sub-studio: {subSite.InnerText}");
                }

                result.Item.AddStudio(subSite.InnerText);
            }

            var date = sceneData.SelectSingleText("//span[contains(@class, 'entry-date')]");
            if (DateTime.TryParseExact(date, "MMM d, yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var sceneDateObj))
            {
                if (Plugin.Instance.Configuration.EnableDebugging)
                {
                    Logger.Debug($"SiteNaughtyAmerica-Update(): Premier date added {sceneDateObj.ToString()}");
                }

                result.Item.PremiereDate = sceneDateObj;
            }

            result.Item.Overview = sceneData.SelectSingleText("//div[contains(@class, 'synopsis')]").Substring("Synopsis".Length);

            if (Plugin.Instance.Configuration.EnableDebugging)
            {
                Logger.Debug($"SiteNaughtyAmerica-Update(): overview: {result.Item.Overview}");

                Logger.Debug($"SiteNaughtyAmerica-Update(): Processing Genres");
            }

            var genreNode = sceneData.SelectNodesSafe("//div[contains(@class, 'categories')]/a");
            foreach (var genreLink in genreNode)
            {
                if (Plugin.Instance.Configuration.EnableDebugging)
                {
                    Logger.Debug($"SiteNaughtyAmerica-Update(): Found genre: {genreLink.InnerText}");
                }

                result.Item.AddGenre(genreLink.InnerText);
            }

            if (Plugin.Instance.Configuration.EnableDebugging)
            {
                Logger.Debug($"SiteNaughtyAmerica-Update(): Processing Actors");
            }

            var actorsNode = sceneData.SelectNodesSafe("//div[@class='performer-list']/a");
            foreach (var actorLink in actorsNode)
            {
                if (Plugin.Instance.Configuration.EnableDebugging)
                {
                    Logger.Debug($"SitePornhub-Update(): Found actor: {actorLink.InnerText}");
                }

                var actorName = actorLink.InnerText;

                string actorsPageURL = actorName.ToLowerInvariant()
                    .Replace(" ", "-", StringComparison.OrdinalIgnoreCase)
                    .Replace("'", string.Empty, StringComparison.OrdinalIgnoreCase);

                var actorURL = $"https://www.naughtyamerica.com/pornstar/{actorsPageURL}";
                var actorData = await HTML.ElementFromURL(actorURL, cancellationToken).ConfigureAwait(false);

                // var actorPhoto = actorData.SelectSingleText("//img[@class='performer-pic']/@src");
                var actorPhotoURL = actorData.SelectSingleText("//img[contains(@class, 'performer-pic')]/@src");

                var res = new PersonInfo
                {
                    Name = actorName,
                };

                if (!string.IsNullOrEmpty(actorPhotoURL))
                {
                    res.ImageUrl = $"https:" + actorPhotoURL;
                }

                result.People.Add(res);
            }

            Logger.Debug($"SiteNaughtyAmerica-Update(): **** Leaving - Updated title: {result.Item.Name}");

            return result;
        }

        public async Task<IEnumerable<RemoteImageInfo>> GetImages(int[] siteNum, string[] sceneID, BaseItem item, CancellationToken cancellationToken)
        {
            Logger.Debug($"SiteNaughtyAmerica-GetImages(): **** Starting");

            var result = new List<RemoteImageInfo>();

            if (sceneID == null)
            {
                Logger.Debug($"SiteNaughtyAmerica-GetImages(): Leaving early empty sceneID");
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
                Logger.Debug($"SiteNaughtyAmerica-GetImages(): Processing image");

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

            Logger.Debug($"SiteNaughtyAmerica-GetImages(): **** Leaving - Found {result.Count} images");

            return result;
        }
    }
}
