using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace PclShell {
    internal sealed class QuickWindow : Form {
        readonly TextBox target=new TextBox {ReadOnly=true,Dock=DockStyle.Fill};
        readonly Label status=new Label {Dock=DockStyle.Fill};
        readonly Button launch=new Button {Text="启动下载扩展",AutoSize=true};
        readonly Button install=new Button {Text="安装自动加载（实验）",AutoSize=true};
        readonly Button undo=new Button {Text="撤销自动加载",AutoSize=true};
        readonly ShellSettings settings=Files.ReadSettings();
        internal QuickWindow() {
            Text="PCL2 Download Repair · 测试版";Font=new Font("Microsoft YaHei UI",9F);AutoScaleMode=AutoScaleMode.Dpi;
            ClientSize=new Size(530,305);MinimumSize=new Size(545,345);StartPosition=FormStartPosition.CenterScreen;
            var grid=new TableLayoutPanel {Dock=DockStyle.Fill,Padding=new Padding(16),ColumnCount=1,RowCount=6};
            foreach(int h in new[]{40,36,65,42,42,0})grid.RowStyles.Add(new RowStyle(h==0?SizeType.Percent:SizeType.Absolute,h==0?100:h));
            grid.Controls.Add(new Label {Text="PCL 下载助手",Font=new Font(Font.FontFamily,16,FontStyle.Bold),Dock=DockStyle.Fill},0,0);
            var row=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2};row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,100));
            var choose=new Button {Text="选择 PCL…",Dock=DockStyle.Fill};row.Controls.Add(target,0,0);row.Controls.Add(choose,1,0);grid.Controls.Add(row,0,1);grid.Controls.Add(status,0,2);
            var buttons=new FlowLayoutPanel {Dock=DockStyle.Fill};var vanilla=new Button {Text="启动原版",AutoSize=true};var manage=new Button {Text="插件管理",AutoSize=true};
            buttons.Controls.Add(launch);buttons.Controls.Add(vanilla);buttons.Controls.Add(manage);grid.Controls.Add(buttons,0,3);
            var experimental=new FlowLayoutPanel {Dock=DockStyle.Fill};experimental.Controls.Add(install);experimental.Controls.Add(undo);grid.Controls.Add(experimental,0,4);
            grid.Controls.Add(new Label {Text="下载位置沿用 PCL 设置。自动加载尚未完成启动实测。",Dock=DockStyle.Fill,ForeColor=Color.DimGray},0,5);Controls.Add(grid);
            choose.Click+=delegate {using(var dialog=new OpenFileDialog {Filter="PCL 程序|*.exe",Title="选择原 PCL2.exe"})if(dialog.ShowDialog(this)==DialogResult.OK){settings.pclPath=dialog.FileName;Files.WriteSettings(settings);RefreshTarget();}};
            launch.Click+=delegate {TryAction(()=>{Launcher.Start(settings.pclPath,false);Close();});};
            vanilla.Click+=delegate {TryAction(()=>{Launcher.Start(settings.pclPath,true);Close();});};
            manage.Click+=delegate {using(var manager=new ManagerWindow())manager.ShowDialog(this);var saved=Files.ReadSettings();settings.pclPath=saved.pclPath;settings.enabled=saved.enabled;RefreshTarget();};
            install.Click+=delegate {
                if(MessageBox.Show(this,"自动加载尚未完成启动实测。安装后，双击原 PCL 也会加载插件。它会备份并修改启动配置，可用旁边的撤销按钮恢复。\n\n是否安装这个实验功能？","实验功能",MessageBoxButtons.OKCancel,MessageBoxIcon.Information)!=DialogResult.OK)return;
                TryAction(()=>{AutoLoadInstaller.Enable(settings.pclPath,Files.Root);RefreshTarget();MessageBox.Show(this,"安装完成，将在下次启动原 PCL 时生效。","PCL 下载助手");});
            };
            undo.Click+=delegate {TryAction(()=>{AutoLoadInstaller.Disable(settings.pclPath);RefreshTarget();MessageBox.Show(this,"已撤销自动加载。","PCL 下载助手");});};
            Shown+=delegate {settings.pclPath=Launcher.FindTarget(settings.pclPath,Directory.GetParent(Files.Root.TrimEnd('\\','/'))?.FullName);RefreshTarget();};
        }
        void TryAction(Action action){try{action();}catch(Exception e){MessageBox.Show(this,e.Message,"PCL 下载助手",MessageBoxButtons.OK,MessageBoxIcon.Information);}}
        void RefreshTarget(){
            launch.Enabled=install.Enabled=undo.Enabled=false;target.Text=settings.pclPath??"未找到 PCL，请点击右侧选择";
            try {
                if(!File.Exists(settings.pclPath)){status.Text="把助手和 PCL-Shell-Auto 文件夹放在 PCL2.exe 旁边，或手动选择 PCL。";return;}
                Files.WriteSettings(settings);var hash=Files.Hash(settings.pclPath);
                int count=Files.Discover(Path.Combine(Files.Root,"plugins")).Count(p=>p.Compatible(settings.pclPath,hash));
                launch.Enabled=install.Enabled=count>0;undo.Enabled=File.Exists(AutoLoadInstaller.StateFile(settings.pclPath));
                status.Text=count>0?"已匹配 PCL "+FileVersionInfo.GetVersionInfo(settings.pclPath).FileVersion+"。先关闭已打开的 PCL，再点击“启动下载扩展”。":"此 PCL 尚未适配，保留原版启动。请等待插件更新。";
            }catch(Exception e){status.Text=e.Message;}
        }
    }
    internal static class Launcher {
        internal static string FindTarget(string saved,string beside){
            if(!string.IsNullOrEmpty(saved)&&File.Exists(saved))return saved;
            if(!string.IsNullOrEmpty(beside))foreach(var name in new[]{"PCL2.exe","Plain Craft Launcher 2.exe"}){var path=Path.Combine(beside,name);if(File.Exists(path))return path;}
            return null;
        }
        internal static RunOptions Prepare(string target,string root){
            if(!File.Exists(target))throw new FileNotFoundException("请先选择原 PCL 程序。");
            var hash=Files.Hash(target);var settings=Files.ReadSettings();
            var selected=Files.Discover(Path.Combine(root,"plugins")).Where(p=>p.Compatible(target,hash)&&(!settings.enabled.TryGetValue(p.id,out var enabled)||enabled)).ToArray();
            if(selected.Length==0)throw new InvalidOperationException("没有启用且兼容的插件，请到插件管理中检查。");
            return new RunOptions {Target=Path.GetFullPath(target),ExpectedHash=hash,ShellDirectory=root,DiagnosticsDirectory=Path.Combine(root,"logs",DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")),PluginManifestFiles=selected.Select(p=>Path.Combine(p.Directory,"plugin.json")).ToArray(),PclArguments=new string[0]};
        }
        internal static void Start(string target,bool vanilla){
            if(!File.Exists(target))throw new FileNotFoundException("请先选择原 PCL 程序。");
            if(ManagerWindow.OriginalPclIsRunning())throw new InvalidOperationException("PCL 已经打开，请先关闭它再启动。");
            if(vanilla){var start=new ProcessStartInfo(target){WorkingDirectory=Path.GetDirectoryName(target),UseShellExecute=false};start.EnvironmentVariables["PCL_SHELL_NO_PLUGINS"]="1";Process.Start(start);return;}
            var options=Prepare(target,Files.Root);Directory.CreateDirectory(options.DiagnosticsDirectory);
            var file=Path.Combine(options.DiagnosticsDirectory,"launch.json");File.WriteAllText(file,Files.Json.Serialize(options));
            Process.Start(new ProcessStartInfo(typeof(Program).Assembly.Location,"--runtime \""+file+"\""){WorkingDirectory=Files.Root});
        }
    }
}
