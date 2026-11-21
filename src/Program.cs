namespace FlydigiKeyMapper;
static class Program {
 [STAThread] static void Main(string[] args) {
 bool diagnostic=args.Any(a=>a=="--render-ui"||a=="--preview-ui"||a=="--refresh-startup"||a=="--remove-startup"||a=="--self-test"||a=="--diagnose"||a=="--gameinput-diag"||a=="--vendor-diag");using var mutex=new Mutex(!diagnostic,"Local\\FlydigiKeyMapper",out bool first); if(!diagnostic&&!first)return;
 ApplicationConfiguration.Initialize();AppPaths.TestMode=args.Contains("--self-test")||args.Contains("--render-ui")||args.Contains("--preview-ui");if(args.Contains("--refresh-startup")){if(StartupTask.Enabled)StartupTask.Set(true);return;}if(args.Contains("--remove-startup")){StartupTask.Set(false);return;}
 if(args.Contains("--gameinput-diag")){File.WriteAllText(AppPaths.FilePath("gameinput.txt"),Native.Devices());return;}
 if(args.Contains("--diagnose")){File.WriteAllText(AppPaths.FilePath("diagnostics.txt"), Native.Devices());return;}
 if(args.Contains("--render-ui")){using var preview=new Mapper(false,false);preview.RenderPreview();return;}
 if(args.Contains("--preview-ui")){Application.Run(new Mapper(false,false));return;}
 if(args.Contains("--self-test")){string report=Mapper.SelfTest();File.WriteAllText(AppPaths.FilePath("self-test.txt"),report);Console.WriteLine(report);return;}
 if(args.Contains("--vendor-diag")){using var v=new VendorInput();using var context=new ApplicationContext();using var t=new System.Windows.Forms.Timer{Interval=50};bool rawWatch=args.Contains("--raw-watch");using var rawLog=rawWatch?new StreamWriter(AppPaths.FilePath("background-raw-live.log")){AutoFlush=true}:null;long end=Environment.TickCount64+(rawWatch?600000:4000);var lines=new List<string>();t.Tick+=(_,_)=>{var state=v.Poll();lines.Add(Environment.TickCount64+" | "+v.Info+" | "+string.Join(",",state));if(rawLog!=null){rawLog.WriteLine(lines[^1]);lines.Clear();}if(Environment.TickCount64>=end){t.Stop();File.WriteAllLines(AppPaths.FilePath("vendor-diagnostic.txt"),lines);context.ExitThread();}};t.Start();Application.Run(context);return;}
 Application.Run(new Mapper(args.Contains("--tray")));
 }
}
