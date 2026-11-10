using System.Text.RegularExpressions;
using Jellyfin.Plugin.MetaTube.Extensions;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Tasks;
#if __EMBY__
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Model.Logging;

#else
using MediaBrowser.Controller.Sorting;
using Microsoft.Extensions.Logging;
using Jellyfin.Data.Enums;
#endif

namespace Jellyfin.Plugin.MetaTube.ScheduledTasks;

public class OrganizeMetadataTask : IScheduledTask
{
    private readonly ILibraryManager _libraryManager;
    private readonly ILogger _logger;

#if __EMBY__
    public OrganizeMetadataTask(ILogManager logManager, ILibraryManager libraryManager)
    {
        _logger = logManager.CreateLogger<OrganizeMetadataTask>();
        _libraryManager = libraryManager;
    }
#else
    public OrganizeMetadataTask(ILogger<OrganizeMetadataTask> logger, ILibraryManager libraryManager)
    {
        _logger = logger;
        _libraryManager = libraryManager;
    }
#endif

    public string Key => $"{Plugin.ProviderName}OrganizeMetadata";

    public string Name => "Organize Metadata";

    public string Description => $"Organizes video metadata provided by {Plugin.ProviderName} in library.";

    public string Category => Plugin.ProviderName;

    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        yield return new TaskTriggerInfo
        {
#if __EMBY__
            Type = TaskTriggerInfo.TriggerDaily,
#else
            Type = TaskTriggerInfoType.DailyTrigger,
#endif
            TimeOfDayTicks = TimeSpan.FromHours(3).Ticks
        };
    }

#if __EMBY__
    public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
