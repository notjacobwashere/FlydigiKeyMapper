using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace FlydigiKeyMapper;
public record KeyChoice(int Code,string Label){public override string ToString()=>Label;}
public class Mapping { public string Name {get;set;}=""; public string Source {get;set;}=""; public int Key {get;set;} }
public class Settings { public bool GameOnly {get;set;}=false; public List<Mapping> Maps {get;set;}=new(); }
static class Program {
 [STAThread] static void Main(string[] args) {
 bool diagnostic=args.Any(a=>a=="--render-ui"||a=="--refresh-startup"||a=="--remove-startup"||a=="--self-test"||a=="--diagnose"||a=="--gameinput-diag"||a=="--vendor-diag");using var mutex=new Mutex(!diagnostic,"Local\\FlydigiKeyMapper",out bool first); if(!diagnostic&&!first)return;
 ApplicationConfiguration.Initialize();AppPaths.TestMode=args.Contains("--self-test");if(args.Contains("--refresh-startup")){if(StartupTask.Enabled)StartupTask.Set(true);return;}if(args.Contains("--remove-startup")){StartupTask.Set(false);return;}
 if(args.Contains("--gameinput-diag")){File.WriteAllText(AppPaths.FilePath("gameinput.txt"),Native.Devices());return;}
 if(args.Contains("--diagnose")){File.WriteAllText(AppPaths.FilePath("diagnostics.txt"), Native.Devices());return;}
 if(args.Contains("--render-ui")){AppPaths.TestMode=true;using var preview=new Mapper(false,false);preview.RenderPreview();return;}
 if(args.Contains("--self-test")){string report=Mapper.SelfTest();File.WriteAllText(AppPaths.FilePath("self-test.txt"),report);Console.WriteLine(report);return;}
 if(args.Contains("--vendor-diag")){using var v=new VendorInput();using var context=new ApplicationContext();using var t=new System.Windows.Forms.Timer{Interval=50};bool rawWatch=args.Contains("--raw-watch");using var rawLog=rawWatch?new StreamWriter(AppPaths.FilePath("background-raw-live.log")){AutoFlush=true}:null;long end=Environment.TickCount64+(rawWatch?600000:4000);var lines=new List<string>();t.Tick+=(_,_)=>{var state=v.Poll();lines.Add(Environment.TickCount64+" | "+v.Info+" | "+string.Join(",",state));if(rawLog!=null){rawLog.WriteLine(lines[^1]);lines.Clear();}if(Environment.TickCount64>=end){t.Stop();File.WriteAllLines(AppPaths.FilePath("vendor-diagnostic.txt"),lines);context.ExitThread();}};t.Start();Application.Run(context);return;}
 Application.Run(new Mapper(args.Contains("--tray")));
 }
}
class Mapper:Form,IMessageFilter {
 readonly ComboBox[] keyBoxes=new ComboBox[6]; readonly Button[] listenButtons=new Button[6]; int listening=-1, capturedVk=-1; long listenDeadline;
 readonly VendorInput? vendor;
 readonly string config=AppPaths.Configuration;
 Settings settings=new(); readonly Label status=new(), live=new(); readonly CheckBox enabled=new(),startup=new(),gameOnly=new();
 readonly System.Windows.Forms.Timer timer=new(){Interval=10}; readonly NotifyIcon tray=new();
 readonly HashSet<string> active=new(); readonly Dictionary<string,long> seen=new(); readonly Dictionary<int,int> held=new(); readonly Dictionary<int,int> rowKeys=new();
 readonly Label[] sources=new Label[6]; readonly Label[] leds=new Label[6]; int learning=-1; long nextDeviceCheck; bool loading=true; readonly bool startHidden;
 public Mapper(bool hidden,bool hardware=true){vendor=hardware?new VendorInput():null;startHidden=hidden; Text="Flydigi Key Mapper"; ClientSize=new(790,590); MinimumSize=new(806,629); BackColor=Color.FromArgb(23,25,33); ForeColor=Color.White; Font=new("Segoe UI",10); AutoScaleMode=AutoScaleMode.Dpi;
 try{if(File.Exists(config))settings=JsonSerializer.Deserialize<Settings>(File.ReadAllText(config))??new();}catch(Exception ex){MessageBox.Show("Could not read settings: "+ex.Message);}
 string[] names={"C","Z","LM","RM","M1","M2"}; if(settings.Maps.Count!=6)settings.Maps=names.Select(n=>new Mapping{Name=n}).ToList();
 Add(new Label{Text="FLYDIGI / KEY MAPPER",Font=new("Segoe UI",19,FontStyle.Bold),AutoSize=true},24,18);
 Add(new Label{Text="Choose a keyboard or numpad key for each extra button, then bind it in GTA.",AutoSize=true},25,61);
 Add(new Label{Text="Button             Independent input                           Keyboard key",AutoSize=true,ForeColor=Color.Silver},26,101);
 for(int i=0;i<6;i++){int row=i; int y=134+i*48; Add(leds[i]=new Label{Text=names[i],Width=65,Height=30,Font=new("Segoe UI",12,FontStyle.Bold)},27,y+3);
 settings.Maps[i].Source=$"V:{VendorInput.Codes[i]:X2}"; Add(sources[i]=new Label{Text=Short(settings.Maps[i].Source),Width=230,Height=33,AutoEllipsis=true,ForeColor=Color.Silver},100,y+3);
 var listen=new Button{Text="Listen",Width=75,Height=32};listenButtons[i]=listen;listen.Click+=(_,_)=>{if(listening==row)CancelListening();else StartListening(row);};Add(listen,335,y);
 var combo=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=150,Height=30};var keys=new List<Keys>{Keys.None};keys.AddRange(Enumerable.Range(65,26).Select(x=>(Keys)x));keys.AddRange(Enumerable.Range(48,10).Select(x=>(Keys)x));keys.AddRange(Enumerable.Range(112,24).Select(x=>(Keys)x));keys.AddRange(Enumerable.Range(96,10).Select(x=>(Keys)x));keys.AddRange(new[]{Keys.Add,Keys.Subtract,Keys.Multiply,Keys.Divide,Keys.Decimal});keys.AddRange(new[]{Keys.Space,Keys.Tab,Keys.Enter,Keys.Escape,Keys.Back,Keys.Insert,Keys.Delete,Keys.Home,Keys.End,Keys.PageUp,Keys.PageDown,Keys.Up,Keys.Down,Keys.Left,Keys.Right,Keys.LShiftKey,Keys.LControlKey,Keys.LMenu});var choices=keys.Select(k=>new KeyChoice((int)k,k==Keys.None?"Unassigned":k.ToString())).ToList();choices.Add(new KeyChoice(0x1000,"Numpad Enter"));if(!choices.Any(k=>k.Code==settings.Maps[i].Key))choices.Add(new KeyChoice(settings.Maps[i].Key,((Keys)settings.Maps[i].Key).ToString()));keyBoxes[i]=combo;combo.Items.AddRange(choices.Cast<object>().ToArray());combo.SelectedIndex=Math.Max(0,choices.FindIndex(k=>k.Code==settings.Maps[i].Key));combo.SelectedIndexChanged+=(_,_)=>{ReleaseAll();settings.Maps[row].Key=((KeyChoice)combo.SelectedItem!).Code;Save();};Add(combo,426,y);
 var clear=new Button{Text="Clear",Width=70,Height=32};clear.Click+=(_,_)=>{ReleaseAll();combo.SelectedIndex=0;Save();};Add(clear,592,y);
 }
 enabled.Text="Enable mappings";enabled.AutoSize=true;enabled.Checked=true;enabled.CheckedChanged+=(_,_)=>ReleaseAll();Add(enabled,26,435);
 gameOnly.Text="Only while GTA / FiveM is focused";gameOnly.AutoSize=true;gameOnly.Checked=settings.GameOnly;gameOnly.CheckedChanged+=(_,_)=>{ReleaseAll();settings.GameOnly=gameOnly.Checked;Save();};Add(gameOnly,220,435);
 startup.Text="Start with Windows (in tray)";startup.AutoSize=true;try{bool legacy=StartupTask.LegacyEnabled;startup.Checked=StartupTask.Enabled||legacy;if(hardware&&legacy)StartupTask.Set(true);}catch(Exception ex){status.Text="Startup setting: "+ex.Message;}
 startup.CheckedChanged+=(_,_)=>{if(loading)return;try{StartupTask.Set(startup.Checked);}catch(Exception ex){loading=true;startup.Checked=!startup.Checked;loading=false;MessageBox.Show("Startup setting failed: "+ex.Message);}};Add(startup,26,472);

