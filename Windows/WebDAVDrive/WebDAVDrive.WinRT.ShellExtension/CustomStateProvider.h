#pragma once

#include "CommonWindowsRtShellExtenstion.CustomStateProvider.g.h"
#include <windows.storage.provider.h>

constexpr CLSID CLSID_CustomStateProviderWebDav = { 0x754f334f, 0x95c, 0x46cd, { 0xb0, 0x33, 0xb2, 0xc0, 0x52, 0x3d, 0x28, 0x29 } };

namespace winrt::CommonWindowsRtShellExtenstion::implementation
{
    struct CustomStateProvider : CustomStateProviderT<CustomStateProvider>
    {
        CustomStateProvider() = default;

        Windows::Foundation::Collections::IIterable<Windows::Storage::Provider::StorageProviderItemProperty> GetItemProperties(_In_ hstring const& itemPath);
    };
}

namespace winrt::CommonWindowsRtShellExtenstion::factory_implementation
{
    struct CustomStateProvider : CustomStateProviderT<CustomStateProvider, implementation::CustomStateProvider>
    {
    };
}
