using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace RelayControlWPF.Presets
{
    internal static class PresetStore
    {
        private static readonly string FolderPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RelayControlWPF");
        private static readonly string FilePath = Path.Combine(FolderPath, "presets.json");

        internal static List<Preset> Load()
        {
            if (!File.Exists(FilePath))
                return new List<Preset>();

            try
            {
                string json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<List<Preset>>(json) ?? new List<Preset>();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"The presets file could not be read and will be reset:\n{ex.Message}",
                    "Presets file corrupt", MessageBoxButton.OK, MessageBoxImage.Warning);
                return new List<Preset>();
            }
        }

        internal static void Save(List<Preset> presets)
        {
            Directory.CreateDirectory(FolderPath);
            string json = JsonSerializer.Serialize(presets, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FilePath, json);
        }
    }
}
