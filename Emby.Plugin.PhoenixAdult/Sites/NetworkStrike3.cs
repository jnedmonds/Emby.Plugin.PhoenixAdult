using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
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
    public class NetworkStrike3 : IProviderBase
    {
        private readonly string searchVariables = "{{\"query\":\"{0}\",\"site\":\"{1}\",\"first\":10,\"skip\":0}}";
        private readonly string searchQuery = @"query getSearchResults($query:String!,$site:Site!,$first:Int,$skip:Int){searchVideos(input:{query:$query,site:$site,first:$first,skip:$skip}){edges{node{videoId title releaseDate slug images{listing{src}}}}}}";
        private readonly string updateVariables = "{{\"slug\":\"{0}\",\"site\":\"{1}\"}}";
        private readonly string updateQuery = @"query getSearchResults($slug:String!,$site:Site!){findOneVideo(input:{slug:$slug,site:$site}){videoId title description releaseDate models{name slug images{listing{highdpi{double}}}}directors{name}categories{name}carousel{listing{highdpi{triple}}}}}";

        public static async Task<JObject> GetDataFromAPI(string url, string query, string variables, CancellationToken cancellationToken)
        {
            JObject json = null;

            var param = new StringContent($"{{\"query\":\"{query}\",\"variables\":{variables}}}", Encoding.UTF8, "application/json");

            Logger.Debug($"{url}, {query}, {variables}");

            var http = await HTTP.Request(url, HttpMethod.Post, param, cancellationToken).ConfigureAwait(false);

            if (http.IsOK)
            {
                Logger.Debug("http.Content: " + http.Content);
                json = (JObject)JObject.Parse(http.Content)["data"];
                Logger.Debug("content to jobject ok");
            }

            return json;
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

            var variables = string.Format(this.searchVariables, searchTitle, Helper.GetSearchSiteName(siteNum).ToUpper());
            var url = Helper.GetSearchSearchURL(siteNum);
            Logger.Debug($"search: {variables}, {url}");

            Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Searching for results");

            var searchResults = await GetDataFromAPI(url, this.searchQuery, variables, cancellationToken).ConfigureAwait(false);
            if (searchResults == null)
            {
                Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): ***** Leaving early empty search from API");
                return result;
            }

            Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Found results {searchResults.Count} now processing");

            foreach (var searchResult in searchResults["searchVideos"]["edges"])
            {
                string sceneUrl = (string)searchResult["node"]["slug"],
                        curID = Helper.Encode(sceneUrl),
                        sceneName = (string)searchResult["node"]["title"],
                        scenePoster = (string)searchResult["node"]["images"]["listing"].First()["src"];
                var sceneDateObj = (DateTime)searchResult["node"]["releaseDate"];
                Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Processing {sceneUrl}");
                Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Found title: {sceneName}");

                var res = new RemoteSearchResult
                {
                    ProviderIds = { { Plugin.Instance.Name, curID } },
                    Name = sceneName,
                    ImageUrl = scenePoster,
                    PremiereDate = sceneDateObj,
                };

                result.Add(res);
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

            var sceneURL = Helper.Decode(sceneID[0]).TrimStart('/');
            Logger.Info($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Loading scene: {sceneURL}");

            var variables = string.Format(this.updateVariables, sceneURL, Helper.GetSearchSiteName(siteNum).ToUpper());
            var url = Helper.GetSearchSearchURL(siteNum);
            Logger.Debug($"update: {variables}, {url}");

            var sceneData = await GetDataFromAPI(url, this.updateQuery, variables, cancellationToken).ConfigureAwait(false);
            if (sceneData == null)
            {
                Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Leaving early empty search from API");
                return result;
            }

            sceneData = (JObject)sceneData["findOneVideo"];

            result.Item.ExternalId = Helper.GetSearchBaseURL(siteNum) + $"/videos/{sceneURL}";
            if (Plugin.Instance.Configuration.EnableDebugging)
            {
                Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): externalID: {result.Item.ExternalId}");
            }

            result.Item.Name = (string)sceneData["title"];
            Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): title: {result.Item.Name}");

            result.Item.Overview = (string)sceneData["description"];
            if (Plugin.Instance.Configuration.EnableDebugging)
            {
                Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): overview: {result.Item.Overview}");
            }

            result.Item.AddStudio(Helper.GetSearchSiteName(siteNum));
            if (Plugin.Instance.Configuration.EnableDebugging)
            {
                Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): studio: {result.Item.Studios[0]}");
            }

            if (Plugin.Instance.Configuration.EnableDebugging)
            {
                Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Finding Premier date");
            }

            var sceneDateObj = (DateTime)sceneData["releaseDate"];
            result.Item.PremiereDate = sceneDateObj;
            if (Plugin.Instance.Configuration.EnableDebugging)
            {
                Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Premier date added {sceneDateObj.ToString()}");
            }

            if (Plugin.Instance.Configuration.EnableDebugging)
            {
                Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Processing Genres");
            }

            foreach (var genreLink in sceneData["categories"])
            {
                result.Item.AddGenre((string)genreLink["name"]);
                if (Plugin.Instance.Configuration.EnableDebugging)
                {
                    Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Found genre: {(string)genreLink["name"]}");
                }
            }

            if (Plugin.Instance.Configuration.EnableDebugging)
            {
                Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Processing Actors");
            }

            foreach (var actorLink in sceneData["models"])
            {
                var actorName = (string)actorLink["name"];

                var actor = new PersonInfo
                {
                    Name = actorName,
                };
                if (Plugin.Instance.Configuration.EnableDebugging)
                {
                    Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Found actor: {actorName}");
                }

                if (actorLink["images"].Any())
                {
                    actor.ImageUrl = (string)actorLink["images"]["listing"].First()["highdpi"]["double"];
                }

                if (Plugin.Instance.Configuration.EnableDebugging)
                {
                    Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Found actor photoURL: {actor.ImageUrl}");
                }

                result.People.Add(actor);
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

            var sceneURL = Helper.Decode(sceneID[0]).TrimStart('/');

            var variables = string.Format(this.updateVariables, sceneURL, Helper.GetSearchSiteName(siteNum).ToUpper());
            var url = Helper.GetSearchSearchURL(siteNum);

            var sceneData = await GetDataFromAPI(url, this.updateQuery, variables, cancellationToken).ConfigureAwait(false);
            if (sceneData == null)
            {
                Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Leaving early empty image");
                return result;
            }

            var video = (JObject)sceneData["findOneVideo"];

            foreach (var image in video["carousel"])
            {
                Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Processing image");

                var img = (string)image["listing"].First()["highdpi"]["triple"];

                result.Add(new RemoteImageInfo
                {
                    Url = img,
                    Type = ImageType.Primary,
                });
                result.Add(new RemoteImageInfo
                {
                    Url = img,
                    Type = ImageType.Backdrop,
                });
            }

            Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): **** Leaving - Found {result.Count} images");

            return result;
        }
    }
}
