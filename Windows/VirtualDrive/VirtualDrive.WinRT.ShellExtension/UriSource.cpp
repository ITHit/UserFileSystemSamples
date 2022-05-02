#include "pch.h"
#include "UriSource.h"

using namespace winrt::Windows::Storage::Provider;

namespace winrt::CommonWindowsRtShellExtenstion::implementation
{
    void UriSource::GetPathForContentUri(hstring const& contentUri, Windows::Storage::Provider::StorageProviderGetPathForContentUriResult const& result)
    {
        CommonShellExtensionRpc::UriSourceProxy uriSourceProxy;

        auto pathResult = uriSourceProxy.GetPathForContentUri(contentUri.c_str());

        result.Path(pathResult.Path());
        result.Status((StorageProviderUriSourceStatus)pathResult.Status());
    }

    void UriSource::GetContentInfoForPath(hstring const& path, Windows::Storage::Provider::StorageProviderGetContentInfoForPathResult const& result)
    {
        CommonShellExtensionRpc::UriSourceProxy uriSourceProxy;

        auto pathResult = uriSourceProxy.GetContentInfoForPath(path);

        result.ContentId(pathResult.ContentId());
        result.ContentUri(pathResult.ContentUri());
        result.Status((StorageProviderUriSourceStatus)pathResult.Status());
    }
}
