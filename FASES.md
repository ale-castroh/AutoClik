# Autoclicker — Plan por fases

Objetivo: autoclicker para Windows minimalista, ordenado, llamativo, ligero y muy preciso,
con tema claro/oscuro que sigue automáticamente al de Windows.

## Tecnología

**C# + WPF sobre .NET 11** (proyecto en `src/Autoclicker`).
- Acceso directo a la API de Windows (SendInput, hotkeys globales, Mica, barra de título oscura).
- Compilar: `dotnet build src\Autoclicker`
- Ejecutar forzando tema (pruebas): `Autoclicker.exe --theme=dark` / `--theme=light`

## Fase 1 — Base y tema ✅
- Proyecto WPF .NET 11, manifiesto PerMonitorV2 (nítido a cualquier escala).
- Ventana compacta con barra de título propia (fijar, minimizar, cerrar), esquinas redondeadas y fondo Mica.
  En sistemas sin Mica usa un fondo sólido.
- Detección del tema de Windows (`AppsUseLightTheme`) y cambio en vivo al alternarlo.
- Color de acento del sistema (tonos de `AccentPalette`, como hace Fluent) y cambio en vivo.
- Paletas `Theme/Light.xaml` y `Theme/Dark.xaml` + estilos en `Theme/Controls.xaml`
  (tarjeta, botón principal con brillo y onda animada, control segmentado, campos, tooltips).

## Fase 2 — Motor de clics (eficacia) ✅
- `Engine/ClickEngine.cs`: hilo dedicado de alta prioridad, línea de tiempo absoluta (sin deriva),
  temporizador de alta resolución (`Engine/HighResTimer.cs`) + giro final de 0,25 ms.
- `Engine/MouseInput.cs`: clics con `SendInput` (izquierdo, derecho, central; simple o doble),
  posición del cursor o coordenada fija (multi-monitor). No clica sobre la propia app.
- Soporta: intervalo desde 1 ms, variación aleatoria, nº de repeticiones, posición fija.
  En la interfaz ya funcionan intervalo, botón, Iniciar/Detener, contador y F6;
  el resto de opciones se exponen en la Fase 3.
- Banco de pruebas `tests/Autoclicker.Bench` (`timing`, `clicks`, `target N`). Medido:

  | Intervalo | Clics/s pedidos | Clics/s reales | Error medio | CPU |
  |---|---|---|---|---|
  | 1 ms | 1000 | 999,9 | 0,12 ms | 4 % |
  | 10 ms | 100 | 99,99 | 0,08 ms | ~0 % |
  | 100 ms | 10 | 10,00 | 0,07 ms | ~0 % |

  Clics reales: 200/200 izquierdos, 100/100 derechos, 100/100 centrales, 20/20 dobles.

## Fase 3 — Interfaz completa, atajo y bandeja ✅
Rediseño "Esencial + Ajustes" (sin scroll): la página principal tiene botón, contador y una sola tarjeta
(Intervalo con unidad, Botón, Tipo, Repetir, Posición); el engranaje abre Ajustes
(Atajo, Modo, Variación, Desvío, Retardo, Bandeja). Esc vuelve a la principal.

Archivos: `MainWindow.xaml(.cs)`, `Input/Hotkey*.cs`, `Interop/TrayIcon.cs`, `Interop/IconFactory.cs`,
`Views/PositionPicker.xaml`. Probado: repetir 15 veces (15/15 clics en la diana), mantener ×3,
F6 y reasignación a Ctrl+F8, Esc cancela, selector devuelve el punto exacto, minimizar oculta a la bandeja.

- Botón Iniciar/Detener fijo arriba con contador en vivo y clics/s reales; ajustes por secciones con scroll.
- Tipo de clic: simple, doble o **mantener pulsado** (duración en ms).
- Repetir: sin fin, **N veces** o **durante X segundos**.
- Posición: cursor o **punto fijo** con selector "Elegir en pantalla" (pantalla atenuada + cruz)
  y **desvío aleatorio ± px** para que no clique siempre en el mismo píxel.
- Avanzado: variación aleatoria del intervalo (± %) y **retardo de inicio** con cuenta atrás.
- Hotkey global (por defecto F6) que funciona con la ventana en segundo plano.
- Hotkey modificable desde la interfaz en modo "pulsa una tecla": se pulsa el botón del atajo
  y la siguiente tecla o combinación (Ctrl/Alt/Shift/Win + tecla) queda asignada.
  También acepta los botones laterales del ratón (X1/X2). Esc cancela la asignación.