#else
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
#endif
    {
        await Task.Yield();

        progress?.Report(0);

        var items = _libraryManager.GetItemList(new InternalItemsQuery
        {
            MediaTypes = new[] { MediaType.Video },
#if __EMBY__
            HasAnyProviderId = new[] { Plugin.ProviderId },
            IncludeItemTypes = new[] { nameof(Movie) },
#else
            HasAnyProviderId = new Dictionary<string, string> { { Plugin.ProviderId, string.Empty } },
            IncludeItemTypes = new[] { BaseItemKind.Movie }
#endif
        }).ToList();

        foreach (var (idx, item) in items.WithIndex())
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report((double)idx / items.Count * 100);

            var genres = item.Genres?.ToList() ?? new List<string>();
            var originalGenres = item.Genres?.ToList() ?? new List<string>();

            // Subtitle state is derived from the file name and sidecar subtitle
            // files on every run. The genre and the badge both follow it, so a
            // deleted sidecar removes both.
            var hasSubtitle = HasEmbeddedChineseSubtitle(item.FileNameWithoutExtension) ||
                              HasExternalChineseSubtitle(item.Path);

            if (hasSubtitle && !genres.Contains(ChineseSubtitle))
                genres.Add(ChineseSubtitle);
            else if (!hasSubtitle && genres.Contains(ChineseSubtitle))
                genres.RemoveAll(s => s.Equals(ChineseSubtitle));

            // Keep the uncensored genre in sync with the file name. Only the genre
            // changes; subtitle badges are owned by the logic below.
            if (Plugin.Instance.Configuration.EnableUncensoredBadge)
            {
                try
                {
                    switch (IsUncensoredRelease(item.Path ?? string.Empty))
                    {
                        case true when !genres.Contains(Uncensored):
                            genres.Add(Uncensored);
                            break;
                        case false when genres.Contains(Uncensored):
                            genres.RemoveAll(s => s.Equals(Uncensored));
                            break;
                    }
                }
                catch (Exception e)
                {
                    _logger.Error("Update Uncensored for video {0}: {1}", item.Name, e.Message);
                }
            }

            // Remove duplicates.
            var orderedGenres =
                (Plugin.Instance.Configuration.EnableGenreSubstitution
                    ? Plugin.Instance.Configuration.GetGenreSubstitutionTable().Substitute(genres)
                    : genres).Distinct().OrderByString(genre => genre).ToList();

            var genresChanged = !originalGenres.SequenceEqual(orderedGenres, StringComparer.OrdinalIgnoreCase);

            // Reconcile the subtitle badge on every run, not only when the genre
            // flips. The badge is drawn by the backend image endpoint from the
            // MetaTube id; items without one keep their existing artwork.
            var imageChanged = false;
            if (Plugin.Instance.Configuration.EnableBadges)
            {
                try
                {
                    imageChanged = SyncPrimaryImage(item, hasSubtitle);
                }
                catch (Exception e)
                {
                    _logger.Error("Update badge for video {0}: {1}", item.Name, e.Message);
                }
            }

            // Persist when either the genres or the primary image changed.
            if (genresChanged)
                item.Genres = orderedGenres.ToArray();

            if (!genresChanged && !imageChanged)
                continue;

            _logger.Info("Organize metadata for video: {0}", item.Name);

#if __EMBY__
            _libraryManager.UpdateItem(item, item, ItemUpdateType.MetadataEdit, null);
#else
            await _libraryManager
                .UpdateItemAsync(item, item, ItemUpdateType.MetadataEdit, cancellationToken)
                .ConfigureAwait(false);
#endif
        }

        progress?.Report(100);
    }

    #region Helper

    private const string ChineseSubtitle = "中文字幕";

    private const string Uncensored = "无码破解";

    private static readonly Regex UncensoredSuffixRe = new(
        @"(?i)-(ltx|uc|u)([-_.]|$)", RegexOptions.Compiled);

    // Ensure the primary image matches the subtitle state. Returns true when the
    // stored image was replaced. The backend image endpoint draws the badge on
    // top of the provider's primary image; a missing MetaTube id leaves the
    // current artwork alone.
    private static bool SyncPrimaryImage(BaseItem item, bool hasSubtitle)
    {
        var pid = item.GetPid(Plugin.ProviderId);
        if (string.IsNullOrWhiteSpace(pid.Id) || string.IsNullOrWhiteSpace(pid.Provider))
            return false;

        var badge = hasSubtitle ? Plugin.Instance.Configuration.BadgeUrl : string.Empty;
        var desired = ApiClient.GetPrimaryImageApiUrl(pid.Provider, pid.Id, pid.Position ?? -1, badge);
        var current = CurrentPrimaryImagePath(item);

        if (string.Equals(current, desired, StringComparison.OrdinalIgnoreCase))
            return false;

        item.SetImage(new ItemImageInfo { Path = desired, Type = ImageType.Primary }, 0);
        return true;
    }

    // Read the current primary image path so an already-correct URL is not
    // rewritten on every run. Older Emby builds may not expose GetImageInfo;
    // in that case an empty path simply triggers a once-off rewrite.
    private static string CurrentPrimaryImagePath(BaseItem item)
    {
        try
        {
            var info = item.GetImageInfo(ImageType.Primary, 0);
            return info == null ? string.Empty : (info.Path ?? string.Empty);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static bool HasTag(string filename, string tag)
    {
        var r = new Regex(@"[-_\s]", RegexOptions.Compiled);
        return r.Split(filename).Contains(tag, StringComparer.OrdinalIgnoreCase);
    }

    private static bool HasTag(string filename, params string[] tags)
    {
        return tags.Any(tag => HasTag(filename, tag));
    }

    private static bool HasEmbeddedChineseSubtitle(string filename)
    {
        if (string.IsNullOrWhiteSpace(filename))
            return false;

        return filename.Contains(ChineseSubtitle) || HasTag(filename, "C", "UC", "ch");
    }

    private static bool HasExternalChineseSubtitle(string path)
    {
        return HasExternalChineseSubtitle(Path.GetFileNameWithoutExtension(path),
            Directory.GetParent(path)?.GetFiles().Select(info => info.Name));
    }

    private static bool HasExternalChineseSubtitle(string basename, IEnumerable<string> files)
    {
        if (files == null)
            return false;
        var r = new Regex(@"\.(ch[ist]|zho?(-(cn|hk|sg|tw))?)\.(ass|srt|ssa|smi|sub|idx|psb|vtt)$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
        return files.Any(name => r.IsMatch(name) &&
                                 r.Replace(name, string.Empty)
                                     .Equals(basename, StringComparison.OrdinalIgnoreCase));
    }

    // Mirror of metatube-plus's uncensored-badge rule: the suffix must be
    // followed by a separator or the end of the file name. RE2 has no
    // lookahead, so the separator is captured by the expression.
    private static bool IsUncensoredRelease(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        return !string.IsNullOrWhiteSpace(name) && UncensoredSuffixRe.IsMatch(name);
    }

    #endregion
}
