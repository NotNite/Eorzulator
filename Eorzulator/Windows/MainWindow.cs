using System;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace Eorzulator.Windows;

public class MainWindow : Window, IDisposable {
    public MainWindow() : base("Eorzulator") {
        this.Flags = ImGuiWindowFlags.AlwaysAutoResize;
    }

    public override void Draw() {
        if (ImGuiComponents.IconButton(FontAwesomeIcon.Cog)) {
            Plugin.WindowManager.GetWindow<ConfigWindow>().IsOpen = true;
        }

        foreach (var core in Plugin.WindowManager.Cores) {
            if (ImGui.Button(core.Name)) {
                Plugin.WindowManager.GetEmulatorWindow(core).IsOpen = true;
            }
        }
    }

    public void Dispose() { }
}
