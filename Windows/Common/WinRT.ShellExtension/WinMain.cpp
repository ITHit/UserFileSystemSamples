#include "pch.h"
#include "ShellExtensionModule.h"

using namespace winrt;
using namespace Windows::Foundation;

void __stdcall TimerProc(HWND hWnd, UINT message, UINT idTimer, DWORD dwTime)
{
    PostQuitMessage(0);
}

LRESULT __stdcall WindowProc(HWND hWnd, UINT uMsg, WPARAM wParam, LPARAM lParam)
{
    switch (uMsg)
    {
    case WM_DESTROY:
        KillTimer(hWnd, 0);
        PostQuitMessage(0);
        break;
    }

    return DefWindowProc(hWnd, uMsg, wParam, lParam);
}

void RunMessageLoop(HINSTANCE hInstance)
{
    std::wstring className = L"ShellExtension Window Class";

    WNDCLASS wc = { };

    wc.lpfnWndProc = WindowProc;
    wc.hInstance = hInstance;
    wc.lpszClassName = className.c_str();

    RegisterClass(&wc);

    HWND hWnd = CreateWindowEx(
        0,
        className.c_str(),
        L"ShellExtension",
        WS_OVERLAPPEDWINDOW,
        CW_USEDEFAULT, CW_USEDEFAULT, CW_USEDEFAULT, CW_USEDEFAULT,
        nullptr,
        nullptr,
        hInstance,
        nullptr
    );

    ShowWindow(hWnd, SW_HIDE);

    SetTimer(hWnd, 0, 20000, (TIMERPROC)TimerProc);

    MSG msg;

    while (GetMessage(&msg, nullptr, 0, 0))
    {
        TranslateMessage(&msg);
        DispatchMessage(&msg);
    }
}

int __stdcall wWinMain(HINSTANCE hInstance, HINSTANCE hPrevInstance, PWSTR pCmdLine, int nCmdShow)
{
    init_apartment();

    ShellExtensionModule module;

    RunMessageLoop(hInstance);
}
