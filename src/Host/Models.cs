using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Web.Script.Serialization;

namespace PclShell {
    public sealed class PluginManifest {
        public string id { get; set; }
        public string name { get; set; }
        public int apiVersion { get; set; }
        public string assembly { get; set; }
        public string entryType { get; set; }
        public string[] supportedTargetHashes { get; set; }
        public string[] supportedVersions { get; set; }
        public string description { get; set; }
        [ScriptIgnore] public string Directory { get; set; }
        [ScriptIgnore] public string Error { get; set; }
        public bool Compatible(string target, string hash) => Error == null && apiVersion == 1 &&
            (supportedVersions ?? new string[0]).Contains(FileVersionInfo.GetVersionInfo(target).FileVersion) &&
            (supportedTargetHashes ?? new string[0]).Any(h => string.Equals(h, hash, StringComparison.OrdinalIgnoreCase));
    }
    public sealed class ShellSettings {
        public string pclPath { get; set; }
        public Dictionary<string, bool> enabled { get; set; } = new Dictionary<string, bool>();
    }
    [Serializable]
    public sealed class RunOptions {
        public string Target, ShellDirectory, ExpectedHash, DiagnosticsDirectory;
        public string[] PclArguments;
        public string[] PluginManifestFiles;
        public bool ValidateFixture;
        public string FixtureInstallVersion;
    }
    internal static class Files {
        internal static readonly JavaScriptSerializer Json = new JavaScriptSerializer();
        internal static string Root => AppDomain.CurrentDomain.BaseDirectory;
        internal static string Hash(string file) {
            using (var hash = SHA256.Create()) using (var stream = File.OpenRead(file))
                return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
        internal static string InFolder(string root, string relative) {
            if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative)) throw new InvalidDataException("插件路径必须是相对路径。");
            string boundary = Path.GetFullPath(root).TrimEnd('\\','/') + Path.DirectorySeparatorChar;
            string path = Path.GetFullPath(Path.Combine(boundary, relative));
            if (!path.StartsWith(boundary, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("插件路径超出了插件目录。");
            return path;
        }
        internal static PluginManifest ReadManifest(string file) {
            var manifest = Json.Deserialize<PluginManifest>(File.ReadAllText(file));
            manifest.Directory = Path.GetDirectoryName(Path.GetFullPath(file));
            if (manifest.apiVersion != 1) throw new InvalidDataException("插件 API 版本不匹配。");
            if (string.IsNullOrWhiteSpace(manifest.id) || string.IsNullOrWhiteSpace(manifest.entryType)) throw new InvalidDataException("插件清单不完整。");
            if (!File.Exists(InFolder(manifest.Directory, manifest.assembly))) throw new FileNotFoundException("插件 DLL 不存在。");
            return manifest;
        }
        internal static List<PluginManifest> Discover(string root) {
            if (!Directory.Exists(root)) return new List<PluginManifest>();
            var result = new List<PluginManifest>();
            foreach (string directory in Directory.GetDirectories(root).OrderBy(x=>x,StringComparer.OrdinalIgnoreCase)) {
                var file = Path.Combine(directory, "plugin.json"); if (!File.Exists(file)) continue;
                try { result.Add(ReadManifest(file)); }
                catch(Exception e) { result.Add(new PluginManifest { id = Path.GetFileName(directory), name = Path.GetFileName(directory), Directory = directory, Error = e.Message }); }
            }
            foreach(var group in result.GroupBy(x=>x.id).Where(g=>g.Count()>1)) foreach(var item in group) item.Error="插件 ID 重复。";
            return result;
        }
        internal static ShellSettings ReadSettings() {
            string file = Path.Combine(Root,"shell.json");
            try { return File.Exists(file) ? Json.Deserialize<ShellSettings>(File.ReadAllText(file)) : new ShellSettings(); }
            catch { return new ShellSettings(); }
        }
        internal static void WriteSettings(ShellSettings settings) {
            File.WriteAllText(Path.Combine(Root,"shell.json"), Json.Serialize(settings));
        }
    }
}
