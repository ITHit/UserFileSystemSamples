#pragma once

#include "CommonWindowsRtShellExtenstion.CustomStateProvider.g.h"
#include <windows.storage.provider.h>

// 000562AA-2879-4CF1-89E8-0AEC9596FE19
constexpr CLSID CLSID_CustomStateProviderVirtualDrive = { 0x562aa, 0x2879, 0x4cf1, { 0x89, 0xe8, 0xa, 0xec, 0x95, 0x96, 0xfe, 0x19 } };

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
