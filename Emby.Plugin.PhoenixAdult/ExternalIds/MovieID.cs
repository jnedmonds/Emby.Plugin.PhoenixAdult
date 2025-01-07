using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;

namespace PhoenixAdult.ExternalId
{
    public class MovieID : IExternalId
    {
        public string Name => Plugin.Instance.Name + " ID";

        public string Key => Plugin.Instance.Name;

        public string UrlFormatString => null;

        public bool Supports(IHasProviderIds item) => item is Movie;
    }
}
