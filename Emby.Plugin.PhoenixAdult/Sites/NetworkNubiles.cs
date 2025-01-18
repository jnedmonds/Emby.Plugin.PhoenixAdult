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
    public class NetworkNubiles : IProviderBase
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

            if (searchDate.HasValue)
            {
                Logger.Debug($"NetworkNubiles-Search() Searching for results with date: {searchDate.ToString()}");

                var date = searchDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

                // var url = Helper.GetSearchSearchURL(siteNum) + $"date/{date}/{date}";
                var url = Helper.GetSearchSearchURL(siteNum) + $"date/{date}";
                var data = await HTML.ElementFromURL(url, cancellationToken).ConfigureAwait(false);

                Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Searching for results");

                var searchResults = data.SelectNodesSafe("//div[contains(@class, 'content-grid-item')]");
                Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Found results {searchResults.Count}");

                foreach (var searchResult in searchResults)
                {
                    var sceneNum = searchResult.SelectSingleText(".//span[@class='title']/a/@href").Split('/')[3];
                    var sceneUrl = new Uri(Helper.GetSearchSearchURL(siteNum) + $"watch/{sceneNum}");
                    string curID = Helper.Encode(sceneUrl.AbsolutePath),
                        sceneName = searchResult.SelectSingleText(".//span[@class='title']/a | //h2"),
                        posterURL = searchResult.SelectSingleText(".//picture/img/@data-srcset").Split(",")[0].Split(" ")[0],
                        sceneDate = searchResult.SelectSingleText(".//span[@class='date']");
                    Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Processing {sceneUrl}");
                    Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Found title: {sceneName}");

                    var res = new RemoteSearchResult
                    {
                        ProviderIds = { { Plugin.Instance.Name, curID } },
                        Name = sceneName,
                        ImageUrl = posterURL,
                    };

                    if (DateTime.TryParseExact(sceneDate, "MMM d, yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var sceneDateObj))
                    {
                        res.PremiereDate = sceneDateObj;
                    }

                    result.Add(res);
                }
            }
            else
            {
                Logger.Debug($"NetworkNubiles-Search() No date found so searching via sceneID");

                if (int.TryParse(searchTitle.Split()[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var sceneNum))
                {
                    Logger.Debug($"NetworkNubiles-Search() sceneID: {sceneNum.ToString()}");

                    var url = Helper.GetSearchSearchURL(siteNum) + $"watch/{sceneNum}";
                    var sceneURL = new Uri(url);
                    var sceneID = new string[] { Helper.Encode(sceneURL.AbsolutePath) };

                    Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Searching for results");

                    var searchResult = await Helper.GetSearchResultsFromUpdate(this, siteNum, sceneID.ToArray(), searchDate, cancellationToken).ConfigureAwait(false);
                    if (searchResult.Any())
                    {
                        result.AddRange(searchResult);
                        Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Found results {searchResult.Count}");
                    }
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

            result.Item.Name = sceneData.SelectSingleText("//div[contains(@class, 'content-pane-title')]//h2");
            Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): title: {result.Item.Name}");

            var description = sceneData.SelectSingleText("//div[@class='col-12 content-pane-column']/div");
            if (string.IsNullOrEmpty(description))
            {
                var paragraphs = sceneData.SelectNodesSafe("//div[@class='col-12 content-pane-column']//p");
                foreach (var paragraph in paragraphs)
                {
                    description += "\n\n" + paragraph.InnerText.Trim();
                }
            }

            if (!string.IsNullOrEmpty(description))
            {
                result.Item.Overview = description;
                if (Plugin.Instance.Configuration.EnableDebugging)
                {
                    Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): overview: {result.Item.Overview}");
                }
            }

            result.Item.AddStudio("Nubiles");
            if (Plugin.Instance.Configuration.EnableDebugging)
            {
                Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): studio: {result.Item.Studios[0]}");
            }

            if (Plugin.Instance.Configuration.EnableDebugging)
            {
                Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Finding Premier date");
            }

            var sceneDate = sceneData.SelectSingleText("//div[contains(@class, 'content-pane')]//span[@class='date']");
            if (DateTime.TryParseExact(sceneDate, "MMM d, yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var sceneDateObj))
            {
                result.Item.PremiereDate = sceneDateObj;
                if (Plugin.Instance.Configuration.EnableDebugging)
                {
                    Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Premier date added {sceneDateObj.ToString()}");
                }
            }

            if (Plugin.Instance.Configuration.EnableDebugging)
            {
                Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Processing Genres");
            }

            var genreNode = sceneData.SelectNodesSafe("//div[@class='categories']/a");
            foreach (var genreLink in genreNode)
            {
                Logger.Debug($"SitePornhub-Update() Found genre: {genreLink.InnerText}");

                var genreName = genreLink.InnerText;

                result.Item.AddGenre(genreName);
                if (Plugin.Instance.Configuration.EnableDebugging)
                {
                    Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Found genre: {genreName}");
                }
            }

            if (Plugin.Instance.Configuration.EnableDebugging)
            {
                Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Processing Actors");
            }

            var actorsNode = sceneData.SelectNodesSafe("//div[contains(@class, 'content-pane-performer')]/a");
            foreach (var actorLink in actorsNode)
            {
                Logger.Debug($"SitePornhub-Update() Found actor: {actorLink.InnerText}");

                string actorName = actorLink.InnerText,
                    actorPageURL = Helper.GetSearchBaseURL(siteNum) + actorLink.Attributes["href"].Value;

                var actorPage = await HTML.ElementFromURL(actorPageURL, cancellationToken).ConfigureAwait(false);
                var actorPhotoURL = "http:" + actorPage.SelectSingleText("//div[contains(@class, 'model-profile')]//img/@src");

                var actor = new PersonInfo
                {
                    Name = actorName,
                    ImageUrl = actorPhotoURL,
                };

                if (Plugin.Instance.Configuration.EnableDebugging)
                {
                    Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Found actor: {actorName}");
                    Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Found actor photoURL: {actorPhotoURL}");
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

            var sceneURL = Helper.Decode(sceneID[0]);
            Logger.Debug($"SceneURL: {sceneURL}");
            if (!sceneURL.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                sceneURL = Helper.GetSearchBaseURL(siteNum) + sceneURL;
            }

            var sceneData = await HTML.ElementFromURL(sceneURL, cancellationToken).ConfigureAwait(false);

            var poster = sceneData.SelectSingleText("//video/@poster");
            if (!string.IsNullOrEmpty(poster))
            {
                if (Plugin.Instance.Configuration.EnableDebugging)
                {
                    Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Processing image");
                }

                result.Add(new RemoteImageInfo
                {
                    Url = poster,
                    Type = ImageType.Backdrop,
                });
            }

            var posterLink = sceneData.SelectSingleText("//*[contains(@class, 'icon-camera')]/../@href");
            var posterData = await HTML.ElementFromURL(posterLink, cancellationToken).ConfigureAwait(false);
            if (Plugin.Instance.Configuration.EnableDebugging)
            {
                Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Processing pics page: {posterLink}");
            }

            var posterImages = sceneData.SelectNodesSafe("//div[contains(@class, 'photo-grid')]//img");
            foreach (var pic in posterImages)
            {
                result.Add(new RemoteImageInfo
                {
                    Url = $"https" + pic.Attributes["src"].Value,
                    Type = ImageType.Primary,
                });

                result.Add(new RemoteImageInfo
                {
                    Url = $"https" + pic.Attributes["src"].Value,
                    Type = ImageType.Backdrop,
                });

                if (Plugin.Instance.Configuration.EnableDebugging)
                {
                    Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Processing image");
                }
            }

            /*
            var photoPageURL = "https://nubiles-porn.com/photo/gallery/" + sceneID[0];
            var photoPage = await HTML.ElementFromURL(photoPageURL, cancellationToken).ConfigureAwait(false);
            var sceneImages = photoPage.SelectNodesSafe("//div[@class='img-wrapper']//source[1]");
            foreach (var sceneImage in sceneImages)
            {
                if (Plugin.Instance.Configuration.EnableDebugging)
                {
                    Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Processing image");
                }

                var posterURL = sceneImage.Attributes["src"].Value;

                result.Add(new RemoteImageInfo
                {
                    Url = posterURL,
                    Type = ImageType.Backdrop,
                });
            }
            */

            Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): **** Leaving - Found {result.Count} images");

            return result;
        }
    }
}
