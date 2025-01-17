using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using PhoenixAdult.Helpers;
using PhoenixAdult.Helpers.Utils;

namespace PhoenixAdult.Sites
{
    public class NetworkPornPros : IProviderBase
    {
        private static readonly Dictionary<string, string[]> Genres = new Dictionary<string, string[]>
        {
            { "Lubed", new[] { "Lube", "Raw", "Wet" } },
            { "Holed", new[] { "Anal", "Ass" } },
            { "POVD", new[] { "Gonzo", "Pov" } },
            { "MassageCreep", new[] { "Massage", "Oil" } },
            { "DeepThroatLove", new[] { "Blowjob", "Deep Throat" } },
            { "PureMature", new[] { "MILF", "Mature" } },
            { "Cum4K", new[] { "Creampie" } },
            { "GirlCum", new[] { "Orgasms", "Girl Orgasm", "Multiple Orgasms" } },
            { "PassionHD", new[] { "Hardcore" } },
            { "BBCPie", new[] { "Interracial", "BBC", "Creampie" } },
            { "Facials4k", new[] { "Facial" } },
        };

        public async Task<List<RemoteSearchResult>> Search(int[] siteNum, string searchTitle, DateTime? searchDate, CancellationToken cancellationToken)
        {
            Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): ***** Starting - searchTitle: {searchTitle}");

            var result = new List<RemoteSearchResult>();
            if (siteNum == null || string.IsNullOrEmpty(searchTitle))
            {
                Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): ***** Leaving early empty search title");
                return result;
            }

            var directURL = searchTitle
                .Replace(" ", "-", StringComparison.OrdinalIgnoreCase)
                .Replace("'", "-", StringComparison.OrdinalIgnoreCase);
            if (int.TryParse(directURL.AsSpan(directURL.Length - 1, 1), out _) && directURL.Substring(directURL.Length - 2, 1) == "-")
            {
                directURL = $"{directURL.Substring(0, directURL.Length - 1)}-{directURL.Substring(directURL.Length - 1, 1)}";
            }

            var sceneURL = new Uri(Helper.GetSearchSearchURL(siteNum) + directURL);
            var sceneID = new List<string> { Helper.Encode(sceneURL.AbsolutePath) };

            if (searchDate.HasValue)
            {
                sceneID.Add(searchDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            }

            Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Searching for results");

            var searchResult = await Helper.GetSearchResultsFromUpdate(this, siteNum, sceneID.ToArray(), searchDate, cancellationToken).ConfigureAwait(false);
            if (searchResult.Any())
            {
                result.AddRange(searchResult);
                Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Found results {searchResult.Count}");
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

            var subSite = Helper.GetSearchSiteName(siteNum);
            var sceneData = await HTML.ElementFromURL(sceneURL, cancellationToken).ConfigureAwait(false);

            result.Item.ExternalId = sceneURL;
            if (Plugin.Instance.Configuration.EnableDebugging)
            {
                Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): externalID: {result.Item.ExternalId}");
            }

            result.Item.Name = sceneData.SelectSingleText("//h1");
            Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): title: {result.Item.Name}");

            var description = sceneData.SelectSingleText("//div[contains(@id, 'description')]");
            if (string.IsNullOrEmpty(description))
            {
                // povd
                description = sceneData.SelectSingleText("//div[contains(@class, 'scene-info')]//div[@class='w-full flex flex-row space-x-4 items-start']/span");
            }

            result.Item.Overview = description;
            if (Plugin.Instance.Configuration.EnableDebugging)
            {
                Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): overview: {result.Item.Overview}");
            }

            result.Item.AddStudio("Porn Pros");
            if (Plugin.Instance.Configuration.EnableDebugging)
            {
                Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): studio: {result.Item.Studios[0]}");
            }

            result.Item.AddStudio(subSite);
            if (Plugin.Instance.Configuration.EnableDebugging)
            {
                Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): studio: {subSite}");
            }

            if (Plugin.Instance.Configuration.EnableDebugging)
            {
                Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Finding Premier date");
            }

            string date = sceneData.SelectSingleText("//div[@class='d-inline d-lg-block mb-1']/span"),
                sceneDate = string.Empty,
                dateFormat = string.Empty;
            if (!string.IsNullOrEmpty(date))
            {
                sceneDate = date;
                dateFormat = "MMMM dd, yyyy";
            }
            else
            {
                if (sceneID.Length > 1)
                {
                    sceneDate = sceneID[1];
                    dateFormat = "yyyy-MM-dd";
                }
            }

            if (!string.IsNullOrEmpty(sceneDate))
            {
                if (DateTime.TryParseExact(sceneDate, dateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var sceneDateObj))
                {
                    result.Item.PremiereDate = sceneDateObj;
                    if (Plugin.Instance.Configuration.EnableDebugging)
                    {
                        Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Premier date added {sceneDateObj.ToString()}");
                    }
                }
            }
            else
            {
                Logger.Warning($"Could not get date for '{result.Item.Name}'. Pulling for Metadata API");

                // get date from MetadataApi
                var metadataApiProvider = Helper.GetMetadataAPIProvider();
                var searchResults = await metadataApiProvider.Search(new int[] { 48, 0 }, result.Item.Name, null, cancellationToken);

                result.Item.PremiereDate = searchResults[0].PremiereDate;
                if (Plugin.Instance.Configuration.EnableDebugging)
                {
                    Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Premier date added {searchResults[0].PremiereDate.ToString()}");
                }
            }

            if (Plugin.Instance.Configuration.EnableDebugging)
            {
                Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Processing Genres");
            }

            if (Genres.ContainsKey(subSite))
            {
                foreach (var genreLink in Genres[subSite])
                {
                    var genreName = genreLink;

                    result.Item.AddGenre(genreName);
                    if (Plugin.Instance.Configuration.EnableDebugging)
                    {
                        Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Found genre: {genreName}");
                    }
                }
            }

            if (Plugin.Instance.Configuration.EnableDebugging)
            {
                Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Processing Actors");
            }

            var actorsNode = sceneData.SelectNodesSafe("//div[contains(@class, 'pt-md')]//a[contains(@href, '/girls/')]");
            if (actorsNode.Any())
            {
                foreach (var actorLink in actorsNode)
                {
                    var actorName = actorLink.InnerText;

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
            }
            else
            {
                actorsNode = sceneData.SelectNodesSafe("//div[contains(@class, 'scene-info')]//a");
                foreach (var actorLink in actorsNode)
                {
                    var actorName = actorLink.InnerText;

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

            var img = sceneData.SelectSingleText("//video[@id='player']/@poster");
            if (!string.IsNullOrEmpty(img))
            {
                Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Processing image");

                if (!img.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    img = "https:" + img;
                }

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

            var gallery = sceneData.SelectNodesSafe("//div[@id='trailer_player']//div[@class='hidden w-full lg:flex flex-row mt-2']/a/img");
            foreach (var image in gallery)
            {
                Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Processing gallery image");

                var url = image.Attributes["src"].Value;
                var uri = new Uri(url);
                result.Add(new RemoteImageInfo
                {
                    Url = uri.GetLeftPart(UriPartial.Path),
                    Type = ImageType.Primary,
                });
                result.Add(new RemoteImageInfo
                {
                    Url = uri.GetLeftPart(UriPartial.Path),
                    Type = ImageType.Backdrop,
                });
            }

            Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): **** Leaving - Found {result.Count} images");

            return result;
        }
    }
}
