using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Windowing;
using Eorzulator.Windows;
using LibRetriX;

namespace Eorzulator;

public class WindowManager : IDisposable {
    public WindowSystem WindowSystem = new WindowSystem("Eorzulator");
    public FileDialogManager FileDialogManager = new();
    private List<Window> windows = new();

    public List<ICore> Cores =>
        this.windows.Where(w => w is EmulatorWindow).Select(w => ((EmulatorWindow) w).Core).ToList();

    public void AddWindow(Window window) {
        this.windows.Add(window);
        this.WindowSystem.AddWindow(window);
    }


    public void RemoveWindow(Window window) {
        this.windows.Remove(window);
        this.WindowSystem.RemoveWindow(window);
    }

    public T GetWindow<T>() where T : Window {
        foreach (var w in this.windows) {
            if (w is T t) return t;
        }

        throw new Exception($"Window of type {typeof(T)} not found.");
    }

    public EmulatorWindow GetEmulatorWindow<T>(T core) where T : ICore {
        foreach (var w in this.windows) {
            if (w is EmulatorWindow emulatorWindow && emulatorWindow.Core == (ICore) core) {
                return emulatorWindow;
            }
        }

        throw new Exception($"Window for core {core} not found.");
    }

    public EmulatorConfigWindow GetConfigWindow<T>(T core) where T : ICore {
        foreach (var w in this.windows) {
            if (w is EmulatorConfigWindow configWindow && configWindow.Core == (ICore) core) {
                return configWindow;
            }
        }

        throw new Exception($"Window for core {core} not found.");
    }

    public void Draw() {
        this.WindowSystem.Draw();
        this.FileDialogManager.Draw();
    }

    public void Dispose() {
        foreach (var window in this.windows) {
            if (window is IDisposable disposable) disposable.Dispose();
        }
        this.windows.Clear();
        this.WindowSystem.RemoveAllWindows();
        this.FileDialogManager.Reset();
    }
}
