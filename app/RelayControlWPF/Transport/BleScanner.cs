using System;
using System.Collections.Generic;
using Windows.Devices.Bluetooth.Advertisement;

namespace RelayControlWPF.Transport
{
    // A discovered relay board, identified by its BLE address (used to
    // reconnect via BluetoothLEDevice.FromBluetoothAddressAsync).
    public sealed record BleDeviceInfo(ulong Address, string Name)
    {
        public override string ToString() => Name; // shown directly by the ComboBox
    }

    // Watches for BLE advertisements matching the NUS-compatible service
    // UUID the relay control firmware advertises. That match is already a
    // reliable identifier on its own, so devices are labeled with a fixed
    // friendly name rather than the advertised LocalName: the firmware
    // defers the name to the scan response to stay under BLE's 31-byte
    // legacy advertising payload limit, and on some Bluetooth adapters/
    // drivers (observed with a Realtek stack) Windows never issues the scan
    // request needed to retrieve it, even with active scanning enabled.
    public sealed class BleScanner : IDisposable
    {
        private static readonly Guid ServiceUuid = Guid.Parse("6E400001-B5A3-F393-E0A9-E50E24DCCA9E");

        private readonly BluetoothLEAdvertisementWatcher _watcher = new();
        private readonly HashSet<ulong> _seen = new();

        // Raised on a background thread for each newly seen device.
        public event Action<BleDeviceInfo>? DeviceFound;

        public BleScanner()
        {
            _watcher.AdvertisementFilter.Advertisement.ServiceUuids.Add(ServiceUuid);
            _watcher.Received += OnAdvertisementReceived;
        }

        public void Start()
        {
            _seen.Clear();
            _watcher.Start();
        }

        public void Stop()
        {
            if (_watcher.Status == BluetoothLEAdvertisementWatcherStatus.Started)
                _watcher.Stop();
        }

        private void OnAdvertisementReceived(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
        {
            if (!_seen.Add(args.BluetoothAddress))
                return; // already reported this scan

            string name = $"RelayControl ({args.BluetoothAddress:X12})";
            DeviceFound?.Invoke(new BleDeviceInfo(args.BluetoothAddress, name));
        }

        public void Dispose()
        {
            _watcher.Received -= OnAdvertisementReceived;
            Stop();
        }
    }
}
