using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Drawing;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using PhoenixAdult.Configuration;

[assembly: CLSCompliant(false)]

namespace PhoenixAdult
{
    public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages, IHasThumbImage
    {
        public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer, IHttpClient http, ILogManager logger)
            : base(applicationPaths, xmlSerializer)
        {
            Instance = this;
            Http = http;

            if (logger != null)
            {
                Log = logger.GetLogger(this.Name);
            }
        }

        public static IHttpClient Http { get; set; }

        public static ILogger Log { get; set; }

        public static Plugin Instance { get; private set; }

        public override string Name => "PhoenixAdult";

        public override Guid Id => Guid.Parse("91fda366-558b-4a18-aab8-2dc8627d2efc");

        ImageFormat IHasThumbImage.ThumbImageFormat => ImageFormat.Png;

        public Stream GetThumbImage()
        {
            var type = this.GetType();
            return type.Assembly.GetManifestResourceStream(type.Namespace + ".plugin.png");
        }

        public IEnumerable<PluginPageInfo> GetPages() => new[]
        {
            new PluginPageInfo
            {
                Name = "phoenixadult",
                EmbeddedResourcePath = this.GetType().Namespace + ".Configuration.configPage.html",
                DisplayName = "Phoenix Adult",
            },
            new PluginPageInfo
            {
                Name = "phoenixadultjs",
                EmbeddedResourcePath = this.GetType().Namespace + ".Configuration.configPage.js",
            },
        };
    }
}
