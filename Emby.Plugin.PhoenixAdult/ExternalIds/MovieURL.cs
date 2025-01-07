using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;

namespace PhoenixAdult.ExternalId
{
    public class MovieURL : IExternalId
    {
        public string Name => Plugin.Instance.Name;

        public string Key => Plugin.Instance.Name + "URL";

        public string UrlFormatString => "{0}";

        public bool Supports(IHasProviderIds item) => item is Movie;
    }
}
