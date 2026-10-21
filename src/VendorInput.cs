using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
namespace FlydigiKeyMapper;
sealed class VendorInput:IDisposable {
 [DllImport("VendorBridge.dll",CallingConvention=CallingConvention.Cdecl,CharSet=CharSet.Unicode)] static extern int BridgeOpen(string path);
 [DllImport("VendorBridge.dll",CallingConvention=CallingConvention.Cdecl)] static extern void BridgeClose();
 [DllImport("VendorBridge.dll",CallingConvention=CallingConvention.Cdecl,CharSet=CharSet.Unicode)] static extern int FindController(string control,out ushort address);
 public static readonly int[] Codes={0x10,0x11,0x16,0x17,0x12,0x13};
 public static readonly string[] Names={"C","Z","LM","RM","M1","M2"};
 readonly CancellationTokenSource stop=new();readonly Task? reader;bool opened,disposed;int code=0x64;long received;string info="Connecting to USB background reader…";
 public string Info=>Volatile.Read(ref info);
 void Status(string value)=>Volatile.Write(ref info,value);
 public VendorInput(){reader=Task.Run(ReadLoop);}
 static ProcessStartInfo StartInfo(string file,params string[] args){var s=new ProcessStartInfo(file){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};foreach(string a in args)s.ArgumentList.Add(a);return s;}
 async Task<string> Query(string file,params string[] args){using var p=new Process{StartInfo=StartInfo(file,args)};p.Start();var output=p.StandardOutput.ReadToEndAsync();var error=p.StandardError.ReadToEndAsync();using var timeout=CancellationTokenSource.CreateLinkedTokenSource(stop.Token);timeout.CancelAfter(TimeSpan.FromSeconds(20));try{await p.WaitForExitAsync(timeout.Token);}finally{if(!p.HasExited)p.Kill(true);}string text=await output,err=await error;if(p.ExitCode!=0)throw new IOException(err.Length>0?err:"Device query failed.");return text;}
 async Task<(string control,ushort bus,ushort address)> Target(string cli){
 string interfaces=await Query(cli,"--extcap-interfaces");var targets=new List<(string,ushort,ushort)>();
 foreach(Match m in Regex.Matches(interfaces,@"interface \{value=(\\\\\.\\USBPcap(\d+))\}")){
  string control=m.Groups[1].Value;ushort bus=ushort.Parse(m.Groups[2].Value);
  int hr=FindController(control,out ushort address);
  if(hr==0)targets.Add((control,bus,address));
  else if(hr==unchecked((int)0x800700EA))throw new IOException("More than one Vader 5S is connected. Connect one controller.");
  else if(hr==unchecked((int)0x80070020))throw new IOException("USB capture is in use. Exit other mapper or capture programs.");
  else if(hr==unchecked((int)0x80070005))throw new IOException("Run the mapper as administrator to access USBPcap.");
 }
 if(targets.Count!=1)throw new IOException("Connect one Vader 5S by USB. If USBPcap was just installed, restart Windows first.");return targets[0];
 } async Task ReadLoop(){string cli=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"USBPcap","USBPcapCMD.exe");while(!stop.IsCancellationRequested){
 try{if(!opened){Status("Finding Flydigi Store component…");string component=await VendorRuntime.Locate(stop.Token);int hr=BridgeOpen(component);if(hr<0)throw new IOException($"Flydigi component failed: 0x{hr:X8}");opened=true;}if(!File.Exists(cli))throw new IOException("USBPcap is required for background input.");Status("Locating Flydigi USB capture address…");var target=await Target(cli);
 using var p=new Process{StartInfo=StartInfo(cli,"-d",target.control,"--devices",target.address.ToString(),"-s","65535","-b","1048576","-o","-")};p.Start();var errors=p.StandardError.ReadToEndAsync();Status("USB background reader connected; press an extra button.");
 using var connection=CancellationTokenSource.CreateLinkedTokenSource(stop.Token);connection.CancelAfter(5000);try{await UsbCaptureDecoder.ReadPcap(p.StandardOutput.BaseStream,target.bus,target.address,k=>{Volatile.Write(ref code,k);Interlocked.Exchange(ref received,Environment.TickCount64);connection.CancelAfter(5000);},connection.Token);}finally{if(!p.HasExited)p.Kill(true);await p.WaitForExitAsync();await errors;}
 if(!stop.IsCancellationRequested)throw new IOException("USB capture ended; reconnecting…");
 }catch(OperationCanceledException)when(stop.IsCancellationRequested){break;}catch(Exception e){Status("USB reader: "+e.Message);}
 Interlocked.Exchange(ref received,0);Volatile.Write(ref code,0x64);try{await Task.Delay(2500,stop.Token);}catch(OperationCanceledException){break;}
 }}
 public string[] Poll(){long age=Environment.TickCount64-Interlocked.Read(ref received);int key=Volatile.Read(ref code);if(Interlocked.Read(ref received)>0&&age<500){Status("Vader 5S • USB background input active");return Codes.Contains(key)?[$"V:{key:X2}"]:[];}return [];}
 public void Dispose(){if(disposed)return;disposed=true;stop.Cancel();try{reader?.Wait(3000);}catch(AggregateException){}if(opened){opened=false;BridgeClose();}}
}



