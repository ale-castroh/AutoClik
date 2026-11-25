using System.Globalization;
using System.Windows.Markup;

namespace Autoclicker.Localization;

/// <summary>
/// Textos de la interfaz en español e inglés. El idioma se elige una vez al arrancar:
/// español si Windows está en español, inglés en cualquier otro caso.
/// Se puede forzar con el argumento --lang=es|en o la variable de entorno AUTOCLICKER_LANG.
/// </summary>
public static class Strings
{
    public static bool IsSpanish { get; } = DetectSpanish();

    private static bool DetectSpanish()
    {
        string? forced = Environment.GetCommandLineArgs()
            .FirstOrDefault(a => a.StartsWith("--lang=", StringComparison.OrdinalIgnoreCase))?["--lang=".Length..]
            ?? Environment.GetEnvironmentVariable("AUTOCLICKER_LANG");
        if (!string.IsNullOrWhiteSpace(forced))
            return forced.Trim().StartsWith("es", StringComparison.OrdinalIgnoreCase);
        return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "es";
    }

    /// <summary>Texto para la clave en el idioma actual.</summary>
    public static string T(string key) =>
        Table.TryGetValue(key, out var pair) ? (IsSpanish ? pair.Es : pair.En) : key;

    /// <summary>Texto con huecos {0}, {1}... rellenados.</summary>
    public static string F(string key, params object[] args) =>
        string.Format(CultureInfo.CurrentCulture, T(key), args);

    /// <summary>"1 clic" / "3 clics" (o click/clicks).</summary>
    public static string Clicks(long n) => n == 1 ? T("UnitClick") : T("UnitClicks");

