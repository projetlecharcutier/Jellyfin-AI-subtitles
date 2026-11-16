using System;
using System.Collections.Generic;
using System.Globalization;
using Jellyfin.Plugin.AISubtitles.Configuration;
using Jellyfin.Plugin.AISubtitles.Services;
using Jellyfin.Plugin.AISubtitles.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AISubtitles;

/// <summary>
/// The main plugin.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
    /// <param name="xmlSerializer">Instance of the <see cref="IXmlSerializer"/> interface.</param>
    /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
    /// <param name="taskManager">Instance of the <see cref="ITaskManager"/> interface.</param>
    /// <param name="mediaEncoder">Instance of the <see cref="IMediaEncoder"/> interface.</param>
    /// <param name="directoryService">Instance of the <see cref="IDirectoryService"/> interface.</param>
    /// <param name="loggerFactory">Instance of the <see cref="ILoggerFactory"/> interface.</param>
    public Plugin(
        IApplicationPaths applicationPaths,
        IXmlSerializer xmlSerializer,
        ILibraryManager libraryManager,
        ITaskManager taskManager,
        IMediaEncoder mediaEncoder,
        IDirectoryService directoryService,
        ILoggerFactory loggerFactory)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
        Mistral = new MistralClient(loggerFactory.CreateLogger<MistralClient>());
        Engine = new SubtitleProcessingService(
            this,
            libraryManager,
            mediaEncoder,
            directoryService,
            Mistral,
            loggerFactory.CreateLogger<SubtitleProcessingService>());
        taskManager.AddTasks(new IScheduledTask[] { new AiSubtitleScanTask(this, loggerFactory.CreateLogger<AiSubtitleScanTask>()) });
    }

    /// <inheritdoc />
    public override string Name => "AI Subtitles";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("00dd78e0-78a0-4522-a95d-d6ba9bc24c7d");

    /// <summary>
    /// Gets the current plugin instance.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <summary>
    /// Gets the subtitle processing engine.
    /// </summary>
    internal SubtitleProcessingService Engine { get; }

    /// <summary>
    /// Gets the Mistral API client.
    /// </summary>
    internal MistralClient Mistral { get; }

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return
        [
            new PluginPageInfo
            {
                Name = Name,
                DisplayName = "AI Subtitles",
                EmbeddedResourcePath = string.Format(CultureInfo.InvariantCulture, "{0}.Configuration.configPage.html", GetType().Namespace),
                EnableInMainMenu = true
            },
            new PluginPageInfo
            {
                Name = "AISubtitlesHistory",
                DisplayName = "AI Subtitles - Historique",
                EmbeddedResourcePath = string.Format(CultureInfo.InvariantCulture, "{0}.Configuration.historyPage.html", GetType().Namespace),
                EnableInMainMenu = true
            }
        ];
    }
}
