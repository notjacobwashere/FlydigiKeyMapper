using System.Runtime.InteropServices;
using System.Text;

namespace FlydigiKeyMapper;

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
}
