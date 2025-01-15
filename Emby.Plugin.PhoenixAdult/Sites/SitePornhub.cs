using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Newtonsoft.Json.Linq;
using PhoenixAdult.Helpers;
using PhoenixAdult.Helpers.Utils;

namespace PhoenixAdult.Sites
{
    public class SitePornhub : IProviderBase
    {
        public async Task<List<RemoteSearchResult>> Search(int[] siteNum, string searchTitle, DateTime? searchDate, CancellationToken cancellationToken)
        {
            Logger.Debug($"SitePornhub-Search() Starting ********************");
            Logger.Debug($"SitePornhub-Search() searchTitle: {searchTitle}");

            var result = new List<RemoteSearchResult>();
            if (siteNum == null || string.IsNullOrEmpty(searchTitle))
            {
                Logger.Debug($"SitePornhub-Search() Leaving early empty search title ********************");
                return result;
            }

            Logger.Debug($"SitePornhub-Search() Searching for results");

            if ((searchTitle.StartsWith("ph", StringComparison.OrdinalIgnoreCase) || int.TryParse(searchTitle, out _)) && !searchTitle.Contains(' ', StringComparison.OrdinalIgnoreCase))
            {
                Logger.Debug($"SitePornhub-Search() Found video ID: {searchTitle}");

                var sceneURL = new Uri(Helper.GetSearchBaseURL(siteNum) + $"/view_video.php?viewkey={searchTitle}");
                var sceneID = new string[] { Helper.Encode(sceneURL.PathAndQuery) };

                var searchResult = await Helper.GetSearchResultsFromUpdate(this, siteNum, sceneID, searchDate, cancellationToken).ConfigureAwait(false);
                if (searchResult.Any())
                {
                    Logger.Debug($"SitePornhub-Search() Found title: {searchResult.First().Name}");
                    result.AddRange(searchResult);
                }
            }
            else
            {
                Logger.Debug($"SitePornhub-Search() Searching for title: {searchTitle}");

                searchTitle = searchTitle.Replace(" ", "+", StringComparison.OrdinalIgnoreCase);
                var url = Helper.GetSearchSearchURL(siteNum) + searchTitle;
                var data = await HTML.ElementFromURL(url, cancellationToken).ConfigureAwait(false);

                var searchResults = data.SelectNodesSafe("//ul[@id='videoSearchResult']/li[@data-video-vkey]");
                foreach (var searchResult in searchResults)
                {
                    var sceneURL = new Uri(Helper.GetSearchBaseURL(siteNum) + searchResult.SelectSingleText(".//a/@href"));
                    string curID = Helper.Encode(sceneURL.PathAndQuery),
                        sceneName = searchResult.SelectSingleText(".//span[@class='title']"),
                        scenePoster = searchResult.SelectSingleText(".//div[@class='phimage']//img/@data-thumb_url");

                    Logger.Debug($"SitePornhub-Search() Found title: {sceneName}");

                    var res = new RemoteSearchResult
                    {
                        ProviderIds = { { Plugin.Instance.Name, curID } },
                        Name = sceneName,
                        ImageUrl = scenePoster,
                    };

                    result.Add(res);
                }
            }

            Logger.Debug($"SitePornhub-Search() Search results: Found {result.Count} results for searchTitle: {searchTitle}");
            Logger.Debug($"SitePornhub-Search() Leaving  ********************");

            return result;
        }

