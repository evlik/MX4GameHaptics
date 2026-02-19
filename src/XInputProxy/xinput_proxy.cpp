/**
 * @file xinput_proxy.cpp
 * @brief XInput 1.4 Proxy DLL for MX4GameHaptics
 *
 * This DLL intercepts XInput calls, forwards them to the real xinput1_4.dll,
 * and sends vibration data to the MX4GameHaptics plugin via named pipe.
 */

#include <Windows.h>
#include "pipe_client.h"

// ============================================================================
// XInput Types (defined locally to avoid header conflicts)
// ============================================================================

#pragma pack(push, 1)

struct XINPUT_GAMEPAD
{
    WORD  wButtons;
    BYTE  bLeftTrigger;
    BYTE  bRightTrigger;
    SHORT sThumbLX;
    SHORT sThumbLY;
    SHORT sThumbRX;
    SHORT sThumbRY;
};

struct XINPUT_STATE
{
    DWORD          dwPacketNumber;
    XINPUT_GAMEPAD Gamepad;
};

struct XINPUT_VIBRATION
{
    WORD wLeftMotorSpeed;
    WORD wRightMotorSpeed;
};

struct XINPUT_CAPABILITIES
{
    BYTE             Type;
    BYTE             SubType;
    WORD             Flags;
    XINPUT_GAMEPAD   Gamepad;
    XINPUT_VIBRATION Vibration;
};

struct XINPUT_BATTERY_INFORMATION
{
    BYTE BatteryType;
    BYTE BatteryLevel;
};

struct XINPUT_KEYSTROKE
{
    WORD  VirtualKey;
    WCHAR Unicode;
    WORD  Flags;
    BYTE  UserIndex;
    BYTE  HidCode;
};

#pragma pack(pop)

// ============================================================================
// Original DLL Function Pointers
// ============================================================================

static HMODULE g_hOriginalXInput = nullptr;

typedef DWORD(WINAPI* PFN_XInputGetState)(DWORD, XINPUT_STATE*);
typedef DWORD(WINAPI* PFN_XInputSetState)(DWORD, XINPUT_VIBRATION*);
typedef DWORD(WINAPI* PFN_XInputGetCapabilities)(DWORD, DWORD, XINPUT_CAPABILITIES*);
typedef void(WINAPI* PFN_XInputEnable)(BOOL);
typedef DWORD(WINAPI* PFN_XInputGetBatteryInformation)(DWORD, BYTE, XINPUT_BATTERY_INFORMATION*);
typedef DWORD(WINAPI* PFN_XInputGetKeystroke)(DWORD, DWORD, XINPUT_KEYSTROKE*);
typedef DWORD(WINAPI* PFN_XInputGetAudioDeviceIds)(DWORD, LPWSTR, UINT*, LPWSTR, UINT*);

static PFN_XInputGetState g_pXInputGetState = nullptr;
static PFN_XInputSetState g_pXInputSetState = nullptr;
static PFN_XInputGetCapabilities g_pXInputGetCapabilities = nullptr;
static PFN_XInputEnable g_pXInputEnable = nullptr;
static PFN_XInputGetBatteryInformation g_pXInputGetBatteryInformation = nullptr;
static PFN_XInputGetKeystroke g_pXInputGetKeystroke = nullptr;
static PFN_XInputGetAudioDeviceIds g_pXInputGetAudioDeviceIds = nullptr;

// ============================================================================
// Initialization and Cleanup
// ============================================================================

static bool LoadOriginalXInput()
{
    if (g_hOriginalXInput != nullptr)
    {
        return true;
    }

    wchar_t systemPath[MAX_PATH];
    UINT length = GetSystemDirectoryW(systemPath, MAX_PATH);

    if (length == 0 || length >= MAX_PATH - 20)
    {
        return false;
    }

    wcscat_s(systemPath, MAX_PATH, L"\\xinput1_4.dll");

    g_hOriginalXInput = LoadLibraryW(systemPath);

    if (g_hOriginalXInput == nullptr)
    {
        return false;
    }

    g_pXInputGetState = reinterpret_cast<PFN_XInputGetState>(
        GetProcAddress(g_hOriginalXInput, "XInputGetState"));

    g_pXInputSetState = reinterpret_cast<PFN_XInputSetState>(
        GetProcAddress(g_hOriginalXInput, "XInputSetState"));

    g_pXInputGetCapabilities = reinterpret_cast<PFN_XInputGetCapabilities>(
        GetProcAddress(g_hOriginalXInput, "XInputGetCapabilities"));

    g_pXInputEnable = reinterpret_cast<PFN_XInputEnable>(
        GetProcAddress(g_hOriginalXInput, "XInputEnable"));

    g_pXInputGetBatteryInformation = reinterpret_cast<PFN_XInputGetBatteryInformation>(
        GetProcAddress(g_hOriginalXInput, "XInputGetBatteryInformation"));

    g_pXInputGetKeystroke = reinterpret_cast<PFN_XInputGetKeystroke>(
        GetProcAddress(g_hOriginalXInput, "XInputGetKeystroke"));

    g_pXInputGetAudioDeviceIds = reinterpret_cast<PFN_XInputGetAudioDeviceIds>(
        GetProcAddress(g_hOriginalXInput, "XInputGetAudioDeviceIds"));

    return g_pXInputGetState != nullptr && g_pXInputSetState != nullptr;
}

