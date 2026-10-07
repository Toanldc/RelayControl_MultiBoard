namespace RelayControlWPF.Presets
{
    public class PresetStep
    {
        public int RelayNumber { get; set; } = 1; // 1-based, matches "R<n>ON"/"R<n>OFF" and the "Relay N" UI label
        public bool TurnOn { get; set; } = true;
        public int DelayMs { get; set; } = 0; // delay before this step fires, >= 0
    }
}
