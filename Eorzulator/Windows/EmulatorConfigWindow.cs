using System;
using System.Collections.Generic;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using LibRetriX;

namespace Eorzulator.Windows;

public class EmulatorConfigWindow : Window, IDisposable {
    public ICore Core;
    private ImGuiKey key = ImGuiKey.Enter;
    private InputTypes inputType = InputTypes.DeviceIdJoypadA;

    public EmulatorConfigWindow(ICore core) : base($"Eorzulator Config - {core.Name}") {
        this.Core = core;
    }

    public void Dispose() { }

    public override void Draw() {
        this.EnumDropdown("##Key", ref this.key);
        this.EnumDropdown("##InputType", ref this.inputType);
        if (ImGuiComponents.IconButton(FontAwesomeIcon.Plus)) {
            if (!Plugin.Configuration.Keybinds.ContainsKey(this.Core.Name)) {
                Plugin.Configuration.Keybinds[this.Core.Name] = new();
            }

            Plugin.Configuration.Keybinds[this.Core.Name][this.inputType] = this.key;
            Configuration.Save();
        }

        ImGui.Separator();

        foreach (var key in Plugin.Configuration.Keybinds.GetValueOrDefault(this.Core.Name, new()).Keys) {
            using (ImRaii.PushId(key.ToString())) {
                ImGui.Text($"{key}: {Plugin.Configuration.Keybinds.GetValueOrDefault(this.Core.Name, new())[key]}");
                ImGui.SameLine();
                if (ImGuiComponents.IconButton(FontAwesomeIcon.TrashAlt)) {
                    Plugin.Configuration.Keybinds[this.Core.Name].Remove(key);
                    Configuration.Save();
                }
            }
        }
    }

    private void EnumDropdown<T>(string label, ref T value) where T : Enum {
        var values = Enum.GetValues(typeof(T));
        var names = Enum.GetNames(typeof(T));
        var index = Array.IndexOf(values, value);

        if (ImGui.BeginCombo(label, names[index])) {
            for (var i = 0; i < values.Length; i++) {
                if (ImGui.Selectable(names[i])) {
                    value = (T) values.GetValue(i)!;
                }
            }
            ImGui.EndCombo();
        }
    }
}