- Modo del atajo: alternar (pulsar para iniciar/detener) o mantener (clica mientras se mantiene pulsado).
- Aviso si el atajo ya lo usa otra aplicación.
- Opción "siempre visible".
- Minimizar a la bandeja del sistema (junto al reloj):
  - Icono en la bandeja que cambia de color cuando está clicando.
  - Clic en el icono: mostrar/ocultar la ventana.
  - Menú con clic derecho: Iniciar/Detener, Mostrar, Salir.
  - Opción para que cerrar la ventana (X) la mande a la bandeja en vez de salir.
  - El atajo global sigue funcionando con la app en la bandeja.

## Fase 4 — Multi-punto (marcadores en pantalla) ✅
Posición → "Puntos". "Añadir" abre la pantalla atenuada: cada clic coloca un marcador, Esc termina.
Clic en un marcador abre su editor (botón, tipo, espera, ◀ ▶ orden, eliminar). Con "Veces" se
cuentan vueltas. Archivos: `Markers/`, `Views/MarkerWindow`, `Views/MarkerEditor`, motor `ClickPoint`.
Probado: 3 puntos × 4 vueltas = 12/12 clics en orden 1→2→3, punto 2 con clic derecho, los clics
atraviesan los marcadores. Límite conocido: si la app de destino también es "siempre visible",
se pone encima de los marcadores mientras clica (siguen funcionando; vuelven arriba cada segundo).

- Modo "Multi-punto": se colocan marcadores numerados (1, 2, 3…) sobre la pantalla y se arrastran
  hasta donde haga falta; el autoclicker los recorre en orden y repite la secuencia.
- Cada marcador con su propio botón (izq/der/central), tipo de clic y espera antes del siguiente.
- Añadir / quitar / reordenar marcadores desde una lista en la app.
- Mientras clica, los marcadores se vuelven traspasables (no interceptan los clics) y semitransparentes.

## Fase 5 — Perfiles, guardado y seguridad ✅
Guardado automático (0,5 s tras cada cambio) en `%AppData%\Autoclicker\settings.json`, escritura atómica.
Perfiles desde el nombre en la barra de título (cambiar, nuevo como copia, renombrar, eliminar con
confirmación). Global: atajo, modo, bandeja, esquina, fijar, posición de ventana. Icono generado con
`tools/make-icon.ps1`. `--data=<carpeta>` usa otra carpeta (pruebas). Probado: guardar/restaurar,
2 perfiles independientes, pausa al cambiar de ventana (11 clics, 0 en pausa), los movimientos
inyectados no disparan la esquina. Sin probar: esquina con el ratón físico (requiere mano humana).

- Guardado automático de todos los ajustes (JSON en `%AppData%\Autoclicker`).
- **Perfiles**: guardar configuraciones con nombre (incluidos los marcadores) y cambiar entre ellas.
- **Parada de emergencia**: llevar el ratón a una esquina de la pantalla detiene todo.
- **Solo en una ventana**: pausa automática si la ventana activa cambia (evita clicar donde no toca).
- Icono propio de la app y pulido de animaciones.

## Fase 6 — Grabadora de clics ✅
Integrada en el modo Puntos: "Grabar" oculta la ventana y muestra una barra flotante; cada clic real
(no los inyectados, ni los hechos sobre la app) se convierte en un marcador con su botón, su espera
real hasta el siguiente y, si se mantuvo ≥ 300 ms, tipo "mantener" con esa duración. Esc, el atajo o
"Terminar" acaban. Selector de velocidad ×0,5/×1/×2/×4 (divide esperas y pulsaciones).
Archivos: `Input/ClickRecorder.cs`, `Views/RecordingBar.xaml`. `--record-injected` solo para pruebas.
Probado: 3 clics grabados (izq., der., mantener 455 ms) → 2 vueltas a ×2 = 6/6 en orden; se guarda en el perfil.

## Versión 2.0.0 — Rediseño completo ✅
- **Barra lateral** con una sección por función (Clics, Anti-AFK, Ajustes abajo); la sección activa
  lleva una raya de acento y un punto verde si la función está en marcha. Ventana de 440 px (antes 380).
  Las secciones miden lo mismo que Clics; Ajustes se desplaza si no cabe. Clics sigue sin scroll.
- **Aspecto nuevo** (el usuario rechazó un primer intento por "botones de mala calidad"): lectura grande
  con Bahnschrift (contador / cuenta atrás) y estado con punto gris o verde; botón principal ancho y
  rectangular con la tecla dentro (Iniciar → Detener con contorno); opciones en una sola hoja con líneas
  finas; la opción elegida de los selectores se eleva en gris (no en acento); campos con línea inferior;
  botones secundarios, teclas e interruptores redibujados con la misma altura y bordes.
