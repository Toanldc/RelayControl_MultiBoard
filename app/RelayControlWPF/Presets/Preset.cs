using System;
using System.Collections.Generic;

namespace RelayControlWPF.Presets
{
    public class Preset
    {
        public Guid Id { get; set; } = Guid.NewGuid(); // stable identity for edit/delete, independent of name
        public string Name { get; set; } = "";
        public List<PresetStep> Steps { get; set; } = new();
    }
}
