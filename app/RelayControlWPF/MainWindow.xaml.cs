using System;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using RelayControlWPF.Transport;

namespace RelayControlWPF
{
    public partial class MainWindow : Window
    {
        private const int RelayCount = 8;

        private enum ConnectionMode { Serial, Ble }

        private ConnectionMode _mode = ConnectionMode.Serial;
        private IRelayTransport? _transport;
        private BleScanner? _bleScanner;

        private bool _isConnected;
        private ManualResetEventSlim? _statusEvent;
        private string? _lastStatusPayload;
        private volatile bool _suppressRelayEvents;

        // UI elements created per relay, kept in arrays so we can update them by index.
        private readonly Ellipse[] _statusDots = new Ellipse[RelayCount];
        private readonly TextBlock[] _subtitles = new TextBlock[RelayCount];
        private readonly CheckBox[] _switches = new CheckBox[RelayCount];
        private readonly bool[] _relayState = new bool[RelayCount];

        public MainWindow()
        {
            InitializeComponent();
            BuildRelayCards();
            SerialModeRadio.IsChecked = true; // fires ModeRadio_Checked, which sets initial control visibility
            RefreshPorts();
            SetControlsEnabled(false);
        }

        // ==================== BUILD RELAY CARDS ====================
        // Creates identical cards in code, laid out in a 2-column grid, so the
        // layout, numbering, and event wiring for each relay stay consistent
        // and easy to extend.
        private void BuildRelayCards()
        {
            for (int i = 0; i < RelayCount; i++)
            {
                int relayIndex = i; // local copy for closure correctness

                var card = new Border
                {
                    Background = (Brush)FindResource("CardBrush"),
                    BorderBrush = (Brush)FindResource("CardBorderBrush"),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(14),
                    Padding = new Thickness(16),
                    Margin = new Thickness(0, 0, 12, 12)
                };

                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                // Status dot
                var dot = new Ellipse
                {
                    Width = 12,
                    Height = 12,
                    Fill = (Brush)FindResource("OffBrush"),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 14, 0)
                };
                Grid.SetColumn(dot, 0);
                grid.Children.Add(dot);
                _statusDots[relayIndex] = dot;

                // Name + subtitle
                var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                var nameText = new TextBlock
                {
                    Text = $"Relay {relayIndex + 1}",
                    Style = (Style)FindResource("RelayNameText")
                };
                var subtitleText = new TextBlock
                {
                    Text = "OFF",
                    Style = (Style)FindResource("SubtitleText"),
                    Margin = new Thickness(0, 2, 0, 0)
                };
                textStack.Children.Add(nameText);
                textStack.Children.Add(subtitleText);
                Grid.SetColumn(textStack, 1);
                grid.Children.Add(textStack);
                _subtitles[relayIndex] = subtitleText;

                // Toggle switch
                var toggle = new CheckBox
                {
                    Style = (Style)FindResource("ToggleSwitch"),
                    VerticalAlignment = VerticalAlignment.Center
                };
                toggle.Checked += (s, e) => OnRelayToggled(relayIndex, true);
                toggle.Unchecked += (s, e) => OnRelayToggled(relayIndex, false);
                Grid.SetColumn(toggle, 2);
                grid.Children.Add(toggle);
                _switches[relayIndex] = toggle;

                card.Child = grid;
                RelayListPanel.Children.Add(card);
            }
        }

        // ==================== CONNECTION MODE ====================
        private void ModeRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (_isConnected)
                Disconnect(); // switching transport while connected doesn't make sense

