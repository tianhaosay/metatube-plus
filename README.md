# MetaTube 插件（metatube-plus 定制版）

> 本仓库是 [jellyfin-plugin-metatube](https://github.com/metatube-community/jellyfin-plugin-metatube) 的定制分支（基线 `f7c1f33`），
> 配合后端 [metatube-plus](https://github.com/tianhaosay/metatube-sdk-go) 使用。
>
> A customised build of the MetaTube plugin for Emby/Jellyfin, made to pair with the
> [metatube-plus](https://github.com/tianhaosay/metatube-sdk-go) backend.

## 配套关系

| 部件 | 仓库 | 负责什么 |
|---|---|---|
| 插件（本仓库） | `tianhaosay/metatube-plus` | 取数通道、标题/简介翻译、设置页 |
| 后端 | [`tianhaosay/metatube-sdk-go`](https://github.com/tianhaosay/metatube-sdk-go) | 演员/流派中文化、简介处理、库体检、批量修复、控制台 `/ui` |

后端 + 本定制插件是**推荐组合**：后端要读的 4 个运行开关直接出现在插件设置页里，改完约 15 秒生效，不用碰配置文件。
只装插件、装上游原版后端也能跑，但这 4 个开关得手工写进 `MetaTube.json`，且拿不到控制台、体检和批量修复。

## 本定制版改了什么

**设置页多出 4 项**（仅 Emby 构建显示，值写进同一份 `MetaTube.json`，由后端读取执行）：

| 设置项 | 字段 | 默认 | 作用 |
|---|---|---|---|
| 隐藏作品简介 | `DisableSummary` | 关 | 后端清空简介字段 |
| 启用演员兜底 (JavDB) | `EnableActorFallback` | 开 | 主源没演员时向 JavDB 查一次，查不到记 12 小时 |
| FANZA 冷却间隔 | `FanzaRateLimit` | `0` | FANZA 两次请求的最小间隔 |
| JavDB 冷却间隔 | `JavdbRateLimit` | `10s` | JavDB 两次请求的最小间隔 |

**两个默认值调整**（只影响新装或重置配置时的初始值）：

- `TranslationMode`：`Disabled` → `Both`（后端不再翻标题/简介，插件必须自己翻）
- `TranslationEngine`：`Baidu` → `GoogleFree`（免密钥即可用）

除此之外没有改动抓取、解析、命名、图片、数据库等任何逻辑。

## 安装（Emby）

1. 从 [Releases](https://github.com/tianhaosay/metatube-plus/releases) 下载 `MetaTube.zip` 或 `MetaTube.dll`。
2. 放到 Emby 的插件目录：`<emby>/config/plugins/MetaTube.dll`。
3. 重启 Emby。
4. 打开 插件 → MetaTube：
   - `Server` 填后端地址，例如 `http://<host>:8080`
   - `Token` 后端未启用鉴权时留空
   - `TranslationMode` 必须非 `Disabled`（推荐 `Title` 或 `Both`）

## 构建

```bash
# Emby 4.x
dotnet build Jellyfin.Plugin.MetaTube/Jellyfin.Plugin.MetaTube.csproj -c Release.Emby

# Jellyfin 10.11.x
dotnet build Jellyfin.Plugin.MetaTube/Jellyfin.Plugin.MetaTube.csproj -c Release
```

产物在 `Jellyfin.Plugin.MetaTube/bin/Release.Emby/net8.0/`。

## 与上游同步

```bash
git fetch upstream
git merge upstream/main
```

定制部分集中在 `Jellyfin.Plugin.MetaTube/Configuration/PluginConfiguration.Plus.cs`，上游改
`PluginConfiguration.cs` 时冲突面很小；合并后记得重新编译并把 DLL 换到 Emby 再重启。

## 上游文档

安装、命名规则、数据源、自动翻译等通用说明仍以上游为准：

- [插件安装](https://metatube-community.github.io/wiki/plugin-installation/)
- [文件命名规则](https://metatube-community.github.io/wiki/naming-rules/)
- [自动翻译](https://metatube-community.github.io/wiki/auto-translation/)
- [数据源](https://metatube-community.github.io/wiki/metadata-providers/)
- [上游 Wiki](https://metatube-community.github.io/wiki/)

## 许可

沿用上游 [MIT](LICENSE) 许可。
