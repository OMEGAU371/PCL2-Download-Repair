using System;
using System.IO;

namespace PclShell {
    internal static class ShellTests {
        internal static void Run() {
            int count=0;string root=Files.Root;
            foreach(string input in new[]{"../bad.dll","a/../../bad.dll",Path.Combine(root,"bad.dll")}){
                bool rejected=false;try{Files.InFolder(root,input);}catch(InvalidDataException){rejected=true;}
                if(!rejected)throw new Exception("Plugin path traversal accepted");count++;
            }
            if(!Files.InFolder(root,"plugins/a.dll").StartsWith(root,StringComparison.OrdinalIgnoreCase))throw new Exception("Valid plugin path rejected");count++;
            var self=typeof(Program).Assembly.Location;var version=System.Diagnostics.FileVersionInfo.GetVersionInfo(self).FileVersion;var hash=Files.Hash(self);
            var manifest=new PluginManifest{apiVersion=1,supportedTargetHashes=new[]{hash},supportedVersions=new[]{version}};
            if(!manifest.Compatible(self,hash))throw new Exception("Exact match rejected");count++;
            if(manifest.Compatible(self,new string('0',64)))throw new Exception("Unknown target hash accepted");count++;
            manifest.apiVersion=2;if(manifest.Compatible(self,hash))throw new Exception("Wrong API version accepted");count++;
            manifest.apiVersion=1;manifest.supportedVersions=new[]{"9.9.9.9"};if(manifest.Compatible(self,hash))throw new Exception("Unknown version accepted");count++;
            var doc=AutoLoadInstaller.ReadConfiguration("<configuration><startup><supportedRuntime version='v4.0'/></startup><runtime><generatePublisherEvidence enabled='false'/></runtime></configuration>");
            AutoLoadInstaller.Register(doc);
            if(doc.SelectSingleNode("/configuration/startup/supportedRuntime")==null||doc.SelectSingleNode("/configuration/runtime/generatePublisherEvidence")==null)throw new Exception("Configuration merge lost original settings");count++;
            AutoLoadInstaller.Register(doc);
            if(doc.SelectNodes("/configuration/runtime/appDomainManagerType").Count!=1)throw new Exception("Configuration registration duplicated");count++;
            AutoLoadInstaller.Unregister(doc);
            if(doc.SelectNodes("/configuration/runtime/appDomainManagerAssembly | /configuration/runtime/appDomainManagerType").Count!=0||doc.SelectSingleNode("/configuration/startup")==null)throw new Exception("Configuration removal lost original settings");count++;
            bool foreignRejected=false;
            try{AutoLoadInstaller.Register(AutoLoadInstaller.ReadConfiguration("<configuration><runtime><appDomainManagerType value='Other.Manager'/></runtime></configuration>"));}catch(InvalidDataException){foreignRejected=true;}
            if(!foreignRejected)throw new Exception("Foreign startup manager was overwritten");count++;
            var foreign=AutoLoadInstaller.ReadConfiguration("<configuration><runtime><appDomainManagerType value='Other.Manager'/></runtime></configuration>");AutoLoadInstaller.Unregister(foreign);
            if(foreign.SelectSingleNode("/configuration/runtime/appDomainManagerType")==null)throw new Exception("Foreign manager removed");count++;
            bool invalidRejected=false;try{AutoLoadInstaller.ReadConfiguration("<wrong/>");}catch(InvalidDataException){invalidRejected=true;}if(!invalidRejected)throw new Exception("Invalid configuration accepted");count++;
            if(Launcher.FindTarget(self,Path.GetTempPath())!=self)throw new Exception("Saved executable selection failed");count++;
            if(Launcher.FindTarget(null,Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N")))!=null)throw new Exception("Missing PCL reported as found");count++;
            File.WriteAllText(Path.Combine(root,"self-test.json"),Files.Json.Serialize(new{success=true,tests=count}));
        }
    }
}
