using System;
using System.IO.Ports;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace RelayControlWPF
{
    public partial class MainWindow : Window
    {
        private const int RelayCount = 8;
        private const int BaudRate = 9600;

        private SerialPort? _serialPort;
        private bool _isConnected;
        private Thread? _readThread;
        private volatile bool _stopReadThread;
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

        // ==================== PORT HANDLING ====================
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

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshPorts();
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
            string? portName = PortComboBox.SelectedItem as string;
            if (string.IsNullOrEmpty(portName) || portName == "No port found")
            {
                MessageBox.Show("Please select a COM port before connecting.", "No COM port",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ConnectButton.IsEnabled = false;
            ConnectButton.Content = "Connecting...";
            StatusText.Text = $"Connecting to {portName}...";

            var connectThread = new Thread(() => ConnectWorker(portName)) { IsBackground = true };
            connectThread.Start();
        }

        // Runs on a background thread so the UI doesn't freeze while we wait
        // for the board's boot handshake or probe it with STATUS.
        private void ConnectWorker(string portName)
        {
            try
            {
                _serialPort = new SerialPort(portName, BaudRate)
                {
                    NewLine = "\n",
                    ReadTimeout = 1000,
                    WriteTimeout = 1000
                };
                _serialPort.Open();

                _isConnected = true;
                _statusEvent = new ManualResetEventSlim(false);
                _lastStatusPayload = null;
                _stopReadThread = false;
                _readThread = new Thread(ReadSerialLoop) { IsBackground = true };
                _readThread.Start();

                // The Nano auto-resets when the port opens, so a command sent right
                // away can be lost during boot. Retry STATUS a few times until it
                // replies - this both confirms it's the relay board and returns the
                // real relay states, which we then use to sync the UI toggles.
                bool identified = false;
                for (int attempt = 0; attempt < 5 && !identified; attempt++)
                {
                    SendCommand("STATUS");
                    identified = _statusEvent.Wait(800);
                }

                string? statusPayload = _lastStatusPayload;
                Dispatcher.Invoke(() => FinishConnect(portName, identified, statusPayload));
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    ConnectButton.IsEnabled = true;
                    ConnectButton.Content = "Connect";
                    StatusText.Text = "Not connected";
                    MessageBox.Show($"Could not open port {portName}:\n{ex.Message}", "Connection error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
        }

        private void FinishConnect(string portName, bool identified, string? statusPayload)
        {
            ConnectButton.IsEnabled = true;

            if (!identified)
            {
                var result = MessageBox.Show(
                    $"Port {portName} did not respond with the expected Relay Controller protocol " +
                    "(no STATUS reply received).\n\n" +
                    "You may have selected the wrong COM port, or the device is not running the relay firmware.\n\n" +
                    "Continue connecting anyway?",
                    "Device not verified",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (result == MessageBoxResult.No)
                {
                    Disconnect();
                    return;
                }

                StatusDot.Fill = (Brush)FindResource("WarningBrush");
                StatusText.Text = $"Connected ({portName}) — unverified";
            }
            else
            {
                StatusDot.Fill = (Brush)FindResource("OnBrush");
                StatusText.Text = $"Connected ({portName}) — verified ✓";
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
            _stopReadThread = true;

            if (_serialPort != null && _serialPort.IsOpen)
            {
                try { _serialPort.Close(); } catch { /* ignore close errors on shutdown */ }
            }
            _serialPort = null;

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

        // ==================== SERIAL READ THREAD ====================
        // Runs on a background thread. Any UI update from here must be marshalled
        // back to the UI thread via Dispatcher.Invoke.
        private void ReadSerialLoop()
        {
            while (!_stopReadThread && _serialPort != null && _serialPort.IsOpen)
            {
                try
                {
                    string line = _serialPort.ReadLine().Trim();
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        Dispatcher.Invoke(() => System.Diagnostics.Debug.WriteLine($"Nano -> {line}"));

                        // A STATUS reply confirms we're actually talking to the relay
                        // firmware (not some other device) and carries the real relay states.
                        if (line.StartsWith("STATUS:"))
                        {
                            _lastStatusPayload = line.Substring("STATUS:".Length);
                            _statusEvent?.Set();
                        }
                    }
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

        // ==================== SENDING COMMANDS ====================
        private void SendCommand(string cmd)
        {
            if (_isConnected && _serialPort != null && _serialPort.IsOpen)
            {
                try
                {
                    _serialPort.Write(cmd + "\n");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Send error", MessageBoxButton.OK, MessageBoxImage.Error);
                    Disconnect();
                }
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
                Thread.Sleep(50); // avoid flooding the Nano with commands too fast
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            Disconnect();
            base.OnClosed(e);
        }
    }
}
