#include "pch.h"
#include "ShellExtensionModule.h"
#include "CustomStateProvider.h"
#include "ClassFactory.h"

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
	DWORD cookie;

	auto customStateProvider = winrt::make<ClassFactory<CustomStateProvider>>();
	winrt::check_hresult(CoRegisterClassObject(CLSID_CustomStateProvider, customStateProvider.get(), CLSCTX_LOCAL_SERVER, REGCLS_MULTIPLEUSE, &cookie));
}

void ShellExtensionModule::Stop()
{	

}
