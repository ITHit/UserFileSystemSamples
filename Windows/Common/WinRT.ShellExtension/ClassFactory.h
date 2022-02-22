#pragma once

template<typename T>
class ClassFactory : public winrt::implements<ClassFactory<T>, IClassFactory>
{
public:

    IFACEMETHODIMP CreateInstance(_In_opt_ IUnknown* unkOuter, REFIID riid, _COM_Outptr_ void** object)
    {
        try
        {
            auto provider = winrt::make<T>();
            winrt::com_ptr<IUnknown> unkn{ provider.as<IUnknown>() };
            winrt::check_hresult(unkn->QueryInterface(riid, object));
            return S_OK;
        }
        catch (...)
        {
            return winrt::to_hresult();
        }
    }
    IFACEMETHODIMP LockServer(BOOL lock) { return S_OK; }
};
