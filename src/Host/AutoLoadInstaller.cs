using System;
using System.IO;
using System.Linq;
using System.Xml;

namespace PclShell {
    internal sealed class InstallState {
        public string target {get;set;}
        public bool hadConfig {get;set;}
        public string originalBase64 {get;set;}
        public string installedHash {get;set;}
    }
    internal static class AutoLoadInstaller {
        const string AssemblyValue="PclShell.Bootstrap, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null";
        const string TypeValue="PclShell.Bootstrap.BootstrapManager";
        internal static string Folder(string target)=>Path.Combine(Path.GetDirectoryName(Path.GetFullPath(target)),"PCL-Shell-Auto");
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
            var hash=Files.Hash(target);
            if(!Files.Discover(Path.Combine(source,"plugins")).Any(p=>p.Compatible(target,hash)))throw new InvalidOperationException("此 PCL 尚未适配，不能安装自动加载。");
            string config=target+".config",destination=Folder(target),stateFile=StateFile(target);
            var doc=ReadConfiguration(File.Exists(config)?File.ReadAllText(config):null);
            bool registered=doc.SelectSingleNode("/configuration/runtime/appDomainManagerAssembly")!=null||doc.SelectSingleNode("/configuration/runtime/appDomainManagerType")!=null;
            InstallState state;
            if(File.Exists(stateFile)){
                state=Files.Json.Deserialize<InstallState>(File.ReadAllText(stateFile));
                if(!string.Equals(state.target,target,StringComparison.OrdinalIgnoreCase)||state.originalBase64==null)throw new InvalidDataException("备份格式不匹配，请先使用原安装方式撤销。");
            }else{
                if(registered)throw new InvalidDataException("已经有启动引导，且缺少本工具的配置备份。请先撤销原引导。");
                state=new InstallState {target=target,hadConfig=File.Exists(config),originalBase64=File.Exists(config)?Convert.ToBase64String(File.ReadAllBytes(config)):"",installedHash=""};
            }
            Register(doc);Directory.CreateDirectory(destination);
            // Copy dependencies before publishing the startup configuration. Never overwrite a running host.
            if(!string.Equals(Path.GetFullPath(source).TrimEnd('\\'),Path.GetFullPath(destination).TrimEnd('\\'),StringComparison.OrdinalIgnoreCase)){
                foreach(var name in new[]{"PclShell.exe","PclShell.exe.config","PclShell.Api.dll","PclShell.Bootstrap.dll"})File.Copy(Path.Combine(source,name),Path.Combine(destination,name),true);
                var pluginDirectory=Path.Combine(destination,"plugins","download-restore");Directory.CreateDirectory(pluginDirectory);
                foreach(var name in new[]{"plugin.json","PclShell.DownloadRestore.dll"})File.Copy(Path.Combine(source,"plugins","download-restore",name),Path.Combine(pluginDirectory,name),true);
            }
            string bootstrap=Path.Combine(Path.GetDirectoryName(target),"PclShell.Bootstrap.dll"),bootstrapSource=Path.Combine(source,"PclShell.Bootstrap.dll");
            if(File.Exists(bootstrap)&&Files.Hash(bootstrap)!=Files.Hash(bootstrapSource)&&!File.Exists(stateFile))throw new IOException("原目录中已有不同的引导 DLL，已停止安装。");
            if(!File.Exists(bootstrap)||Files.Hash(bootstrap)!=Files.Hash(bootstrapSource))File.Copy(bootstrapSource,bootstrap,true);
            var settingsFile=Path.Combine(destination,"shell.json");
            var settings=File.Exists(settingsFile)?Files.Json.Deserialize<ShellSettings>(File.ReadAllText(settingsFile)):new ShellSettings();settings.pclPath=target;
            WriteAtomic(settingsFile,Files.Json.Serialize(settings));
            WriteAtomic(stateFile,Files.Json.Serialize(state));
            WriteAtomic(config,doc.OuterXml);
            state.installedHash=Files.Hash(config);WriteAtomic(stateFile,Files.Json.Serialize(state));
        }
        internal static void Disable(string target){
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
            File.Delete(stateFile);
            // Leave the now-inactive DLLs in place; a running process may still be using them.
        }
    }
}
