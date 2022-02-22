#include "pch.h"
#include "CustomStateProvider.h"
#include "ShellExtensionModule.h"
#include <fstream>

namespace winrt
{
    using namespace winrt::Windows::Storage::Provider;
}

namespace winrt::CommonWindowsRtShellExtenstion::implementation
{
    Windows::Foundation::Collections::IIterable<Windows::Storage::Provider::StorageProviderItemProperty> CustomStateProvider::GetItemProperties(hstring const& itemPath)
    {
        auto propertyVector{ winrt::single_threaded_vector<winrt::StorageProviderItemProperty>() };

        try
        {
            CommonShellExtensionRpc::CustomStateProviderProxy stateProviderProxy;
            auto itemProperties = stateProviderProxy.GetItemProperties(itemPath);

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
