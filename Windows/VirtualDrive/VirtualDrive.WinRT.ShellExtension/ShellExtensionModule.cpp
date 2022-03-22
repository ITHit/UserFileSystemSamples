#include "pch.h"
#include "..\..\Common\WinRT.ShellExtension\ShellExtensionModule.h"
#include "..\..\Common\WinRT.ShellExtension\ClassFactory.h"

using namespace winrt::CommonWindowsRtShellExtenstion::implementation;

ShellExtensionModule::ShellExtensionModule()
{
	Start();
}

ShellExtensionModule::~ShellExtensionModule()
{
	Stop();
}

void ShellExtensionModule::Start()
{
	DWORD cookie = 0;

	auto customStateProviderVirtualDrive = winrt::make<ClassFactory<CustomStateProvider>>();
	winrt::check_hresult(CoRegisterClassObject(CLSID_CustomStateProviderVirtualDrive, customStateProviderVirtualDrive.get(), CLSCTX_LOCAL_SERVER, REGCLS_MULTI_SEPARATE, &cookie));
}

void ShellExtensionModule::Stop()
{

}
