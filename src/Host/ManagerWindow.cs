using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace PclShell {
    internal sealed class ManagerWindow : Form {
        readonly TextBox target=new TextBox { Dock=DockStyle.Fill,ReadOnly=true };
        readonly Label status=new Label { Dock=DockStyle.Fill,AutoEllipsis=true };
        readonly ListView plugins=new ListView { Dock=DockStyle.Fill,View=View.Details,CheckBoxes=true,FullRowSelect=true,HideSelection=false };
        readonly ShellSettings settings=Files.ReadSettings();
        readonly Button launch=new Button { Text="启用兼容插件启动",AutoSize=true,Height=35 };
        List<PluginManifest> manifests;
        string currentHash;
        bool refreshing;
        internal ManagerWindow() {
            Text="PCL Shell · 第三方插件启动器"; Font=new Font("Microsoft YaHei UI",9F); AutoScaleMode=AutoScaleMode.None;
            ClientSize=new Size(720,425); MinimumSize=new Size(680,430); StartPosition=FormStartPosition.CenterScreen; BackColor=Color.White;
            var grid=new TableLayoutPanel { Dock=DockStyle.Fill,Padding=new Padding(20),RowCount=6,ColumnCount=1 };
            foreach(int size in new[]{38,40,45,0,45,35}) grid.RowStyles.Add(new RowStyle(size==0?SizeType.Percent:SizeType.Absolute,size==0?100:size));
            grid.Controls.Add(new Label {Text="原 PCL + 独立插件",Dock=DockStyle.Fill,Font=new Font(Font.FontFamily,16F,FontStyle.Bold)},0,0);
            var row=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2}; row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,110));
            var browse=new Button {Text="选择原 PCL…",Dock=DockStyle.Fill};row.Controls.Add(target,0,0);row.Controls.Add(browse,1,0);grid.Controls.Add(row,0,1);grid.Controls.Add(status,0,2);
            plugins.Columns.Add("插件",175);plugins.Columns.Add("兼容状态",160);plugins.Columns.Add("说明",290);grid.Controls.Add(plugins,0,3);
            var buttons=new FlowLayoutPanel { Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft,Padding=new Padding(0,5,0,0) };
            var vanilla=new Button {Text="直接启动原版",AutoSize=true,Height=35};var refresh=new Button {Text="重新检查",AutoSize=true,Height=35};
            buttons.Controls.Add(launch);buttons.Controls.Add(vanilla);buttons.Controls.Add(refresh);grid.Controls.Add(buttons,0,4);
            grid.Controls.Add(new Label {Text="更新 PCL 后会重新检查兼容性。未适配时保留原版启动入口。",Dock=DockStyle.Fill,ForeColor=Color.DimGray},0,5);Controls.Add(grid);
            browse.Click+=delegate {using(var picker=new OpenFileDialog {Filter="原 PCL 程序|*.exe",Title="选择原 PCL2.exe"}) if(picker.ShowDialog(this)==DialogResult.OK) { settings.pclPath=picker.FileName; Files.WriteSettings(settings); RefreshCompatibility(); }};
            refresh.Click+=delegate {RefreshCompatibility();};launch.Click+=delegate {Start(false);};vanilla.Click+=delegate {Start(true);};
            plugins.ItemChecked+=delegate(object sender,ItemCheckedEventArgs e) {if(refreshing)return;var manifest=(PluginManifest)e.Item.Tag;if(e.Item.Checked && !manifest.Compatible(settings.pclPath,currentHash)){e.Item.Checked=false;return;}settings.enabled[manifest.id]=e.Item.Checked;Files.WriteSettings(settings);};
            Shown+=delegate {RefreshCompatibility();};
            using(var graphics=CreateGraphics()) {
                float scale=graphics.DpiX/96F;
                if(Math.Abs(scale-1F)>0.01F) {
                    ClientSize=new Size((int)(720*scale),(int)(425*scale));MinimumSize=new Size((int)(680*scale),(int)(430*scale));
                    grid.Padding=new Padding((int)(20*scale));
                    foreach(RowStyle style in grid.RowStyles)if(style.SizeType==SizeType.Absolute)style.Height*=scale;
                    row.ColumnStyles[1].Width*=scale;
                    foreach(ColumnHeader column in plugins.Columns)column.Width=(int)(column.Width*scale);
                    foreach(Button button in buttons.Controls)button.Height=(int)(35*scale);
                }
            }
        }
        void RefreshCompatibility() {
            refreshing=true;
            try {
                target.Text=settings.pclPath??"请先选择原 PCL 程序";plugins.Items.Clear();launch.Enabled=false;
                manifests=Files.Discover(Path.Combine(Files.Root,"plugins"));
                if(string.IsNullOrWhiteSpace(settings.pclPath)||!File.Exists(settings.pclPath)){status.Text="原程序尚未选择，或路径已改变。";return;}
                if(string.Equals(Path.GetFullPath(settings.pclPath),typeof(Program).Assembly.Location,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("请选择原 PCL2.exe。");
                currentHash=Files.Hash(settings.pclPath);
                var version=FileVersionInfo.GetVersionInfo(settings.pclPath).FileVersion;
                int compatible=0;
                foreach(var manifest in manifests){bool supported=manifest.Compatible(settings.pclPath,currentHash);if(supported)compatible++;var item=new ListViewItem(manifest.name??manifest.id){Tag=manifest,Checked=supported&&(!settings.enabled.TryGetValue(manifest.id,out var enabled)||enabled)};item.SubItems.Add(manifest.Error??(supported?"匹配，启动时再检查接口":"尚未适配此 PCL"));item.SubItems.Add(manifest.description??"");plugins.Items.Add(item);}
                status.Text="PCL "+version+" · "+compatible+" 个匹配插件 · 文件指纹 "+currentHash.Substring(0,12);
                launch.Enabled=compatible>0;
            }catch(Exception e){status.Text=e.Message;}finally{refreshing=false;}
        }
        void Start(bool vanilla) {
            try {
                if(!File.Exists(settings.pclPath))throw new FileNotFoundException("请先选择原 PCL 程序。");
                if(!vanilla && OriginalPclIsRunning())throw new InvalidOperationException("原 PCL 已在运行。请先关闭它，再从此外层启动器启动，插件才能参与初始化。");
                string directory=Path.GetDirectoryName(settings.pclPath);
                if(vanilla){var start=new ProcessStartInfo(settings.pclPath){WorkingDirectory=directory,UseShellExecute=false};start.EnvironmentVariables["PCL_SHELL_NO_PLUGINS"]="1";Process.Start(start);return;}
                RefreshCompatibility();
                var selected=plugins.CheckedItems.Cast<ListViewItem>().Select(i=>(PluginManifest)i.Tag).Where(p=>p.Compatible(settings.pclPath,currentHash)).ToArray();
                if(selected.Length==0)throw new InvalidOperationException("没有已启用且兼容的插件，可使用“直接启动原版”。");
                var logs=Path.Combine(Files.Root,"logs",DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));Directory.CreateDirectory(logs);
                var options=new RunOptions {Target=Path.GetFullPath(settings.pclPath),ExpectedHash=currentHash,ShellDirectory=Files.Root,DiagnosticsDirectory=logs,PluginManifestFiles=selected.Select(p=>Path.Combine(p.Directory,"plugin.json")).ToArray(),PclArguments=new string[0]};
                var optionFile=Path.Combine(logs,"launch.json");File.WriteAllText(optionFile,Files.Json.Serialize(options));
                Process.Start(new ProcessStartInfo(typeof(Program).Assembly.Location,"--runtime \""+optionFile+"\""){WorkingDirectory=Files.Root});
                status.Text="已启动。若原 PCL 已在运行，请关闭它后再通过此外层启动。";
            }catch(Exception e){MessageBox.Show(this,e.Message,"无法启动",MessageBoxButtons.OK,MessageBoxIcon.Information);}
        }
        internal static bool OriginalPclIsRunning() {
            try {
                if(!System.Threading.Mutex.TryOpenExisting("PCL_SingletonMutex",out var mutex))return false;
                using(mutex){bool acquired;try{acquired=mutex.WaitOne(0);}catch(System.Threading.AbandonedMutexException){acquired=true;}if(acquired)mutex.ReleaseMutex();return !acquired;}
            }catch(UnauthorizedAccessException){return true;}
        }
    }
}
