using System.Text.Json;
using NonetMusicPlayer.Core.Plugins;

namespace NonetMusicPlayerPlugin;

/// <summary>开发期调用宿主 API 验证清单；声明式插件的运行入口仍由 manifest/page 指定。</summary>
public static class PluginDefinition
{
    public static PluginManifest Read(Stream source)
    {
        var manifest = JsonSerializer.Deserialize<PluginManifest>(source, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("Empty plugin manifest.");
        manifest.Validate(); PluginRepository.Validate(manifest.RepositoryOwner, manifest.RepositoryName); return manifest;
    }
}


/// <summary>通用 Hello World 后端；页面内容/动作由 JSON 定义，不依赖具体宿主控件。</summary>
public sealed class HelloWorld : INonetExtension, INonetRuntimeAware
{
    private PluginRuntimeContext? _runtime;
    public void AttachRuntime(PluginRuntimeContext context) => _runtime = context;
    private readonly ExtensionFrame _frame = new() { State = new() { ["count"] = 0, ["greeting"] = "Hello, Nonet!" } };
    public ValueTask<ExtensionFrame> InitializeAsync(ExtensionInitialization initialization, CancellationToken cancellationToken)
    {
        _runtime?.Log("Hello World initialized.");
        var config = System.Text.Json.Nodes.JsonNode.Parse(initialization.Configuration);
        _frame.State["greeting"] = config?["greeting"]?.GetValue<string>() ?? "Hello, Nonet!"; return ValueTask.FromResult(_frame);
    }
    public ValueTask<ExtensionFrame> InvokeAsync(ExtensionInvocation invocation, CancellationToken cancellationToken)
    {
        if (invocation.Action == "greet") { _frame.State["count"] = _frame.State["count"]!.GetValue<int>() + 1; _frame.Revision++; }
        return ValueTask.FromResult(_frame);
    }
    public ValueTask<ExtensionFrame> SyncAsync(CancellationToken cancellationToken) => ValueTask.FromResult(_frame);
    public ValueTask<ExtensionFrame> CompleteAsync(string requestId, System.Text.Json.Nodes.JsonObject result, CancellationToken cancellationToken) => ValueTask.FromResult(_frame);
    public ValueTask<ExtensionFrame> EventAsync(ExtensionEvent value, CancellationToken cancellationToken) => ValueTask.FromResult(_frame);
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
