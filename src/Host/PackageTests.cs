using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace PclShell {
    internal static class PackageTests {
        internal static int Run(string folder){
            Directory.CreateDirectory(folder);var rows=new List<string>();
            foreach(string relative in PackageIntegrity.RuntimeFiles.Concat(new[]{"PclShell.exe.config","loader-setup.exe.config","install.bat","uninstall.bat"})){
                string path=Files.InFolder(folder,relative);Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,"test:"+relative);rows.Add(Files.Hash(path)+"  "+relative);
            }
            string manifest=Path.Combine(folder,"SHA256SUMS.txt");File.WriteAllLines(manifest,rows);
            PackageIntegrity.Verify(folder);int count=1;
            string binary=Path.Combine(folder,"PclShell.exe");File.AppendAllText(binary,"changed");
            ExpectRejected(()=>PackageIntegrity.Verify(folder));count++;
            File.WriteAllText(binary,"test:PclShell.exe");
            File.WriteAllLines(manifest,rows.Concat(new[]{new string('0',64)+"  ../outside.dll"}));
            ExpectRejected(()=>PackageIntegrity.Verify(folder));count++;
            File.WriteAllLines(manifest,rows.Concat(new[]{rows[0]}));ExpectRejected(()=>PackageIntegrity.Verify(folder));count++;
            File.WriteAllLines(manifest,rows);
            string marker=binary+":Zone.Identifier";
            Mark(binary);
            ExpectRejected(()=>PackageIntegrity.ClearVerifiedDownloadMark(binary,new string('0',64)));
            if(!MarkerExists(marker))throw new Exception("A mismatched file had its download marker cleared");count++;
            string hash=Files.Hash(binary);PackageIntegrity.ClearVerifiedDownloadMark(binary,hash);
            if(MarkerExists(marker)||Files.Hash(binary)!=hash)throw new Exception("Verified download-mark handling changed file bytes or left the marker");count++;
            var hashes=PackageIntegrity.Verify(folder);var installed=Path.Combine(folder,"installed");
            foreach(string relative in PackageIntegrity.RuntimeFiles){
                string source=Files.InFolder(folder,relative),destination=Files.InFolder(installed,relative);Mark(source);Directory.CreateDirectory(Path.GetDirectoryName(destination));File.Copy(source,destination);
                if(!MarkerExists(destination+":Zone.Identifier"))throw new Exception("Test copy did not retain the Internet marker");
            }
            string unrelated=Path.Combine(installed,"third-party.dll");File.WriteAllText(unrelated,"unlisted");Mark(unrelated);
            PackageIntegrity.PrepareInstalledRuntime(installed,hashes);
            foreach(string relative in PackageIntegrity.RuntimeFiles){var file=Files.InFolder(installed,relative);if(MarkerExists(file+":Zone.Identifier")||Files.Hash(file)!=hashes[relative])throw new Exception("Installed runtime was not prepared correctly");}count++;
            if(!MarkerExists(unrelated+":Zone.Identifier"))throw new Exception("An unlisted third-party file had its marker removed");count++;
            return count;
        }
        static void Mark(string file){
            using(var handle=CreateFile(file+":Zone.Identifier",0x40000000,7,IntPtr.Zero,2,0x80,IntPtr.Zero)){
                if(handle.IsInvalid)throw new IOException("Cannot create the test download marker.");
                using(var stream=new FileStream(handle,FileAccess.Write)){var bytes=Encoding.ASCII.GetBytes("[ZoneTransfer]\r\nZoneId=3\r\n");stream.Write(bytes,0,bytes.Length);}
            }
        }
        static void ExpectRejected(Action action){bool rejected=false;try{action();}catch(InvalidDataException){rejected=true;}if(!rejected)throw new Exception("Invalid package was accepted");}
        static bool MarkerExists(string path){using(var handle=CreateFile(path,0x80000000,7,IntPtr.Zero,3,0x80,IntPtr.Zero))return !handle.IsInvalid;}
        [DllImport("kernel32.dll",EntryPoint="CreateFileW",CharSet=CharSet.Unicode,SetLastError=true)]
        static extern SafeFileHandle CreateFile(string path,uint access,uint share,IntPtr security,uint creation,uint flags,IntPtr template);
    }
}
