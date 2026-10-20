using System;
using System.Reflection;
using System.Windows.Threading;

namespace PclShell.Api {
    public interface IPclPlugin {
        string Id { get; }
        bool Probe(PluginContext context, out string reason);
        void Enable(PluginContext context);
    }
    public sealed class PluginContext {
        public const int ApiVersion = 1;
        public Assembly PclAssembly { get; set; }
        public string PclPath { get; set; }
        public string PclSha256 { get; set; }
        public Dispatcher Dispatcher { get; set; }
        public Action<string> Log { get; set; }
    }
}