            _mode = SerialModeRadio.IsChecked == true ? ConnectionMode.Serial : ConnectionMode.Ble;
            PortComboBox.Visibility = _mode == ConnectionMode.Serial ? Visibility.Visible : Visibility.Collapsed;
            BleDeviceComboBox.Visibility = _mode == ConnectionMode.Ble ? Visibility.Visible : Visibility.Collapsed;
        }

        // ==================== PORT / DEVICE HANDLING ====================
        private void RefreshPorts()
        {
            PortComboBox.Items.Clear();
            string[] ports = SerialPort.GetPortNames();

            if (ports.Length == 0)
            {
                PortComboBox.Items.Add("No port found");
                PortComboBox.SelectedIndex = 0;
                return;
            }

            foreach (string port in ports)
            {
                PortComboBox.Items.Add(port);
            }
            PortComboBox.SelectedIndex = 0;
        }

        private void StartBleScan()
        {
            BleDeviceComboBox.Items.Clear();
            RefreshButton.IsEnabled = false;

            _bleScanner?.Dispose();
            _bleScanner = new BleScanner();
            _bleScanner.DeviceFound += OnBleDeviceFound;
            _bleScanner.Start();

            var scanTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
            scanTimer.Tick += (s, args) =>
            {
                scanTimer.Stop();
                _bleScanner?.Stop();
                RefreshButton.IsEnabled = true;

                if (BleDeviceComboBox.Items.Count == 0)
                {
                    BleDeviceComboBox.Items.Add("No device found");
                    BleDeviceComboBox.SelectedIndex = 0;
                }
            };
            scanTimer.Start();
        }

        private void OnBleDeviceFound(BleDeviceInfo device)
        {
            Dispatcher.Invoke(() =>
            {
                BleDeviceComboBox.Items.Add(device);
                if (BleDeviceComboBox.Items.Count == 1)
                    BleDeviceComboBox.SelectedIndex = 0;
            });
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            if (_mode == ConnectionMode.Serial)
                RefreshPorts();
            else
                StartBleScan();
        }

        // ==================== CONNECT / DISCONNECT ====================
        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isConnected)
                Connect();
            else
                Disconnect();
        }

        private void Connect()
        {
            string target;
            string targetLabel;

            if (_mode == ConnectionMode.Serial)
            {
                string? portName = PortComboBox.SelectedItem as string;
                if (string.IsNullOrEmpty(portName) || portName == "No port found")
                {
                    MessageBox.Show("Please select a COM port before connecting.", "No COM port",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                target = portName;
                targetLabel = portName;
            }
            else
            {
                if (BleDeviceComboBox.SelectedItem is not BleDeviceInfo device)
                {
                    MessageBox.Show("Please scan and select a BLE device before connecting.", "No BLE device",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                target = device.Address.ToString();
                targetLabel = device.Name;
            }

            ConnectButton.IsEnabled = false;
            ConnectButton.Content = "Connecting...";
            StatusText.Text = $"Connecting to {targetLabel}...";

            _transport = _mode == ConnectionMode.Serial ? new SerialTransport() : new BleTransport();
            _transport.LineReceived += OnTransportLineReceived;

            var connectThread = new Thread(() => ConnectWorker(target, targetLabel)) { IsBackground = true };
            connectThread.Start();
        }

        // Runs on a background thread so the UI doesn't freeze while we wait
        // for the board's boot handshake or probe it with STATUS. Blocking on
        // the transport's async calls here is safe because a plain background
        // Thread has no SynchronizationContext for their continuations to
        // deadlock on.
        private void ConnectWorker(string target, string targetLabel)
        {
            try
            {
                _transport!.ConnectAsync(target).GetAwaiter().GetResult();

                _isConnected = true;
                _statusEvent = new ManualResetEventSlim(false);
                _lastStatusPayload = null;

                // The board firmware resets/advertises after connect, so a command
                // sent right away can be missed. Retry STATUS a few times until it
                // replies - this both confirms it's the relay board and returns the
                // real relay states, which we then use to sync the UI toggles.
                bool identified = false;
                for (int attempt = 0; attempt < 5 && !identified; attempt++)
                {
                    SendCommand("STATUS");
                    identified = _statusEvent.Wait(800);
                }

                string? statusPayload = _lastStatusPayload;
                Dispatcher.Invoke(() => FinishConnect(targetLabel, identified, statusPayload));
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    ConnectButton.IsEnabled = true;
                    ConnectButton.Content = "Connect";
                    StatusText.Text = "Not connected";
                    MessageBox.Show($"Could not connect to {targetLabel}:\n{ex.Message}", "Connection error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
        }

        private void FinishConnect(string targetLabel, bool identified, string? statusPayload)
        {
            ConnectButton.IsEnabled = true;

            if (!identified)
            {
                var result = MessageBox.Show(
                    $"{targetLabel} did not respond with the expected Relay Controller protocol " +
                    "(no STATUS reply received).\n\n" +
                    "You may have selected the wrong port/device, or it is not running the relay firmware.\n\n" +
                    "Continue connecting anyway?",
                    "Device not verified",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (result == MessageBoxResult.No)
                {
                    Disconnect();
                    return;
                }

                StatusDot.Fill = (Brush)FindResource("WarningBrush");
                StatusText.Text = $"Connected ({targetLabel}) — unverified";
            }
            else
            {
                StatusDot.Fill = (Brush)FindResource("OnBrush");
                StatusText.Text = $"Connected ({targetLabel}) — verified ✓";
                ApplyInitialRelayStates(statusPayload);
            }

            ConnectButton.Content = "Disconnect";
            ConnectButton.Background = (Brush)FindResource("DangerBrush");

            SetControlsEnabled(true);
        }

        // Syncs the toggle switches to the relay states reported by the board
        // (from its "STATUS:10101010" reply) without re-sending commands back to it.
        private void ApplyInitialRelayStates(string? statusPayload)
        {
            if (string.IsNullOrEmpty(statusPayload))
                return;

            if (statusPayload.Length != RelayCount)
            {
                // Most likely cause: the board is still running older firmware with a
                // different relay count (re-flash it) rather than a real protocol error.
                StatusText.Text += $" — state NOT synced (board reported {statusPayload.Length} relays, app expects {RelayCount}; re-flash the board firmware)";
                return;
            }

            _suppressRelayEvents = true;
            try
            {
                for (int i = 0; i < RelayCount; i++)
                {
                    bool state = statusPayload[i] == '1';
                    _switches[i].IsChecked = state;
                    _relayState[i] = state;
                    UpdateRelayVisual(i, state);
                }
            }
            finally
            {
                _suppressRelayEvents = false;
            }
        }

        private void Disconnect()
        {
            if (_transport != null)
            {
                _transport.LineReceived -= OnTransportLineReceived;
                _transport.Close();
                _transport.Dispose();
                _transport = null;
            }

            _isConnected = false;
            _statusEvent?.Dispose();
            _statusEvent = null;

            StatusDot.Fill = (Brush)FindResource("DangerBrush");
            StatusText.Text = "Not connected";
            ConnectButton.Content = "Connect";
            ConnectButton.Background = (Brush)FindResource("AccentBrush");
            ConnectButton.IsEnabled = true;

            SetControlsEnabled(false);
        }

        private void SetControlsEnabled(bool enabled)
        {
            foreach (var sw in _switches)
                sw.IsEnabled = enabled;

            AllOnButton.IsEnabled = enabled;
            AllOffButton.IsEnabled = enabled;
        }

        // ==================== TRANSPORT EVENTS ====================
        // Raised on a background thread by whichever transport is active.
        // Any UI update from here must be marshalled back via Dispatcher.Invoke.
        private void OnTransportLineReceived(string line)
        {
            System.Diagnostics.Debug.WriteLine($"Board -> {line}");

            // A STATUS reply confirms we're actually talking to the relay
            // firmware (not some other device) and carries the real relay states.
            if (line.StartsWith("STATUS:"))
            {
                _lastStatusPayload = line.Substring("STATUS:".Length);
                _statusEvent?.Set();
            }
        }

        // ==================== SENDING COMMANDS ====================
        private void SendCommand(string cmd)
        {
            if (_isConnected && _transport != null && _transport.IsOpen)
            {
                _ = SendCommandAsync(cmd);
            }
        }

        private async Task SendCommandAsync(string cmd)
        {
            try
            {
                await _transport!.SendAsync(cmd);
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(ex.Message, "Send error", MessageBoxButton.OK, MessageBoxImage.Error);
                    Disconnect();
                });
            }
        }

        private void OnRelayToggled(int relayIndex, bool state)
        {
            _relayState[relayIndex] = state;
            UpdateRelayVisual(relayIndex, state);

            // Toggles set programmatically from ApplyInitialRelayStates only sync
            // the UI to the board's reported state - it's already there, so don't echo a command back.
            if (_suppressRelayEvents)
                return;

            string cmd = $"R{relayIndex + 1}{(state ? "ON" : "OFF")}";
            SendCommand(cmd);
        }

        private void UpdateRelayVisual(int relayIndex, bool state)
        {
            _statusDots[relayIndex].Fill = (Brush)FindResource(state ? "OnBrush" : "OffBrush");
            _subtitles[relayIndex].Text = state ? "ON" : "OFF";
            _subtitles[relayIndex].Foreground = state
                ? (Brush)FindResource("OnBrush")
                : (Brush)FindResource("TextSecondaryBrush");
        }

        // ==================== BOTTOM ACTIONS ====================
        private void AllOnButton_Click(object sender, RoutedEventArgs e) => SetAll(true);

        private void AllOffButton_Click(object sender, RoutedEventArgs e) => SetAll(false);

        private void SetAll(bool state)
        {
            for (int i = 0; i < RelayCount; i++)
            {
                // Setting IsChecked triggers Checked/Unchecked, which calls OnRelayToggled
                // and sends the command, so we don't need to send it again here.
                _switches[i].IsChecked = state;
                Thread.Sleep(50); // avoid flooding the board with commands too fast
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            Disconnect();
            _bleScanner?.Dispose();
            base.OnClosed(e);
        }
    }
}
