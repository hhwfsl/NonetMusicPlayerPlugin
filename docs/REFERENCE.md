# 插件接口与能力参考

SDK 4.16 的选择器方向及提示控制见 [PLUGIN_DEVELOPMENT](PLUGIN_DEVELOPMENT.md)，近期统计与查询结果见 [UNIVERSAL_EXTENSIONS](UNIVERSAL_EXTENSIONS.md)，图表和卡片外观见 [STYLE_EXTENSIONS](STYLE_EXTENSIONS.md)。基础示例只声明实际使用的能力；额外功能须按宿主提供的能力列表协商。

完整界面方案见 [INTERFACE_PRESETS](INTERFACE_PRESETS.md)，使用 SDK 4.15 的可选 `ui.composition.v1` 与既有 styles 服务。布局文件只描述表现，不能写入设置、运行脚本或新增业务功能。

类型级样式、动画、资源与可撤销叠加使用 SDK 4.14 的可选 `ui.styles.v1`，参见 [风格扩展协议](STYLE_EXTENSIONS.md)。默认 Hello World 不要求此能力；清单的 `autoStart` 与 `nativePage` 也是可选字段。

SDK 4.4 的宿主服务目录、原生视图、流程钩子与 PCM 接口详见 [通用扩展参考](UNIVERSAL_EXTENSIONS.md)。可选能力按实际需要声明，不能按客户端版本推断能力。

## 1. 清单与兼容性

`manifest.json` 位于包根目录。必填项包括稳定 `id`、用户可见名称、数字版本、`type`、`contractVersion`、运行形态、入口和页面。新插件填写作者与 GitHub 仓库信息。

- `id`：小写字母开头，后续为小写字母、数字、点或连字符；一个 ID 对应一个插件及其持久数据。
- `version`：如 `1.0.0`。同版本不重复覆盖，旧版本不得覆盖新版本。
- `repositoryOwner` / `repositoryName`：用于本地导入后的更新检查，不由文件名推断。
- `platform`：可选 RID，如 `win-x64`。代码包按平台分别发布；纯数据包可不指定。
- `requiredCapabilities`：只填插件必须依赖的能力。宿主在安装前检查，握手时再次协商。
- `navigationLabel` / `navigationIcon`：侧边栏名称和图标。
- `permissions`：声明申请的服务权限，不表示操作系统隔离。
- `events` / `providedServices` / `contributions`：事件订阅、插件间服务、UI 插槽。

SDK 的程序集版本固定为 `4.0.0.0`，功能版本可递增。Contract 2 的 `INonetExtension` 不新增必需方法；额外字段、服务和能力是可选的。旧 Contract 1 的音源、歌词、主题与 UI 插件继续由兼容适配层处理。

## 2. 选择运行形态

| 形态 | 清单配置 | 适用情况 | 限制 |
| --- | --- | --- | --- |
| 托管 | `runtime: managed`、`in-process`、`extensionClass`、DLL 入口 | 共享 .NET 运行时、轻量业务 | 与主进程共享故障和内存，首次启用须确认信任 |
| 独立进程 | `runtime: process`、`process`、可执行入口 | 网络、长任务、可使用 Native AOT | 有进程开销，仍非 OS 沙箱 |
| 声明式 | `runtime: declarative`、无入口 | 布局、页面、菜单、简单宿主动作 | 不能附带程序、脚本或任意代码；需 `runtime.declarative.v1` |

Native AOT 在构建时生成本机代码，适合无反射依赖的独立进程。不同操作系统应在对应系统构建与测试；不能把自包含 JIT 包标记为 AOT。托管 DLL 不因主程序的 AOT 设置自动变为可动态加载的 AOT 模块。

## 3. 生命周期

`INonetExtension` 六个入口：

1. `InitializeAsync`：读取初始化配置、存储目录与宿主能力；创建状态。
2. `InvokeAsync`：处理用户动作或服务调用，不阻塞 UI。
3. `SyncAsync`：返回当前状态；长任务设 `Busy=true`，宿主约每 200 ms 同步。
4. `CompleteAsync`：按请求 ID 接收宿主服务结果，移除已完成请求。
5. `EventAsync`：处理清单订阅的事件及生命周期通知。
6. `DisposeAsync`：取消任务、释放网络/计时器/文件资源；实现必须允许重复或提前清理。

