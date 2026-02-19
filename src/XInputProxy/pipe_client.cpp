#include "pipe_client.h"

namespace MX4Haptics
{
    PipeClient::PipeClient()
        : PipeClient(DEFAULT_PIPE_NAME)
    {
    }

    PipeClient::PipeClient(const wchar_t* pipeName)
        : m_hPipe(INVALID_HANDLE_VALUE)
        , m_pipeName(pipeName)
        , m_lastConnectAttempt(0)
        , m_connected(false)
    {
    }

    PipeClient::~PipeClient()
    {
        Disconnect();
    }

    PipeClient::PipeClient(PipeClient&& other) noexcept
        : m_hPipe(other.m_hPipe)
        , m_pipeName(other.m_pipeName)
        , m_lastConnectAttempt(other.m_lastConnectAttempt)
        , m_connected(other.m_connected)
    {
        other.m_hPipe = INVALID_HANDLE_VALUE;
        other.m_connected = false;
    }

    PipeClient& PipeClient::operator=(PipeClient&& other) noexcept
    {
        if (this != &other)
        {
            Disconnect();

            m_hPipe = other.m_hPipe;
            m_pipeName = other.m_pipeName;
            m_lastConnectAttempt = other.m_lastConnectAttempt;
            m_connected = other.m_connected;

            other.m_hPipe = INVALID_HANDLE_VALUE;
            other.m_connected = false;
        }
        return *this;
    }

    bool PipeClient::Connect()
    {
        if (m_connected && m_hPipe != INVALID_HANDLE_VALUE)
        {
            return true;
        }

        // Close any stale handle
        if (m_hPipe != INVALID_HANDLE_VALUE)
        {
            CloseHandle(m_hPipe);
            m_hPipe = INVALID_HANDLE_VALUE;
        }

        // Wait for the pipe to become available (with timeout)
        if (!WaitNamedPipeW(m_pipeName, CONNECTION_TIMEOUT_MS))
        {
            m_connected = false;
            return false;
        }

        // Open the pipe
        m_hPipe = CreateFileW(
            m_pipeName,
            GENERIC_WRITE,
            0,                         // No sharing
            nullptr,                   // Default security
            OPEN_EXISTING,
            FILE_ATTRIBUTE_NORMAL,
            nullptr                    // No template
        );

        if (m_hPipe == INVALID_HANDLE_VALUE)
        {
            m_connected = false;
            return false;
        }

        // Set pipe to message mode for cleaner protocol
        DWORD mode = PIPE_READMODE_BYTE;
        SetNamedPipeHandleState(m_hPipe, &mode, nullptr, nullptr);

        m_connected = true;
        return true;
    }

    void PipeClient::Disconnect()
    {
        if (m_hPipe != INVALID_HANDLE_VALUE)
        {
            FlushFileBuffers(m_hPipe);
            CloseHandle(m_hPipe);
            m_hPipe = INVALID_HANDLE_VALUE;
        }
        m_connected = false;
    }

    bool PipeClient::IsConnected() const
    {
        return m_connected && m_hPipe != INVALID_HANDLE_VALUE;
    }

    bool PipeClient::SendVibration(WORD leftMotor, WORD rightMotor)
    {
        // Normalize from 0-65535 to 0-255
        VibrationMessage msg;
        msg.leftMotor = static_cast<uint8_t>(leftMotor >> 8);
        msg.rightMotor = static_cast<uint8_t>(rightMotor >> 8);

        return SendVibrationRaw(msg);
    }

    bool PipeClient::SendVibrationRaw(const VibrationMessage& message)
    {
        // Skip if both motors are zero (no vibration)
        if (message.leftMotor == 0 && message.rightMotor == 0)
        {
            return true;
        }

        // Try to connect if not connected (with rate limiting)
        if (!TryConnect())
        {
            return false;
        }

        return WriteToPipe(&message, sizeof(VibrationMessage));
    }

    bool PipeClient::TryConnect()
    {
        if (m_connected)
        {
            return true;
        }

        // Rate-limit connection attempts to avoid performance impact
        ULONGLONG currentTick = GetTickCount64();
        if (currentTick - m_lastConnectAttempt < RETRY_INTERVAL_MS)
        {
            return false;
        }

        m_lastConnectAttempt = currentTick;
        return Connect();
    }

    bool PipeClient::WriteToPipe(const void* data, DWORD size)
    {
        if (!m_connected || m_hPipe == INVALID_HANDLE_VALUE)
        {
            return false;
        }

        DWORD bytesWritten = 0;
        BOOL success = WriteFile(
            m_hPipe,
            data,
            size,
            &bytesWritten,
            nullptr  // Synchronous write
        );

        if (!success || bytesWritten != size)
        {
            // Pipe broken, disconnect and let it reconnect on next attempt
            Disconnect();
            return false;
        }

        return true;
    }

    // Global instance with lazy initialization
    PipeClient& GetGlobalPipeClient()
    {
        static PipeClient instance;
        return instance;
    }

} // namespace MX4Haptics
