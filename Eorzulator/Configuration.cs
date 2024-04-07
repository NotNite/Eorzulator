using Dalamud.Configuration;
using System;
using System.Collections.Generic;
using ImGuiNET;
using LibRetriX;
using Newtonsoft.Json;

namespace Eorzulator;

[Serializable]
public class Configuration : IPluginConfiguration {
    [JsonIgnore]
    private static Dictionary<InputTypes, ImGuiKey> SticklessDefaultKeybinds = new() {
        {InputTypes.DeviceIdJoypadUp, ImGuiKey.UpArrow},
        {InputTypes.DeviceIdJoypadDown, ImGuiKey.DownArrow},
        {InputTypes.DeviceIdJoypadLeft, ImGuiKey.LeftArrow},
        {InputTypes.DeviceIdJoypadRight, ImGuiKey.RightArrow},

        {InputTypes.DeviceIdJoypadA, ImGuiKey.Z},
        {InputTypes.DeviceIdJoypadB, ImGuiKey.X},
        {InputTypes.DeviceIdJoypadX, ImGuiKey.A},
        {InputTypes.DeviceIdJoypadY, ImGuiKey.S},
        {InputTypes.DeviceIdJoypadL, ImGuiKey.Q},
        {InputTypes.DeviceIdJoypadR, ImGuiKey.W},

        {InputTypes.DeviceIdJoypadStart, ImGuiKey.Enter},
        {InputTypes.DeviceIdJoypadSelect, ImGuiKey.Backspace}
    };

    public int Version { get; set; } = 0;

    public Dictionary<string, Dictionary<InputTypes, ImGuiKey>> Keybinds { get; set; } = new() {
        {"melonDS", SticklessDefaultKeybinds},
        {"Nestopia", SticklessDefaultKeybinds},
        {"Snes9x", SticklessDefaultKeybinds},
        {"mGBA", SticklessDefaultKeybinds}
    };

    public static void Save() {
        Services.PluginInterface.SavePluginConfig(Plugin.Configuration);
    }
}
