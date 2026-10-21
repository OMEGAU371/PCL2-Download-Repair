using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace PclShell {
    internal static class PackageIntegrity {
        internal static readonly string[] RuntimeFiles={"PclShell.exe","PclShell.Api.dll","PclShell.Bootstrap.dll","loader-setup.exe","plugins/download-repairer/PclShell.DownloadRestore.dll"};
        internal static Dictionary<string,string> Verify(string folder){
            var hashes=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            foreach(var line in File.ReadAllLines(Path.Combine(folder,"SHA256SUMS.txt"))){
                if(string.IsNullOrWhiteSpace(line))continue;
                if(line.Length<67||line.Substring(64,2)!="  "||!line.Substring(0,64).All(Uri.IsHexDigit))throw new InvalidDataException("安装包校验清单格式错误，请重新下载完整安装包。");
                string relative=line.Substring(66).Replace('\\','/');string path=Files.InFolder(folder,relative);
                if(hashes.ContainsKey(relative))throw new InvalidDataException("安装包校验清单含重复路径。");
                if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0||!string.Equals(Files.Hash(path),line.Substring(0,64),StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("安装包文件校验失败："+relative+"。请从项目发布页重新下载。");
                hashes.Add(relative,line.Substring(0,64));
            }
            foreach(string required in RuntimeFiles.Concat(new[]{"PclShell.exe.config","loader-setup.exe.config","install.bat","uninstall.bat"}))
                if(!hashes.ContainsKey(required))throw new InvalidDataException("安装包校验清单缺少："+required);
            return hashes;
        }
        internal static void ClearVerifiedDownloadMark(string file,string expectedHash){
            if((File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0||!string.Equals(Files.Hash(file),expectedHash,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("运行文件校验失败，保留其下载标记："+Path.GetFileName(file));
            // Scope is a single verified package binary, never an entire directory or a Windows policy.
            if(!DeleteFile(file+":Zone.Identifier")){
                int error=Marshal.GetLastWin32Error();
                if(error!=2&&error!=3&&error!=123)throw new IOException("无法处理已核验文件的下载标记："+Path.GetFileName(file),new Win32Exception(error));
            }
        }
        internal static void PrepareInstalledRuntime(string folder,IDictionary<string,string> hashes){
            foreach(string relative in RuntimeFiles)ClearVerifiedDownloadMark(Files.InFolder(folder,relative),hashes[relative]);
        }
        [DllImport("kernel32.dll",EntryPoint="DeleteFileW",CharSet=CharSet.Unicode,SetLastError=true)]
        [return:MarshalAs(UnmanagedType.Bool)]
        static extern bool DeleteFile(string file);
    }
}