- Acento de Windows: tono base para rellenos (texto blanco) en oscuro, "Dark 1" en claro; para iconos y
  textos de acento, "Light 1" / "Dark 2" (`AccentTextBrush`). Antes, en oscuro, salía un azul pálido.
- Iconos que se veían descentrados (▶, ■, logo): ahora son formas dibujadas, no letras de una fuente.
- **Fondo translúcido** (acrílico, Windows 11 22H2+), activado por defecto; con él las hojas usan un fondo
  más opaco (`CardTranslucentBrush`). Se puede volver a Mica en Ajustes → General.
- **Logo nuevo** propio (antes un cursor sobre azul, que recordaba a otros autoclickers): ratón blanco con
  contorno grafito, centrado, y una chispa ámbar de tres rayas iguales en abanico, separada del ratón;
  sin fondo. En la bandeja la chispa se pone verde mientras clica. Las mismas formas en
  `Theme/Controls.xaml`, `Interop/IconFactory.cs` y `tools/make-icon.ps1`.
- **Funciones experimentales** (Ajustes → Experimental, apagado por defecto): sin ellas el Anti-AFK no
  aparece; al apagarlas se detiene y se vuelve a Clics. Quien ya usaba el anti-AFK (1.4) lo tiene encendido.
- Anti-AFK: sección propia con cuenta atrás, estado ("Activo en Roblox"), "Último clic hace 2 min, 4 en
  esta sesión" y **Probar ahora** (un clic inmediato).
- Ajustes → Acerca de: versión y botón para abrir la carpeta de ajustes y `error.log`.
- Pruebas: la de anti-AFK activa antes las funciones experimentales y comprueba que se ocultan; la del atajo
  lee la pista de `StartButton` (`AutomationProperties.HelpText`), porque la tecla va dentro del botón.
- **Corregido (fallo desde 1.4.0):** con el ratón no se podía pulsar nada de la página principal (Iniciar,
  intervalo, tipo...). El contenedor con scroll de Ajustes compartía celda con ella y, aunque su contenido
  estuviera oculto, quedaba encima tragándose los clics. Ahora se oculta entero fuera de Ajustes. Las
  pruebas no lo vieron porque pulsaban por UI Automation: nueva `tests/ui/clics-reales.ps1` con clics de
  ratón reales en cada sección (comprueba antes de cada clic que debajo está su propia ventana y no arranca
  si hay otro AutoClik abierto).

## Versión 1.4.0 — Anti-AFK experimental ✅
- **Ajustes → Experimental → Anti-AFK** (`Input/AntiAfk.cs`): cada 1–18 min (15 por defecto) trae al
  frente la ventana elegida, hace UN clic real en el punto elegido y devuelve la ventana activa y el
  cursor (~150 ms; `AttachThreadInput` + `SetForegroundWindow`, clic con pausas de 15/25 ms porque
  algunos juegos necesitan ver el ratón encima antes de pulsar). Si estaba minimizada, la restaura y la
  vuelve a minimizar.
- No hace nada si ya estás en esa ventana; espera a 1,5 s sin teclado/ratón (máx. 60 s) para no robar
  teclas; se pospone mientras el autoclicker clica, se graba o se elige un punto.
- La ventana se recuerda por nombre de proceso (+ título para mostrar) y el punto en coordenadas de
  cliente; se busca la ventana visible más grande del proceso. Ajustes globales (no por perfil).
- Probado con Roblox real mediante script: el clic llega, se vuelve a la ventana y el cursor queda igual.
- **Ajustes ahora tiene scroll** (máximo la altura de la página principal); la principal sigue sin él.
- Pruebas: nueva `tests/ui/antiafk.ps1` (la diana hace de juego; `--antiafk-seconds=N` solo pruebas).

## Versión 1.3.0 — Mejoras ✅
- **Registro de errores**: los errores inesperados van a `%AppData%\AutoClik\error.log` (máx. ~256 KB,
  luego `error.old.log`). Un error de la interfaz ya no cierra la app: se avisa una vez y sigue abierta.
  Los errores del motor se registran con su causa (`ClickEngine.LastError`).
- **Devolver el ratón a su sitio** (Ajustes → Clics, por perfil, desactivado por defecto): con posición
  fija o puntos, el cursor vuelve a donde estaba en la misma llamada a `SendInput` que el clic.
