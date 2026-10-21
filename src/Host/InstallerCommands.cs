using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace PclShell {
    internal static class InstallerCommands {
        internal static int Run(string[] args){
            bool silent=args.Contains("--silent");
            try{
                string target=args.Skip(1).FirstOrDefault(a=>!a.StartsWith("--",StringComparison.Ordinal));
                target=target??Launcher.FindTarget(Files.ReadSettings().pclPath,Directory.GetParent(Files.Root.TrimEnd('\\','/'))?.FullName);
                if(target==null){if(silent)throw new FileNotFoundException("没有找到 PCL2.exe。");using(var picker=new OpenFileDialog {Title="选择原 PCL2.exe",Filter="PCL 程序|*.exe"}){if(picker.ShowDialog()!=DialogResult.OK)return 2;target=picker.FileName;}}
                if(args[0]=="--install"){
                    AutoLoadInstaller.Enable(target,Files.Root);
                    bool compatible=Files.Discover(Path.Combine(Files.Root,"plugins")).Any(p=>p.Compatible(target,Files.Hash(target)));
                    if(!silent)MessageBox.Show("已安装到 PCL\\plugins-loader。以后直接打开原 PCL 即会加载兼容插件。\n"+(compatible?"下载插件已匹配当前版本。":"当前 PCL 尚无匹配的下载插件，暂时保留原版行为。")+"\n更新后若接入失效，可再次运行 install.bat 修复。","PCL 插件加载器");
                }else{
                    if(!silent&&MessageBox.Show("将恢复原启动配置并删除已安装的加载器和插件。下载好的游戏及其他 PCL 设置保留。\n\n确认卸载？","卸载 PCL 插件加载器",MessageBoxButtons.OKCancel,MessageBoxIcon.Question)!=DialogResult.OK)return 2;
                    AutoLoadInstaller.Uninstall(target);
                }
                return 0;
            }catch(Exception e){
                Directory.CreateDirectory(Path.Combine(Files.Root,"logs"));File.WriteAllText(Path.Combine(Files.Root,"logs","setup-error.txt"),e.ToString());
                if(!silent)MessageBox.Show(e.Message,"安装或卸载未完成",MessageBoxButtons.OK,MessageBoxIcon.Information);
                return 1;
            }
        }
    }
}
