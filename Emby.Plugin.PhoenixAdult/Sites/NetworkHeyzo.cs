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
using PhoenixAdult.Configuration;
using PhoenixAdult.Helpers;
using PhoenixAdult.Helpers.Utils;

namespace PhoenixAdult.Sites
{
    public class NetworkHeyzo : IProviderBase
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

            var splitedSearchTitle = searchTitle.Split();
            var movieID = string.Empty;
            if (int.TryParse(splitedSearchTitle[0], out _))
            {
                movieID = splitedSearchTitle[0];
            }

            if (!string.IsNullOrEmpty(movieID))
            {
                var sceneURL = new Uri(Helper.GetSearchBaseURL(siteNum) + $"/moviepages/{movieID}/index.html");
                var sceneID = new string[] { Helper.Encode(sceneURL.AbsolutePath) };

                var searchResult = await Helper.GetSearchResultsFromUpdate(this, siteNum, sceneID, searchDate, cancellationToken).ConfigureAwait(false);
                if (searchResult.Any())
                {
                    result.AddRange(searchResult);

                    Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Found results {searchResult.Count}");
                }
            }
            else
            {
                var url = string.Format(CultureInfo.InvariantCulture, Helper.GetSearchSearchURL(siteNum), searchTitle);
                var data = await HTML.ElementFromURL(url, cancellationToken).ConfigureAwait(false);

                var searchResults = data.SelectNodesSafe("//div[@id='movies']//div[contains(@class, 'movie')]");
                Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Found results {searchResults.Count} now processing");

                foreach (var searchResult in searchResults)
                {
                    var sceneURL = new Uri(Helper.GetSearchBaseURL(siteNum) + $"/en/?v={searchResult.SelectSingleText(".//a/@href")}");
                    string curID = Helper.Encode(sceneURL.AbsolutePath),
                        sceneName = searchResult.SelectSingleText(".//a[@class='actor']"),
                        sceneDate = searchResult.SelectSingleText(".//p[@class='release']").Replace("Release:", string.Empty, StringComparison.OrdinalIgnoreCase).Trim(),
                        scenePoster = Helper.GetSearchBaseURL(siteNum) + searchResult.SelectSingleText(".//img/@data-original");
                    Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Found title: {sceneName}");

                    var res = new RemoteSearchResult
                    {
                        ProviderIds = { { Plugin.Instance.Name, curID } },
                        Name = sceneName,
                        ImageUrl = scenePoster,
                    };

                    if (DateTime.TryParseExact(sceneDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var sceneDateObj))
                    {
                        res.PremiereDate = sceneDateObj;
                    }

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

            result.Item.Name = sceneData.SelectSingleText("//h1").Trim().Split("\n").First();
            Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): title: {result.Item.Name}");

            foreach (var movieInfo in sceneData.SelectNodesSafe("//table[@class='movieInfo']//tr"))
            {
                var cellNode = movieInfo.SelectNodesSafe("./td");
                switch (cellNode[0].InnerText)
                {
                    case "Released":
                        if (Plugin.Instance.Configuration.EnableDebugging)
                        {
                            Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Finding Premier date");
                        }

                        var date = cellNode[1].InnerText.Trim();
                        if (DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var sceneDateObj))
                        {
                            result.Item.PremiereDate = sceneDateObj;
                            if (Plugin.Instance.Configuration.EnableDebugging)
                            {
                                Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Premier date added {sceneDateObj.ToString()}");
                            }
                        }

                        break;

                    case "Sex Styles":
                        if (Plugin.Instance.Configuration.EnableDebugging)
                        {
                            Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Processing Genres");
                        }

                        var genreNode = cellNode[1].SelectNodesSafe(".//a[contains(@href, 'category')]");
                        foreach (var genreLink in genreNode)
                        {
                            var genreName = genreLink.InnerText;

                            if (!string.IsNullOrEmpty(genreName))
                            {
                                result.Item.AddGenre(genreName);
                                if (Plugin.Instance.Configuration.EnableDebugging)
                                {
                                    Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Found genre: {genreName}");
                                }
                            }
                        }

                        break;
                    case "Actress(es)":
                        if (Plugin.Instance.Configuration.EnableDebugging)
                        {
                            Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Processing Actors");
                        }

                        var actorsNode = cellNode[1].SelectNodesSafe(".//a[contains(@href, 'actor')]");
                        foreach (var actorLink in actorsNode)
                        {
                            var actorName = actorLink.InnerText;

                            switch (Plugin.Instance.Configuration.JAVActorNamingStyle)
                            {
                                case JAVActorNamingStyle.JapaneseStyle:
                                    actorName = string.Join(" ", actorName.Split().Reverse());

                                    break;
                            }

                            var actor = new PersonInfo
                            {
                                Name = actorName,
                            };
                            if (Plugin.Instance.Configuration.EnableDebugging)
                            {
                                Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Found actor: {actorName}");
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

            var movieID = sceneURL.Replace("/index.html", string.Empty, StringComparison.OrdinalIgnoreCase).Split("/").Last();
            if (!string.IsNullOrEmpty(movieID) && int.TryParse(movieID, out _))
            {
                if (Plugin.Instance.Configuration.EnableDebugging)
                {
                    Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Processing image");
                }

                result.Add(new RemoteImageInfo
                {
                    Url = $"https://en.heyzo.com/contents/3000/{movieID}/images/player_thumbnail_en.jpg",
                    Type = ImageType.Primary,
                });
            }

            for (var i = 0; i <= 21; i++)
            {
                if (Plugin.Instance.Configuration.EnableDebugging)
                {
                    Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Processing image");
                }

                string index = i.ToString(CultureInfo.InvariantCulture).PadLeft(3, '0'),
                    img = $"https://en.heyzo.com/contents/3000/{movieID}/gallery/{index}.jpg";

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
