using System;
using System.Threading.Tasks;

namespace RelayControlWPF.Transport
{
    // A connected, line-oriented channel to the relay board: send an ASCII
    // command, receive ASCII reply lines. One implementation per physical
    // transport (Serial, BLE) so MainWindow's connect/send/receive logic
    // doesn't need to know which one is active.
    public interface IRelayTransport : IDisposable
    {
        // Raised for each newline-terminated reply line, on a background thread.
        event Action<string>? LineReceived;

        bool IsOpen { get; }

        // target is a transport-specific identifier: COM port name for
        // Serial, BLE device address (decimal string) for Ble.
        Task ConnectAsync(string target);

        Task SendAsync(string line);

        void Close();
    }
}
