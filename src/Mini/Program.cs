using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

internal static class Program {
    [STAThread]
    static int Main(){
        try{
            var host=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"PCL-Shell-Auto","PclShell.exe");
            if(!File.Exists(host))throw new FileNotFoundException("请先完整解压压缩包，保留 PCL-Shell-Auto 文件夹与本程序放在一起。\n然后把它们放到 PCL2.exe 旁边。");
            Process.Start(new ProcessStartInfo(host,"--quick"){WorkingDirectory=Path.GetDirectoryName(host)});return 0;
        }catch(Exception e){MessageBox.Show(e.Message,"PCL 下载助手",MessageBoxButtons.OK,MessageBoxIcon.Information);return 1;}
    }
}
