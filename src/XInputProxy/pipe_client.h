#pragma once

#ifndef PIPE_CLIENT_H
#define PIPE_CLIENT_H

#include <Windows.h>
#include <cstdint>

namespace MX4Haptics
{
    /**
     * @brief Vibration message structure sent through the named pipe.
     *
     * Contains normalized motor intensity values (0-255) derived from
     * XInput's 0-65535 range by dividing by 256.
     */
    struct VibrationMessage
    {
        uint8_t leftMotor;   ///< Left motor intensity (0-255)
        uint8_t rightMotor;  ///< Right motor intensity (0-255)
    };

    static_assert(sizeof(VibrationMessage) == 2, "VibrationMessage must be exactly 2 bytes");

    /**
     * @brief Named pipe client for sending vibration data to the Logi plugin.
     *
     * Handles connection management, reconnection attempts, and asynchronous
     * writing to minimize impact on game performance.
     */
    class PipeClient
    {
    public:
        /**
         * @brief Default pipe name for MX4GameHaptics communication.
         */
        static constexpr const wchar_t* DEFAULT_PIPE_NAME = L"\\\\.\\pipe\\MX4GameHaptics";

        /**
         * @brief Timeout for pipe connection attempts in milliseconds.
         */
        static constexpr DWORD CONNECTION_TIMEOUT_MS = 100;

        /**
         * @brief Interval between connection retry attempts in milliseconds.
         */
        static constexpr DWORD RETRY_INTERVAL_MS = 1000;

        /**
         * @brief Constructs a new PipeClient with the default pipe name.
         */
        PipeClient();

        /**
         * @brief Constructs a new PipeClient with a custom pipe name.
         * @param pipeName Full pipe path (e.g., "\\\\.\\pipe\\MyPipe")
         */
        explicit PipeClient(const wchar_t* pipeName);

        /**
         * @brief Destructor. Closes the pipe connection if open.
         */
        ~PipeClient();

        // Non-copyable
        PipeClient(const PipeClient&) = delete;
        PipeClient& operator=(const PipeClient&) = delete;

        // Movable
        PipeClient(PipeClient&& other) noexcept;
        PipeClient& operator=(PipeClient&& other) noexcept;

        /**
         * @brief Attempts to connect to the named pipe server.
         * @return true if connection successful or already connected
         */
        bool Connect();

        /**
         * @brief Disconnects from the pipe server.
         */
        void Disconnect();

        /**
         * @brief Checks if the pipe is currently connected.
         * @return true if connected
         */
        bool IsConnected() const;

        /**
         * @brief Sends vibration data through the pipe.
         *
         * If not connected, attempts to connect first. If connection fails,
         * the data is silently dropped to avoid impacting game performance.
         *
         * @param leftMotor Left motor intensity (0-65535, will be normalized to 0-255)
         * @param rightMotor Right motor intensity (0-65535, will be normalized to 0-255)
         * @return true if data was sent successfully
         */
        bool SendVibration(WORD leftMotor, WORD rightMotor);

        /**
         * @brief Sends a raw vibration message through the pipe.
         * @param message Pre-constructed vibration message
         * @return true if data was sent successfully
         */
        bool SendVibrationRaw(const VibrationMessage& message);

    private:
        /**
         * @brief Attempts connection if retry interval has passed.
         * @return true if connected (either already or newly connected)
         */
        bool TryConnect();

        /**
         * @brief Writes data to the pipe.
         * @param data Pointer to data buffer
         * @param size Size of data in bytes
         * @return true if write successful
         */
        bool WriteToPipe(const void* data, DWORD size);

        HANDLE m_hPipe;                ///< Pipe handle
        const wchar_t* m_pipeName;     ///< Pipe name
        ULONGLONG m_lastConnectAttempt; ///< Tick count of last connection attempt
        bool m_connected;              ///< Connection state
    };

    /**
     * @brief Global pipe client instance for use by proxy functions.
     *
     * Initialized on first use (lazy initialization).
     */
    PipeClient& GetGlobalPipeClient();

} // namespace MX4Haptics

#endif // PIPE_CLIENT_H
