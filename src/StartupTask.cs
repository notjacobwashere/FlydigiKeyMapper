using System.Diagnostics;
using System.Security.Principal;
using System.Xml.Linq;
using Microsoft.Win32;
namespace FlydigiKeyMapper;
static class StartupTask {
 static string UserId=>WindowsIdentity.GetCurrent().User!.Value;
 static string Name=>"FlydigiKeyMapper-"+UserId;
 static int Run(params string[] args){using var p=new Process{StartInfo=new ProcessStartInfo("schtasks.exe"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true}};foreach(var a in args)p.StartInfo.ArgumentList.Add(a);p.Start();var output=p.StandardOutput.ReadToEndAsync();var error=p.StandardError.ReadToEndAsync();p.WaitForExit();Task.WaitAll(output,error);if(p.ExitCode!=0&&args[0]!="/Query")throw new InvalidOperationException(error.Result+output.Result);return p.ExitCode;}
 public static bool Enabled=>Run("/Query","/TN",Name)==0;
 public static bool LegacyEnabled{get{using var run=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");return run?.GetValue("FlydigiKeyMapper")!=null;}}
 public static string Definition(string executable,string sid){XNamespace ns="http://schemas.microsoft.com/windows/2004/02/mit/task";return new XElement(ns+"Task",new XAttribute("version","1.2"),
  new XElement(ns+"Triggers",new XElement(ns+"LogonTrigger",new XElement(ns+"Enabled",true),new XElement(ns+"UserId",sid))),
  new XElement(ns+"Principals",new XElement(ns+"Principal",new XAttribute("id","Author"),new XElement(ns+"UserId",sid),new XElement(ns+"LogonType","InteractiveToken"),new XElement(ns+"RunLevel","HighestAvailable"))),
  new XElement(ns+"Settings",new XElement(ns+"MultipleInstancesPolicy","IgnoreNew"),new XElement(ns+"DisallowStartIfOnBatteries",false),new XElement(ns+"StopIfGoingOnBatteries",false),new XElement(ns+"StartWhenAvailable",true),new XElement(ns+"ExecutionTimeLimit","PT0S")),
  new XElement(ns+"Actions",new XAttribute("Context","Author"),new XElement(ns+"Exec",new XElement(ns+"Command",executable),new XElement(ns+"Arguments","--tray"),new XElement(ns+"WorkingDirectory",Path.GetDirectoryName(executable))))).ToString();}
 public static void Set(bool enabled){if(enabled){string path=Path.GetTempFileName();try{File.WriteAllText(path,Definition(Environment.ProcessPath!,UserId));Run("/Create","/TN",Name,"/XML",path,"/F");}finally{File.Delete(path);}}else if(Enabled)Run("/Delete","/TN",Name,"/F");using var run=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run",true);run?.DeleteValue("FlydigiKeyMapper",false);}
}
