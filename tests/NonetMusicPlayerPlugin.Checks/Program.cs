using System.IO.Compression;
using System.Text.Json.Nodes;
using NonetMusicPlayer.Core.Plugins;

/// <summary>只写 artifacts 夹具；验证初始工程能打包、配置、拒绝越界和不破坏已有输出。</summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            var root = Path.GetFullPath(args.FirstOrDefault() ?? ".");
            var work = Path.Combine(root, "artifacts", "checks-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(work);
            var source = Path.Combine(work, "source"); Directory.CreateDirectory(source);
            foreach (var name in new[] { "manifest.json", "page.json", "plugin_config_schema.json", "LICENSE" }) File.Copy(Path.Combine(root, "plugin", name), Path.Combine(source, name));
            string[] rids = ["win-x64", "win-x86", "win-arm64", "linux-x64", "linux-arm64", "osx-x64"];
            foreach (var rid in rids)
            {
                var target = Path.Combine(source, "bin", rid); Directory.CreateDirectory(target);
                File.Copy(Path.Combine(root, "plugin/bin/Release/net10.0/NonetMusicPlayerPlugin.dll"), Path.Combine(target, "NonetMusicPlayerPlugin.dll"));
            }
            var package = PluginPackageBuilder.Pack(source, Path.Combine(work, "hello.impp"), false, null, PluginPlatformPolicy.CurrentRid);
            var manifest = PluginPackageInspector.Inspect(package); Require(manifest.Type == "extension" && !manifest.Enabled, "Manifest is valid and installs disabled.");
            using (var zip = ZipFile.OpenRead(package)) Require(zip.Entries.Count == 5 && zip.GetEntry("LICENSE") is not null && zip.Entries.All(e => !e.FullName.Contains("sdk") && !e.FullName.Contains("samples")), "Only runtime JSON and the required GPL license are distributed.");
            foreach (var rid in rids)
            {
                var platformPackage = PluginPackageBuilder.Pack(source, Path.Combine(work, "hello-" + rid + ".impp"), false, null, rid);
                Require(PluginPackageInspector.Inspect(platformPackage).EntryPoints.Keys.SequenceEqual([rid]), "Each package advertises exactly one target.");
                using var zip = ZipFile.OpenRead(platformPackage);
                Require(zip.Entries.Count == 5 && zip.Entries.Where(e => e.FullName.StartsWith("bin/")).All(e => e.FullName.StartsWith("bin/" + rid + "/")), "No other-platform payload is bundled.");
            }
            using var schemaFile = File.OpenRead(Path.Combine(root, "plugin", PluginConfigSchema.FileName)); var schema = PluginConfigSchema.Read(schemaFile);
            var values = PluginConfigSchema.Defaults(schema); values["greeting"] = "你好，プラグイン！"; PluginConfigSchema.Validate(schema, values);
            var page = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "plugin", manifest.PageEntry)))!;
            using var resolved = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(PluginConfigSchema.Substitute(page, values).ToJsonString()));
            Require(ExtensionContract.ReadPage(resolved).Root.Children.First().Bind == "greeting", "Generic page binds developer-defined state.");
            // 新增字段是可选能力，基础示例不依赖它们；开发者可显式协商使用。
            using var optionalPage = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("""{"schemaVersion":2,"root":{"type":"grid","columnsBind":"layoutColumns","contextActions":[{"type":"button","text":"Item action","action":"custom","parameter":"item.id"}]}}"""));
            var optional = ExtensionContract.ReadPage(optionalPage);
            Require(optional.Root.ColumnsBind == "layoutColumns" && optional.Root.ContextActions.Single().Action == "custom"
                && ExtensionContract.Capabilities.Contains("ui.layout-bind.v1") && ExtensionContract.Capabilities.Contains("ui.context-menu.v1"), "Optional dynamic layout and context menu descriptions are valid.");
            using var controlsPage = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("""{"schemaVersion":2,"root":{"type":"stack","children":[{"type":"icon","icon":"Sidebar"},{"type":"checkbox","bind":"checked","input":"selection","action":"select"},{"type":"button","variant":"ghost","selectedIf":"active","openMenuOnClick":true,"contextActions":[{"type":"button","text":"Rename","action":"rename"}]}]}}"""));
            Require(ExtensionContract.ReadPage(controlsPage).Root.Children.Count == 3 && ExtensionContract.Capabilities.Contains("ui.controls.v1"), "Optional controls preserve the existing interface.");
            using var selectPage = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("""{"schemaVersion":2,"root":{"type":"select","bind":"choices","selectedBind":"choice","popupPlacement":"top","suppressTooltips":true}}"""));
            var choice = ExtensionContract.ReadPage(selectPage).Root;
            Require(choice is { PopupPlacement: "top", SuppressTooltips: true } && ExtensionContract.Capabilities.Contains("ui.select-options.v1"), "SDK 4.16 optional select fields are available.");
            Require(ExtensionContract.Capabilities.Contains("statistics.charts.v1") && StyleSheetContract.ControlTypes.Contains("ListeningBarChart"), "Current chart and style interfaces are directly referenced.");
            ExtensionContract.ValidateFrame(new() { Requests = [new("prompt", "dialogs.prompt", new() { ["text"] = "Rename", ["initial"] = "Old" })] });
            var backend = new NonetMusicPlayerPlugin.HelloWorld(); var frame = backend.InitializeAsync(new(2, values.ToJsonString(), work, ExtensionContract.Capabilities), default).Result;
            Require(frame.State["greeting"]!.GetValue<string>() == "你好，プラグイン！", "Configuration initializes plugin state.");
            var hash = System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(package));
            Reject(() => PluginPackageBuilder.Pack(source, package), "Existing output is protected.");
            Require(hash.SequenceEqual(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(package))), "Rejected build leaves old package intact.");
            Reject(() => PluginPackageBuilder.Pack(Path.Combine(root, "plugin"), Path.Combine(work, "bad.impkg")), "Only impp is accepted.");
            Reject(() => PluginPathPolicy.ValidateRelativePath("../secret"), "Directory traversal is rejected.");
            Reject(() => PluginPackageBuilder.Pack(Path.Combine(root, "plugin"), Path.Combine(work, "extra.impp"), additionalFiles: ["page.json", "../README.md"]), "Undeclared escaping files are rejected.");
            var oversized = Path.Combine(work, "oversized-license.impp");
            using (var sourceZip = ZipFile.OpenRead(package))
            using (var invalidZip = ZipFile.Open(oversized, ZipArchiveMode.Create))
            {
                foreach (var entry in sourceZip.Entries.Where(e => e.FullName != "LICENSE"))
                { using var input = entry.Open(); using var destination = invalidZip.CreateEntry(entry.FullName).Open(); input.CopyTo(destination); }
                using var writer = new StreamWriter(invalidZip.CreateEntry("LICENSE").Open()); writer.Write(new string('x', 256 * 1024 + 1));
            }
            Reject(() => PluginPackageInspector.Inspect(oversized), "License exception remains bounded and cannot bypass package policy.");
            Console.WriteLine("PASS six clean platform packages, SDK 4.16 optional capabilities, configuration, overwrite protection and path validation"); return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static void Reject(Action action, string message) { try { action(); } catch (Exception error) when (error is InvalidDataException or IOException or ArgumentException) { return; } throw new InvalidOperationException(message); }
}
