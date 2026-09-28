using System;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;

namespace RelayControlWPF.Transport
{
    // Talks to the same command protocol as SerialTransport, carried over a
    // Nordic UART Service (NUS)-compatible GATT profile - the same UUIDs the
    // ESP32-C3 firmware advertises.
    public sealed class BleTransport : IRelayTransport
    {
        private static readonly Guid ServiceUuid = Guid.Parse("6E400001-B5A3-F393-E0A9-E50E24DCCA9E");
        private static readonly Guid RxCharUuid = Guid.Parse("6E400002-B5A3-F393-E0A9-E50E24DCCA9E"); // app -> board
        private static readonly Guid TxCharUuid = Guid.Parse("6E400003-B5A3-F393-E0A9-E50E24DCCA9E"); // board -> app

        private BluetoothLEDevice? _device;
        private GattDeviceService? _service;
        private GattCharacteristic? _rxCharacteristic;
        private GattCharacteristic? _txCharacteristic;
        private readonly StringBuilder _lineBuffer = new();

        public event Action<string>? LineReceived;

        public bool IsOpen => _device != null && _device.ConnectionStatus == BluetoothConnectionStatus.Connected;

        public async Task ConnectAsync(string target)
        {
            ulong address = ulong.Parse(target);

            _device = await BluetoothLEDevice.FromBluetoothAddressAsync(address)
                ?? throw new InvalidOperationException("BLE device not found (it may be out of range or turned off).");

            var serviceResult = await _device.GetGattServicesForUuidAsync(ServiceUuid, BluetoothCacheMode.Uncached);
            if (serviceResult.Status != GattCommunicationStatus.Success || serviceResult.Services.Count == 0)
                throw new InvalidOperationException("Relay control BLE service not found on this device.");
            _service = serviceResult.Services[0];

            var rxResult = await _service.GetCharacteristicsForUuidAsync(RxCharUuid, BluetoothCacheMode.Uncached);
            var txResult = await _service.GetCharacteristicsForUuidAsync(TxCharUuid, BluetoothCacheMode.Uncached);
            if (rxResult.Status != GattCommunicationStatus.Success || rxResult.Characteristics.Count == 0 ||
                txResult.Status != GattCommunicationStatus.Success || txResult.Characteristics.Count == 0)
                throw new InvalidOperationException("Relay control BLE characteristics not found on this device.");

            _rxCharacteristic = rxResult.Characteristics[0];
            _txCharacteristic = txResult.Characteristics[0];

            _lineBuffer.Clear();
            _txCharacteristic.ValueChanged += OnValueChanged;
            var notifyStatus = await _txCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                GattClientCharacteristicConfigurationDescriptorValue.Notify);
            if (notifyStatus != GattCommunicationStatus.Success)
                throw new InvalidOperationException("Could not enable notifications on the relay control BLE service.");
        }

        // Runs on a background thread supplied by the OS Bluetooth stack.
        // Notifications aren't guaranteed to land on line boundaries, so
        // buffer and split the same way the Serial reader does.
        private void OnValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            var bytes = new byte[args.CharacteristicValue.Length];
            using DataReader reader = DataReader.FromBuffer(args.CharacteristicValue);
            reader.ReadBytes(bytes);

            lock (_lineBuffer)
            {
                foreach (byte b in bytes)
                {
                    char c = (char)b;
                    if (c == '\n')
                    {
                        string line = _lineBuffer.ToString().Trim();
                        _lineBuffer.Clear();
                        if (line.Length > 0)
                            LineReceived?.Invoke(line);
                    }
                    else if (c != '\r')
                    {
                        _lineBuffer.Append(c);
                    }
                }
            }
        }

        public async Task SendAsync(string line)
        {
            var characteristic = _rxCharacteristic ?? throw new InvalidOperationException("Not connected.");

            using var writer = new DataWriter();
            writer.WriteString(line + "\n");

            GattWriteOption writeOption = characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.WriteWithoutResponse)
                ? GattWriteOption.WriteWithoutResponse
                : GattWriteOption.WriteWithResponse;

            GattCommunicationStatus status = await characteristic.WriteValueAsync(writer.DetachBuffer(), writeOption);
            if (status != GattCommunicationStatus.Success)
                throw new InvalidOperationException($"BLE write failed ({status}).");
        }

        public void Close()
        {
            if (_txCharacteristic != null)
            {
                _txCharacteristic.ValueChanged -= OnValueChanged;
                _txCharacteristic = null;
            }
            _rxCharacteristic = null;

            // Windows only tears down the actual GATT connection once every
            // cached service/device object referencing it is disposed - an
            // undisposed GattDeviceService alone is enough to keep it alive
            // even after the BluetoothLEDevice is disposed. That leaves the
            // board never seeing a real disconnect (so its status LED stays
            // "connected" and it never resumes advertising), and blocks the
            // next reconnect attempt. Dispose child objects before the device.
            try { _service?.Dispose(); } catch { /* ignore close errors on shutdown */ }
            _service = null;

            try { _device?.Dispose(); } catch { /* ignore close errors on shutdown */ }
            _device = null;
        }

        public void Dispose() => Close();
    }
}
