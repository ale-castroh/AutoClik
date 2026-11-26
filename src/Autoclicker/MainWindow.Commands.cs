using Autoclicker.Interop;
using Autoclicker.Localization;
using Autoclicker.Macros;

namespace Autoclicker;

/// <summary>
/// Línea de comandos y accesos directos (2.6): órdenes que llegan al arrancar o desde otra copia
/// (--play "Macro", --start, --profile...) y los accesos directos del escritorio que las usan.
/// </summary>
public partial class MainWindow
{
    /// <summary>Ejecuta las órdenes en orden (p. ej. --profile "Juego" --start).</summary>
    public void RunCommands(IEnumerable<AppCommand> commands)
    {
        foreach (var command in commands)
        {
            if (_exiting) return;
            switch (command.Kind)
            {
                case CommandKind.Start:
                    if (!_recording && !_macroRecording && StartButton.IsChecked != true) StartButton.IsChecked = true;
                    break;
                case CommandKind.Stop:
                    StartButton.IsChecked = false;
                    if (PlayMacroButton.IsChecked == true) SetChecked(PlayMacroButton, false, raise: true);
                    break;
                case CommandKind.Toggle:
                    if (!_recording && !_macroRecording) StartButton.IsChecked = StartButton.IsChecked != true;
                    break;
                case CommandKind.Play:
                    PlayFromCommand(command.Value ?? "");
                    break;
                case CommandKind.Profile:
                    string? profile = _data.Profiles.Keys.FirstOrDefault(k => string.Equals(k, command.Value, StringComparison.CurrentCultureIgnoreCase));
                    if (profile != null) SwitchProfile(profile);
                    else CommandProblem(Strings.F("CmdNoProfile", command.Value ?? ""));
                    break;
                case CommandKind.Watch:
                    if (ExperimentalSwitch.IsChecked != true) CommandProblem(Strings.T("CmdWatchExperimental"));
                    else WatchToggle.IsChecked = command.Value == "on";
                    break;
                case CommandKind.Show:
                    BringToFront();
                    break;
                case CommandKind.Hide:
                    if (IsVisible) HideToTray();
                    break;
                case CommandKind.Exit:
                    _exiting = true;
                    Close();
                    return;
            }
        }
    }

    private void PlayFromCommand(string name)
    {
        Macro? macro = _macros.FirstOrDefault(m => string.Equals(m.Name, name, StringComparison.CurrentCultureIgnoreCase));
        if (macro == null)
        {
            CommandProblem(Strings.F("CmdNoMacro", name));
            return;
        }
        if (_macroRecording || _recording || _editor != null) return;
        // Como el atajo de una macro: si ya se está reproduciendo esa, la orden la para
        if (PlayMacroButton.IsChecked == true)
        {
            bool same = macro == _macro;
            SetChecked(PlayMacroButton, false, raise: true);
            if (same) return;
        }
        SelectMacro(macro);
        UpdateMacroUi();
        SetChecked(PlayMacroButton, true, raise: true);
    }

    /// <summary>Una orden que no se pudo cumplir: aviso en la bandeja (la ventana puede estar oculta) y en la sección.</summary>
    private void CommandProblem(string text)
    {
        _tray?.ShowBalloon("AutoClik", text);
        _notice = text;
        UpdateStatus();
    }

    // ================= Accesos directos =================

    private void MacroShortcut()
    {
        if (_macro is not { } macro) return;
        string? path = Shortcut.Create("AutoClik - " + macro.Name, "--play " + Shortcut.Quote(macro.Name),
                                       Strings.F("ShortcutMacroDesc", macro.Name));
        _macroNotice = Strings.T(path != null ? "ShortcutCreated" : "ShortcutFailed");
        UpdateMacroUi();
    }

    private void ProfileShortcut()
    {
        string name = _data.CurrentProfile;
        string? path = Shortcut.Create(Strings.F("ShortcutProfileTitle", name),
                                       // Toggle: el mismo icono empieza y, si ya clica, para
                                       "--profile " + Shortcut.Quote(name) + " --toggle",
                                       Strings.F("ShortcutProfileDesc", name));
        _notice = Strings.T(path != null ? "ShortcutCreated" : "ShortcutFailed");
        UpdateStatus();
    }
}
