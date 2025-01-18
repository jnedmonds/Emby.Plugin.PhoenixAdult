using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection.Emit;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using PhoenixAdult.Helpers;
using PhoenixAdult.Helpers.Utils;

namespace PhoenixAdult.Sites
{
    public class ActorFreeones : IProviderBase
    {
        public async Task<List<RemoteSearchResult>> Search(int[] siteNum, string actorName, DateTime? actorDate, CancellationToken cancellationToken)
        {
            Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): ***** Starting - searchTitle: {actorName}");

            var result = new List<RemoteSearchResult>();

            Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Searching for results");

            var url = Helper.GetSearchSearchURL(siteNum) + actorName;
            var actorData = await HTML.ElementFromURL(url, cancellationToken).ConfigureAwait(false);

            foreach (var actorNode in actorData.SelectNodesSafe("//div[contains(@class, 'grid-item')]"))
            {
                var actorURL = new Uri(Helper.GetSearchBaseURL(siteNum) + actorNode.SelectSingleText(".//a/@href").Replace("/feed", "/bio", StringComparison.OrdinalIgnoreCase));
                string curID = Helper.Encode(actorURL.AbsolutePath),
                    name = actorNode.SelectSingleText(".//p/@title"),
                    imageURL = actorNode.SelectSingleText(".//img/@src");

                Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Found actor {name}");

                var res = new RemoteSearchResult
                {
                    ProviderIds = { { Plugin.Instance.Name, curID } },
                    Name = name,
                    ImageUrl = imageURL,
                };

                result.Add(res);
            }

            Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): **** Leaving - Search results: Found {result.Count} results for searchTitle: {actorName}");

            return result;
        }

        public async Task<MetadataResult<BaseItem>> Update(int[] siteNum, string[] sceneID, CancellationToken cancellationToken)
        {
            Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): **** Starting");

            var result = new MetadataResult<BaseItem>()
            {
                Item = new Person(),
            };

            var actorURL = Helper.Decode(sceneID[0]);
            if (!actorURL.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                actorURL = Helper.GetSearchBaseURL(siteNum) + actorURL;
            }

            Logger.Info($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Loading Actor: {actorURL}");

            var actorData = await HTML.ElementFromURL(actorURL, cancellationToken).ConfigureAwait(false);

            result.Item.ExternalId = actorURL;
            if (Plugin.Instance.Configuration.EnableDebugging)
            {
                Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): externalID: {result.Item.ExternalId}");
            }

            string name = actorData.SelectSingleText("//h1").Replace(" Bio", string.Empty, StringComparison.OrdinalIgnoreCase),
                aliases = actorData.SelectSingleText("//p[contains(., 'Aliases')]/following-sibling::div/p");

            result.Item.OriginalTitle = name + ", " + aliases;
            Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Actor Name: {result.Item.OriginalTitle}");

            string overview;
            string ethenicity = actorData.SelectSingleText("//span[@data-test='link_span_ethnicity']/text()") ?? "Unknown";
            string braSize = actorData.SelectSingleText("//span[@data-test='link_span_bra']/text()") ?? "Unknown";
            string boobType = actorData.SelectSingleText("//span[@data-test='link_span_boobs']/text()") ?? "Unknown";
            string waist = actorData.SelectSingleText("//span[@data-test='link_span_waist']/text()") ?? "Unknown";
            string hip = actorData.SelectSingleText("//span[@data-test='link_span_hip']/text()") ?? "Unknown";
            string height = actorData.SelectSingleText("//span[@data-test='link_span_height']/text()") ?? "Unknown";
            string weight = actorData.SelectSingleText("//span[@data-test='link_span_weight']/text()") ?? "Unknown";
            string hairColor = actorData.SelectSingleText("//span[@data-test='link_span_hair_color']/text()") ?? "Unknown";
            string eyeColor = actorData.SelectSingleText("//span[@data-test='link_span_eye_color']/text()") ?? "Unknown";
            string piercingLocations = actorData.SelectSingleText("//span[@data-test='link_span_piercingLocations']/text()") ?? "None";
            string tattooLocations = actorData.SelectSingleText("//span[@data-test='link_span_tattooLocations']/text()") ?? "None";

            overview = $"Ethenicity: {ethenicity} Hair Color: {hairColor} Eye Color: {eyeColor}\n";
            overview += $"Bra Size: {braSize} ({boobType}) - {height} / {weight} - {waist} / {hip}\n";
            overview += $"Piercing Locations: {piercingLocations} / Tattoos: {tattooLocations}\n";

            // result.Item.Overview = "\u200B";
            result.Item.Overview = overview;

            if (Plugin.Instance.Configuration.EnableDebugging)
            {
                Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Overview: {result.Item.Overview}");
            }

            /* var actorDate = actorData.SelectSingleText("//div[p[contains(., 'Personal Information')]]//span[contains(., 'Born On')]")
                .Replace("Born On", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Trim(); */
            var actorDate = actorData.SelectSingleText("//span[contains(@data-test, 'link_span_dateOfBirth')]").Trim();
            if (DateTime.TryParseExact(actorDate, "MMMM d, yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var sceneDateObj))
            {
                result.Item.PremiereDate = sceneDateObj;
                if (Plugin.Instance.Configuration.EnableDebugging)
                {
                    Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Birth Date added {sceneDateObj.ToString()}");
                }
            }

            var bornPlaceList = new List<string>();

            // var bornPlaceNode = actorData.SelectNodesSafe("//div[p[contains(., 'Personal Information')]]//a[@data-test='link-country']/..//span[text()]");
            var bornPlaceNode = actorData.SelectNodesSafe("//span[@data-test='link_span_placeOfBirth']/..//span[text()]");
            foreach (var bornPlace in bornPlaceNode)
            {
                var location = bornPlace.InnerText.Trim();

                if (!string.IsNullOrEmpty(location))
                {
                    bornPlaceList.Add(location);
                    if (Plugin.Instance.Configuration.EnableDebugging)
                    {
                        Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Place of Birth added {location}");
                    }
                }
            }

            result.Item.ProductionLocations = new string[] { string.Join(", ", bornPlaceList) };

            Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): **** Leaving - Updated Actor: {result.Item.OriginalTitle}");

            return result;
        }

        public async Task<IEnumerable<RemoteImageInfo>> GetImages(int[] siteNum, string[] sceneID, BaseItem item, CancellationToken cancellationToken)
        {
            Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): **** Starting");

            var result = new List<RemoteImageInfo>();

            if (sceneID == null)
            {
                Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): ***** Leaving early empty search sceneID");
                return result;
            }

            var actorURL = Helper.Decode(sceneID[0]);
            if (!actorURL.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                actorURL = Helper.GetSearchBaseURL(siteNum) + actorURL;
            }

            var actorData = await HTML.ElementFromURL(actorURL, cancellationToken).ConfigureAwait(false);

            var img = actorData.SelectSingleText("//div[contains(@class, 'image-container')]//a/img/@src");
            if (!string.IsNullOrEmpty(img))
            {
                result.Add(new RemoteImageInfo
                {
                    Type = ImageType.Primary,
                    Url = img,
                });

                if (Plugin.Instance.Configuration.EnableDebugging)
                {
                    Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): Processing image");
                }
            }

            Logger.Debug($"{this.GetType().Name}-{IProviderBase.GetCurrentMethod()}(): **** Leaving - Found {result.Count} images");

            return result;
        }
    }
}
