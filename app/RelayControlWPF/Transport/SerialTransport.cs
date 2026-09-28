using System;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;

namespace RelayControlWPF.Transport
{
    public sealed class SerialTransport : IRelayTransport
    {
        private const int BaudRate = 9600;

        private SerialPort? _port;
        private Thread? _readThread;
        private volatile bool _stopReadThread;

        public event Action<string>? LineReceived;

        public bool IsOpen => _port?.IsOpen == true;

        public Task ConnectAsync(string target)
        {
            _port = new SerialPort(target, BaudRate)
            {
                NewLine = "\n",
                ReadTimeout = 1000,
                WriteTimeout = 1000
            };
            _port.Open();

            _stopReadThread = false;
            _readThread = new Thread(ReadLoop) { IsBackground = true };
            _readThread.Start();

            return Task.CompletedTask;
        }

        public Task SendAsync(string line)
        {
            var port = _port ?? throw new InvalidOperationException("Not connected.");
            return Task.Run(() => port.Write(line + "\n"));
        }

        // Runs on a background thread until the port closes or errors out.
        private void ReadLoop()
        {
            while (!_stopReadThread && _port != null && _port.IsOpen)
            {
                try
                {
                    string line = _port.ReadLine().Trim();
                    if (!string.IsNullOrWhiteSpace(line))
                        LineReceived?.Invoke(line);
                }
                catch (TimeoutException)
                {
                    // No data within the timeout window; loop and try again.
                }
                catch (Exception)
                {
                    break; // port likely closed/disconnected
                }
            }
        }

        public void Close()
        {
            _stopReadThread = true;

            if (_port != null && _port.IsOpen)
            {
                try { _port.Close(); } catch { /* ignore close errors on shutdown */ }
            }
            _port = null;
        }

        public void Dispose() => Close();
    }
}
