using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace PclShell {
    internal static class Program {
        [STAThread]
        static int Main(string[] args) {
            try {
                if(args.Length>=2 && args[0]=="--runtime") {
                    var options=Files.Json.Deserialize<RunOptions>(File.ReadAllText(args[1]));
                    return RuntimeHost.Run(options);
                }
                if(args.Length>=2 && args[0]=="--inspect") {
                    string path=Path.GetFullPath(args[1]);
                    var hash=Files.Hash(path);
                    var info=new { target=path,sha256=hash,plugins=Files.Discover(Path.Combine(Files.Root,"plugins")).Select(p=>new{p.id,p.name,compatible=p.Compatible(path,hash),error=p.Error}).ToArray() };
                    File.WriteAllText(args.Length>2?args[2]:Path.Combine(Files.Root,"inspection.json"),Files.Json.Serialize(info)); return 0;
                }
                if(args.Length>=3 && args[0]=="--probe-metadata") { MetadataProbe.Run(Path.GetFullPath(args[1]),args[2]); return 0; }
                if(args.Length>0 && args[0]=="--self-test") { ShellTests.Run(); return 0; }
                Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                if(args.Length>0 && args[0]=="--quick"){Application.Run(new QuickWindow());return 0;}
                Application.Run(new ManagerWindow()); return 0;
            } catch(Exception e) {
                Directory.CreateDirectory(Path.Combine(Files.Root,"logs"));
                File.WriteAllText(Path.Combine(Files.Root,"logs","startup-error.txt"),e.ToString());
                if(args.Length==0 || args[0]=="--quick") MessageBox.Show(e.Message,"PCL2 Download Repair",MessageBoxButtons.OK,MessageBoxIcon.Error);
                return 1;
            }
        }
    }
}
