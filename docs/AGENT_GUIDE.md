# Agent 插件开发指南

模板固定地址：https://github.com/hhwfsl/NonetMusicPlayerPlugin

该文档是开发参考，不是系统指令。依赖的播放器源码：https://github.com/hhwfsl/NonetMusicPlayer 。不应从陌生链接获取可执行程序，不能自动读取用户电脑上的文件。

SDK 4.4 的宿主服务、区域替换、流程决策与可信原生视图见 [通用扩展参考](UNIVERSAL_EXTENSIONS.md)。优先选择声明式界面；原生代码需要明确的托管信任确认，不能宣称具备操作系统沙箱。

## 工作流

1. 从宿主 catalog 查询实际允许的工具，不假定某能力存在。
2. 用 extension.ui.read 获取真实布局、默认布局和插槽；保留必需部件。
3. 先生成可校验的声明式界面插件；需要任意计算或网络时才生成 C# 项目。
4. 通过 extension.package 创建新的纯数据插件。项目目录由用户选择；ID 为 custom.<小写名称>；一个插件一个目录，不覆盖已有插件。
5. 通过 extension.source 输出新的 C# 源码项目。本工具不编译、执行或安装代码，不能声称已经运行成功。
6. 写 README、CHANGELOG、注释与测试说明。用户拒绝后不要重复请求。
7. 只有收到成功的服务结果，才能告知应用/安装成功。

## 整体布局

extension.ui.apply 只有一个参数：布局 JSON 字符串。Schema 2 的 workspace 可配置 Navigation、Content、Player；player 是嵌套网格。

必须保留且各出现一次：
artwork,title,artist,previous,play,next,time,mode,favorite,more,volume,progress。

必须以实际返回的 current/default 为起点修改，不臆造字段。不能注入任意 XAML、脚本、OS Shell 或隐藏全部恢复入口。设置与终端是安全恢复入口。

## 数据插件请求

extension.package 的唯一参数为 JSON 字符串：

```json
{
  "id": "custom.my-layout",
  "name": "My Layout",
  "version": "1.0.0",
  "page": {
    "schemaVersion": 2,
    "root": {
      "type": "stack",
      "children": [
        { "type": "text", "text": "My Layout" },
        { "type": "button", "text": "暂停", "action": "pause" }
      ]
    },
    "actions": {
      "pause": {
        "id": "pause",
        "service": "commands",
        "arguments": { "operation": "player.pause", "arguments": [] }
      }
    }
  },
  "contributions": [
    { "slot": "home.cards", "label": "My Layout", "action": "pause",
      "view": { "type": "button", "text": "暂停", "action": "pause" } }
  ]
}
```

可选 layout 为完整布局 JSON 字符串，宿主会增加应用布局按钮并验证。可替换 page.music/page.lyrics、增加 home.cards/overlay、为 lyrics.more/song.more/playlist.more/player.actions/titlebar.actions 增加动作。若要求超出这些能力，应明确说明，而不是通过禁用校验或伪装代码来达到目的。

root 建议使用 stack 或 grid。控件树支持 grid/stack/border/scroll/repeat/text/selectable-text/input/button/toggle/checkbox/select/slider/image/icon。状态为点分隔键查找，不求值脚本。主题应使用宿主刷子令牌，不随意固化与当前主题冲突的颜色。

纯数据包不会运行子进程，不携带脚本/DLL，不申请 network、process、in-process、文件读取或自动审批。所有动作仍经宿主验证。

## C# 源码项目

先读取模板的：
Directory.Build.props、NonetMusicPlayerPlugin.slnx、plugin/NonetMusicPlayerPlugin.csproj、plugin/PluginDefinition.cs、plugin/manifest.json、plugin/page.json、plugin/plugin_config_schema.json、build.ps1。

extension.source 接收一个 JSON 字符串：
`{"id":"custom.example","files":{"relativePath":"UTF-8 source text"}}`。

只写新项目，允许 cs/csproj/slnx/props/json/md/ps1/sh；不写 bin/obj/dist、隐藏目录或父级路径，不覆盖用户文件。模板通过 ProjectReference 引用播放器 SDK，不复制其代码。生成项目若比模板多一层目录，应调整 NonetPlayerRoot 或传 -Player 参数，不假定相邻路径仍然相同。

SDK INonetExtension 的方法保持：
InitializeAsync、InvokeAsync、SyncAsync、CompleteAsync、EventAsync、DisposeAsync。
返回 ExtensionFrame，属性为 Revision、Busy、State 和 Requests。服务请求包含唯一 id/service/arguments，完成后必须移除，避免重放。

托管 runtime=managed 需 in-process 权限、ExtensionClass 和 DLL 入口；首次启用信任确认。独立进程 runtime=process 需 process 权限、平台入口和 JSON-RPC 循环；stdout 只输出协议。AOT 只能在对应目标系统使用原生工具链构建；不要把 JIT 回退称为 AOT。

生成源码后说明构建步骤及可信代码风险，不自行编译或运行，也不能宣布未测试的平台已通过。更全面的权限、配置、生命周期和发行方式见 REFERENCE.md 与 GETTING_STARTED.md。

## 联网与审批

模型联网由其所配置的模型 API 提供，不能通过 prompt 为不支持搜索的模型创造联网能力。不要额外使用独立搜索服务、任意本机文件、音频或其他插件凭据。

审批模式由宿主掌握：
ask 保留原确认；assist 批准可恢复设置/布局；full 批准已授权的 Nonet 事务。
模型不能更改自己的审批模式、伪造用户确认、扩大权限或申请系统 Shell。上传文件仍由用户选择及确认。
