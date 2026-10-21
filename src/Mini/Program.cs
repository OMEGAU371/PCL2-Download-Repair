using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;

internal static class Program {
    [STAThread]
    static int Main(string[] args){
        try{
            if(args.Length>0&&args[0]=="--cleanup")return Cleanup.Run(args);
            var host=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"PclShell.exe");
            if(!File.Exists(host))throw new FileNotFoundException("请完整解压 plugins-loader 文件夹，并把它放到 PCL2.exe 旁边。");
            bool setup=args.Length>0&&(args[0]=="--install"||args[0]=="--uninstall");
            string arguments=setup?string.Join(" ",args.Select(Quote)):"--quick";
            using(var process=Process.Start(new ProcessStartInfo(host,arguments){WorkingDirectory=Path.GetDirectoryName(host),UseShellExecute=false})){
                if(setup){process.WaitForExit();return process.ExitCode;}
            }
            return 0;
        }catch(Exception e){MessageBox.Show(e.Message,"PCL 下载助手",MessageBoxButtons.OK,MessageBoxIcon.Information);return 1;}
    }
    static string Quote(string value){return "\""+value.Replace("\\\"","\\\\\"").TrimEnd('\\')+"\"";}
}
