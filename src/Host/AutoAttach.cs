using System;
using System.IO;
using System.Linq;

namespace PclShell {
    public static class AutoAttach {
        public static void Attach(string target,string shellDirectory) {
            var settingsFile=Path.Combine(shellDirectory,"shell.json");
            var settings=File.Exists(settingsFile)?Files.Json.Deserialize<ShellSettings>(File.ReadAllText(settingsFile)):new ShellSettings();
            if(!string.Equals(Path.GetFullPath(settings.pclPath??"."),Path.GetFullPath(target),StringComparison.OrdinalIgnoreCase))return;
            var hash=Files.Hash(target);
            bool bypass=Environment.GetEnvironmentVariable("PCL_SHELL_NO_PLUGINS")=="1";
            var selected=Files.Discover(Path.Combine(shellDirectory,"plugins")).Where(p=>!bypass&&p.Compatible(target,hash)&&(!settings.enabled.TryGetValue(p.id,out var enabled)||enabled)).ToArray();
            // This optional harness is restricted again by FixtureValidation's compiled registry marker.
            var validation=Environment.GetEnvironmentVariable("PCL_SHELL_FIXTURE_RESULT");
            bool fixture=!string.IsNullOrEmpty(validation)&&hash=="cddc8482003c4dbf2aff8e67f6a20cd9a61801abe37138e2982a6671e02a51d8";
            if(selected.Length==0&&!fixture)return;
            var logs=fixture?validation:Path.Combine(shellDirectory,"logs","direct-"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));
            PluginRuntime.Schedule(new RunOptions {Target=target,ShellDirectory=shellDirectory,ExpectedHash=hash,DiagnosticsDirectory=logs,PluginManifestFiles=selected.Select(p=>Path.Combine(p.Directory,"plugin.json")).ToArray(),ValidateFixture=fixture});
        }
    }
}
