#if __EMBY__
using System.ComponentModel;
using Emby.Web.GenericEdit;
using MediaBrowser.Model.Attributes;
#endif

namespace Jellyfin.Plugin.MetaTube.Configuration;

// Switches that the metatube-plus backend reads from the same MetaTube.json.
// The plugin settings page is the only place to change them; the operator console
// mirrors the values read-only. Field names match the backend JSON keys.
public partial class PluginConfiguration
{
#if __EMBY__
    [DisplayName("隐藏作品简介")]
    [Description("开启后不写入、也不翻译作品简介（标题、流派、演员不受影响）。建议把 Translation mode 设为 Title。")]
#endif
    public bool DisableSummary { get; set; } = false;

#if __EMBY__
    [DisplayName("启用演员兜底 (JavDB)")]
    [Description("主源没有演员时向 JavDB 确认一次；查不到的结果记住 12 小时。")]
#endif
    public bool EnableActorFallback { get; set; } = true;

#if __EMBY__
    [DisplayName("FANZA 冷却间隔")]
    [Description("两次请求之间的最小间隔，例如 3s；填 0 表示不限制。")]
#endif
    public string FanzaRateLimit { get; set; } = "0";

#if __EMBY__
    [DisplayName("JavDB 冷却间隔")]
    [Description("两次请求之间的最小间隔，例如 5s；填 0 表示不限制。")]
#endif
    public string JavdbRateLimit { get; set; } = "5s";
}
