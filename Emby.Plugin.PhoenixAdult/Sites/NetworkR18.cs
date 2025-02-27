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
    public class NetworkR18 : IProviderBase
    {
        private static readonly IDictionary<string, string> CensoredWords = new Dictionary<string, string>
        {
            { "A*****t", "Assault" },
            { "A****p", "Asleep" },
            { "A***e", "Abuse" },
            { "B***d", "Blood" },
            { "B**d", "Bled" },
            { "C***d", "Child" },
            { "C*ck", "Cock" },
            { "D******e", "Disgrace" },
            { "D***king", "Drinking" },
            { "D***k", "Drunk" },
            { "D**g", "Drug" },
            { "F*****g", "Forcing" },
            { "F***e", "Force" },
            { "G*******g", "Gangbang" },
            { "G******g", "Gang Bang" },
            { "H*********n", "Humiliation" },
            { "H*******e", "Hypnotize" },
            { "H*******m", "Hypnotism" },
            { "H**t", "Hurt" },
            { "I****t", "Incest" },
            { "K****p", "Kidnap" },
            { "K****r", "Killer" },
            { "K**l", "Kill" },
            { "K*d", "Kid" },
            { "M************n", "Mother And Son" },
            { "M****t", "Molest" },
            { "P********t", "Passed Out" },
            { "P****h", "Punish" },
            { "R****g", "Raping" },
            { "R**e", "Rape" },
            { "RStepB****************r", "Stepbrother and Sister" },
            { "S*********l", "School Girl" },
            { "S********l", "Schoolgirl" },
            { "S******g", "Sleeping" },
            { "S*****t", "Student" },
            { "S***e", "Slave" },
            { "S**t", "Scat" },
            { "Sch**l", "School" },
            { "StepM************n", "Stepmother and Son" },
            { "T******e", "Tentacle" },
            { "T*****e", "Torture" },
            { "U*********s", "Unconscious" },
            { "V*****e", "Violate" },
            { "V*****t", "Violent" },
            { "Y********l", "Young Girl" },
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

            string searchJAVID = null;
            var splitedTitle = searchTitle.Split();
            if (splitedTitle.Length > 1 && int.TryParse(splitedTitle[1], out _))
            {
                searchJAVID = $"{splitedTitle[0]}-{splitedTitle[1]}";
                searchTitle = searchJAVID;
            }

            var url = Helper.GetSearchSearchURL(siteNum) + searchTitle.Replace("-", " ", 1, StringComparison.OrdinalIgnoreCase);
            var data = await HTML.ElementFromURL(url, cancellationToken).ConfigureAwait(false);

            Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Searching for results");

            var searchResults = data.SelectNodesSafe("//li[contains(@class, 'item-list')]");
            Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Found results {searchResults.Count}");

            foreach (var searchResult in searchResults)
            {
                var sceneUrl = new Uri(searchResult.SelectSingleText(".//a/@href"));
                string curID = Helper.Encode(sceneUrl.AbsolutePath),
                    sceneName = Decensor(searchResult.SelectSingleText(".//dt")),
                    scenePoster = searchResult.SelectSingleText(".//img/@data-original"),
                    javID = searchResult.SelectSingleText(".//img/@alt");

                Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Processing {sceneUrl}");
                Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Found title: {sceneName}");

                var res = new RemoteSearchResult
                {
                    ProviderIds = { { Plugin.Instance.Name, curID } },
                    Name = $"{javID} {sceneName}",
                    ImageUrl = scenePoster,
                };

                if (!string.IsNullOrEmpty(searchJAVID))
                {
                    res.IndexNumber = 100 - LevenshteinDistance.Calculate(searchJAVID, javID, StringComparison.OrdinalIgnoreCase);
                }

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

            var javID = sceneData.SelectSingleText("//dt[text()='DVD ID:']/following-sibling::dd[1]");
            if (javID.StartsWith("--", StringComparison.OrdinalIgnoreCase))
            {
                javID = sceneData.SelectSingleText("//dt[text()='Content ID:']/following-sibling::dd[1]");
            }

            if (javID.Contains(' ', StringComparison.OrdinalIgnoreCase))
            {
                javID = javID.Replace(" ", "-", StringComparison.OrdinalIgnoreCase);
            }

            result.Item.OriginalTitle = javID.ToUpperInvariant();
            result.Item.Name = Decensor(sceneData.SelectSingleText("//cite[@itemprop='name']"));
            Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): title: {result.Item.Name}");

            result.Item.Overview = Decensor(sceneData.SelectSingleText("//div[@class='cmn-box-description01']").Replace("Product Description", string.Empty, StringComparison.OrdinalIgnoreCase));
            if (Plugin.Instance.Configuration.EnableDebugging)
            {
                Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): overview: {result.Item.Overview}");
            }

            var studio = sceneData.SelectSingleText("//dd[@itemprop='productionCompany']");
            if (!string.IsNullOrEmpty(studio))
            {
                result.Item.AddStudio(studio);
                if (Plugin.Instance.Configuration.EnableDebugging)
                {
                    Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): studio: {studio}");
                }
            }

            if (Plugin.Instance.Configuration.EnableDebugging)
            {
                Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Finding Premier date");
            }

            var date = sceneData.SelectSingleText("//dd[@itemprop='dateCreated']");
            if (!string.IsNullOrEmpty(date))
            {
                date = date
                    .Replace(".", string.Empty, StringComparison.OrdinalIgnoreCase)
                    .Replace(",", string.Empty, StringComparison.OrdinalIgnoreCase)
                    .Replace("Sept", "Sep", StringComparison.OrdinalIgnoreCase)
                    .Replace("June", "Jun", StringComparison.OrdinalIgnoreCase)
                    .Replace("July", "Jul", StringComparison.OrdinalIgnoreCase)
                    .Trim();

                if (DateTime.TryParseExact(date, "MMM dd yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var sceneDateObj))
                {
                    result.Item.PremiereDate = sceneDateObj;
                    if (Plugin.Instance.Configuration.EnableDebugging)
                    {
                        Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Premier date added {sceneDateObj.ToString()}");
                    }
                }
            }

            if (Plugin.Instance.Configuration.EnableDebugging)
            {
                Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Processing Genres");
            }

            var genreNode = sceneData.SelectNodesSafe("//a[@itemprop='genre']");
            foreach (var genreLink in genreNode)
            {
                var genreName = genreLink.InnerText;
                genreName = Decensor(genreName);

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

            var actorsNode = sceneData.SelectNodesSafe("//div[@itemprop='actors']//span[@itemprop='name']");
            foreach (var actorLink in actorsNode)
            {
                var actorName = actorLink.InnerText;

                if (actorName != "----")
                {
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

                    var photoXpath = string.Format(CultureInfo.InvariantCulture, "//div[@id='{0}']//img[contains(@alt, '{1}')]/@src", actorName.Replace(" ", string.Empty, StringComparison.OrdinalIgnoreCase), actorName);
                    var actorPhoto = sceneData.SelectSingleText(photoXpath);

                    if (!actorPhoto.Contains("nowprinting.gif", StringComparison.OrdinalIgnoreCase))
                    {
                        actor.ImageUrl = actorPhoto;
                        if (Plugin.Instance.Configuration.EnableDebugging)
                        {
                            Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Found actor photoURL: {actorPhoto}");
                        }
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

            if (Plugin.Instance.Configuration.EnableDebugging)
            {
                Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Processing image");
            }

            var img = sceneData.SelectSingleText("//img[contains(@alt, 'cover')]/@src");
            result.Add(new RemoteImageInfo
            {
                Url = img,
                Type = ImageType.Primary,
            });

            var imgNodes = sceneData.SelectNodesSafe("//section[@id='product-gallery']//img");
            foreach (var sceneImages in imgNodes)
            {
                if (Plugin.Instance.Configuration.EnableDebugging)
                {
                    Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Processing image");
                }

                result.Add(new RemoteImageInfo
                {
                    Url = sceneImages.Attributes["data-src"].Value,
                    Type = ImageType.Primary,
                });

                result.Add(new RemoteImageInfo
                {
                    Url = sceneImages.Attributes["data-src"].Value,
                    Type = ImageType.Backdrop,
                });
            }

            Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): **** Leaving - Found {result.Count} images");

            return result;
        }

        private static string Decensor(string text)
        {
            var result = text;

            foreach (var word in CensoredWords)
            {
                if (!result.Contains('*', StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                result = result.Replace(word.Key, word.Value, StringComparison.OrdinalIgnoreCase);
            }

            return result;
        }
    }
}
