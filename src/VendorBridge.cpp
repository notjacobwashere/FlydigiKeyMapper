#define UNICODE
#include <windows.h>
#include <roapi.h>
#include <winstring.h>
#include <windows.gaming.input.h>
#include <activation.h>
#include <wrl/client.h>
#include <atomic>
#include <thread>
#include <future>
#include <string>
using Microsoft::WRL::ComPtr;
// Minimal ABI declarations for interoperability with the user's installed Store component.
// No vendor implementation or generated SDK files are distributed.
struct __declspec(uuid("0F2036F6-4979-4FE4-981C-A03DAC01B2DA")) Device : IInspectable {
 virtual HRESULT STDMETHODCALLTYPE InstanceId(UINT32*)=0;
 virtual HRESULT STDMETHODCALLTYPE Firmware(void*)=0;
 virtual HRESULT STDMETHODCALLTYPE Product(USHORT*)=0;
 virtual HRESULT STDMETHODCALLTYPE Vendor(USHORT*)=0;
 virtual HRESULT STDMETHODCALLTYPE Hardware(void*)=0;
 virtual HRESULT STDMETHODCALLTYPE AddMessage(IUnknown*,INT64*)=0;
 virtual HRESULT STDMETHODCALLTYPE RemoveMessage(INT64)=0;
 virtual HRESULT STDMETHODCALLTYPE SendA(UINT32,BYTE*)=0;
 virtual HRESULT STDMETHODCALLTYPE SendB(UINT32,BYTE*)=0;
};
struct DeviceView : IInspectable {
 virtual HRESULT STDMETHODCALLTYPE GetAt(UINT32,IInspectable**)=0;
 virtual HRESULT STDMETHODCALLTYPE Size(UINT32*)=0;
};
struct __declspec(uuid("D6698FE1-2CF2-4FC4-B583-71B060266A30")) DeviceFactory : IInspectable {
 virtual HRESULT STDMETHODCALLTYPE AddDevice(IUnknown*,INT64*)=0;
 virtual HRESULT STDMETHODCALLTYPE RemoveDevice(INT64)=0;
 virtual HRESULT STDMETHODCALLTYPE AddRemoved(IUnknown*,INT64*)=0;
 virtual HRESULT STDMETHODCALLTYPE RemoveRemoved(INT64)=0;
 virtual HRESULT STDMETHODCALLTYPE Devices(DeviceView**)=0;
};
static std::thread worker;
static std::atomic<bool> stopping{false};
static std::atomic<int> lastStatus{0}, deviceCount{0},listed{0},lastVid{0},lastPid{0};
static HRESULT send(Device* d,BYTE id){BYTE b[]={0x5a,id,8,0,0,0,0,0,0,0};return d->SendB(sizeof(b),b);}
static void poll(DeviceFactory* f,bool close){
 ComPtr<DeviceView> view;HRESULT hr=f->Devices(&view);if(FAILED(hr)){lastStatus=hr;deviceCount=0;return;}
 UINT32 size=0;if(FAILED(view->Size(&size))||size>128){lastStatus=E_FAIL;return;}listed=size;int found=0;
 for(UINT32 i=0;i<size;i++){ComPtr<IInspectable> obj;ComPtr<Device>d;hr=view->GetAt(i,&obj);if(SUCCEEDED(hr))hr=obj.As(&d);if(FAILED(hr)){lastStatus=hr;continue;}USHORT vid=0,pid=0;hr=d->Vendor(&vid);if(SUCCEEDED(hr))hr=d->Product(&pid);lastVid=vid;lastPid=pid;if(FAILED(hr)){lastStatus=hr;continue;}if(vid!=0x37d7||pid!=0x2801)continue;
 found++;hr=send(d.Get(),close?0x18:0x16);if(!close&&SUCCEEDED(hr))hr=send(d.Get(),0x85);lastStatus=hr;}
 deviceCount=found;
}
extern "C" __declspec(dllexport) int __cdecl BridgeOpen(wchar_t const* path){
 if(!path)return E_INVALIDARG;if(worker.joinable())return S_FALSE;stopping=false;std::wstring owned(path);std::promise<int> ready;auto result=ready.get_future();
 worker=std::thread([owned,p=std::move(ready)]()mutable{
 HRESULT hr=RoInitialize(RO_INIT_MULTITHREADED);if(FAILED(hr)){p.set_value(hr);return;}
 ComPtr<DeviceFactory> factory;auto mod=LoadLibraryExW(owned.c_str(),nullptr,LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR|LOAD_LIBRARY_SEARCH_DEFAULT_DIRS);
 if(!mod){p.set_value(HRESULT_FROM_WIN32(GetLastError()));RoUninitialize();return;}
 using Fn=HRESULT(WINAPI*)(HSTRING,IActivationFactory**);auto fn=reinterpret_cast<Fn>(GetProcAddress(mod,"DllGetActivationFactory"));HSTRING name=nullptr;
 if(!fn)hr=E_NOINTERFACE;else{const wchar_t* cls=L"D4XGaming.Devices.D4XDevice";hr=WindowsCreateString(cls,static_cast<UINT32>(wcslen(cls)),&name);if(SUCCEEDED(hr)){ComPtr<IActivationFactory> f;hr=fn(name,&f);if(SUCCEEDED(hr))hr=f.As(&factory);}}
  if(name)WindowsDeleteString(name);
 // Initialize the documented WGI raw-controller enumeration before reading the custom factory.
 HSTRING rawName=nullptr;ComPtr<ABI::Windows::Gaming::Input::IRawGameControllerStatics> rawStatics;
 const wchar_t* rawClass=L"Windows.Gaming.Input.RawGameController";
 if(SUCCEEDED(WindowsCreateString(rawClass,static_cast<UINT32>(wcslen(rawClass)),&rawName))){
  if(SUCCEEDED(RoGetActivationFactory(rawName,IID_PPV_ARGS(&rawStatics)))){ComPtr<__FIVectorView_1_Windows__CGaming__CInput__CRawGameController> controllers;rawStatics->get_RawGameControllers(&controllers);}
  WindowsDeleteString(rawName);
 }
 p.set_value(hr);
 if(SUCCEEDED(hr)){while(!stopping){poll(factory.Get(),false);for(int i=0;i<50&&!stopping;i++)Sleep(20);}poll(factory.Get(),true);}
 factory.Reset();rawStatics.Reset();deviceCount=0;RoUninitialize();
 // The component maintains WinRT callbacks; keep its module loaded until process exit.
 });int hr=result.get();if(hr<0&&worker.joinable())worker.join();return hr;
}
extern "C" __declspec(dllexport) int __cdecl BridgeStatus(int* count){*count=deviceCount;return lastStatus;}
extern "C" __declspec(dllexport) void __cdecl BridgeClose(){stopping=true;if(worker.joinable())worker.join();}

extern "C" __declspec(dllexport) int __cdecl BridgeListed(int* vid,int* pid){*vid=lastVid;*pid=lastPid;return listed;}