        public async Task<MetadataResult<BaseItem>> Update(int[] siteNum, string[] sceneID, CancellationToken cancellationToken)
        {
            Logger.Debug($"SitePornhub-Update() Starting ********************");

            var result = new MetadataResult<BaseItem>()
            {
                Item = new Movie(),
                People = new List<PersonInfo>(),
            };

            if (sceneID == null)
            {
                Logger.Debug($"SitePornhub-Update() Leaving early empty sceneID ********************");
                return result;
            }

            var sceneURL = Helper.Decode(sceneID[0]);
            if (!sceneURL.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                sceneURL = Helper.GetSearchBaseURL(siteNum) + sceneURL;
            }

            Logger.Info($"SitePornhub-Update() Loading scene: {sceneURL}");

            var http = await HTTP.Request(sceneURL, HttpMethod.Post, cancellationToken).ConfigureAwait(false);
            var sceneData = HTML.ElementFromStream(http.ContentStream);
            var json = sceneData.SelectSingleText("//script[@type='application/ld+json']");
            JObject sceneDataJSON = null;
            if (!string.IsNullOrEmpty(json))
            {
                sceneDataJSON = JObject.Parse(json);
            }

            result.Item.ExternalId = sceneURL;

            result.Item.Name = sceneData.SelectSingleText("//h1[@class='title']");
            var studioName = sceneData.SelectSingleText("//div[@class='userInfo']//a");
            result.Item.AddStudio("Pornhub");

            if (!string.IsNullOrEmpty(studioName))
            {
                result.Item.AddStudio(studioName);
            }

            Logger.Debug($"SitePornhub-Update() Title: {result.Item.Name}");

            if (sceneDataJSON != null)
            {
                Logger.Debug($"SitePornhub-Update() Parsing JSON for upload date");

                var date = (string)sceneDataJSON["uploadDate"];
                if (date != null)
                {
                    Logger.Debug($"SitePornhub-Update() Found date - converting");

                    if (DateTime.TryParse(date, CultureInfo.InvariantCulture, DateTimeStyles.None, out var sceneDateObj))
                    {
                        Logger.Debug($"SitePornhub-Update() Premier date added");

                        result.Item.PremiereDate = sceneDateObj;
                    }
                }
            }

            Logger.Debug($"SitePornhub-Update() Processing Genres");

            var genreNode = sceneData.SelectNodesSafe("(//div[@class='categoriesWrapper'] | //div[@class='tagsWrapper'])/a");
            foreach (var genreLink in genreNode)
            {
                Logger.Debug($"SitePornhub-Update() Found genre: {genreLink.InnerText}");

                var genreName = genreLink.InnerText;

                result.Item.AddGenre(genreName);
            }

            Logger.Debug($"SitePornhub-Update() Processing Actors");

            var actorsNode = sceneData.SelectNodesSafe("//div[contains(@class, 'pornstarsWrapper')]/a");

            foreach (var actorLink in actorsNode)
            {
                Logger.Debug($"SitePornhub-Update() Found actor: {actorLink.InnerText}");

                string actorName = actorLink.InnerText,
                        actorPhotoURL = actorLink.SelectSingleText(".//img[@class='avatar']/@src");

                result.People.Add(new PersonInfo
                {
                    Name = actorName,
                    ImageUrl = actorPhotoURL,
                });
            }

            Logger.Debug($"SitePornhub-Update() Updated title: {result.Item.Name}");
            Logger.Debug($"SitePornhub-Update() Leaving  ********************");

            return result;
        }

        public async Task<IEnumerable<RemoteImageInfo>> GetImages(int[] siteNum, string[] sceneID, BaseItem item, CancellationToken cancellationToken)
        {
            Logger.Debug($"SitePornhub-GetImages() Starting ********************");

            var result = new List<RemoteImageInfo>();

            if (sceneID == null)
            {
                Logger.Debug($"SitePornhub-GetImages() Leaving early empty sceneID ********************");
                return result;
            }

            var sceneURL = Helper.Decode(sceneID[0]);
            if (!sceneURL.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                sceneURL = Helper.GetSearchBaseURL(siteNum) + sceneURL;
            }

            var http = await HTTP.Request(sceneURL, HttpMethod.Post, cancellationToken).ConfigureAwait(false);
            var sceneData = HTML.ElementFromStream(http.ContentStream);

            var img = sceneData.SelectSingleText("//div[@id='player']//img/@src");
            if (!string.IsNullOrEmpty(img))
            {
                Logger.Debug($"SitePornhub-GetImages() Processing image");

                result.Add(new RemoteImageInfo
                {
                    Url = img,
                    Type = ImageType.Primary,
                });
            }

            Logger.Debug($"SitePornhub-GetImages() Found {result.Count()} images");
            Logger.Debug($"SitePornhub-GetImages() Leaving  ********************");

            return result;
        }
    }
}
