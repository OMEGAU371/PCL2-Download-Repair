using PclShell.Api;
using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;

namespace PclShell {
    internal static class FixtureValidation {
        internal static void Start(PluginContext context,string folder,int pluginCount,string installVersion) {
            var secret=context.PclAssembly.GetType("PCL.ModSecret",true);
            var registry=secret.GetField("RegFolder",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic)?.GetRawConstantValue() as string;
            if(registry!="PCLShellFixture") throw new InvalidOperationException("验证模式仅允许独立的 PCLShellFixture 测试构建。");
            var mainModule=context.PclAssembly.GetType("PCL.ModMain",true);
            var timer=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(250) };
            bool navigated=false,installStarted=false;
            var start=DateTime.UtcNow;
            timer.Tick += delegate {
                try {
                    if((DateTime.UtcNow-start).TotalSeconds>120) throw new TimeoutException("测试没有在 120 秒内完成。");
                    var main=mainModule.GetField("FrmMain").GetValue(null) as FrameworkElement;
                    if(main==null || !main.IsLoaded) return;
                    if(!navigated) {
                        if(!string.IsNullOrEmpty(installVersion)) context.PclAssembly.GetType("PCL.PageDownloadInstall",true).GetField("VersionWaitingSelect").SetValue(null,installVersion);
                        var stackType=context.PclAssembly.GetType("PCL.FormMain+PageStackData",true);
                        var stack=Activator.CreateInstance(stackType);
                        stackType.GetField("Page").SetValue(stack,Enum.ToObject(stackType.GetField("Page").FieldType,1));
                        var method=main.GetType().GetMethod("PageChange");
                        method.Invoke(main,new[]{stack,Enum.ToObject(method.GetParameters()[1].ParameterType,0)});
                        navigated=true; return;
                    }
                    var page=mainModule.GetField("FrmDownloadInstall").GetValue(null) as FrameworkElement;
                    if(page==null) return;
                    if(pluginCount==0) {
                        var loading=page.FindName("LoadMinecraft") as FrameworkElement;
                        var label=loading?.FindName("LabText") as System.Windows.Controls.TextBlock;
                        if(label==null || !label.Text.Contains("不再提供")) return;
                        File.WriteAllText(Path.Combine(folder,"fixture-result.json"),Files.Json.Serialize(new{success=true,plugins=0,restored=false,restrictionVisible=true,originalExeSha256=Files.Hash(context.PclPath)}));
                        context.Log("FIXTURE_CONTROL_SUCCESS original restriction retained");timer.Stop();Application.Current.Shutdown();return;
                    }
                    var content=page.FindName("PanAllBack") as FrameworkElement;
                    var versions=page.GetType().GetField("MinecraftVersions")?.GetValue(page);
                    int count=versions==null?0:(int)versions.GetType().GetProperty("Count").GetValue(versions);
                    var panel=page.FindName("PanMinecraft") as System.Windows.Controls.Panel;
                    if(count==0 || content?.Visibility!=Visibility.Visible || panel?.Children.Count==0) return;
                    string installedJar=null;
                    if(!string.IsNullOrEmpty(installVersion)) {
                        if(!installStarted) {
                            if(!(bool)page.GetType().GetField("IsInSelectPage").GetValue(page)) return;
                            page.GetType().GetMethod("BtnStart_Click",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(page,null);
                            installStarted=true;context.Log("FIXTURE_NATIVE_INSTALL_STARTED "+installVersion);return;
                        }
                        var game=(string)context.PclAssembly.GetType("PCL.ModMinecraft",true).GetProperty("McFolderSelected").GetValue(null);
                        var versionDirectory=Path.Combine(game,"versions",installVersion);
                        installedJar=Path.Combine(versionDirectory,installVersion+".jar");
                        if(!File.Exists(installedJar)||!File.Exists(Path.Combine(versionDirectory,installVersion+".json"))||File.Exists(Path.Combine(versionDirectory,".pclignore"))) return;
                    }
                    File.WriteAllText(Path.Combine(folder,"fixture-result.json"),Files.Json.Serialize(new {success=true,plugins=pluginCount,restored=true,versionCount=count,contentVisible=true,versionGroups=panel.Children.Count,nativeInstallVersion=installVersion,nativeInstallJar=installedJar,entryAssembly=Assembly.GetEntryAssembly()?.GetName().Name,applicationName=AppDomain.CurrentDomain.SetupInformation.ApplicationName,originalExeSha256=Files.Hash(context.PclPath)}));
                    context.Log("FIXTURE_SUCCESS versions="+count); timer.Stop(); Application.Current.Shutdown();
                } catch(Exception e) {
                    File.WriteAllText(Path.Combine(folder,"fixture-result.json"),Files.Json.Serialize(new{success=false,error=e.ToString()}));
                    timer.Stop(); Application.Current.Shutdown(1);
                }
            };
            timer.Start();
        }
    }
}
