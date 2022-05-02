#pragma once

#include "CommonWindowsRtShellExtenstion.UriSource.g.h"
#include <windows.storage.provider.h>

// {6D45BC7A-D0B7-4913-8984-FD7261550C08}
static const GUID CLSID_UriSource =
{ 0x6d45bc7a, 0xd0b7, 0x4913, { 0x89, 0x84, 0xfd, 0x72, 0x61, 0x55, 0xc, 0x8 } };


namespace winrt::CommonWindowsRtShellExtenstion::implementation
{
    struct UriSource : UriSourceT<UriSource>
    {
        UriSource() = default;

        void GetPathForContentUri(_In_ hstring const& contentUri, _Out_ Windows::Storage::Provider::StorageProviderGetPathForContentUriResult const& result);
        void GetContentInfoForPath(_In_ hstring const& path, _Out_ Windows::Storage::Provider::StorageProviderGetContentInfoForPathResult const& result);
    };
}

namespace winrt::CommonWindowsRtShellExtenstion::factory_implementation
{
    struct UriSource : UriSourceT<UriSource, implementation::UriSource>
    {
    };
}
