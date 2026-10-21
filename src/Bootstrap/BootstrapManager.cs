using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace PclShell.Bootstrap {
    public sealed class BootstrapManager : AppDomainManager {
        int attached;
        public override void InitializeNewDomain(AppDomainSetup info) {
            base.InitializeNewDomain(info);
            if(!AppDomain.CurrentDomain.IsDefaultAppDomain())return;
            // Let CLR establish the application's compatibility context and STA entry thread first.
            // Touching System.IO during early domain initialization can cache legacy path handling.
            AppDomain.CurrentDomain.AssemblyLoad+=OnAssemblyLoad;
            if(AppDomain.CurrentDomain.GetAssemblies().Any(a=>a.GetName().Name=="PresentationFramework"))Attach();
        }
        void OnAssemblyLoad(object sender,AssemblyLoadEventArgs args){
            if(args.LoadedAssembly.GetName().Name=="PresentationFramework")Attach();
        }
        void Attach(){
            if(System.Threading.Interlocked.Exchange(ref attached,1)!=0)return;
            AppDomain.CurrentDomain.AssemblyLoad-=OnAssemblyLoad;
            string root=Path.GetDirectoryName(typeof(BootstrapManager).Assembly.Location);
            string shell=Path.Combine(root,"PCL","plugins-loader");
            if(!File.Exists(Path.Combine(shell,"PclShell.exe")))shell=Path.Combine(root,"PCL-Shell-Auto");
            try {
                string config=AppDomain.CurrentDomain.SetupInformation.ConfigurationFile;
                if(!config.EndsWith(".exe.config",StringComparison.OrdinalIgnoreCase))return;
                string target=config.Substring(0,config.Length-7);
                string image=Process.GetCurrentProcess().MainModule.FileName;
                if(!string.Equals(Path.GetFullPath(target),Path.GetFullPath(image),StringComparison.OrdinalIgnoreCase))return;
                string[] args=Environment.GetCommandLineArgs().Skip(1).ToArray();
                if(args.Any(a=>new[]{"--update","--gpu","--memory"}.Contains(a,StringComparer.OrdinalIgnoreCase)))return;
                string host=Path.Combine(shell,"PclShell.exe");
                if(!File.Exists(host))return;
                Assembly.LoadFrom(host).GetType("PclShell.AutoAttach",true).GetMethod("Attach").Invoke(null,new object[]{target,shell});
            }catch(Exception e){
                try {Directory.CreateDirectory(Path.Combine(shell,"logs"));File.AppendAllText(Path.Combine(shell,"logs","bootstrap-error.log"),DateTime.Now+" "+e+Environment.NewLine);}catch{}
            }
        }
    }
}
