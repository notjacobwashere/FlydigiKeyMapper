using System.Diagnostics;

namespace FlydigiKeyMapper;

sealed class UpdatesPage : UserControl
{
    readonly Label state = Ui.Text("Check GitHub for a newer version.", 9.5f, tone: Tone.Muted);
    readonly StudioButton check;
    readonly StudioButton download;
    readonly TextBox notes = new()
    {
        Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
        Dock = DockStyle.Fill, BorderStyle = BorderStyle.None,
        Text = "Release notes will appear here after checking for updates."
    };
    readonly CancellationTokenSource stop = new();
    ReleaseInfo? latest;
    bool installing;

    public UpdatesPage()
    {
        Dock = DockStyle.Fill;
        var version = new Surface();
        var contents = Ui.Rows(34, 31, 38, 28);
        contents.Controls.Add(Ui.Text($"Installed version {UpdateService.Current.ToString(3)}", 15, true), 0, 0);
        contents.Controls.Add(Ui.Text("Releases are distributed through GitHub.", 9.5f, tone: Tone.Muted), 0, 1);
        var buttons = Ui.Columns(157, 177, 143, -100);
        check = Ui.Button("Check for updates", async (_, _) => await Check(), ButtonKind.Primary);
        download = Ui.Button("Download and install", async (_, _) => await Install());
        download.Enabled = false;
        var release = Ui.Button("View on GitHub", (_, _) => Open(latest?.Page.AbsoluteUri ?? UpdateService.RepositoryUrl + "/releases"));
        foreach (var button in new[] { check, download, release }) button.Margin = new Padding(0, 2, 8, 2);
        buttons.Controls.Add(check, 0, 0);
        buttons.Controls.Add(download, 1, 0);
        buttons.Controls.Add(release, 2, 0);
        contents.Controls.Add(buttons, 0, 2);
        contents.Controls.Add(state, 0, 3);
        version.Controls.Add(contents);

        var releaseNotes = new Surface();
        var releases = Ui.Rows(35, 0);
        releases.Controls.Add(Ui.Text("Release notes", 14, true), 0, 0);
        releases.Controls.Add(notes, 0, 1);
        releaseNotes.Controls.Add(releases);

        var dependencies = new Surface();
        var requirements = Ui.Rows(28, 29, 38);
        requirements.Controls.Add(Ui.Text("USB requirements", 14, true), 0, 0);
        requirements.Controls.Add(Ui.Text("USBPcap and Flydigi Space Station for Xbox are required. Close Space Station while mapping.", 9, tone: Tone.Muted), 0, 1);
        var links = Ui.Columns(187, -100);
        links.Controls.Add(Ui.Button("Open Microsoft Store", (_, _) => Open(VendorRuntime.StoreUrl)), 0, 0);
        requirements.Controls.Add(links, 0, 2);
        dependencies.Controls.Add(requirements);

        var page = Ui.Rows(163, 13, 192, 13, 127, 0);
        page.Controls.Add(version, 0, 0);
        page.Controls.Add(releaseNotes, 0, 2);
        page.Controls.Add(dependencies, 0, 4);
        Controls.Add(Mapper.ScrollPage(page, 508));
    }

    void Open(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) { state.Text = "Could not open the link. " + ex.Message; }
    }
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
