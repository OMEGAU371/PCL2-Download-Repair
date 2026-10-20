using PclShell.Api;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;

namespace PclShell {
    public sealed class RuntimeEntry : MarshalByRefObject {
        public override object InitializeLifetimeService() => null;
        public int Run(string optionsJson) {
            var options=Files.Json.Deserialize<RunOptions>(optionsJson);
            Directory.SetCurrentDirectory(Path.GetDirectoryName(options.Target));
            PluginRuntime.Schedule(options);
            return AppDomain.CurrentDomain.ExecuteAssembly(options.Target, options.PclArguments ?? new string[0]);
        }
    }
    internal static class PluginRuntime {
        internal static void Schedule(RunOptions options) {
            Directory.CreateDirectory(options.DiagnosticsDirectory);
            string logFile = Path.Combine(options.DiagnosticsDirectory,"runtime.log");
            object logLock = new object();
            Action<string> log = message => { lock(logLock) File.AppendAllText(logFile, DateTime.Now.ToString("HH:mm:ss.fff") + " " + message + Environment.NewLine); };
            log("TARGET " + options.Target + " SHA256 " + Files.Hash(options.Target));
            if (!string.Equals(Files.Hash(options.Target),options.ExpectedHash,StringComparison.OrdinalIgnoreCase)) throw new IOException("PCL 文件在启动期间发生变化，请重新启动外层启动器。");
            var dispatcher = Dispatcher.CurrentDispatcher;
            var retainedPlugins = new List<IPclPlugin>();
            dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(() => {
                try {
                    var targetAssembly = AppDomain.CurrentDomain.GetAssemblies().Single(a=>!a.IsDynamic && string.Equals(a.Location,options.Target,StringComparison.OrdinalIgnoreCase));
                    log("APP_READY entry=" + Assembly.GetEntryAssembly()?.GetName().Name + " base=" + AppDomain.CurrentDomain.BaseDirectory + " appName=" + AppDomain.CurrentDomain.SetupInformation.ApplicationName);
                    if (Application.Current == null) throw new InvalidOperationException("没有发现 PCL 的 WPF Application。");
                    var context = new PluginContext { PclAssembly=targetAssembly,PclPath=options.Target,PclSha256=options.ExpectedHash,Dispatcher=dispatcher,Log=log };
                    foreach(string file in options.PluginManifestFiles) {
                        try {
                            var manifest=Files.ReadManifest(file);
                            if (!manifest.Compatible(options.Target,options.ExpectedHash)) { log("SKIP incompatible " + manifest.id); continue; }
                            var assembly=Assembly.LoadFrom(Files.InFolder(manifest.Directory,manifest.assembly));
                            var plugin=(IPclPlugin)Activator.CreateInstance(assembly.GetType(manifest.entryType,true));
                            if(plugin.Id!=manifest.id) throw new InvalidDataException("插件 ID 与清单不匹配。");
                            if(!plugin.Probe(context,out var reason)) { log("SKIP probe " + plugin.Id + ": " + reason); continue; }
                            plugin.Enable(context); retainedPlugins.Add(plugin); log("ENABLED " + plugin.Id);
                        } catch(Exception e) { log("PLUGIN_ERROR " + e); }
                    }
                    if (options.ValidateFixture) FixtureValidation.Start(context,options.DiagnosticsDirectory,retainedPlugins.Count,options.FixtureInstallVersion);
                } catch(Exception e) { log("BOOTSTRAP_ERROR " + e); }
            }));
        }
    }
    internal static class RuntimeHost {
        internal static int Run(RunOptions options) {
            var setup = new AppDomainSetup { ApplicationBase=Path.GetDirectoryName(options.Target), ApplicationName=Path.GetFileName(options.Target), ConfigurationFile=options.Target+".config", ShadowCopyFiles="false" };
            var domain=AppDomain.CreateDomain("PCL Shell Runtime",null,setup);
            try {
                var runner=(RuntimeEntry)domain.CreateInstanceFromAndUnwrap(typeof(RuntimeEntry).Assembly.Location,typeof(RuntimeEntry).FullName);
                return runner.Run(Files.Json.Serialize(options));
            } finally { AppDomain.Unload(domain); }
        }
    }
}
