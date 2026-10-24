using System.Diagnostics;
namespace FlydigiKeyMapper;
sealed class UpdatesPage:UserControl {
 readonly Label state=new(){AutoSize=false,Height=64,Dock=DockStyle.Top,Text="Check GitHub Releases for a newer version."};
 readonly Button check=new(){Text="Check for updates",AutoSize=true};
 readonly Button download=new(){Text="Download and install",AutoSize=true,Enabled=false};
 readonly Button release=new(){Text="View on GitHub",AutoSize=true};
 readonly TextBox notes=new(){Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,Dock=DockStyle.Fill,BackColor=Color.FromArgb(30,32,42),ForeColor=Color.White};
 readonly CancellationTokenSource stop=new();ReleaseInfo? latest;bool installing;
 public UpdatesPage(){Dock=DockStyle.Fill;Padding=new Padding(22);BackColor=Color.FromArgb(23,25,33);ForeColor=Color.White;
  var buttons=new FlowLayoutPanel{Dock=DockStyle.Top,Height=45};foreach(var button in new[]{check,download,release}){button.ForeColor=Color.White;button.BackColor=Color.FromArgb(55,60,75);button.FlatStyle=FlatStyle.Flat;button.FlatAppearance.BorderColor=Color.FromArgb(85,90,105);}buttons.Controls.AddRange(new Control[]{check,download,release});
  var description=new Label{Text=$"Installed: {UpdateService.Current.ToString(3)}\r\n{UpdateService.Repository}",Dock=DockStyle.Top,Height=55};
  var dependencies=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=85,FlowDirection=FlowDirection.TopDown};dependencies.Controls.Add(new Label{Text="Required: Flydigi Space Station for Xbox + USBPcap. Close the Store app while mapping.",AutoSize=true});
  var store=new LinkLabel{Text="Get Flydigi's official Store app",AutoSize=true,LinkColor=Color.LightSkyBlue};store.LinkClicked+=(_,_)=>Open(VendorRuntime.StoreUrl);dependencies.Controls.Add(store);
  Controls.Add(notes);Controls.Add(buttons);Controls.Add(state);Controls.Add(description);Controls.Add(dependencies);
  check.Click+=async(_,_)=>await Check();download.Click+=async(_,_)=>await Install();release.Click+=(_,_)=>Open(latest?.Page.AbsoluteUri??UpdateService.RepositoryUrl+"/releases");
 }
 static void Open(string url)=>Process.Start(new ProcessStartInfo(url){UseShellExecute=true});
 async Task Install(){
  if(latest==null||installing)return;
  installing=true;check.Enabled=false;download.Enabled=false;
  try{
   state.Text="Downloading update…";
   var progress=new Progress<int>(p=>{if(!IsDisposed)state.Text=$"Downloading update… {p}%";});
   string directory=Path.Combine(AppPaths.DataDirectory,"Updates",Guid.NewGuid().ToString("N"));
   string installer=await UpdateService.Download(latest,directory,progress,stop.Token);
   if(IsDisposed)return;
   using var installerLock=File.OpenRead(installer);
   await UpdateService.VerifyInstaller(installer,latest,stop.Token);
   state.Text="Verified. Starting setup…";
   using var process=Process.Start(new ProcessStartInfo(installer){UseShellExecute=true,Verb="runas",Arguments=$"/S /UPDATEPID={Environment.ProcessId}"});
   if(process==null)throw new IOException("Windows could not start setup.");
   Application.Exit();
  }catch(OperationCanceledException){if(!IsDisposed)state.Text="Update cancelled.";}
  catch(Exception e){if(!IsDisposed)state.Text="Could not install update: "+e.Message;}
  finally{installing=false;if(!IsDisposed){check.Enabled=true;download.Enabled=latest?.Version>UpdateService.Current&&latest?.Installer!=null;}}
 }
 async Task Check(){check.Enabled=false;download.Enabled=false;state.Text="Checking GitHub…";try{latest=await UpdateService.Check(stop.Token);if(IsDisposed)return;if(latest==null){state.Text="No public release is available yet.";notes.Text="";}else{state.Text=latest.Version>UpdateService.Current?$"Version {latest.Tag} is available.":"You have the latest version.";notes.Text=latest.Notes;download.Enabled=latest.Version>UpdateService.Current&&latest.Installer!=null;}}catch(OperationCanceledException){}catch(Exception e){if(!IsDisposed)state.Text="Could not check for updates: "+e.Message;}finally{if(!IsDisposed)check.Enabled=true;}}
 protected override void Dispose(bool disposing){if(disposing)stop.Cancel();base.Dispose(disposing);}
}



