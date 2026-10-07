using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RelayControlWPF.Presets
{
    public partial class PresetEditorView : UserControl
    {
        private readonly Guid _presetId;
        private readonly int _relayCount;
        private readonly List<PresetStep> _steps;
        private readonly List<TextBox> _delayTextBoxes = new();

        public event Action<Preset>? Saved;
        public event Action? Cancelled;

        public PresetEditorView(Preset? existing, int relayCount)
        {
            InitializeComponent();
            _relayCount = relayCount;

            if (existing != null)
            {
                _presetId = existing.Id;
                NameTextBox.Text = existing.Name;
                _steps = existing.Steps
                    .Select(s => new PresetStep { RelayNumber = s.RelayNumber, TurnOn = s.TurnOn, DelayMs = s.DelayMs })
                    .ToList();
                TitleTextBlock.Text = "Edit Preset";
            }
            else
            {
                _presetId = Guid.NewGuid();
                _steps = new List<PresetStep>();
                TitleTextBlock.Text = "New Preset";
            }

            RebuildStepsUI();
        }

        // ==================== BUILD STEP ROWS ====================
        // Rebuilds every row from _steps so visual order always matches the
        // backing list exactly - same "rebuild rather than patch" approach
        // MainWindow's relay-card grid already relies on for simplicity.
        private void RebuildStepsUI()
        {
            StepsPanel.Children.Clear();
            _delayTextBoxes.Clear();

            if (_steps.Count == 0)
            {
                StepsPanel.Children.Add(new TextBlock
                {
                    Text = "No steps yet. Click \"+ Add Step\" below.",
                    Style = (Style)FindResource("SubtitleText"),
                    Margin = new Thickness(0, 4, 0, 4)
                });
                return;
            }

            for (int i = 0; i < _steps.Count; i++)
            {
                StepsPanel.Children.Add(BuildStepRow(i));
            }
        }

        private Border BuildStepRow(int rowIndex)
        {
            var step = _steps[rowIndex];

            var row = new Border
            {
                Background = (Brush)FindResource("CardBrush"),
                BorderBrush = (Brush)FindResource("CardBorderBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 8)
            };

            var dock = new DockPanel();

            // Remove / Down / Up buttons, docked right so they stay visually
            // separate from the editable fields.
            var buttonsPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(buttonsPanel, Dock.Right);

            var upButton = SmallButton("▲", "SecondaryButton");
            upButton.IsEnabled = rowIndex > 0;
            upButton.Click += (s, e) => MoveStep(rowIndex, rowIndex - 1);

            var downButton = SmallButton("▼", "SecondaryButton");
            downButton.IsEnabled = rowIndex < _steps.Count - 1;
            downButton.Click += (s, e) => MoveStep(rowIndex, rowIndex + 1);

            var removeButton = SmallButton("✕", "DangerButton");
            removeButton.Click += (s, e) => RemoveStep(rowIndex);

            buttonsPanel.Children.Add(upButton);
            buttonsPanel.Children.Add(downButton);
            buttonsPanel.Children.Add(removeButton);
            dock.Children.Add(buttonsPanel);

            // Editable fields, filling the remaining space.
            var fieldsPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            fieldsPanel.Children.Add(new TextBlock
            {
                Text = $"{rowIndex + 1}.",
                Style = (Style)FindResource("SubtitleText"),
                Width = 22,
                VerticalAlignment = VerticalAlignment.Center
            });

            var relayCombo = new ComboBox
            {
                Style = (Style)FindResource("DarkComboBox"),
                Width = 110,
                Margin = new Thickness(0, 0, 8, 0)
            };
            for (int r = 1; r <= _relayCount; r++)
                relayCombo.Items.Add($"Relay {r}");
            relayCombo.SelectedIndex = Math.Clamp(step.RelayNumber - 1, 0, _relayCount - 1);
            relayCombo.SelectionChanged += (s, e) => step.RelayNumber = relayCombo.SelectedIndex + 1;
            fieldsPanel.Children.Add(relayCombo);

            var actionCombo = new ComboBox
            {
                Style = (Style)FindResource("DarkComboBox"),
                Width = 80,
                Margin = new Thickness(0, 0, 8, 0)
            };
            actionCombo.Items.Add("ON");
            actionCombo.Items.Add("OFF");
            actionCombo.SelectedIndex = step.TurnOn ? 0 : 1;
            actionCombo.SelectionChanged += (s, e) => step.TurnOn = actionCombo.SelectedIndex == 0;
            fieldsPanel.Children.Add(actionCombo);

            var delayBox = new TextBox
            {
                Style = (Style)FindResource("DarkTextBox"),
                Width = 90,
                MaxLength = 6,
                Text = step.DelayMs.ToString()
            };
            _delayTextBoxes.Add(delayBox);
            fieldsPanel.Children.Add(delayBox);

            fieldsPanel.Children.Add(new TextBlock
            {
                Text = "ms delay",
                Style = (Style)FindResource("SubtitleText"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0)
            });

            dock.Children.Add(fieldsPanel);
            row.Child = dock;
            return row;
        }

        private Button SmallButton(string content, string styleKey) => new Button
        {
            Content = content,
            Style = (Style)FindResource(styleKey),
            Width = 32,
            Height = 32,
            Padding = new Thickness(0),
            Margin = new Thickness(4, 0, 0, 0),
            FontSize = 12
        };

        // ==================== STEP LIST EDITING ====================
        private void MoveStep(int from, int to)
        {
            if (!CommitDelayEdits()) return;
            if (to < 0 || to >= _steps.Count) return;

            (_steps[from], _steps[to]) = (_steps[to], _steps[from]);
            RebuildStepsUI();
        }

        private void RemoveStep(int index)
        {
            if (!CommitDelayEdits()) return;
            _steps.RemoveAt(index);
            RebuildStepsUI();
        }

        private void AddStepButton_Click(object sender, RoutedEventArgs e)
        {
            if (!CommitDelayEdits()) return;
            _steps.Add(new PresetStep { RelayNumber = 1, TurnOn = true, DelayMs = 0 });
            RebuildStepsUI();
        }

        // Parses every visible delay TextBox back into _steps before any
        // reorder/remove/add/save, so in-progress edits are never silently
        // discarded by a rebuild. Stops at (and names) the first invalid row.
        private bool CommitDelayEdits()
        {
            for (int i = 0; i < _delayTextBoxes.Count; i++)
            {
                if (!int.TryParse(_delayTextBoxes[i].Text.Trim(), out int delay) || delay < 0)
                {
                    MessageBox.Show(
                        $"Step {i + 1}: delay must be a non-negative whole number of milliseconds.",
                        "Invalid delay", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
                _steps[i].DelayMs = delay;
            }
            return true;
        }

        // ==================== SAVE / CANCEL ====================
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!CommitDelayEdits()) return;

            string name = NameTextBox.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("Please enter a preset name.", "Name required",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_steps.Count == 0)
            {
                MessageBox.Show("Add at least one step before saving.", "No steps",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Saved?.Invoke(new Preset { Id = _presetId, Name = name, Steps = _steps });
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Cancelled?.Invoke();
        }
    }
}