独立进程用 UTF-8 单行 JSON-RPC 2.0 传输，stdout 仅输出协议，日志写 stderr。方法为 `initialize`、`extension.invoke`、`extension.sync`、`extension.event`、`extension.complete`。初始化请求的 `contractVersion` 为字符串 `"2"`，配置、调用和事件参数按协议以 JSON 字符串传递；返回值为 JSON 对象。使用 SDK 的源生成上下文序列化，不依赖反射。

停用、卸载、关闭分别对应 `lifecycle.disable`、`lifecycle.uninstall`、`lifecycle.shutdown`。宿主会先停止旧实例再更新文件；清理有时间限制，插件不得依赖无限等待的析构行为。需要这些事件时在清单声明相应生命周期方法。

## 4. 页面与控件

页面格式：

```json
{
  "schemaVersion": 2,
  "root": {
    "type": "stack",
    "children": [
      { "type": "text", "bind": "greeting", "fontSize": 20 },
      { "type": "button", "text": "问候", "action": "greet" }
    ]
  }
}
```

容器：`grid`、`stack`、`border`、`scroll`、`repeat`。内容：`text`、`selectable-text`、`image`、`icon`。输入：`input`、`button`、`toggle`、`checkbox`、`select`、`slider`。

常用属性：

- `bind` 从 Frame.State 读取点分隔路径；重复项用 `item.title`。
- `input` 是回传的字段名，`action` 是后端动作名；只做键查询，不求值表达式。
- `parameter` 可回传固定字符串或 `item.id`。
- `visibleIf` / `selectedIf` 控制显示和选中。
- `rows` / `columns` 支持固定值、`Auto` 和星号；`columnsBind` 用状态调整列宽。
- `row` / `column` 指定网格位置；`padding` / `margin` 是厚度字符串。
- `enterAction` 用于回车执行；多行输入的 Shift+Enter 保留换行。
- `contextActions` 是原生菜单按钮集合，`openMenuOnClick` 允许更多按钮打开菜单。
- `variant` 可用 `ghost`、`pill`、`row`；`tooltip` 可提供完整名称。
- `scrollToEnd` 跟随底部，用户上翻时不强制归底。
- `ownsHeader` 让插件自行绘制页面标题区域，不替换应用窗口标题栏。

控件树最多 512 节点、24 层；状态最多 2 MB；请求最多 16 个。请使用分页或有界数据，避免返回全部历史。长任务的状态修订号仅在内容变化时递增；不要持久化每个流式片段。

## 5. 配置表单

统一文件名 `plugin_config_schema.json`（也兼容无扩展名的 `plugin_config_schema`，不能同时存在两份）。顶层键就是配置字段 ID，每项定义：

- `type`、`description`、`default`、`hint`。
- 可选枚举、数值范围、长度限制。
- `ui:group` 分类，`ui:collapsed` 折叠高级组。
- 敏感字段标记为 `sensitive`，避免出现在索引、终端及日志中。
- Agent 模型枚举可使用 `ui:widget: agent-model` 及约定的 `agent.models` 能力。

示例：

```json
{
  "greeting": {
    "type": "string",
    "description": "问候内容",
    "default": "Hello, Nonet!",
    "max_length": 200
  }
}
```

用户不能增删 Schema 或改变控件类型。宿主保存后重新初始化插件，Contract 2 配置持久化到插件的独立配置存储；敏感字段不会写入普通安装索引。不要在自己日志中输出配置、API Key 或请求鉴权头。

## 6. 宿主服务与权限

Frame.Requests 包含 `id`、`service`、`arguments`。请求 ID 必须唯一；重复帧不会重复执行同一事务。结果由 `CompleteAsync` 返回，插件应检查 `success`，不得仅因发出了请求就宣称成功。

