using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
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
            Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): ***** Starting - searchTitle: {searchTitle}");

            var result = new List<RemoteSearchResult>();
            if (siteNum == null || string.IsNullOrEmpty(searchTitle))
            {
                Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): ***** Leaving early empty search title");
                return result;
            }

            Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Searching for results");

            if ((searchTitle.StartsWith("ph", StringComparison.OrdinalIgnoreCase) || int.TryParse(searchTitle, out _)) && !searchTitle.Contains(' ', StringComparison.OrdinalIgnoreCase))
            {
                Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Found video ID: {searchTitle}");

                var sceneURL = new Uri(Helper.GetSearchBaseURL(siteNum) + $"/view_video.php?viewkey={searchTitle}");
                var sceneID = new string[] { Helper.Encode(sceneURL.PathAndQuery) };

                var searchResult = await Helper.GetSearchResultsFromUpdate(this, siteNum, sceneID, searchDate, cancellationToken).ConfigureAwait(false);
                if (searchResult.Any())
                {
                    Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Found title: {searchResult.First().Name}");
                    result.AddRange(searchResult);
                }
            }
            else
            {
                Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Searching for results");

                searchTitle = searchTitle.Replace(" ", "+", StringComparison.OrdinalIgnoreCase);
                var searchURL = Helper.GetSearchSearchURL(siteNum) + searchTitle;
                var searchData = await HTML.ElementFromURL(searchURL, cancellationToken).ConfigureAwait(false);

                var searchResultNodes = searchData.SelectNodesSafe("//ul[@id='videoSearchResult']/li[@data-video-vkey]");

                Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Found results {searchResultNodes.Count} now processing");

                foreach (var searchResult in searchResultNodes)
                {
                    var sceneUrl = new Uri(Helper.GetSearchBaseURL(siteNum) + searchResult.SelectSingleText(".//a/@href"));

                    Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Processing {sceneUrl}");

                    string curID = Helper.Encode(sceneUrl.PathAndQuery);
                    string sceneName = searchResult.SelectSingleText(".//span[@class='title']");
                    string scenePoster = searchResult.SelectSingleText(".//div[@class='phimage']//img/@data-thumb_url");

                    Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Found title: {sceneName}");

                    var res = new RemoteSearchResult
                    {
                        ProviderIds = { { Plugin.Instance.Name, curID } },
                        Name = sceneName,
                        ImageUrl = scenePoster,
                    };

                    result.Add(res);
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
                Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): **** Leaving early empty sceneID");
                return result;
            }

            var sceneURL = Helper.Decode(sceneID[0]);
            if (!sceneURL.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                sceneURL = Helper.GetSearchBaseURL(siteNum) + sceneURL;
            }

            Logger.Info($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Loading scene: {sceneURL}");

            // var http = await HTTP.Request(sceneURL, HttpMethod.Post, cancellationToken).ConfigureAwait(false);
            // var sceneData = HTML.ElementFromStream(http.ContentStream);
            var sceneData = await HTML.ElementFromURL(sceneURL, cancellationToken).ConfigureAwait(false);

            var json = sceneData.SelectSingleText("//script[@type='application/ld+json']");
            JObject sceneDataJSON = null;
            if (!string.IsNullOrEmpty(json))
            {
                sceneDataJSON = JObject.Parse(json);
            }

            result.Item.ExternalId = sceneURL;

            if (Plugin.Instance.Configuration.EnableDebugging)
            {
                Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): externalID: {result.Item.ExternalId}");
            }

            result.Item.Name = sceneData.SelectSingleText("//h1[@class='title']");

            Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): title: {result.Item.Name}");

            result.Item.AddStudio("Pornhub");

            if (Plugin.Instance.Configuration.EnableDebugging)
            {
                Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): studio: Pornhub");
            }

            var subSites = sceneData.SelectNodesSafe("//div[@class='userInfo']//a");
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

            if (sceneDataJSON != null)
            {
                if (Plugin.Instance.Configuration.EnableDebugging)
                {
                    Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Parsing JSON for upload date");
                }

                var date = (string)sceneDataJSON["uploadDate"];
                if (date != null)
                {
                    if (Plugin.Instance.Configuration.EnableDebugging)
                    {
                        Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Found date - converting");
                    }

                    if (DateTime.TryParse(date, CultureInfo.InvariantCulture, DateTimeStyles.None, out var sceneDateObj))
                    {
                        if (Plugin.Instance.Configuration.EnableDebugging)
                        {
                            Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Premier date added {sceneDateObj.ToString()}");
                        }

                        result.Item.PremiereDate = sceneDateObj;
                    }
                }
            }

            if (Plugin.Instance.Configuration.EnableDebugging)
            {
                Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Processing Genres");
            }

            var genreNode = sceneData.SelectNodesSafe("(//div[@class='categoriesWrapper'] | //div[@class='tagsWrapper'])/a");
            foreach (var genreLink in genreNode)
            {
                if (Plugin.Instance.Configuration.EnableDebugging)
                {
                    Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Found genre: {genreLink.InnerText.Trim()}");
                }

                result.Item.AddGenre(genreLink.InnerText);
            }

            if (Plugin.Instance.Configuration.EnableDebugging)
            {
                Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Processing Actors");
            }

            var actorsNode = sceneData.SelectNodesSafe("//div[contains(@class, 'pornstarsWrapper')]/a");
            foreach (var actorLink in actorsNode)
            {
                string actorName = actorLink.InnerText;
                string actorPhotoURL = actorLink.SelectSingleText(".//img[@class='avatar']/@src");

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
                    res.ImageUrl = actorPhotoURL;

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
                Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): **** Leaving early empty sceneID");
                return result;
            }

            var sceneURL = Helper.Decode(sceneID[0]);
            if (!sceneURL.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                sceneURL = Helper.GetSearchBaseURL(siteNum) + sceneURL;
            }

            var sceneData = await HTML.ElementFromURL(sceneURL, cancellationToken).ConfigureAwait(false);

            var imageUrl = sceneData.SelectSingleText("//div[@id='player']//img/@src");
            if (!string.IsNullOrEmpty(imageUrl))
            {
                Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Processing image");

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
