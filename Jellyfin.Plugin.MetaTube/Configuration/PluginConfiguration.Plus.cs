#if __EMBY__
using System.ComponentModel;
using Emby.Web.GenericEdit;
using MediaBrowser.Model.Attributes;
#endif

namespace Jellyfin.Plugin.MetaTube.Configuration;

// Switches used by the plugin itself or mirrored by the metatube-plus backend.
// Field names match the backend's MetaTube.json keys.
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
    [Description("两次请求之间的最小间隔，例如 10s；填 0 表示不限制。")]
#endif
    public string JavdbRateLimit { get; set; } = "10s";

#if __EMBY__
    [DisplayName("自动添加无码破解标签")]
    [Description("Organize Metadata 每天运行时，给文件名带 -LTX / -U / -UC 的影片加「无码破解」类型；文件改名后自动摘除。不影响中文字幕角标。")]
#endif
    public bool EnableUncensoredBadge { get; set; } = true;
}
