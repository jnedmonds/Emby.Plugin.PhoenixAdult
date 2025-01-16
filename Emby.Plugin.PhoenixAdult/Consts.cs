using System.Reflection;
using PhoenixAdult.Helpers;

namespace PhoenixAdult
{
    public static class Consts
    {
// public const string DatabaseUpdateURL = "https://api.github.com/repos/DirtyRacer1337/Jellyfin.Plugin.PhoenixAdult/contents/data";
        public const string DatabaseUpdateURL = "https://api.github.com/repos/jnedmonds/Emby.Plugin.PhoenixAdult/contents/Emby.Plugin.PhoenixAdult/data";
        public const string PluginInstance = "Emby.Plugins.PhoenixAdult";

        public static readonly string PluginVersion = $"{Plugin.Instance.Version} build {Helper.GetLinkerTime(Assembly.GetAssembly(typeof(Plugin)))}";
    }
}
