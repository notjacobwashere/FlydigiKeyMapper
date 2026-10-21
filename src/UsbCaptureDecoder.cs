using System.Buffers.Binary;
namespace FlydigiKeyMapper;
// Offline-only decoder. Does not open a USB device, load a driver, or emit keyboard input.
static class UsbCaptureDecoder {
 public static List<byte> Decode(ReadOnlySpan<byte> packet,ushort bus,ushort device){
  var keys=new List<byte>();if(packet.Length<27)return keys;
  int header=BinaryPrimitives.ReadUInt16LittleEndian(packet);
  uint length=BinaryPrimitives.ReadUInt32LittleEndian(packet[23..]);
  if(header<27||header>packet.Length||length>(uint)(packet.Length-header))return keys;
  if(BinaryPrimitives.ReadUInt32LittleEndian(packet[10..])!=0||(packet[16]&1)==0||(packet[22]!=1&&packet[22]!=3)||(packet[21]&0x80)==0)return keys;
  if(BinaryPrimitives.ReadUInt16LittleEndian(packet[17..])!=bus||BinaryPrimitives.ReadUInt16LittleEndian(packet[19..])!=device)return keys;
  var data=packet.Slice(header,(int)length);
  while(data.Length>=4){
   // Initial implementation accepts complete, short GIP frames only. Chunked frames need real capture evidence.
   if((data[1]&0xc0)!=0)break;
   int payloadSize=data[3];if(payloadSize>data.Length-4)break;
   var payload=data.Slice(4,payloadSize);
   if(data[0]==0x10&&payload.Length==21&&payload[0]==0x5a&&payload[1]==0x17&&payload[2]==0x12){
    byte key=payload[19];if(key<=0x1f||key==0x64)keys.Add(key);
   }
   data=data[(4+payloadSize)..];
  }
  return keys;
 }
 public static async Task ReadPcap(Stream input,ushort bus,ushort device,Action<byte> onKey,CancellationToken cancel){
  byte[] header=new byte[24];await input.ReadExactlyAsync(header,cancel);
  uint magic=BinaryPrimitives.ReadUInt32LittleEndian(header);
  if(magic!=0xa1b2c3d4&&magic!=0xa1b23c4d)throw new InvalidDataException("Expected little-endian pcap.");
  if(BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(20))!=249)throw new InvalidDataException("Expected USBPcap link type.");
  uint snaplen=BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(16));
  if(snaplen==0||snaplen>4*1024*1024)throw new InvalidDataException("Invalid capture size limit.");
  byte[] record=new byte[16];
  while(!cancel.IsCancellationRequested){
   int first=await input.ReadAsync(record.AsMemory(0,1),cancel);if(first==0)return;
   await input.ReadExactlyAsync(record.AsMemory(1),cancel);
   uint count=BinaryPrimitives.ReadUInt32LittleEndian(record.AsSpan(8));
   if(count>snaplen||count>BinaryPrimitives.ReadUInt32LittleEndian(record.AsSpan(12)))throw new InvalidDataException("Invalid capture record size.");
   byte[] packet=new byte[(int)count];await input.ReadExactlyAsync(packet,cancel);
   foreach(byte key in Decode(packet,bus,device))onKey(key);
  }
 }
}


