using PclShell.Api;
using System;
using System.Linq;
using Expression = System.Linq.Expressions.Expression;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;

namespace PclShell.DownloadRestore {
    public sealed class DownloadRestorePlugin : IPclPlugin {
        public string Id => "download-restore";
        readonly ConditionalWeakTable<object,object> installed = new ConditionalWeakTable<object,object>();
        Type pageType;
        MethodInfo initialize, finish;
        FieldInfo realLoader;
        PluginContext context;
        public bool Probe(PluginContext value,out string reason) {
            try {
                pageType=value.PclAssembly.GetType("PCL.PageDownloadInstall",true);
                if(!typeof(FrameworkElement).IsAssignableFrom(pageType)) throw new MissingMemberException("下载页的基类不匹配。");
                finish=pageType.GetMethod("LoadMinecraft_OnFinish",BindingFlags.Instance|BindingFlags.NonPublic);
                if(finish==null || finish.GetParameters().Length!=0) throw new MissingMethodException("版本列表完成回调不匹配。");
                initialize=pageType.GetMethods(BindingFlags.Instance|BindingFlags.Public).Single(m=>m.Name=="PageLoaderInit" && m.IsGenericMethodDefinition && m.GetGenericArguments().Length==2 && m.GetParameters().Length==8);
                realLoader=value.PclAssembly.GetType("PCL.ModDownload",true).GetField("DlClientListLoader",BindingFlags.Public|BindingFlags.Static);
                if(realLoader==null || !realLoader.FieldType.IsGenericType || realLoader.FieldType.GetGenericArguments().Length!=2) throw new MissingFieldException("Minecraft 版本列表加载器不匹配。");
                foreach(var name in new[]{"LoadMinecraft","PanLoad","PanAllBack"}) {
                    if(pageType.GetProperty(name,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)==null && pageType.GetField(name,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)==null)
                        throw new MissingMemberException("缺少下载页控件："+name);
                }
                reason="接口检查通过"; return true;
            } catch(Exception e) { reason=e.Message; return false; }
        }
        public void Enable(PluginContext value) {
            context=value;
            EventManager.RegisterClassHandler(pageType,FrameworkElement.LoadedEvent,new RoutedEventHandler(OnLoaded),true);
            context.Log("DOWNLOAD_RESTORE registered WPF Loaded class handler");
        }
        void OnLoaded(object sender,RoutedEventArgs e) {
            if(sender.GetType()!=pageType || installed.TryGetValue(sender,out _)) return;
            try {
                var page=(FrameworkElement)sender;
                var executable=(string)context.PclAssembly.GetType("PCL.ModBase",true).GetField("PathExe",BindingFlags.Static|BindingFlags.Public).GetValue(null);
                if(!string.Equals(System.IO.Path.GetFullPath(executable),System.IO.Path.GetFullPath(context.PclPath),StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("PCL 当前识别的主程序路径不匹配，保留原页面。");
                var loader=realLoader.GetValue(null);
                var genericTypes=realLoader.FieldType.GetGenericArguments();
                var loaderParameter=Expression.Parameter(realLoader.FieldType,"loader");
                var callback=Expression.Lambda(typeof(Action<>).MakeGenericType(realLoader.FieldType),Expression.Call(Expression.Constant(sender,pageType),finish),loaderParameter).Compile();
                var controls=new[]{"LoadMinecraft","PanLoad","PanAllBack"}.Select(n=>page.FindName(n) ?? Member(sender,n)).ToArray();
                if(controls.Any(c=>c==null)) throw new MissingMemberException("下载页的命名控件不可用。");
                initialize.MakeGenericMethod(genericTypes).Invoke(sender,new object[]{controls[0],controls[1],controls[2],null,loader,callback,null,true});
                installed.Add(sender,new object());
                context.Log("DOWNLOAD_RESTORE bound original DlClientListLoader to original page");
            } catch(Exception error) { context.Log("DOWNLOAD_RESTORE failed; original page retained: "+error); }
        }
        static object Member(object instance,string name) {
            var flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
            return instance.GetType().GetProperty(name,flags)?.GetValue(instance) ?? instance.GetType().GetField(name,flags)?.GetValue(instance);
        }
    }
}
