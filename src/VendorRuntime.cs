using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Security.Cryptography;
namespace FlydigiKeyMapper;
static class VendorRuntime {
 public const string StoreUrl="https://apps.microsoft.com/detail/9NB9DQG1BN6P";
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern IntPtr AddDllDirectory(string directory);
 static readonly List<IntPtr> directories=new();static string? located;
 public static async Task<string> Locate(CancellationToken cancel){
  if(located!=null&&File.Exists(located))return located;
  string ps=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),@"WindowsPowerShell\v1.0\powershell.exe");
  var start=new ProcessStartInfo(ps){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
  foreach(var a in new[]{"-NoProfile","-Command","$apps=@(Get-AppxPackage -Name 'Flydigi.*' | Where-Object {$_.Architecture -eq 'X64'}); $p=@(@($apps)+@($apps | ForEach-Object {$_.Dependencies} | Where-Object {$_.Name -like 'Microsoft.VCLibs*' -and $_.Architecture -eq 'X64'}) | Select-Object Name,InstallLocation,Version -Unique); ConvertTo-Json -InputObject $p -Compress"})start.ArgumentList.Add(a);
  using var p=Process.Start(start)??throw new IOException("Could not query installed Store components.");var output=p.StandardOutput.ReadToEndAsync();var error=p.StandardError.ReadToEndAsync();using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancel);timeout.CancelAfter(20000);
  try{await p.WaitForExitAsync(timeout.Token);}finally{if(!p.HasExited)p.Kill(true);}
  string text=await output,errors=await error;if(p.ExitCode!=0||string.IsNullOrWhiteSpace(text))throw new IOException("Install Flydigi Space Station for Xbox from Microsoft Store, then restart the mapper.");
  using var document=JsonDocument.Parse(text);var packages=document.RootElement.EnumerateArray().ToArray();
  var folders=packages.Select(x=>x.GetProperty("InstallLocation").GetString()??"").Where(Directory.Exists).ToArray();
  foreach(var package in packages.Where(x=>(x.GetProperty("Name").GetString()??"").StartsWith("Flydigi.",StringComparison.OrdinalIgnoreCase))){
   string folder=package.GetProperty("InstallLocation").GetString()??"";
   string dll=Path.Combine(folder,"D4XGaming.Devices.dll");if(File.Exists(dll)){
    // WindowsApps may deny loading a DLL outside its package. Import the user's
    // existing component locally; these proprietary files are never distributed.
    string cache=Path.Combine(AppContext.BaseDirectory,"VendorRuntimeCache");Directory.CreateDirectory(cache);
    foreach(string filename in new[]{"D4XGaming.Devices.dll","MSVCP140_APP.dll","VCRUNTIME140_APP.dll","VCRUNTIME140_1_APP.dll"}){
     string? original=filename=="D4XGaming.Devices.dll"?dll:folders.Select(f=>Path.Combine(f,filename)).FirstOrDefault(File.Exists);
     if(original==null)throw new IOException("Flydigi's Store runtime dependency is missing. Repair its Store installation.");
     string destination=Path.Combine(cache,filename);
     using var source=File.OpenRead(original);string sourceHash=Convert.ToHexString(SHA256.HashData(source));source.Position=0;
     bool matches=false;if(File.Exists(destination)){using var existing=File.OpenRead(destination);matches=sourceHash==Convert.ToHexString(SHA256.HashData(existing));}
     if(!matches){using var outputFile=File.Create(destination);source.CopyTo(outputFile);}
    }
    IntPtr cookie=AddDllDirectory(cache);if(cookie!=IntPtr.Zero)directories.Add(cookie);
    located=Path.Combine(cache,"D4XGaming.Devices.dll");return located;
   }
  }
  throw new IOException("Flydigi Store component not found. Install Flydigi Space Station for Xbox, then restart the mapper.");
 }
}

