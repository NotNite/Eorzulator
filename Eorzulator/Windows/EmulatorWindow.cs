using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Timers;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using LibRetriX;
using Task = System.Threading.Tasks.Task;

namespace Eorzulator.Windows;

public class EmulatorWindow : Window, IDisposable {
    public ICore Core;
    private bool running = false;
    private bool lockInput = false;

    private Timer timer = new(1000f / 60f);
    private bool runFrame = false;

    private TextureStuff.EmulatorTexture? texture;
    private Dictionary<ImGuiKey, bool> keyStates = new();
    private Vector2 mousePos = Vector2.Zero;
    private Vector2 savedMousePos = Vector2.Zero;
    private bool mouseClick = false;

    public EmulatorWindow(ICore core) : base($"Eorzulator - {core.Name}") {
        this.RespectCloseHotkey = false;
        this.Core = core;
        this.Core.GetInputState = this.GetInputState;
        this.Core.OpenFileStream = (path, access) => File.Open(path, FileMode.Open, access);
        this.Core.CloseFileStream = stream => stream.Close();
        this.Core.RenderAudioFrames += this.RenderAudioFrames;
        this.Core.RenderVideoFrame += this.RenderVideoFrame;
        this.Core.GeometryChanged += this.GeometryChanged;
        this.timer.Elapsed += this.TimerElapsed;
        this.timer.Start();
    }

    public override void PreDraw() {
        this.Flags = this.lockInput ? ImGuiWindowFlags.NoMove : ImGuiWindowFlags.None;
    }

    public void Dispose() {
        this.Shutdown();
        this.texture?.Dispose();
        this.Core.RenderAudioFrames -= this.RenderAudioFrames;
        this.Core.RenderVideoFrame -= this.RenderVideoFrame;
        this.Core.GeometryChanged -= this.GeometryChanged;
        this.texture?.Dispose();
        this.timer.Elapsed -= this.TimerElapsed;
        this.timer.Stop();
    }

    private void TimerElapsed(object? sender, ElapsedEventArgs e) {
        this.runFrame = true;
    }

    private void GeometryChanged(GameGeometry geometry) {
        this.texture?.Dispose();
        this.texture = TextureStuff.GetTexture((int)geometry.BaseWidth, (int)geometry.BaseHeight);
    }

    private void PollInput() {
        foreach (var key in Plugin.Configuration.Keybinds
                     .GetValueOrDefault(this.Core.Name, new())
                     .Values) {
            var state = ImGui.IsKeyDown(key);
            this.keyStates[key] = state;
        }

        this.mouseClick = ImGui.IsMouseDown(ImGuiMouseButton.Left);
    }

    private short GetInputState(uint port, InputTypes inputType) {
        if (port != 0) return 0;
        if (Plugin.Configuration.Keybinds
            .GetValueOrDefault(this.Core.Name, new())
            .TryGetValue(inputType, out var key)) {
            return this.keyStates.GetValueOrDefault(key) ? (short)1 : (short)0;
        }

        if (inputType is InputTypes.DeviceIdMouseX or InputTypes.DeviceIdPointerX) {
            var diff = this.mousePos.X - this.savedMousePos.X;
            this.savedMousePos.X = this.mousePos.X;
            return (short)diff;
        }

        if (inputType is InputTypes.DeviceIdMouseY or InputTypes.DeviceIdPointerY) {
            var diff = this.mousePos.Y - this.savedMousePos.Y;
            this.savedMousePos.Y = this.mousePos.Y;
            return (short)diff;
        }

        if (inputType is InputTypes.DeviceIdMouseLeft or InputTypes.DeviceIdPointerPressed) {
            return this.mouseClick ? (short)1 : (short)0;
        }

        return 0;
    }

    private uint RenderAudioFrames(ReadOnlySpan<short> data, uint numframes) {
        return numframes;
    }

    private void RenderVideoFrame(ReadOnlySpan<byte> data, uint width, uint height, uint pitch) {
        var bytes = data.ToArray();
        bytes = TextureStuff.FixTexture(bytes, width, height, pitch, this.Core.PixelFormat);

        // lol what


        this.texture?.Mutate((box, stream) => {
            unsafe {
                var dst = (byte*)box.DataPointer;
                var pitch = box.RowPitch;
                const int pixelSize = 4;
                TextureStuff.CopyTexture2D(bytes, dst, width, height, pixelSize, (uint)pitch);
            }
        });
    }

    public override void Draw() {
        if (this.running && this.IsFocused) {
            this.PollInput();
        }

        try {
            if (this.running && this.runFrame) {
                this.Core.RunFrame();
                this.runFrame = false;
            }
        } catch (Exception e) {
            Services.PluginLog.Error(e, "Failed to run frame");
        }

        if (ImGuiComponents.IconButton(FontAwesomeIcon.Play)) {
            Plugin.WindowManager.FileDialogManager.OpenFileDialog(
                "Select ROM",
                "{" + string.Join(",", this.Core.SupportedExtensions) + "}",
                (success, path) => {
                    if (!success) return;
                    if (this.running) this.Shutdown();
                    Services.PluginLog.Debug($"Loading game: {path}");
                    var ret = this.Core.LoadGame(path);
                    Services.PluginLog.Debug(ret ? "Loaded game" : "Failed to load game");
                    if (ret) this.running = true;
                }
            );
        }

        ImGui.SameLine();

        var stopped = !this.running;
        if (stopped) ImGui.BeginDisabled();
        try {
            if (ImGuiComponents.IconButton(FontAwesomeIcon.Stop)) {
                Task.Run(this.Shutdown);
            }
        } finally {
            if (stopped) ImGui.EndDisabled();
        }

        ImGui.SameLine();

        if (ImGuiComponents.IconButton(FontAwesomeIcon.Cog)) {
            Plugin.WindowManager.GetConfigWindow(this.Core).IsOpen = true;
        }

        ImGui.Separator();

        if (this.texture != null) {
            var pos = ImGui.GetCursorScreenPos();
            ImGui.Image(this.texture.Handle, new Vector2(this.texture.Width, this.texture.Height));
            var mouse = ImGui.GetMousePos();
            var newMousePos = mouse - pos;

            var bounds = new Vector2(this.texture.Width, this.texture.Height);

            // HACK: DS touchscreens are only on the lower half
            if (this.Core.Name == "melonDS") {
                bounds.Y /= 2;
                newMousePos.Y -= bounds.Y;
            }

            if (newMousePos.X < 0) newMousePos.X = 0;
            if (newMousePos.Y < 0) newMousePos.Y = 0;
            if (newMousePos.X >= bounds.X) newMousePos.X = bounds.X - 1;
            if (newMousePos.Y >= bounds.Y) newMousePos.Y = bounds.Y - 1;

            this.mousePos = newMousePos;
        }

        if (this.running) {
            if (this.IsFocused && this.lockInput) {
                ImGui.SetKeyboardFocusHere();
                var empty = "";
                ImGui.InputText("##Eorzulator_EmulatorWindow_Empty", ref empty, 0);
                if (ImGui.IsKeyDown(ImGuiKey.Escape)) this.lockInput = false;
                ImGui.TextUnformatted("Press Escape to unlock input");
            } else {
                if (ImGui.IsItemClicked()) this.lockInput = true;
            }
        }
    }

    private void Shutdown() {
        if (!this.running) return;

        Services.PluginLog.Debug("Shutting down emulator");
        try {
            this.Core.UnloadGame();
            this.running = false;
        } catch (Exception e) {
            Services.PluginLog.Error(e, "Failed to shutdown emulator");
        }
    }
}
