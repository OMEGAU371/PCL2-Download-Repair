using System;
using System.Diagnostics;
using System.IO;

internal static class Cleanup {
    // Invoked only after the startup registration has been removed and the setup host exits.
    internal static int Run(string[] args){
        try{
            if(args.Length!=5)throw new InvalidOperationException("Invalid cleanup arguments.");
            int pid=int.Parse(args[1]);string target=Path.GetFullPath(args[2]),folder=Path.GetFullPath(args[3]),nonce=args[4];
            string expected=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(target),"PCL","plugins-loader"));
            if(!string.Equals(folder,expected,StringComparison.OrdinalIgnoreCase)||nonce.Length!=32)throw new InvalidOperationException("Cleanup destination is outside the installed loader.");
            try{using(var process=Process.GetProcessById(pid))if(!process.WaitForExit(30000))throw new TimeoutException("Setup did not exit.");}catch(ArgumentException){}
            string marker=Path.Combine(folder,".uninstall-token");
            if(!File.Exists(marker)||File.ReadAllText(marker)!=target+"\n"+nonce)throw new InvalidOperationException("Cleanup ownership marker mismatch.");
            RejectLinks(folder);
            for(int attempt=0;attempt<20;attempt++){
                try{Directory.Delete(folder,true);File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"result.txt"),"success");return 0;}
                catch(IOException){if(attempt==19)throw;System.Threading.Thread.Sleep(250);}
                catch(UnauthorizedAccessException){if(attempt==19)throw;System.Threading.Thread.Sleep(250);}
            }
            return 1;
        }catch(Exception e){File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"result.txt"),e.ToString());return 1;}
    }
    static void RejectLinks(string directory){
        if((File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0)throw new IOException("Refusing to traverse a directory link.");
        foreach(string path in Directory.GetFileSystemEntries(directory)){
            var attributes=File.GetAttributes(path);
            if((attributes&FileAttributes.ReparsePoint)!=0)throw new IOException("Refusing to traverse a file-system link.");
            if((attributes&FileAttributes.Directory)!=0)RejectLinks(path);
        }
    }
}
