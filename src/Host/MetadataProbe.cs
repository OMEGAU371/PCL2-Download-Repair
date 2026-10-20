using PclShell.Api;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;

namespace PclShell {
    internal static class MetadataProbe {
        internal static void Run(string target,string report) {
            var assembly=Assembly.LoadFrom(target);
            var resources=assembly.GetManifestResourceNames().Where(n=>n.EndsWith("Resources.resources",StringComparison.Ordinal)).Select(n=>new ResourceManager(n.Substring(0,n.Length-".resources".Length),assembly)).ToArray();
            ResolveEventHandler resolve=(sender,args)=>{
                string name=new AssemblyName(args.Name).Name;
                if(name.EndsWith(".resources",StringComparison.OrdinalIgnoreCase)) return null;
                foreach(var resource in resources) { try { if(resource.GetObject(name) is byte[] bytes)return Assembly.Load(bytes); }catch(MissingManifestResourceException){} }
                return null;
            };
            AppDomain.CurrentDomain.AssemblyResolve+=resolve;
            try {
                var results=new List<object>();var hash=Files.Hash(target);
                foreach(var manifest in Files.Discover(Path.Combine(Files.Root,"plugins"))) {
                    if(!manifest.Compatible(target,hash)){results.Add(new{id=manifest.id,compatible=false,probe=false,reason="hash or version mismatch"});continue;}
                    var plugin=(IPclPlugin)Activator.CreateInstance(Assembly.LoadFrom(Files.InFolder(manifest.Directory,manifest.assembly)).GetType(manifest.entryType,true));
                    bool valid=plugin.Probe(new PluginContext{PclAssembly=assembly,PclPath=target,PclSha256=hash,Log=delegate{}},out var reason);
                    results.Add(new{id=plugin.Id,compatible=true,probe=valid,reason});
                }
                File.WriteAllText(report,Files.Json.Serialize(new{target,sha256=hash,results}));
            }finally{AppDomain.CurrentDomain.AssemblyResolve-=resolve;}
        }
    }
}