| 服务 | 用途 | 权限或条件 |
| --- | --- | --- |
| `catalog` | 当前允许的命令及参数 | 按权限过滤 |
| `commands` | 播放、搜索、歌单、设置等统一事务 | 逐操作检查，危险事务确认 |
| `lyrics` | 读取、关联、取消关联 | music-read / lyrics-write |
| `config` | 打开开发者定义的配置表单 | 用户动作 |
| `files.pick` | 用户选择并确认接收插件的文件快照 | user-files + 用户动作；不授予路径访问 |
| `dialogs.prompt` | 文本输入 | 用户动作 |
| `dialogs.confirm` / `dialogs.notify` | 确认或提示 | 确认须用户动作 |
| `plugin:<id>/<service>` | 调用另一已启用插件声明的服务 | plugin-services；阻止递归环路 |
| `ui` | 读取、验证、应用或恢复布局 | ui-extend + ui.workspace.v1 |
| `development` | 生成新的数据插件或源码项目 | ui-extend + plugins-control + plugin-development + development.v1 |

命令请求示例：

```csharp
_frame.Requests.Add(new ExtensionHostRequest(
    Guid.NewGuid().ToString("N"), "commands",
    new System.Text.Json.Nodes.JsonObject {
        ["operation"] = "player.pause",
        ["arguments"] = new System.Text.Json.Nodes.JsonArray()
    }));
_frame.Busy = true;
```

在 `CompleteAsync` 中移除请求，处理结果，再设 `Busy=false`。不要把用户字符串拼成 Shell；命令的权限、ID、参数数量和确认由宿主验证。

## 7. UI 插槽与整体布局

插槽包括 `lyrics.more`、`song.more`、`playlist.more`、`player.actions`、`titlebar.actions`、`home.cards`、`overlay`、`page.music`、`page.lyrics`、`page.songs`、`page.history`、`page.albums`、`page.artists`、`page.playlist`、`page.statistics`。菜单使用 Label/Action，页面插槽使用 View 控件树；需 ui-extend。打开插件页还需 navigation。

`ui` 服务参数 `operation=read` 返回当前/默认布局与插槽。应用时传 `operation=apply` 和 `layout`（JSON 字符串）；恢复用 `reset`。布局 Schema 2 可重排 Navigation、Content、Player，并使用嵌套网格设置播放器控件位置、大小、间距、文字大小。必要部件必须完整保留且各出现一次：

`artwork,title,artist,previous,play,next,time,mode,favorite,more,volume,progress`。

布局最多 100 KiB；播放器网格深度最多四层。验证失败不替换原布局，应用前保留可恢复备份。设置和终端仍可恢复默认布局。

这不是任意 XAML 注入、任意系统标题栏替换或 OS 级桌面定制。任意 C# 插件的操作系统能力来自其进程权限，而不是声明式 UI 服务。不要为了定制界面申请不必要的系统权限。

## 8. 自动审批

声明 `supportsApprovalModes: true` 并申请 `approvals.v1`，配置字段 `approvalMode` 使用 `ask`、`assist`、`full`。

- ask：保留宿主原本的确认。
- assist：自动批准可恢复的设置/布局，删除和插件启停仍确认。
- full：自动批准插件已获授权的宿主事务。

升级模式须由用户在宿主配置表单确认；模型参数、安装包或重新导入不能预授予。新增权限会撤销旧审批授权。审批不扩大服务权限，不免除文件选择授权，也不开放 Shell/任意文件读取。代码插件仍应可信。

## 9. 存储、调试与发行

生成项目的目录由用户选择并在设置中保存，不接受模型路径参数，不覆盖已有项目。

只在初始化获得的 StorageDirectory 中写自己的持久数据；它与安装包分离，同 ID 更新后保留。原始音频不是插件数据，修改标签需专门声明与用户授权。

执行模板测试，再在真实宿主导入包。检查停用/重新启用、更新同 ID、重启、卸载并保留文件、删除文件、不同窗口尺寸和不同主题。异常日志应包含操作和错误类别，不包含用户内容或凭据。

GitHub Release 每个平台一个 `.impp`；清单版本须与发行版本一致。宿主根据 ID 判断更新、按平台筛选资产。卸载时用户选择是否同时删除文件；删除不可撤销，保留时仅断开注册，不移动到 Retained。
