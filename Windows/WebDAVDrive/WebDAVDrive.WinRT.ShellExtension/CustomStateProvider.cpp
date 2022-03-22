#include "pch.h"
#include "CustomStateProvider.h"
#include "..\..\Common\WinRT.ShellExtension\ShellExtensionModule.h"

namespace winrt
{
    using namespace winrt::Windows::Storage::Provider;
}

namespace winrt::CommonWindowsRtShellExtenstion::implementation
{
    using namespace Windows::Foundation::Collections;
    using namespace Windows::Storage::Provider;

    IIterable<StorageProviderItemProperty> CustomStateProvider::GetItemProperties(hstring const& itemPath)
    {
        auto propertyVector{ winrt::single_threaded_vector<winrt::StorageProviderItemProperty>() };

        try
        {
            CommonShellExtensionRpc::CustomStateProviderProxy stateProviderProxy;
            auto itemProperties = stateProviderProxy.GetItemProperties(itemPath, false);

            for (const auto& itemProp : itemProperties)
            {
                winrt::StorageProviderItemProperty storageItemProperty;
                storageItemProperty.Id(itemProp.Id());
                storageItemProperty.Value(itemProp.Value());
                storageItemProperty.IconResource(itemProp.IconResource());

                propertyVector.Append(storageItemProperty);
            }
        }
        catch (...)
        {
        }

        return propertyVector;
    }
}