- **Iniciar con Windows** (Ajustes → General): entrada en `HKCU\...\Run` con `--tray` (arranca oculto en
  la bandeja). No va en settings.json; con `--data` se usa `autostart.txt` y nunca se toca el registro.
  Si el .exe cambia de carpeta, la entrada se corrige al abrirlo.
- **Arrastres en la grabadora**: si al soltar el ratón está a ≥ 12 px de donde se pulsó, el punto es
  `Kind = Drag` con destino (`EndX/EndY`) y duración; se reproduce en línea recta (no la trayectoria
  exacta). Al mover el marcador, el destino se mueve igual. En el editor, elegir otro tipo lo quita.
- Investigado y descartado: clicar en una ventana tapada. Roblox ignora los clics enviados como mensajes
  (PostMessage/SendMessage, probado en el PC del usuario); en una máquina virtual Hyperion no lo deja
  arrancar. Lo único viable sería traer Roblox al frente un instante en cada clic (roba el foco ~150 ms).
- Pruebas: nueva `tests/ui/extras.ps1`; batería completa pasa.

## Versión 1.2.1 — Correcciones ✅
- Multi-punto con "Solo en una ventana": la secuencia ya no avanza mientras está en pausa; al volver
  sigue por el mismo punto (antes se saltaba puntos).
- Las casillas X/Y aceptan coordenadas negativas (monitores a la izquierda o encima del principal).
- Pruebas: batería completa pasa. Sin prueba automática específica para estos dos casos.

## Versión 1.2.0 — AutoClik ligero ✅
- Pasa a **.NET Framework 4.8** (incluido en Windows 10 1903+ y 11): `dist\AutoClik.exe` de **~195 KB**
  (antes 61 MB), abre en ~0,5 s siempre (antes ~4 s la primera vez). Memoria física ~80 MB visible
  (antes ~105) y **~10 MB en la bandeja** (antes ~26). La memoria privada sube algo (~66 MB frente a ~50).
- Cambios del port: `DllImport` en lugar de `LibraryImport`, hooks con delegados, `Compat.cs`
  (Clamp, IsDigit, tiempos), JSON propio (`Persistence/MiniJson.cs`) que lee también el formato anterior.
- Nombre **AutoClik** (ejecutable, ventana, bandeja). Ajustes en `%AppData%\AutoClik`; los de
  `%AppData%\Autoclicker` se importan solos.
- **Una sola copia**: abrir el .exe otra vez trae al frente la ventana existente, también desde la
  bandeja (`SingleInstance.cs`). Las copias con `--data` usan otro nombre y no interfieren.
- Pruebas: batería completa pasa (+ nueva `tests/ui/instancia.ps1`).

## Versión 1.1.0 — Idioma automático ✅
Español si Windows está en español, inglés en cualquier otro idioma (`Localization/Strings.cs`,
`{l:T Clave}` en XAML). Forzable con `--lang=es|en` o `AUTOCLICKER_LANG`. El perfil inicial se
llama "Predeterminado"/"Default" según el idioma; los nombres ya guardados no cambian.

## Fase 7 — Pruebas y entrega ✅
- `build.ps1` → `dist\Autoclicker.exe`: **un solo archivo autónomo** (incluye .NET), 61 MB comprimido.
  La primera ejecución tarda ~3–4 s (se descomprime una vez en `%TEMP%\.net`); después ~0,8 s.
- Decisiones forzadas por el Control inteligente de aplicaciones (SAC) de Windows 11:
  sin ReadyToRun (SAC bloquea DLL sin firmar con código nativo precompilado) y con extracción
  completa (WPF no puede cargar `DirectWriteForwarder.dll` desde dentro del paquete).
- Memoria: ~50 MB privada; working set ~105 MB visible → **~26 MB en la bandeja** (se libera al ocultarse).
- `tests\run-all.ps1`: banco del motor + 6 pruebas de interfaz (básico, atajo, puntos, perfiles,
  ventana, grabadora) contra el .exe final, con datos en `%TEMP%` (no toca la configuración real). Todo pasa.
- Precisión final: 1000 clics/s reales; error medio 0,08–0,3 ms según intervalo; CPU ≤ 6 % a 1 ms.
- No probado: escalas 100 %/150 % (solo 125 %, la del equipo), otro PC sin .NET, parada en esquina
  y grabadora con el ratón físico.
- Para repartirlo a otras personas sin avisos de Windows: firmar el .exe con un certificado de firma de
  código (p. ej. Azure Trusted Signing); sin firma, SmartScreen/SAC pueden avisar o bloquearlo en otros PC.
