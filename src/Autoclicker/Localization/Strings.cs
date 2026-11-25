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
            .FirstOrDefault(a => a.StartsWith("--lang=", StringComparison.OrdinalIgnoreCase))?.Substring("--lang=".Length)
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

    /// <summary>"1 tecla" / "3 teclas" (o key/keys).</summary>
    public static string Keys(long n) => n == 1 ? T("UnitKey") : T("UnitKeys");

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
        ["TrayRunning"] = ("AutoClik · Clicando", "AutoClik · Clicking"),
        ["TrayStopped"] = ("AutoClik · Detenido", "AutoClik · Stopped"),

        // ---- Botón principal y estado ----
        ["StartBtn"] = ("Iniciar", "Start"),
        ["StopBtn"] = ("Detener", "Stop"),
        ["UnitClick"] = ("clic", "click"),
        ["UnitClicks"] = ("clics", "clicks"),
        ["UnitLaps"] = ("vueltas", "rounds"),
        ["RateRunning"] = ("{0}, {1:0.0} por segundo", "{0}, {1:0.0} per second"),
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
        ["RestoreCursor"] = ("Devolver el ratón a su sitio", "Put the mouse back"),
        ["RestoreCursorTip"] = ("Con posición fija o puntos, el cursor vuelve a donde estaba tras cada clic. Algunos juegos no lo admiten",
                                "With a fixed position or points, the cursor returns to where it was after each click. Some games don't support it"),
        ["General"] = ("General", "General"),
        ["StartWithWindows"] = ("Iniciar con Windows", "Start with Windows"),
        ["StartWithWindowsTip"] = ("Se abre al iniciar sesión, oculto en la bandeja", "Opens when you sign in, hidden in the tray"),
        ["Translucent"] = ("Fondo translúcido", "Translucent background"),
        ["TranslucentTip"] = ("Deja ver, borroso, lo que hay detrás de la ventana (Windows 11)",
                              "Shows what's behind the window, blurred (Windows 11)"),
        ["About"] = ("Acerca de", "About"),
        ["AboutVersion"] = ("AutoClik {0}", "AutoClik {0}"),
        ["AboutHint"] = ("Ajustes y registro de errores", "Settings and error log"),
        ["OpenDataFolder"] = ("Abrir carpeta", "Open folder"),
        ["OpenDataFolderTip"] = ("Abre la carpeta con settings.json y error.log", "Opens the folder with settings.json and error.log"),

        // ---- Macros (2.1) ----
        ["Macros"] = ("Macros", "Macros"),
        ["Macro"] = ("Macro", "Macro"),
        ["MacroRecord"] = ("Grabar", "Record"),
        ["MacroRecordTip"] = ("Graba lo que haces con el ratón y el teclado. La ventana se oculta mientras grabas",
                              "Records what you do with the mouse and keyboard. The window hides while you record"),
        ["MacroPlay"] = ("Reproducir", "Play"),
        ["MacroLibraryTip"] = ("Tus macros: elegir, renombrar, duplicar, exportar, importar o eliminar",
                               "Your macros: pick, rename, duplicate, export, import or delete"),
        ["MacroNone"] = ("Ninguna", "None"),
        ["MacroOnce"] = ("Una vez", "Once"),
        ["MacroLoops"] = ("veces", "times"),
        ["MacroPlayTimes"] = ("Reproducir", "Play"),
        ["MacroPlayFor"] = ("Reproducir durante", "Play for"),
        ["MacroRecordWhat"] = ("Grabar", "Record"),
        ["MacroModeAll"] = ("Todo", "All"),
        ["MacroModeMouse"] = ("Ratón", "Mouse"),
        ["MacroModeKeys"] = ("Teclas", "Keys"),
        ["MacroRecordHotkey"] = ("Grabar macro", "Record macro"),
        ["MacroPlayHotkey"] = ("Reproducir macro", "Play macro"),
        ["MacroBarPrivacy"] = ("no escribas contraseñas", "don't type passwords"),
        ["MacroBarCounts"] = ("{0} {1}, {2} {3}", "{0} {1}, {2} {3}"),
        ["MacroNothing"] = ("No se grabó nada", "Nothing was recorded"),
        ["MacroN"] = ("Macro {0}", "Macro {0}"),
        ["MacroSaved"] = ("Macro guardada", "Macro saved"),
        ["MacroNoneHint"] = ("Graba una macro primero", "Record a macro first"),
        ["MacroRename"] = ("Renombrar…", "Rename…"),
        ["MacroRenameTitle"] = ("Renombrar macro", "Rename macro"),
        ["MacroDuplicate"] = ("Duplicar", "Duplicate"),
        ["MacroCopyOf"] = ("{0} (copia)", "{0} (copy)"),
        ["MacroExport"] = ("Exportar…", "Export…"),
        ["MacroImport"] = ("Importar…", "Import…"),
        ["MacroDelete"] = ("Eliminar macro", "Delete macro"),
        ["MacroDeleteConfirm"] = ("Se eliminará «{0}». No se puede deshacer.", "\"{0}\" will be deleted. This can't be undone."),
        ["MacroExists"] = ("Ya existe una macro con ese nombre", "A macro with that name already exists"),
        ["MacroFileFilter"] = ("Macro de AutoClik (*.autoclik)|*.autoclik|Todos los archivos (*.*)|*.*",
                               "AutoClik macro (*.autoclik)|*.autoclik|All files (*.*)|*.*"),
        ["MacroExported"] = ("Macro exportada", "Macro exported"),
        ["MacroImported"] = ("Macro importada", "Macro imported"),
        ["MacroFileError"] = ("No se pudo leer o guardar el archivo", "Couldn't read or save the file"),
        ["MacroPlayingLoopOf"] = ("Reproduciendo, vuelta {0} de {1}", "Playing, loop {0} of {1}"),
        ["MacroPlayingLoop"] = ("Reproduciendo, vuelta {0}", "Playing, loop {0}"),
        ["MacroOfTotal"] = ("de {0} en esta vuelta", "of {0} in this loop"),
        ["MacroEmptyState"] = ("Sin macros", "No macros"),
        ["MacroEmptyHint"] = ("Pulsa Grabar para crear la primera", "Press Record to create your first one"),
        ["MacroReady"] = ("Lista", "Ready"),
        ["MacroCounts"] = ("{0:N0} {1}, {2:N0} {3}, {4:N0} movimientos", "{0:N0} {1}, {2:N0} {3}, {4:N0} moves"),
        ["UnitKey"] = ("tecla", "key"),
        ["UnitKeys"] = ("teclas", "keys"),

        // ---- Barra lateral ----
        ["NavClicks"] = ("Clics", "Clicks"),
        ["Emergency"] = ("Parar al llevar el ratón a una esquina", "Stop when the mouse hits a corner"),
        ["EmergencyTip"] = ("Parada de emergencia: mueve el ratón a cualquier esquina de la pantalla",
                            "Emergency stop: move the mouse to any corner of the screen"),
        ["MinToTray"] = ("Minimizar a la bandeja", "Minimize to tray"),
        ["CloseToTray"] = ("Cerrar a la bandeja", "Close to tray"),
        ["CloseToTrayTip"] = ("La X oculta la ventana; para salir, usa el menú del icono",
                              "The X hides the window; to exit, use the tray icon menu"),

        // ---- Experimental: anti-AFK ----
        ["Experimental"] = ("Experimental", "Experimental"),
        ["ExperimentalTitle"] = ("Funciones experimentales", "Experimental features"),
        ["ExperimentalHint"] = ("Muestra lo que aún está en pruebas, como el Anti-AFK", "Shows features still being tested, like Anti-AFK"),
        ["AntiAfk"] = ("Anti-AFK", "Anti-AFK"),
        ["AntiAfkTip"] = ("Cada pocos minutos trae la ventana elegida al frente un instante, hace un clic y vuelve a donde estabas. Pensado para juegos como Roblox, que expulsan tras 20 min sin actividad",
                          "Every few minutes it brings the chosen window to the front for a moment, clicks once and returns you to where you were. Meant for games like Roblox, which kick you after 20 min of inactivity"),
        ["AfkOffHint"] = ("Cada pocos minutos, un clic en la ventana elegida", "Every few minutes, one click in the chosen window"),
        ["AfkNotConfigured"] = ("Elige la ventana y el punto", "Pick the window and the spot"),
        ["AfkStateOff"] = ("Desactivado", "Off"),
        ["AfkStateActive"] = ("Activo en {0}", "Active in {0}"),
        ["AfkStart"] = ("Activar", "Turn on"),
        ["AfkStop"] = ("Desactivar", "Turn off"),
        ["AfkNextMinutes"] = ("Próximo clic en {0} min", "Next click in {0} min"),
        ["AfkNextSeconds"] = ("Próximo clic en {0} s", "Next click in {0} s"),
        ["AfkMissing"] = ("{0} no está abierto", "{0} is not open"),
        ["AfkInWindow"] = ("Estás en {0}", "You're in {0}"),
        ["AfkPaused"] = ("En espera mientras clica o graba", "On hold while clicking or recording"),
        ["AfkFailed"] = ("No se pudo traer la ventana al frente", "Couldn't bring the window to the front"),
        ["AfkWaitingIdle"] = ("Esperando a que dejes de usar el PC…", "Waiting until you stop using the PC…"),
        ["AfkTarget"] = ("Ventana y punto", "Window and point"),
        ["AfkNoTarget"] = ("Sin elegir", "Not picked"),
        ["AfkTargetValue"] = ("{0} · X {1} · Y {2}", "{0} · X {1} · Y {2}"),
        ["AfkPickTip"] = ("Haz clic en un sitio inofensivo de la ventana (un botón o una zona vacía)",
                          "Click a harmless spot in the window (a button or an empty area)"),
        ["AfkTest"] = ("Probar", "Test"),
        ["AfkTestNow"] = ("Probar ahora", "Test now"),
        ["AfkTestHint"] = ("Un clic ahora, para comprobar el punto", "One click now, to check the spot"),
        ["AfkTestDone"] = ("Clic de prueba hecho", "Test click done"),
        ["AfkNever"] = ("Aún no ha hecho ningún clic", "No clicks yet"),
        ["AfkLastJust"] = ("Último clic hace un momento, {0} en esta sesión", "Last click just now, {0} this session"),
        ["AfkLast"] = ("Último clic hace {0} min, {1} en esta sesión", "Last click {0} min ago, {1} this session"),
        ["AfkExperimentalNote"] = ("Experimental: en cada clic trae la ventana al frente un instante y te devuelve a la tuya. Espera a que dejes de usar el PC para no robarte teclas.",
                                   "Experimental: each click brings the window to the front for a moment and returns you to yours. It waits until you stop using the PC so it doesn't steal keystrokes."),
        ["AfkEvery"] = ("Cada", "Every"),
        ["AfkEveryHint"] = ("De 1 a 18 minutos", "1 to 18 minutes"),
        ["AfkEveryTip"] = ("Roblox expulsa tras 20 minutos sin actividad: deja algo de margen",
                           "Roblox kicks you after 20 minutes of inactivity: leave some margin"),

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
        ["DragInfo"] = ("Arrastre hasta X {0} · Y {1} en {2} ms. Elige otro tipo para quitarlo",
                        "Drag to X {0} · Y {1} in {2} ms. Pick another type to remove it"),
        ["DragMarkerTip"] = ("Arrastre grabado · Arrastra para mover · Clic para editar",
                             "Recorded drag · Drag to move · Click to edit"),

        // ---- Errores ----
        ["UnexpectedError"] = ("Ha ocurrido un error inesperado. AutoClik sigue abierto; los detalles están en:\n{0}",
                               "An unexpected error occurred. AutoClik is still open; details are in:\n{0}"),

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
