using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RelayControlWPF.Presets
{
    public partial class PresetsView : UserControl
    {
        private readonly List<Preset> _presets;
        private readonly int _relayCount;
        private readonly Func<bool> _isConnected;
        private readonly Action<Preset> _runPreset;

        // _presets is the SAME list instance MainWindow holds (not a copy), so
        // every add/edit/delete here is immediately visible to MainWindow too;
        // this view just owns persisting it after each change.
        public PresetsView(List<Preset> presets, int relayCount, Func<bool> isConnected, Action<Preset> runPreset)
        {
            InitializeComponent();
            _presets = presets;
            _relayCount = relayCount;
            _isConnected = isConnected;
            _runPreset = runPreset;

            RefreshPresetList();
        }

        private void RefreshPresetList()
        {
            PresetListPanel.Children.Clear();

            if (_presets.Count == 0)
            {
                PresetListPanel.Children.Add(new TextBlock
                {
                    Text = "No presets yet. Click \"+ New Preset\" to create one.",
                    Style = (Style)FindResource("SubtitleText"),
                    Margin = new Thickness(0, 8, 0, 0)
                });
                return;
            }

            foreach (var preset in _presets)
            {
                PresetListPanel.Children.Add(BuildPresetRow(preset));
            }
        }

        private Border BuildPresetRow(Preset preset)
        {
            var row = new Border
            {
                Background = (Brush)FindResource("CardBrush"),
                BorderBrush = (Brush)FindResource("CardBorderBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(16),
                Margin = new Thickness(0, 0, 0, 10)
            };

            var dock = new DockPanel();

            var buttonsPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(buttonsPanel, Dock.Right);

            var runButton = RowButton("Run", "FlatButton");
            runButton.Background = (Brush)FindResource("OnBrush");
            runButton.Click += (s, e) =>
            {
                if (!_isConnected())
                {
                    MessageBox.Show("Connect to a board before running a preset.", "Not connected",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                _runPreset(preset);
            };

            var editButton = RowButton("Edit", "SecondaryButton");
            editButton.Click += (s, e) => ShowEditor(preset);

            var deleteButton = RowButton("Delete", "DangerButton");
            deleteButton.Click += (s, e) => DeletePreset(preset);

            buttonsPanel.Children.Add(runButton);
            buttonsPanel.Children.Add(editButton);
            buttonsPanel.Children.Add(deleteButton);
            dock.Children.Add(buttonsPanel);

            var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            textStack.Children.Add(new TextBlock
            {
                Text = preset.Name,
                Style = (Style)FindResource("RelayNameText")
            });
            textStack.Children.Add(new TextBlock
            {
                Text = $"{preset.Steps.Count} step{(preset.Steps.Count == 1 ? "" : "s")}",
                Style = (Style)FindResource("SubtitleText"),
                Margin = new Thickness(0, 2, 0, 0)
            });
            dock.Children.Add(textStack);

            row.Child = dock;
            return row;
        }

        private Button RowButton(string content, string styleKey) => new Button
        {
            Content = content,
            Style = (Style)FindResource(styleKey),
            Height = 36,
            MinWidth = 64,
            Padding = new Thickness(14, 0, 14, 0),
            Margin = new Thickness(8, 0, 0, 0),
            FontSize = 14
        };

        // ==================== LIST <-> EDITOR ====================
        private void NewPresetButton_Click(object sender, RoutedEventArgs e) => ShowEditor(null);

        private void ShowEditor(Preset? existing)
        {
            var editor = new PresetEditorView(existing, _relayCount);

            editor.Saved += saved =>
            {
                if (existing == null)
                {
                    _presets.Add(saved);
                }
                else
                {
                    int index = _presets.FindIndex(p => p.Id == existing.Id);
                    if (index >= 0)
                        _presets[index] = saved;
                }

                PresetStore.Save(_presets);
                ShowList();
            };
            editor.Cancelled += ShowList;

            EditorHost.Children.Clear();
            EditorHost.Children.Add(editor);
            ListHost.Visibility = Visibility.Collapsed;
            EditorHost.Visibility = Visibility.Visible;
        }

        private void ShowList()
        {
            EditorHost.Children.Clear();
            EditorHost.Visibility = Visibility.Collapsed;
            ListHost.Visibility = Visibility.Visible;
            RefreshPresetList();
        }

        private void DeletePreset(Preset preset)
        {
            var result = MessageBox.Show(
                $"Delete preset \"{preset.Name}\"? This cannot be undone.",
                "Delete preset", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            _presets.RemoveAll(p => p.Id == preset.Id);
            PresetStore.Save(_presets);
            RefreshPresetList();
        }
    }
}
