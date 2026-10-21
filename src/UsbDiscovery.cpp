#define UNICODE
#include <windows.h>
#include <winioctl.h>
#include <usbioctl.h>
#include <string>
#include <vector>
#include <set>
// Published USBPcap query (BSD-2-Clause USBPcap.h). Does not start filtering.
static constexpr DWORD GetHubLink=CTL_CODE(FILE_DEVICE_UNKNOWN,0x803,METHOD_BUFFERED,FILE_ANY_ACCESS);
struct Handle{HANDLE value;explicit Handle(HANDLE h):value(h){}~Handle(){if(value!=INVALID_HANDLE_VALUE)CloseHandle(value);}operator HANDLE()const{return value;}};
static std::wstring path(std::wstring name){if(name.compare(0,4,L"\\??\\")==0)return L"\\\\.\\"+name.substr(4);if(!name.empty()&&name[0]==L'\\')return name;return L"\\\\.\\"+name;}
static void scan(std::wstring const& name,std::vector<USHORT>& matches,std::set<std::wstring>& visited,int depth){
 if(depth>16||visited.size()>128||!visited.insert(name).second)return;
 Handle hub(CreateFileW(path(name).c_str(),GENERIC_WRITE,FILE_SHARE_WRITE,nullptr,OPEN_EXISTING,0,nullptr));if(hub.value==INVALID_HANDLE_VALUE)return;
 USB_NODE_INFORMATION node{};node.NodeType=UsbHub;DWORD bytes=0;
 if(!DeviceIoControl(hub,IOCTL_USB_GET_NODE_INFORMATION,&node,sizeof(node),&node,sizeof(node),&bytes,nullptr))return;
 for(ULONG port=1;port<=node.u.HubInformation.HubDescriptor.bNumberOfPorts;port++){
  USB_NODE_CONNECTION_INFORMATION info;ZeroMemory(&info,sizeof(info));info.ConnectionIndex=port;
  if(!DeviceIoControl(hub,IOCTL_USB_GET_NODE_CONNECTION_INFORMATION,&info,sizeof(info),&info,sizeof(info),&bytes,nullptr)||info.ConnectionStatus!=DeviceConnected)continue;
  if(info.DeviceDescriptor.idVendor==0x37d7&&info.DeviceDescriptor.idProduct==0x2801&&info.DeviceAddress>0&&info.DeviceAddress<128)matches.push_back(info.DeviceAddress);
  if(info.DeviceIsHub){std::vector<BYTE> buffer(8192);auto child=reinterpret_cast<USB_NODE_CONNECTION_NAME*>(buffer.data());child->ConnectionIndex=port;
   if(DeviceIoControl(hub,IOCTL_USB_GET_NODE_CONNECTION_NAME,child,sizeof(USB_NODE_CONNECTION_NAME),child,static_cast<DWORD>(buffer.size()),&bytes,nullptr)&&child->ActualLength<=buffer.size()&&bytes>=sizeof(USB_NODE_CONNECTION_NAME)){
    buffer[buffer.size()-1]=0;buffer[buffer.size()-2]=0;scan(child->NodeName,matches,visited,depth+1);
   }
  }
 }
}
extern "C" __declspec(dllexport) int __cdecl FindController(wchar_t const* control,USHORT* address){
 if(!control||!address)return E_INVALIDARG;*address=0;
 Handle filter(CreateFileW(control,0,0,nullptr,OPEN_EXISTING,0,nullptr));if(filter.value==INVALID_HANDLE_VALUE)return HRESULT_FROM_WIN32(GetLastError());
 wchar_t link[4096]{};DWORD bytes=0;BOOL ok=DeviceIoControl(filter,GetHubLink,nullptr,0,link,sizeof(link)-sizeof(wchar_t),&bytes,nullptr);
 if(!ok)return HRESULT_FROM_WIN32(GetLastError());if(bytes<sizeof(wchar_t))return E_FAIL;
 // Release the filter query handle before USBPcapCMD opens this channel for capture.
 CloseHandle(filter.value);filter.value=INVALID_HANDLE_VALUE;
 std::vector<USHORT> matches;std::set<std::wstring> visited;scan(link,matches,visited,0);
 if(matches.empty())return S_FALSE;if(matches.size()!=1)return HRESULT_FROM_WIN32(ERROR_MORE_DATA);*address=matches[0];return S_OK;
}