static void UnloadOriginalXInput()
{
    if (g_hOriginalXInput != nullptr)
    {
        FreeLibrary(g_hOriginalXInput);
        g_hOriginalXInput = nullptr;
    }

    g_pXInputGetState = nullptr;
    g_pXInputSetState = nullptr;
    g_pXInputGetCapabilities = nullptr;
    g_pXInputEnable = nullptr;
    g_pXInputGetBatteryInformation = nullptr;
    g_pXInputGetKeystroke = nullptr;
    g_pXInputGetAudioDeviceIds = nullptr;
}

// ============================================================================
// DLL Entry Point
// ============================================================================

BOOL APIENTRY DllMain(HMODULE hModule, DWORD ul_reason_for_call, LPVOID lpReserved)
{
    (void)hModule;
    (void)lpReserved;

    switch (ul_reason_for_call)
    {
    case DLL_PROCESS_ATTACH:
        DisableThreadLibraryCalls(hModule);
        if (!LoadOriginalXInput())
        {
            return FALSE;
        }
        break;

    case DLL_PROCESS_DETACH:
        MX4Haptics::GetGlobalPipeClient().Disconnect();
        UnloadOriginalXInput();
        break;

    case DLL_THREAD_ATTACH:
    case DLL_THREAD_DETACH:
        break;
    }

    return TRUE;
}

// ============================================================================
// XInput API Exports
// ============================================================================

#define XINPUT_EXPORT extern "C" __declspec(dllexport)

XINPUT_EXPORT DWORD WINAPI XInputGetState_Proxy(DWORD dwUserIndex, XINPUT_STATE* pState)
{
    if (g_pXInputGetState == nullptr)
    {
        return ERROR_DEVICE_NOT_CONNECTED;
    }
    return g_pXInputGetState(dwUserIndex, pState);
}

XINPUT_EXPORT DWORD WINAPI XInputSetState_Proxy(DWORD dwUserIndex, XINPUT_VIBRATION* pVibration)
{
    if (g_pXInputSetState == nullptr)
    {
        return ERROR_DEVICE_NOT_CONNECTED;
    }

    // Send vibration data to MX4 haptics
    if (pVibration != nullptr)
    {
        MX4Haptics::GetGlobalPipeClient().SendVibration(
            pVibration->wLeftMotorSpeed,
            pVibration->wRightMotorSpeed
        );
    }

    return g_pXInputSetState(dwUserIndex, pVibration);
}

XINPUT_EXPORT DWORD WINAPI XInputGetCapabilities_Proxy(DWORD dwUserIndex, DWORD dwFlags, XINPUT_CAPABILITIES* pCapabilities)
{
    if (g_pXInputGetCapabilities == nullptr)
    {
        return ERROR_DEVICE_NOT_CONNECTED;
    }
    return g_pXInputGetCapabilities(dwUserIndex, dwFlags, pCapabilities);
}

XINPUT_EXPORT void WINAPI XInputEnable_Proxy(BOOL enable)
{
    if (g_pXInputEnable != nullptr)
    {
        g_pXInputEnable(enable);
    }
}

XINPUT_EXPORT DWORD WINAPI XInputGetBatteryInformation_Proxy(DWORD dwUserIndex, BYTE devType, XINPUT_BATTERY_INFORMATION* pBatteryInformation)
{
    if (g_pXInputGetBatteryInformation == nullptr)
    {
        return ERROR_DEVICE_NOT_CONNECTED;
    }
    return g_pXInputGetBatteryInformation(dwUserIndex, devType, pBatteryInformation);
}

XINPUT_EXPORT DWORD WINAPI XInputGetKeystroke_Proxy(DWORD dwUserIndex, DWORD dwReserved, XINPUT_KEYSTROKE* pKeystroke)
{
    if (g_pXInputGetKeystroke == nullptr)
    {
        return ERROR_DEVICE_NOT_CONNECTED;
    }
    return g_pXInputGetKeystroke(dwUserIndex, dwReserved, pKeystroke);
}

XINPUT_EXPORT DWORD WINAPI XInputGetAudioDeviceIds_Proxy(
    DWORD dwUserIndex,
    LPWSTR pRenderDeviceId,
    UINT* pRenderCount,
    LPWSTR pCaptureDeviceId,
    UINT* pCaptureCount)
{
    if (g_pXInputGetAudioDeviceIds == nullptr)
    {
        return ERROR_DEVICE_NOT_CONNECTED;
    }
    return g_pXInputGetAudioDeviceIds(
        dwUserIndex,
        pRenderDeviceId,
        pRenderCount,
        pCaptureDeviceId,
        pCaptureCount
    );
}
