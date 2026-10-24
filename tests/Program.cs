using System.Buffers.Binary;
using System.Text.Json;
using FlydigiKeyMapper;
int passed=0;void Check(bool value,string name){if(!value)throw new Exception(name);Console.WriteLine("PASS "+name);passed++;}
byte[] Frame(byte key,ushort bus=4,ushort device=17){var b=new byte[52];BinaryPrimitives.WriteUInt16LittleEndian(b,27);b[16]=1;BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(17),bus);BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(19),device);b[21]=0x81;b[22]=1;BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(23),25);b[27]=0x10;b[30]=21;b[31]=0x5a;b[32]=0x17;b[33]=0x12;b[50]=key;return b;}
foreach(byte key in new byte[]{0x10,0x11,0x16,0x17,0x12,0x13,0x64})Check(UsbCaptureDecoder.Decode(Frame(key),4,17).SequenceEqual(new[]{key}),"Independent code "+key.ToString("X2"));
Check(UsbCaptureDecoder.Decode(Frame(0x10),3,17).Count==0,"Other USB channel excluded");Check(UsbCaptureDecoder.Decode(Frame(0x10),4,18).Count==0,"Other device excluded");
var frame=Frame(0x10);frame[21]=1;Check(UsbCaptureDecoder.Decode(frame,4,17).Count==0,"Outgoing packets excluded");frame=Frame(0x10);frame[10]=1;Check(UsbCaptureDecoder.Decode(frame,4,17).Count==0,"Failed transfers excluded");frame=Frame(0x10);frame[23]=250;Check(UsbCaptureDecoder.Decode(frame,4,17).Count==0,"Truncated packet excluded");frame=Frame(0x10);frame[28]=0x80;Check(UsbCaptureDecoder.Decode(frame,4,17).Count==0,"Unsupported fragments excluded");Check(UsbCaptureDecoder.Decode(Frame(4),4,17).SequenceEqual(new byte[]{4}),"Normal button clears prior extra identity");
using var pcap=new MemoryStream();byte[] global=new byte[24];BinaryPrimitives.WriteUInt32LittleEndian(global,0xa1b2c3d4);BinaryPrimitives.WriteUInt32LittleEndian(global.AsSpan(16),65535);BinaryPrimitives.WriteUInt32LittleEndian(global.AsSpan(20),249);pcap.Write(global);foreach(byte code in new byte[]{0x10,0x64,0x11,0x64}){frame=Frame(code);byte[] record=new byte[16];BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(8),(uint)frame.Length);BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(12),(uint)frame.Length);pcap.Write(record);pcap.Write(frame);}pcap.Position=0;var codes=new List<byte>();await UsbCaptureDecoder.ReadPcap(pcap,4,17,codes.Add,CancellationToken.None);Check(codes.SequenceEqual(new byte[]{0x10,0x64,0x11,0x64}),"Stream preserves presses and releases");
string release=JsonSerializer.Serialize(new{draft=false,prerelease=false,tag_name="v0.2.0",html_url="https://github.com/notjacobwashere/FlydigiKeyMapper/releases/tag/v0.2.0",body="New release",assets=new[]{new{name="FlydigiKeyMapper-Setup-0.2.0-x64.exe",browser_download_url="https://github.com/notjacobwashere/FlydigiKeyMapper/releases/download/v0.2.0/FlydigiKeyMapper-Setup-0.2.0-x64.exe",digest="sha256:"+new string('a',64),size=42}}});
var parsed=UpdateService.Parse(release);Check(parsed.Version==new Version(0,2,0)&&parsed.Installer!=null,"Stable release and matching installer parsed");Check(UpdateService.ParseVersion("v1.2.3")==new Version(1,2,3),"GitHub version prefix accepted");Check(UpdateService.ParseVersion("v1.2.3-beta")==null,"Nonstable version excluded");Check(UpdateService.TrustedPage("https://example.com/notjacobwashere/FlydigiKeyMapper/releases/x")==null,"External update host excluded");Check(UpdateService.TrustedPage("https://github.com/other/repo/releases/x")==null,"Other repository excluded");Check(UpdateService.TrustedPage("http://github.com/notjacobwashere/FlydigiKeyMapper/releases/x")==null,"Unencrypted update URL excluded");bool rejected=false;try{UpdateService.Parse(release.Replace("\"draft\":false","\"draft\":true"));}catch(InvalidDataException){rejected=true;}Check(rejected,"Draft release excluded");Check(parsed.Sha256==new string('a',64)&&parsed.Size==42,"GitHub checksum and size retained");
Check(UpdateService.Parse(release.Replace("sha256:","sha512:")).Sha256==null,"Unsupported checksum rejected");
Check(UpdateService.Parse(release.Replace("/download/v0.2.0/","/download/v0.1.0/")).Installer==null,"Installer from another release rejected");
Check(UpdateService.ParseVersion("v1.2")==null,"Incomplete version excluded");
string temporary=Path.Combine(Path.GetTempPath(),"Flydigi-update-test-"+Guid.NewGuid().ToString("N"));
try{
 await File.WriteAllBytesAsync(temporary,new byte[]{1,2,3});
 var verified=parsed with{Size=3,Sha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(new byte[]{1,2,3}))};
 await UpdateService.VerifyInstaller(temporary,verified,CancellationToken.None);Check(true,"Complete installer checksum verified");
 async Task Reject(ReleaseInfo info,string label){bool failed=false;try{await UpdateService.VerifyInstaller(temporary,info,CancellationToken.None);}catch(InvalidDataException){failed=true;}Check(failed,label);}
 await Reject(verified with{Size=4},"Truncated installer rejected");
 await Reject(verified with{Sha256=new string('0',64)},"Modified installer rejected");
 await Reject(verified with{Sha256=null},"Missing checksum blocks execution");
 using var cancelled=new CancellationTokenSource();cancelled.Cancel();bool wasCancelled=false;
 try{await UpdateService.VerifyInstaller(temporary,verified,cancelled.Token);}catch(OperationCanceledException){wasCancelled=true;}Check(wasCancelled,"Verification cancellation honored");
}finally{File.Delete(temporary);}
if(args.Contains("--live-update-download")){
 var live=await UpdateService.Check(CancellationToken.None)??throw new Exception("No live release");
 string folder=Path.Combine(Path.GetTempPath(),"Flydigi-live-update-"+Guid.NewGuid().ToString("N"));
 string file=await UpdateService.Download(live,folder,new Progress<int>(),CancellationToken.None);
 Check(File.Exists(file),"Live GitHub installer downloaded and verified without executing");
 File.Delete(file);Directory.Delete(folder);
}
Console.WriteLine($"{passed} checks passed.");
