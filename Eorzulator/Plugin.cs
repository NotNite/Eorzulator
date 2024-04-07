using System;
using System.Collections.Generic;
using System.IO;
using Dalamud.Game.Command;
using Dalamud.Plugin;
using Eorzulator.Windows;
using LibRetriX;

namespace Eorzulator;

public sealed class Plugin : IDalamudPlugin {
    public const string CommandName = "/eorzulator";

    public static WindowManager WindowManager = new();
    public static Configuration Configuration = null!;
    public static List<ICore> Cores = new();

    public Plugin(DalamudPluginInterface pluginInterface) {
        pluginInterface.Create<Services>();

        Configuration = Services.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Configuration.Save();

        WindowManager.AddWindow(new MainWindow());
        WindowManager.AddWindow(new ConfigWindow());

        PreloadDependencies();
        AddCore(LibRetriX.MelonDS.Core.Instance);
        AddCore(LibRetriX.Nestopia.Core.Instance);
        AddCore(LibRetriX.Snes9X.Core.Instance);
        AddCore(LibRetriX.MGBA.Core.Instance);

        Services.CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand) {
            HelpMessage = "Open the emulator selection window"
        });

        Services.PluginInterface.UiBuilder.Draw += this.DrawUi;
        Services.PluginInterface.UiBuilder.OpenMainUi += this.OpenMainUi;
    }

    public void Dispose() {
        Services.PluginInterface.UiBuilder.OpenMainUi -= this.OpenMainUi;
        Services.PluginInterface.UiBuilder.Draw -= this.DrawUi;
        Services.CommandManager.RemoveHandler(CommandName);

        foreach (var core in Cores) {
            if (core is IDisposable disposable) disposable.Dispose();
        }
        Cores.Clear();

        WindowManager.Dispose();
    }

    private static void PreloadDependencies() {
        string[] files = ["MelonDS", "Nestopia", "Snes9X", "MGBA"];
        foreach (var file in files) {
            var path = Path.Combine(
                Services.PluginInterface.AssemblyLocation.DirectoryName!,
                $"{file}.dll"
            );
            PathResolvers.Paths[file] = path;
        }
    }

    private static void AddCore(ICore core) {
        Cores.Add(core);
        WindowManager.AddWindow(new EmulatorWindow(core));
        WindowManager.AddWindow(new EmulatorConfigWindow(core));

        var configPath = Services.PluginInterface.GetPluginConfigDirectory();
        core.SaveRootPath = Path.Combine(
            configPath,
            "saves",
            core.Name
        );
        core.SystemRootPath = Path.Combine(
            configPath,
            "system",
            core.Name
        );
        Directory.CreateDirectory(core.SaveRootPath);
        Directory.CreateDirectory(core.SystemRootPath);
    }

    private void OnCommand(string command, string args) {
        if (args == "config" || args == "settings") {
            WindowManager.GetWindow<ConfigWindow>().IsOpen = true;
            return;
        }

        foreach (var core in Cores) {
            if (core.Name.ToLower() == args.ToLower()) {
                WindowManager.GetEmulatorWindow(core).IsOpen = true;
                return;
            }
        }

        this.OpenMainUi();
    }

    private void DrawUi() {
        WindowManager.Draw();
    }

    public void OpenMainUi() {
        WindowManager.GetWindow<MainWindow>().IsOpen = true;
    }
}