    private static readonly Dictionary<string, (string Es, string En)> Table = new()
    {
        // ---- Barra de título y bandeja ----
        ["Profiles"] = ("Perfiles", "Profiles"),
        ["Settings"] = ("Ajustes", "Settings"),
        ["Minimize"] = ("Minimizar", "Minimize"),
        ["Close"] = ("Cerrar", "Close"),
        ["PinTip"] = ("Mantener siempre visible", "Keep on top"),
        ["UnpinTip"] = ("Dejar de estar siempre visible", "Stop keeping on top"),
        ["TrayStart"] = ("Iniciar", "Start"),
        ["TrayStop"] = ("Detener", "Stop"),
        ["TrayShow"] = ("Mostrar ventana", "Show window"),
        ["TrayExit"] = ("Salir", "Exit"),
        ["TrayRunning"] = ("Autoclicker · Clicando", "Autoclicker · Clicking"),
        ["TrayStopped"] = ("Autoclicker · Detenido", "Autoclicker · Stopped"),

        // ---- Botón principal y estado ----
        ["StartBtn"] = ("Iniciar", "Start"),
        ["StopBtn"] = ("Detener", "Stop"),
        ["UnitClick"] = ("clic", "click"),
        ["UnitClicks"] = ("clics", "clicks"),
        ["UnitLaps"] = ("vueltas", "rounds"),
        ["Rate"] = ("{0} · {1:0.0}/s", "{0} · {1:0.0}/s"),
        ["StartsIn"] = ("Empieza en {0:0.0} s", "Starts in {0:0.0} s"),
        ["WaitingWindow"] = ("Cambia a la ventana de destino", "Switch to the target window"),
        ["PausedWindow"] = ("En pausa · otra ventana activa", "Paused · another window is active"),
        ["Clicking"] = ("Clicando", "Clicking"),
        ["Completed"] = ("Completado", "Completed"),
        ["ClickError"] = ("Error al clicar", "Click error"),
        ["Stopped"] = ("Detenido", "Stopped"),
        ["NeedPoint"] = ("Añade al menos un punto", "Add at least one point"),
        ["StoppedCorner"] = ("Parado: ratón en la esquina", "Stopped: mouse in a corner"),
        ["Recorded"] = ("Grabados {0} {1}", "Recorded {0} {1}"),
        ["HoldHint"] = ("Mantén {0} para clicar", "Hold {0} to click"),
        ["KeyToStart"] = ("{0} para iniciar", "{0} to start"),
        ["KeyToStop"] = ("{0} para detener", "{0} to stop"),

        // ---- Opciones principales ----
        ["Interval"] = ("Intervalo", "Interval"),
        ["Button"] = ("Botón", "Button"),
        ["Left"] = ("Izquierdo", "Left"),
        ["Right"] = ("Derecho", "Right"),
        ["Middle"] = ("Central", "Middle"),
        ["Type"] = ("Tipo", "Type"),
        ["Single"] = ("Simple", "Single"),
        ["Double"] = ("Doble", "Double"),
        ["Hold"] = ("Mantener", "Hold"),
        ["HoldFor"] = ("Mantener pulsado durante", "Hold down for"),
        ["Repeat"] = ("Repetir", "Repeat"),
        ["Forever"] = ("Sin fin", "Forever"),
        ["Times"] = ("Veces", "Times"),
        ["Time"] = ("Tiempo", "Time"),
        ["StopAfter"] = ("Parar después de", "Stop after"),
        ["ClickFor"] = ("Clicar durante", "Click for"),
        ["Position"] = ("Posición", "Position"),
        ["Cursor"] = ("Cursor", "Cursor"),
        ["Fixed"] = ("Fijo", "Fixed"),
        ["Points"] = ("Puntos", "Points"),
        ["CursorTip"] = ("Clica donde esté el cursor", "Clicks wherever the cursor is"),
        ["FixedTip"] = ("Clica siempre en el mismo punto", "Always clicks the same spot"),
        ["PointsTip"] = ("Recorre varios puntos marcados en la pantalla", "Goes through several points marked on screen"),
        ["Pick"] = ("Elegir", "Pick"),
        ["PickTip"] = ("Elegir el punto en la pantalla", "Pick the point on screen"),
        ["Add"] = ("Añadir", "Add"),
        ["AddTip"] = ("Haz clic en la pantalla para colocar puntos", "Click on the screen to place points"),
        ["Record"] = ("Grabar", "Record"),
        ["RecordTip"] = ("Graba tus clics (posición, botón y tiempos) y los convierte en puntos",
                         "Records your clicks (position, button and timing) and turns them into points"),
        ["ClearTip"] = ("Quitar todos los puntos", "Remove all points"),
        ["PointsHint"] = ("Clic en un marcador para editarlo; arrástralo para moverlo", "Click a marker to edit it; drag it to move it"),
        ["NoPoints"] = ("Sin puntos", "No points"),
        ["OnePoint"] = ("1 punto", "1 point"),
        ["NPoints"] = ("{0} puntos", "{0} points"),
        ["Speed"] = ("Velocidad", "Speed"),
        ["SpeedTip"] = ("Reproduce la secuencia más lenta o más rápida (esperas y pulsaciones)",
                        "Plays the sequence slower or faster (waits and holds)"),
        ["SpeedHalf"] = ("×0,5", "×0.5"),

        // ---- Ajustes ----
        ["Hotkey"] = ("Atajo", "Hotkey"),
        ["StartStop"] = ("Iniciar / detener", "Start / stop"),
        ["HotkeyWorksHidden"] = ("Funciona aunque la ventana esté oculta", "Works even when the window is hidden"),
        ["KeyCapTip"] = ("Clic para cambiar el atajo", "Click to change the hotkey"),
        ["PressKey"] = ("Pulsa una tecla…", "Press a key…"),
        ["PressKeyHint"] = ("Tecla, combinación o botón lateral del ratón · Esc cancela",
                            "Key, combination or side mouse button · Esc cancels"),
        ["HotkeyTaken"] = ("{0} lo usa otra aplicación", "{0} is used by another app"),
        ["HotkeyTakenPick"] = ("{0} lo usa otra aplicación: elige otro", "{0} is used by another app: pick another"),
        ["Mode"] = ("Modo", "Mode"),
        ["Toggle"] = ("Alternar", "Toggle"),
        ["ToggleTip"] = ("Pulsa una vez para empezar y otra para parar", "Press once to start and again to stop"),
        ["HoldModeTip"] = ("Clica solo mientras mantienes pulsado el atajo", "Clicks only while you hold the hotkey"),
        ["ClicksGroup"] = ("Clics", "Clicks"),
        ["Jitter"] = ("Variación del intervalo", "Interval variation"),
        ["JitterTip"] = ("Ritmo irregular, más natural", "Irregular, more natural rhythm"),
        ["Offset"] = ("Desvío del punto fijo", "Fixed point offset"),
        ["OffsetTip"] = ("Clica alrededor del punto fijo, no siempre en el mismo píxel",
                         "Clicks around the fixed point, not always on the same pixel"),
        ["Delay"] = ("Retardo de inicio", "Start delay"),
        ["DelayTip"] = ("Cuenta atrás antes del primer clic", "Countdown before the first click"),
        ["LockWindow"] = ("Pausar si cambia la ventana activa", "Pause if the active window changes"),
        ["LockWindowTip"] = ("Clica solo en la ventana que estaba activa al empezar; si cambias a otra, se pausa y sigue al volver",
                             "Only clicks in the window that was active when it started; switching away pauses it and switching back resumes"),
        ["General"] = ("General", "General"),
        ["Emergency"] = ("Parar al llevar el ratón a una esquina", "Stop when the mouse hits a corner"),
        ["EmergencyTip"] = ("Parada de emergencia: mueve el ratón a cualquier esquina de la pantalla",
                            "Emergency stop: move the mouse to any corner of the screen"),
        ["MinToTray"] = ("Minimizar a la bandeja", "Minimize to tray"),
        ["CloseToTray"] = ("Cerrar a la bandeja", "Close to tray"),
        ["CloseToTrayTip"] = ("La X oculta la ventana; para salir, usa el menú del icono",
                              "The X hides the window; to exit, use the tray icon menu"),

        // ---- Perfiles ----
        ["DefaultProfile"] = ("Predeterminado", "Default"),
        ["ProfileTip"] = ("Perfil: {0} · clic para cambiar", "Profile: {0} · click to switch"),
        ["ProfileN"] = ("Perfil {0}", "Profile {0}"),
        ["NewProfile"] = ("Nuevo perfil…", "New profile…"),
        ["NewProfileTitle"] = ("Nuevo perfil (copia del actual)", "New profile (copy of current)"),
        ["Rename"] = ("Renombrar…", "Rename…"),
        ["RenameTitle"] = ("Renombrar perfil", "Rename profile"),
        ["DeleteProfile"] = ("Eliminar perfil", "Delete profile"),
        ["DeleteConfirm"] = ("Se eliminará «{0}» con sus puntos. No se puede deshacer.",
                             "\"{0}\" and its points will be deleted. This can't be undone."),
        ["Delete"] = ("Eliminar", "Delete"),
        ["ProfileExists"] = ("Ya existe un perfil con ese nombre", "A profile with that name already exists"),
        ["Cancel"] = ("Cancelar", "Cancel"),
        ["Save"] = ("Guardar", "Save"),
        ["NameRequired"] = ("Escribe un nombre", "Enter a name"),

        // ---- Marcadores ----
        ["MarkerTip"] = ("Arrastra para mover · Clic para editar", "Drag to move · Click to edit"),
        ["PointN"] = ("Punto {0}", "Point {0}"),
        ["MoveEarlier"] = ("Mover antes", "Move earlier"),
        ["MoveLater"] = ("Mover después", "Move later"),
        ["DeletePoint"] = ("Eliminar punto", "Delete point"),
        ["DefaultOpt"] = ("General", "Default"),
        ["DefaultButtonTip"] = ("El botón elegido en la ventana principal", "The button chosen in the main window"),
        ["DefaultKindTip"] = ("El tipo elegido en la ventana principal", "The type chosen in the main window"),
        ["LeftShort"] = ("Izq.", "Left"),
        ["RightShort"] = ("Der.", "Right"),
        ["WaitNext"] = ("Espera hasta el siguiente", "Wait until next"),
        ["WaitNextHint"] = ("Vacío = intervalo general", "Empty = main interval"),

        // ---- Selector de posición y grabadora ----
        ["PickHint"] = ("Clic para elegir · Esc para cancelar", "Click to pick · Esc to cancel"),
        ["PickManyHint"] = ("Clic para añadir punto · Esc para terminar", "Click to add a point · Esc to finish"),
        ["Recording"] = ("Grabando", "Recording"),
        ["Finish"] = ("Terminar", "Finish"),
        ["ToFinish"] = ("{0} para terminar", "{0} to finish"),
        ["EscOr"] = ("Esc o {0}", "Esc or {0}"),

        // ---- Teclas ----
        ["Shift"] = ("Mayús", "Shift"),
        ["MouseX1"] = ("Ratón X1", "Mouse X1"),
        ["MouseX2"] = ("Ratón X2", "Mouse X2"),
        ["KeyHex"] = ("Tecla {0:X2}", "Key {0:X2}"),
    };
}

/// <summary>Uso en XAML: Text="{l:T Clave}".</summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class TExtension(string key) : MarkupExtension
{
    public string Key { get; set; } = key;

    public override object ProvideValue(IServiceProvider serviceProvider) => Strings.T(Key);
}
