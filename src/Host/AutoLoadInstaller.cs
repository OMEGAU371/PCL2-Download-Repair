using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Diagnostics;
using System.Reflection;

namespace PclShell {
    internal sealed class InstallState {
        public string target {get;set;}
        public bool hadConfig {get;set;}
        public string originalBase64 {get;set;}
        public string installedHash {get;set;}
        public bool hadBootstrap {get;set;}
        public string originalBootstrapBase64 {get;set;}
        public string installedBootstrapHash {get;set;}
    }
    internal static class AutoLoadInstaller {
        const string AssemblyValue="PclShell.Bootstrap, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null";
        const string TypeValue="PclShell.Bootstrap.BootstrapManager";
        internal static string Folder(string target)=>Path.Combine(Path.GetDirectoryName(Path.GetFullPath(target)),"PCL","plugins-loader");
        internal static string StateFile(string target)=>Path.Combine(Folder(target),"autoload-state.json");
        internal static XmlDocument ReadConfiguration(string content){
            var doc=new XmlDocument {PreserveWhitespace=true,XmlResolver=null};doc.LoadXml(content??"<configuration><runtime /></configuration>");
            if(doc.DocumentElement?.Name!="configuration")throw new InvalidDataException("不支持的启动配置格式。");
            return doc;
        }
        internal static void Register(XmlDocument doc){
            var runtime=doc.SelectSingleNode("/configuration/runtime");
            if(runtime==null){runtime=doc.CreateElement("runtime");doc.DocumentElement.AppendChild(runtime);}
            foreach(var pair in new[]{new[]{"appDomainManagerAssembly",AssemblyValue},new[]{"appDomainManagerType",TypeValue}}){
                var node=runtime.SelectSingleNode(pair[0]) as XmlElement;
                if(node!=null&&node.GetAttribute("value")!=pair[1])throw new InvalidDataException("发现其他启动引导，已停止安装以保留原配置。");
            }
            foreach(var pair in new[]{new[]{"appDomainManagerAssembly",AssemblyValue},new[]{"appDomainManagerType",TypeValue}}){
                var node=runtime.SelectSingleNode(pair[0]) as XmlElement;
                if(node==null){node=doc.CreateElement(pair[0]);runtime.AppendChild(node);}node.SetAttribute("value",pair[1]);
            }
        }
        internal static void Unregister(XmlDocument doc){
            foreach(XmlElement node in doc.SelectNodes("/configuration/runtime/appDomainManagerAssembly | /configuration/runtime/appDomainManagerType").Cast<XmlElement>().ToArray())
                if((node.Name=="appDomainManagerAssembly"&&node.GetAttribute("value")==AssemblyValue)||(node.Name=="appDomainManagerType"&&node.GetAttribute("value")==TypeValue))node.ParentNode.RemoveChild(node);
        }
        internal static void WriteAtomic(string file,string content){
            string temporary=file+"."+Guid.NewGuid().ToString("N")+".tmp";
            try{File.WriteAllText(temporary,content,new System.Text.UTF8Encoding(false));if(File.Exists(file))File.Replace(temporary,file,null);else File.Move(temporary,file);}finally{if(File.Exists(temporary))File.Delete(temporary);}
        }
        internal static void Enable(string target,string source){
            target=Path.GetFullPath(target);
            if(!File.Exists(target))throw new FileNotFoundException("原 PCL 程序不存在。");
            if(ManagerWindow.OriginalPclIsRunning())throw new InvalidOperationException("请先关闭 PCL，再安装自动加载。");
            var packageHashes=PackageIntegrity.Verify(source);
            var metadata=Assembly.ReflectionOnlyLoad(File.ReadAllBytes(target));
            if(metadata.GetName().Name!="Plain Craft Launcher 2"||!metadata.ImageRuntimeVersion.StartsWith("v4.",StringComparison.Ordinal))throw new InvalidOperationException("请选择 .NET Framework 版本的原 PCL2.exe。此运行时尚不受加载器支持。");
            string config=target+".config",destination=Folder(target),stateFile=StateFile(target);
            string bootstrap=Path.Combine(Path.GetDirectoryName(target),"PclShell.Bootstrap.dll"),bootstrapSource=Path.Combine(source,"PclShell.Bootstrap.dll");
            var doc=ReadConfiguration(File.Exists(config)?File.ReadAllText(config):null);
            bool registered=doc.SelectSingleNode("/configuration/runtime/appDomainManagerAssembly")!=null||doc.SelectSingleNode("/configuration/runtime/appDomainManagerType")!=null;
            InstallState state;
            if(File.Exists(stateFile)){
                state=Files.Json.Deserialize<InstallState>(File.ReadAllText(stateFile));
                if(!string.Equals(state.target,target,StringComparison.OrdinalIgnoreCase)||state.originalBase64==null)throw new InvalidDataException("备份格式不匹配，请先使用原安装方式撤销。");
                // A PCL update or a user edit may replace the configuration. Preserve that newer baseline.
                if(!File.Exists(config)||Files.Hash(config)!=state.installedHash){
                    state.hadConfig=File.Exists(config);
                    if(!state.hadConfig)state.originalBase64="";
                    else if(!registered)state.originalBase64=Convert.ToBase64String(File.ReadAllBytes(config));
                    else{var baseline=ReadConfiguration(File.ReadAllText(config));Unregister(baseline);state.originalBase64=Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(baseline.OuterXml));}
                }
                if(File.Exists(bootstrap)&&state.installedBootstrapHash!=null&&Files.Hash(bootstrap)!=state.installedBootstrapHash&&Files.Hash(bootstrap)!=Files.Hash(bootstrapSource))throw new IOException("引导 DLL 已被其他程序更改，已停止覆盖。");
            }else{
                if(registered)throw new InvalidDataException("已经有启动引导，且缺少本工具的配置备份。请先撤销原引导。");
                if(File.Exists(bootstrap)&&Files.Hash(bootstrap)!=Files.Hash(bootstrapSource))throw new IOException("原目录中已有不同的引导 DLL，请先用旧版卸载或撤销。");
                state=new InstallState {target=target,hadConfig=File.Exists(config),originalBase64=File.Exists(config)?Convert.ToBase64String(File.ReadAllBytes(config)):"",installedHash="",hadBootstrap=File.Exists(bootstrap),originalBootstrapBase64=File.Exists(bootstrap)?Convert.ToBase64String(File.ReadAllBytes(bootstrap)):"",installedBootstrapHash=Files.Hash(bootstrapSource)};
            }
            Register(doc);Directory.CreateDirectory(destination);
            // Copy dependencies before publishing the startup configuration. Never overwrite a running host.
            if(!string.Equals(Path.GetFullPath(source).TrimEnd('\\'),Path.GetFullPath(destination).TrimEnd('\\'),StringComparison.OrdinalIgnoreCase)){
                foreach(var name in new[]{"PclShell.exe","PclShell.exe.config","PclShell.Api.dll","PclShell.Bootstrap.dll","loader-setup.exe","loader-setup.exe.config","install.bat","uninstall.bat","README.md","SECURITY.md","LICENSE","TESTING.md","SHA256SUMS.txt"})File.Copy(Path.Combine(source,name),Path.Combine(destination,name),true);
                CopyPluginTree(Path.Combine(source,"plugins"),Path.Combine(destination,"plugins"));
            }
            PackageIntegrity.PrepareInstalledRuntime(destination,packageHashes);
            if(!File.Exists(bootstrap)||Files.Hash(bootstrap)!=Files.Hash(bootstrapSource))File.Copy(bootstrapSource,bootstrap,true);
            PackageIntegrity.ClearVerifiedDownloadMark(bootstrap,packageHashes["PclShell.Bootstrap.dll"]);
            state.installedBootstrapHash=Files.Hash(bootstrapSource);
            var settingsFile=Path.Combine(destination,"shell.json");
            var settings=File.Exists(settingsFile)?Files.Json.Deserialize<ShellSettings>(File.ReadAllText(settingsFile)):new ShellSettings();settings.pclPath=target;
            WriteAtomic(settingsFile,Files.Json.Serialize(settings));
            WriteAtomic(stateFile,Files.Json.Serialize(state));
            WriteAtomic(config,doc.OuterXml);
            state.installedHash=Files.Hash(config);WriteAtomic(stateFile,Files.Json.Serialize(state));
        }
        internal static void Disable(string target,bool keepBackup=false){
            target=Path.GetFullPath(target);string stateFile=StateFile(target),config=target+".config";
            if(!File.Exists(stateFile))throw new FileNotFoundException("没有找到自动加载备份。");
            var state=Files.Json.Deserialize<InstallState>(File.ReadAllText(stateFile));
            if(!string.Equals(state.target,target,StringComparison.OrdinalIgnoreCase)||state.originalBase64==null)throw new InvalidDataException("备份格式不匹配，请使用原安装方式撤销。");
            if(File.Exists(config)){
                if(string.Equals(Files.Hash(config),state.installedHash,StringComparison.OrdinalIgnoreCase)){
                    if(state.hadConfig){string temporary=config+".restore.tmp";File.WriteAllBytes(temporary,Convert.FromBase64String(state.originalBase64));File.Replace(temporary,config,null);}
                    else File.Delete(config);
                }else{var doc=ReadConfiguration(File.ReadAllText(config));Unregister(doc);WriteAtomic(config,doc.OuterXml);}
            }
            if(!keepBackup)File.Delete(stateFile);
            // Leave the now-inactive DLLs in place; a running process may still be using them.
        }
        internal static bool IsRegistered(string target){
            try{var config=target+".config";return File.Exists(config)&&ReadConfiguration(File.ReadAllText(config)).SelectSingleNode("/configuration/runtime/appDomainManagerType") is XmlElement node&&node.GetAttribute("value")==TypeValue;}catch{return false;}
        }
        internal static void CopyPluginTree(string source,string destination){
            if((File.GetAttributes(source)&FileAttributes.ReparsePoint)!=0)throw new IOException("插件目录不能是文件系统链接。");
            Directory.CreateDirectory(destination);
            if((File.GetAttributes(destination)&FileAttributes.ReparsePoint)!=0)throw new IOException("安装目录不能是文件系统链接。");
            foreach(string entry in Directory.GetFileSystemEntries(source)){
                var attributes=File.GetAttributes(entry);var output=Path.Combine(destination,Path.GetFileName(entry));
                if((attributes&FileAttributes.ReparsePoint)!=0||(File.Exists(output)&&(File.GetAttributes(output)&FileAttributes.ReparsePoint)!=0))throw new IOException("插件包含文件系统链接，已停止复制。");
                if((attributes&FileAttributes.Directory)!=0)CopyPluginTree(entry,output);else File.Copy(entry,output,true);
            }
        }
        internal static void Uninstall(string target){
            target=Path.GetFullPath(target);
            if(ManagerWindow.OriginalPclIsRunning())throw new InvalidOperationException("请先关闭 PCL，再卸载加载器。");
            string stateFile=StateFile(target),folder=Folder(target);
            if(!File.Exists(stateFile))throw new FileNotFoundException("没有找到已安装的加载器备份。若只解压但未安装，删除解压文件夹即可。");
            var state=Files.Json.Deserialize<InstallState>(File.ReadAllText(stateFile));
            Disable(target);
            string bootstrap=Path.Combine(Path.GetDirectoryName(target),"PclShell.Bootstrap.dll");
            bool shared=Directory.GetFiles(Path.GetDirectoryName(target),"*.exe.config").Where(p=>!string.Equals(p,target+".config",StringComparison.OrdinalIgnoreCase)).Any(p=>File.ReadAllText(p).Contains(TypeValue));
            if(!shared&&File.Exists(bootstrap)&&state.installedBootstrapHash!=null&&Files.Hash(bootstrap)==state.installedBootstrapHash){
                if(state.hadBootstrap)File.WriteAllBytes(bootstrap,Convert.FromBase64String(state.originalBootstrapBase64));else File.Delete(bootstrap);
            }
            string nonce=Guid.NewGuid().ToString("N");File.WriteAllText(Path.Combine(folder,".uninstall-token"),target+"\n"+nonce);
            string temporary=Path.Combine(Path.GetTempPath(),"PclPluginLoader-Uninstall-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temporary);
            string helper=Path.Combine(temporary,"cleanup.exe");File.Copy(Path.Combine(Files.Root,"loader-setup.exe"),helper);
            File.Copy(Path.Combine(Files.Root,"loader-setup.exe.config"),helper+".config");
            var arguments="--cleanup "+Process.GetCurrentProcess().Id+" \""+target+"\" \""+folder+"\" "+nonce;
            Process.Start(new ProcessStartInfo(helper,arguments){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=temporary});
        }
    }
}