 var diag=new Button{Text="Device report",Width=120,Height=31};diag.Click+=(_,_)=>{var p=AppPaths.FilePath("diagnostics.txt");File.WriteAllText(p,Native.Devices()+"\r\n"+vendor?.Info+"\r\nObserved inputs:\r\n"+string.Join("\r\n",seen.Keys));Process.Start(new ProcessStartInfo(p){UseShellExecute=true});};Add(diag,590,469);
 live.Text="Press a controller button to test detection.";live.SetBounds(26,514,730,24);Controls.Add(live);status.Text="Each button lights green when detected. Choose Unassigned to disable a key.";status.SetBounds(26,545,730,37);status.ForeColor=Color.LightSkyBlue;Controls.Add(status);
 tray.Icon=SystemIcons.Application;tray.Text="Flydigi Key Mapper";tray.Visible=true;var menu=new ContextMenuStrip();menu.Items.Add("Open",null,(_,_)=>{Show();WindowState=FormWindowState.Normal;Activate();});menu.Items.Add("Exit",null,(_,_)=>{tray.Visible=false;Application.Exit();});tray.ContextMenuStrip=menu;tray.DoubleClick+=(_,_)=>{Show();WindowState=FormWindowState.Normal;Activate();};
 FormClosing+=(_,e)=>{ReleaseAll();if(e.CloseReason==CloseReason.UserClosing){e.Cancel=true;Hide();tray.ShowBalloonTip(2000,"Flydigi Key Mapper","Running in the tray. Right-click its icon to exit.",ToolTipIcon.Info);}};
 FormClosed+=(_,_)=>{Application.RemoveMessageFilter(this);ReleaseAll();vendor?.Dispose();tray.Dispose();};Application.ApplicationExit+=(_,_)=>{ReleaseAll();vendor?.Dispose();};
 var mappings=new TabPage("Mappings"){BackColor=BackColor,ForeColor=Color.White,AutoScroll=true};var existing=Controls.Cast<Control>().ToArray();foreach(var control in existing)mappings.Controls.Add(control);
 var updates=new TabPage("Updates"){BackColor=BackColor};updates.Controls.Add(new UpdatesPage());var tabs=new TabControl{Dock=DockStyle.Fill,ForeColor=Color.Black};tabs.TabPages.Add(mappings);tabs.TabPages.Add(updates);Controls.Add(tabs);ClientSize=new(790,625);MinimumSize=new(806,664); Application.AddMessageFilter(this);Deactivate+=(_,_)=>CancelListening();
 Shown+=(_,_)=>{if(startHidden)Hide();};Native.Register(Handle);timer.Tick+=(_,_)=>TickInputs();timer.Start();loading=false;if(hardware)Save();
 }
 public void RenderPreview(){timer.Stop();enabled.Checked=false;Show();Application.DoEvents();PerformLayout();using var bitmap=new Bitmap(Width,Height);DrawToBitmap(bitmap,new Rectangle(0,0,Width,Height));bitmap.Save(Path.Combine(AppContext.BaseDirectory,"mappings-preview.png"));var tabs=Controls.OfType<TabControl>().Single();tabs.SelectedIndex=1;Application.DoEvents();PerformLayout();DrawToBitmap(bitmap,new Rectangle(0,0,Width,Height));bitmap.Save(Path.Combine(AppContext.BaseDirectory,"updates-preview.png"));} public static string SelfTest(){
 var results=new List<string>();void Check(bool ok,string name){if(!ok)throw new Exception("FAIL: "+name);results.Add("PASS: "+name);}
 using var f=new Mapper(false,false);f.loading=true;f.timer.Stop();f.enabled.Checked=false;
 Check(Native.AbiValid(),"Windows input structure sizes");Check(Native.ScanCode(0x1000)==0xE01C&&Native.ScanCode((int)Keys.Enter)==0x1C,"Numpad Enter remains distinct from Enter");Check(Native.ScanCode((int)Keys.NumPad0)!=Native.ScanCode((int)Keys.Insert)&&Native.ScanCode((int)Keys.NumPad0)!=0,"Numpad zero remains distinct from Insert");
 Check(f.settings.Maps.Count==6,"Six mapping slots");Check(StartupTask.Definition(@"C:\Test & Space\mapper.exe","S-1-5-21-123").Contains("HighestAvailable")&&System.Xml.Linq.XDocument.Parse(StartupTask.Definition(@"C:\Test & Space\mapper.exe","S-1-5-21-123")).Descendants().Any(x=>x.Name.LocalName=="Command"&&x.Value==@"C:\Test & Space\mapper.exe"),"Startup task preserves executable path and requests required elevation");
 Check(CapturedKey(13,(0x1cL<<16)|(1L<<24))==0x1000&&CapturedKey(13,0x1cL<<16)==13,"Listening distinguishes keypad Enter");
 Check(CapturedKey((int)Keys.Insert,0x52L<<16)==(int)Keys.NumPad0&&CapturedKey((int)Keys.Insert,(0x52L<<16)|(1L<<24))==(int)Keys.Insert,"Listening distinguishes numpad with Num Lock off");
 Check(CapturedKey((int)Keys.ControlKey,(0x1dL<<16)|(1L<<24))==(int)Keys.RControlKey,"Listening preserves right Control");
 f.StartListening(0);var captured=Message.Create(f.Handle,0x100,(IntPtr)(int)Keys.F9,(IntPtr)(0x43L<<16));Check(f.PreFilterMessage(ref captured)&&f.settings.Maps[0].Key==(int)Keys.F9&&f.listening<0,"Listening saves the next key and stops");var released=Message.Create(f.Handle,0x101,(IntPtr)(int)Keys.F9,IntPtr.Zero);Check(f.PreFilterMessage(ref released)&&f.capturedVk<0,"Captured release is consumed");

 foreach(var m in f.settings.Maps)m.Source="";
 f.learning=0;f.UpdateSource("X:0:",new[]{"X:0:12"});Check(f.settings.Maps[0].Source=="X:0:12"&&f.learning==-1,"Learn rising button press");
 f.learning=1;f.UpdateSource("X:0:",new[]{"X:0:12"});Check(f.learning==1,"Held input does not retrigger learning");
 f.UpdateSource("X:0:",Array.Empty<string>());Check(f.active.Count==0,"Release clears active button");
 f.UpdateSource("X:0:",new[]{"X:0:12"});Check(f.settings.Maps[1].Source==""&&f.learning==-1,"Reject duplicate source");
 f.UpdateSource("X:0:",Array.Empty<string>());f.learning=1;f.UpdateSource("X:0:",new[]{"X:0:13"});Check(f.settings.Maps[1].Source=="X:0:13","Second independent input");
 var copy=JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(f.settings));Check(copy?.Maps[1].Source=="X:0:13","Settings round trip");
 f.tray.Visible=false;f.tray.Dispose();return string.Join("\r\n",results);
 }
 void Add(Control c,int x,int y){c.Location=new(x,y);Controls.Add(c);}
 string Short(string s)=>s==""?"Not learned":s.StartsWith("V:")?"Vader 5S extra "+VendorInput.Names[Array.IndexOf(VendorInput.Codes,Convert.ToInt32(s[2..],16))]:s.StartsWith("G:")?"GameInput button "+s.Split(':').Last():s.StartsWith("X:")?"Xbox "+s[2..]:"HID button "+s.Split('|').Last();
 void Save(){if(loading)return;try{File.WriteAllText(config,JsonSerializer.Serialize(settings,new JsonSerializerOptions{WriteIndented=true}));}catch(Exception ex){status.Text="Save failed: "+ex.Message;}}
 void UpdateSource(string prefix,IEnumerable<string> inputs){var next=inputs.ToHashSet();var old=active.Where(x=>x.StartsWith(prefix,StringComparison.Ordinal)).ToHashSet();active.RemoveWhere(x=>x.StartsWith(prefix,StringComparison.Ordinal));active.UnionWith(next);foreach(var s in next){seen[s]=Environment.TickCount64;if(!old.Contains(s)){try{File.AppendAllText(AppPaths.FilePath("button-events.txt"),DateTime.Now.ToString("O")+" "+s+Environment.NewLine);}catch{}live.Text="Detected: "+Short(s);if(learning>=0){int row=learning;learning=-1; if(settings.Maps.Where((_,i)=>i!=row).Any(m=>m.Source==s)){status.Text="That input is already assigned. These controls may duplicate one another.";continue;}settings.Maps[row].Source=s;sources[row].Text=Short(s);Save();status.Text="Learned "+settings.Maps[row].Name+". Check that the original Xbox buttons do not trigger it too.";}}}}
 void TickInputs(){if(listening>=0&&Environment.TickCount64>listenDeadline)CancelListening();UpdateSource("V:",vendor?.Poll()??[]);if(vendor!=null)live.Text=vendor.Info;for(uint i=0;i<4;i++){var list=new List<string>();if(Native.XInputGetState(i,out var s)==0){for(int b=0;b<16;b++)if((s.pad.buttons&(1<<b))!=0)list.Add($"X:{i}:{b}");if(s.pad.lt>160)list.Add($"X:{i}:LT");if(s.pad.rt>160)list.Add($"X:{i}:RT");}UpdateSource($"X:{i}:",list);}
 bool allow=enabled.Checked&&learning<0&&listening<0&&capturedVk<0&&(!settings.GameOnly||Native.GameFocused());var desired=new Dictionary<int,int>();
 for(int i=0;i<6;i++){var m=settings.Maps[i];bool down=active.Contains(m.Source);leds[i].ForeColor=down?Color.FromArgb(110,240,185):Color.White;leds[i].BackColor=down?Color.FromArgb(30,85,65):BackColor;if(allow&&down&&m.Key!=0)desired[m.Key]=desired.GetValueOrDefault(m.Key)+1;}
 foreach(int key in held.Keys.Except(desired.Keys).ToArray())if(Native.SendKey(key,false))held.Remove(key);
 foreach(int key in desired.Keys.Except(held.Keys).ToArray())if(Native.SendKey(key,true))held[key]=1;else status.Text="Windows blocked keyboard output. Check GTA and mapper privilege levels.";
 // Disconnected raw HID devices must not leave a key held.
 if(Environment.TickCount64>=nextDeviceCheck){nextDeviceCheck=Environment.TickCount64+1000;foreach(var s in active.Where(s=>s.StartsWith("H:")).ToArray())if(!Native.DeviceExists(s.Split('|')[0][2..]))active.Remove(s);}
 }
 void StartListening(int row){CancelListening();ReleaseAll();listening=row;listenDeadline=Environment.TickCount64+15000;listenButtons[row].Text="Cancel";status.Text="Listening for "+settings.Maps[row].Name+": press a keyboard key (including numpad). Click Cancel to stop.";Activate();listenButtons[row].Focus();}
 void CancelListening(){if(listening>=0){listenButtons[listening].Text="Listen";listening=-1;status.Text="Keyboard listening stopped.";}capturedVk=-1;}
 public static int CapturedKey(int vk,long data){int scan=(int)((data>>16)&255);bool ext=(data&(1L<<24))!=0;if(vk==(int)Keys.Enter&&ext)return 0x1000;if(vk==(int)Keys.ShiftKey)return scan==0x36?(int)Keys.RShiftKey:(int)Keys.LShiftKey;if(vk==(int)Keys.ControlKey)return ext?(int)Keys.RControlKey:(int)Keys.LControlKey;if(vk==(int)Keys.Menu)return ext?(int)Keys.RMenu:(int)Keys.LMenu;if(!ext){int num=scan switch{0x52=>0,0x4f=>1,0x50=>2,0x51=>3,0x4b=>4,0x4c=>5,0x4d=>6,0x47=>7,0x48=>8,0x49=>9,_=>-1};if(num>=0)return (int)Keys.NumPad0+num;if(scan==0x53)return (int)Keys.Decimal;}return vk;}
 public bool PreFilterMessage(ref Message m){bool down=m.Msg==0x100||m.Msg==0x104,up=m.Msg==0x101||m.Msg==0x105;if(!down&&!up)return false;int vk=(int)m.WParam;if(capturedVk>=0){if(vk==capturedVk){if(up)capturedVk=-1;return true;}return false;}if(listening<0)return false;if(!ContainsFocus&&!loading){CancelListening();return false;}if(up)return true;int row=listening;int code=CapturedKey(vk,m.LParam.ToInt64());if(code<=0||code>0x1000)return true;var box=keyBoxes[row];int index=-1;for(int i=0;i<box.Items.Count;i++)if(box.Items[i] is KeyChoice choice&&choice.Code==code)index=i;if(index<0){index=box.Items.Add(new KeyChoice(code,code==0x1000?"Numpad Enter":((Keys)code).ToString()));}CancelListening();capturedVk=vk;box.SelectedIndex=index;status.Text=settings.Maps[row].Name+" will send "+box.SelectedItem+". Saved.";return true;}
 void ReleaseAll(){foreach(int k in held.Keys.ToArray())if(Native.SendKey(k,false))held.Remove(k);}
 protected override void WndProc(ref Message m){if(m.Msg==0x8000+141){Application.Exit();return;}if(m.Msg==0xFF){try{var result=Native.Read(m.LParam);if(result!=null)UpdateSource(result.Value.prefix,result.Value.buttons);}catch{status.Text="A HID report could not be decoded.";}}base.WndProc(ref m);}
}
static class Native {
 [StructLayout(LayoutKind.Sequential)] public struct Pad { public ushort buttons; public byte lt,rt; public short lx,ly,rx,ry; }
 [StructLayout(LayoutKind.Sequential)] public struct State { public uint packet; public Pad pad; }
 [DllImport("xinput1_4.dll")] public static extern uint XInputGetState(uint index,out State state);
 [StructLayout(LayoutKind.Sequential)] struct Device { public IntPtr handle; public uint type; }
 [StructLayout(LayoutKind.Sequential)] struct RawDevice {public ushort page,usage;public uint flags;public IntPtr target;}
 [DllImport("user32.dll")] static extern bool RegisterRawInputDevices(RawDevice[] devices,uint count,uint size);
 [DllImport("user32.dll")] static extern uint GetRawInputData(IntPtr raw,uint command,IntPtr data,ref uint size,uint header);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern uint GetRawInputDeviceInfo(IntPtr device,uint command,IntPtr data,ref uint size);
 [DllImport("user32.dll")] static extern uint GetRawInputDeviceList([Out] Device[]? devices,ref uint count,uint size);
 [DllImport("hid.dll")] static extern int HidP_GetUsages(int type,ushort page,ushort link,[Out] ushort[] usages,ref uint count,IntPtr preparsed,byte[] report,uint length);
 public static void Register(IntPtr window){var ds=new[]{new RawDevice{page=1,usage=4,flags=0x100,target=window},new RawDevice{page=1,usage=5,flags=0x100,target=window}};if(!RegisterRawInputDevices(ds,2,(uint)Marshal.SizeOf<RawDevice>()))MessageBox.Show("Could not register HID input.");}
 static string DeviceName(IntPtr h){uint size=0;GetRawInputDeviceInfo(h,0x20000007,IntPtr.Zero,ref size);if(size==0)return "unknown";IntPtr p=Marshal.AllocHGlobal((int)(size+1)*2);try{GetRawInputDeviceInfo(h,0x20000007,p,ref size);return Marshal.PtrToStringUni(p)??"unknown";}finally{Marshal.FreeHGlobal(p);}}
 static Device[] List(){uint n=0;uint size=(uint)Marshal.SizeOf<Device>();GetRawInputDeviceList(null,ref n,size);var d=new Device[n];if(GetRawInputDeviceList(d,ref n,size)==uint.MaxValue)return Array.Empty<Device>();return d.Take((int)n).ToArray();}
 public static string Devices()=>"Flydigi Key Mapper device report\r\n"+DateTime.Now+"\r\n\r\n"+string.Join("\r\n",List().Where(d=>d.type==2).Select(d=>DeviceName(d.handle)))+"\r\n\r\nXbox slots: "+string.Join(", ",Enumerable.Range(0,4).Where(i=>XInputGetState((uint)i,out _)==0))+"\r\nExtra buttons must be exposed as distinct inputs to be mapped independently.";
 public static bool DeviceExists(string name)=>List().Any(d=>d.type==2&&DeviceName(d.handle)==name);
 public static (string prefix,List<string> buttons)? Read(IntPtr handle){uint size=0;uint header=(uint)(8+IntPtr.Size*2);GetRawInputData(handle,0x10000003,IntPtr.Zero,ref size,header);if(size<header+8||size>65536)return null;IntPtr data=Marshal.AllocHGlobal((int)size);try{if(GetRawInputData(handle,0x10000003,data,ref size,header)==uint.MaxValue||Marshal.ReadInt32(data)!=2)return null;IntPtr dev=Marshal.ReadIntPtr(data,8);string prefix="H:"+DeviceName(dev)+"|";uint ps=0;GetRawInputDeviceInfo(dev,0x20000005,IntPtr.Zero,ref ps);if(ps==0||ps>65536)return null;IntPtr prep=Marshal.AllocHGlobal((int)ps);try{if(GetRawInputDeviceInfo(dev,0x20000005,prep,ref ps)==uint.MaxValue)return null;int len=Marshal.ReadInt32(data,(int)header),count=Marshal.ReadInt32(data,(int)header+4);if(len<=0||count<=0||(long)header+8+(long)len*count>size)return null;var report=new byte[len];Marshal.Copy(data+(int)header+8+(count-1)*len,report,0,len);var usages=new ushort[128];uint n=128;int result=HidP_GetUsages(0,9,0,usages,ref n,prep,report,(uint)len);if(result!=0x110000)return null;return(prefix,usages.Take((int)n).Select(u=>prefix+u).ToList());}finally{Marshal.FreeHGlobal(prep);}}finally{Marshal.FreeHGlobal(data);}}
 [StructLayout(LayoutKind.Sequential)] struct Input {public uint type;public Union u;}
 [StructLayout(LayoutKind.Explicit)] struct Union {[FieldOffset(0)]public Keyboard k;[FieldOffset(0)] public Mouse m;}
 [StructLayout(LayoutKind.Sequential)] struct Keyboard {public ushort vk,scan;public uint flags,time;public UIntPtr extra;}
 [StructLayout(LayoutKind.Sequential)] struct Mouse {public int x,y;public uint data,flags,time;public UIntPtr extra;}
 public static bool AbiValid()=>Marshal.SizeOf<Input>()==(IntPtr.Size==8?40:28)&&Marshal.SizeOf<State>()==16;
 [DllImport("user32.dll",SetLastError=true)]static extern uint SendInput(uint count,Input[] inputs,int size);
 [DllImport("user32.dll")]static extern uint MapVirtualKey(uint code,uint map);
 public static uint ScanCode(int key)=>key switch{
 0x1000=>0xE01C,96=>0x52,97=>0x4F,98=>0x50,99=>0x51,100=>0x4B,101=>0x4C,102=>0x4D,103=>0x47,104=>0x48,105=>0x49,
 106=>0x37,107=>0x4E,109=>0x4A,110=>0x53,111=>0xE035,
 45=>0xE052,46=>0xE053,36=>0xE047,35=>0xE04F,33=>0xE049,34=>0xE051,37=>0xE04B,38=>0xE048,39=>0xE04D,40=>0xE050,
 _=>MapVirtualKey((uint)key,4)};
 public static bool SendKey(int key,bool down){uint scan=ScanCode(key);bool ext=(scan&0xFF00)!=0;var i=new Input{type=1,u=new Union{k=new Keyboard{scan=(ushort)(scan&255),flags=8u|(down?0u:2u)|(ext?1u:0u)}}};return SendInput(1,new[]{i},Marshal.SizeOf<Input>())==1;}
 [DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();[DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr window,out uint pid);
 public static bool GameFocused(){try{GetWindowThreadProcessId(GetForegroundWindow(),out uint id);string n=Process.GetProcessById((int)id).ProcessName;return n.Equals("GTA5",StringComparison.OrdinalIgnoreCase)||n.StartsWith("GTA5_",StringComparison.OrdinalIgnoreCase)||n.Equals("GTAIV",StringComparison.OrdinalIgnoreCase)||n.StartsWith("FiveM",StringComparison.OrdinalIgnoreCase);}catch{return false;}}
}















