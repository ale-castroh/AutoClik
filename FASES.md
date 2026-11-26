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

## Versión 2.7.0 — Clic por permanencia y buscar actualizaciones ✅
- **Clic por permanencia** (accesibilidad; `Input/DwellClicker.cs`, `Views/DwellRing`, `MainWindow.Dwell.cs`, Ajustes →
  Accesibilidad entre Clics y Estadísticas). Cada 25 ms se mira el cursor: si se movió más que "Ignorar temblor" (6 px),
  queda armado; quieto el tiempo elegido (0,8 s, de 0,3 a 5) clica una vez con SendInput (izquierdo, derecho o doble) y se
  desarma hasta que se mueva. Al encenderlo, o con un botón pulsado (arrastrar), hay que moverse antes. El círculo
  (ventana traspasable WS_EX_TRANSPARENT, sin foco) aparece a los 150 ms. Se pausa con el autoclicker, grabando,
  reproduciendo, eligiendo un punto o capturando un atajo; la esquina de emergencia lo apaga. Atajo propio (id 6,
  ninguno por defecto, KeyDialog con "Quitar") y casilla en el menú de la bandeja. Se guarda en las preferencias.
  Apagado por defecto. A petición del usuario, **Repetir: Una vez / Sin fin** ("Una vez" por defecto): en "Sin fin",
  al llenarse el círculo arranca el autoclicker en el cursor (`_dwellRunSettings`: intervalo y jitter de Clics, el
  botón y tipo de la permanencia, sin límites ni puntos) y se para al moverse más que el temblor; F6 y la esquina
  también lo paran. No cancela lo programado y sus clics cuentan en las estadísticas.
- **Buscar actualizaciones** (`Interop/UpdateChecker.cs`, `MainWindow.Updates.cs`, Ajustes → Acerca de). Apagado por
  defecto: apagado no se conecta a nada. Encendido, a los 8 s de abrir y cada hora mira si toca (una vez al día).
  Seguridad: GET HTTPS solo a api.github.com/repos/ale-castroh/AutoClik/releases/latest (TLS 1.2, sin redirecciones,
  sin credenciales ni cookies, agente "AutoClik/x.y.z", 10 s, como mucho 512 KB); de la respuesta solo se usa
  `tag_name` si es `v1.2.3` (y se ignoran draft/prerelease); el botón "Ver en GitHub" abre una dirección FIJA del
  código (…/AutoClik/releases), nunca la de la respuesta. Nunca descarga ni instala. Aviso en Acerca de, puntito de
  acento en el engranaje (Tag "Update") y globo de la bandeja la primera vez que se ve una versión.
  Pruebas sin internet: `--update-test=<archivo.json>`.
- Pruebas: nueva `tests/ui/permanencia-actualizaciones.ps1` (clics de la permanencia sobre la diana con PidAt, no repite,
  temblor, derecho, doble, apagado, esquina; respuestas nueva/igual/maliciosas/enorme/prerelease, apagado no comprueba,
  "Buscar ahora"); `clics-reales.ps1` pulsa el tipo de clic de la permanencia con el ratón de verdad (el interruptor no).

## Versión 2.6.0 — Estadísticas, modo portátil y línea de comandos ✅
- **Estadísticas** (`Persistence/UsageStats.cs`, `MainWindow.Stats.cs`): `stats.json` junto a settings.json, solo local.
  Hoy (se pone a cero al cambiar el día) y total: clics, teclas, macros reproducidas, tiempo y sesión más larga.
  Se suman al parar el autoclicker (solo si el motor llegó a arrancar: `_clickRunCounting`) y al parar una macro
  (vueltas completas + eventos de la última hasta donde llegó). Las pruebas del editor no cuentan. Uso por macro
  ("Reproducida 12 veces · última, ayer") bajo la lectura de Macros; renombrar/borrar la macro lo actualiza.
  Tarjeta en Ajustes (entre Clics y General) con Hoy/Total y Reiniciar.
- **Modo portátil** (`Persistence/PortableMode.cs`): si existe `AutoClik-datos` junto al .exe, `SettingsStore.DataDirectory`
  lo usa (manda incluso sobre `--data`, que sigue separando el mutex y el archivo de "Iniciar con Windows" de las pruebas).
  Activar copia ajustes, stats, macros e imágenes allí; desactivar los devuelve (sustituyendo) y borra la carpeta.
  Los dos reinician AutoClik (`App.Restart`, `--wait-pid=N`) sin volver a guardar al cerrar (si no, se recreaba la carpeta).
  Sin permiso de escritura junto al .exe (Archivos de programa), avisa y no cambia nada.
  Fallo encontrado por el usuario en un USB real: con el .exe en la raíz (`D:\`), `TrimEnd('\\')` dejaba "D:" y la ruta
  "D:AutoClik-datos" era relativa a la unidad; la vista previa de una imagen de Vigilar (`new Uri`) fallaba en el
  constructor y AutoClik quedaba abierto sin ventana (y cada nuevo .exe solo le avisaba). Ahora la ruta es completa
  (`Path.GetFullPath`), la vista previa no puede tumbar el arranque y, si el arranque falla, se avisa y se cierra.
  La prueba lo cubre con `subst` (raíz de una unidad simulada).
- **Línea de comandos** (`CommandLine.cs`, `MainWindow.Commands.cs`): --start, --stop, --toggle, --play "Macro" (otra vez
  la misma = parar), --profile "Nombre", --watch on|off, --show, --hide, --exit, --help (ventana con la lista). Si ya hay
  una copia abierta, la nueva le pasa las órdenes por una tubería con nombre (`SingleInstance`, solo el usuario actual)
  y se cierra; si no, arranca oculta con --play/--start/--toggle (mini ventana). --stop/--exit sin copia abierta no hacen nada.
- **Accesos directos** (`Interop/Shortcut.cs`, WScript.Shell por reflexión): menú de la macro → "Crear acceso directo en el
  escritorio" (`--play`), menú del perfil → "Acceso directo que empieza a clicar" (`--profile X --toggle`). Con `--data`
  se crean en `<data>\desktop` y llevan el `--data`.
- **Exportar / importar perfiles**: menú del perfil y Ajustes → General. `.autoclik-profile` con el perfil y las imágenes
  de Vigilar en base64 (al importar solo se aceptan nombres de archivo .png simples). Nombre repetido → "P (2)".
- Pruebas: nueva `tests/ui/estadisticas-ordenes.ps1` (incluye el modo portátil en una copia del .exe en la carpeta de
  pruebas y los diálogos Guardar/Abrir, escritos con el teclado solo si el diálogo está delante); `clics-reales.ps1`
  pulsa Hoy/Total con el ratón de verdad.

## Versión 2.5.0 — Programar y cadenas de macros ✅
- **Programar** (`Scheduling/Schedule.cs`, `Views/ScheduleDialog`, `MainWindow.Schedule.cs`): botón de reloj
  junto a Iniciar (Clics, sin añadir filas: Clics sigue sin scroll) y junto a Reproducir (Macros).
  - Empezar ahora, a una hora (ya pasada = mañana) o dentro de X minutos. Al terminar *solos* (nº de clics,
    tiempo, vueltas de la macro): nada, avisar (globo de la bandeja + sonido), suspender o apagar el PC.
  - Suspender/apagar: `Views/PowerCountdown`, cuenta atrás de 60 s siempre delante, Cancelar es el botón por
    defecto. Si la paras tú, no se hace nada al final.
  - El reloj se pone en color de acento con algo programado; pulsarlo cancela. Empezar a mano (botón, atajo,
    Vigilar) cancela lo programado de esa sección. Solo en memoria: cerrar AutoClik lo pierde.
  - Pruebas: `--schedule-seconds`, `--power-test` (solo anota en power-test.txt, nunca apaga) y
    `--countdown-seconds=N`.
- **Cadenas de macros** (menú → "Nueva cadena…", `Views/ChainDialog`): una macro cuyo archivo lleva `Chain`
  (macro, veces, pausa después) en vez de eventos. Al reproducir se juntan todas en una sola macro (con sus
  posiciones relativas ya aplicadas y un evento `Mark` para la pausa final), así funcionan velocidad, vueltas,
  mini ventana, F8, atajo propio, Programar y Vigilar sin cambios en el reproductor. Renombrar una macro
  actualiza las cadenas que la usan; si falta una, avisa ("Falta la macro…").
- Pruebas: nueva `tests/ui/programar.ps1` (empieza sola a su hora, apaga de mentira tras la cuenta atrás,
  cancelar con el reloj, Cancelar en la cuenta atrás, cadena A×2 → pausa → B, nueva cadena desde el menú, macro
  programada "ahora" + avisar); `clics-reales.ps1` abre y cancela Programar con el ratón de verdad.
- Detalle visual: la etiqueta de la tecla (F6, F7, F8, F10) de los botones principales queda centrada por dentro y a la altura del texto (medido al píxel).

## Versión 2.4.0 — Vigilar la pantalla ✅
- **Vigilar** (experimental: sección nueva en la barra lateral, solo con Ajustes → Experimental; atajo F10,
  configurable en Ajustes → Atajo). Reglas "cuando aparezca esto, haz aquello", guardadas en el perfil:
  - Qué vigilar: un **color en un punto** (se elige con la cruz y se guarda el color que tiene) o una
    **imagen** recortada arrastrando sobre la pantalla (`Views/RegionPicker`; PNG en `images/`), buscada en
    toda la pantalla o en una zona. Tolerancia 0-100 por canal; en imágenes, hasta un 3 % de píxeles distintos.
  - Qué hacer: clic encima (el centro de la imagen o el punto), clic en otro punto, pulsar una tecla,
    iniciar los clics, reproducir una macro o parar todo. Una vez o cada vez.
  - Salta al **aparecer** (no estaba en la revisión anterior y ahora sí): no repite mientras siga a la vista.
  - `Watch/ScreenCapture.cs` (BitBlt del escritorio virtual, píxeles físicos), `Watch/Watcher.cs` (hilo,
    ~5 revisiones por segundo, una captura completa por revisión), `Watch/WatchRule.cs`. Buscar una imagen en
    3 monitores (5360×1440) tarda ~10-30 ms más ~40 ms de captura (filtro por el píxel central).
  - Los juegos a pantalla completa "exclusiva" se ven negros; en ventana o sin bordes funciona.
- **Esperar a un color** en macros (editor → Insertar → "Esperar a un color"): evento `WaitColor`; el
  reproductor mira el punto cada 50 ms hasta que tiene el color o pasa el máximo (30 s por defecto, editable),
  y retrasa el resto de la vuelta lo que haya esperado. Sigue la ventana si la macro es relativa.
- Pruebas: nueva `tests/ui/vigilar.ps1` (regla de color y de imagen sobre la diana, que ahora dibuja un
  cuadrado amarillo; no repite; F10; esperar a un color que está y a uno que no llega); `clics-reales.ps1`
  abre "Nueva regla" y la cancela con el ratón de verdad.

## Versión 2.3.0 — Editor de macros ✅
- **Editor** (`Views/MacroEditor`, menú de la macro → "Editar…"): ventana propia; la principal queda
  desactivada mientras está abierto. La macro se ve como **pasos** (`Macros/MacroSteps.cs`): movimientos
  seguidos, clic, arrastre, pulsar tecla, mantener/soltar tecla o botón, rueda y texto, con la espera antes
  de cada uno. Ida y vuelta sin pérdidas (eventos → pasos → eventos da lo mismo; probado con 1344 eventos).
- Editar: borrar (varios a la vez), subir/bajar, deshacer (Ctrl+Z, 100 niveles); espera, tecla, cuánto se
  mantiene, posición (con "Elegir") y texto del paso elegido. Insertar después del paso elegido: espera (+1 s
  al siguiente), tecla, texto y clic. Limpiar: acortar esperas de más de X s y quitar movimientos (su tiempo
  pasa al paso siguiente: la macro dura lo mismo). Probar reproduce una vez la copia sin guardar y vuelve al
  editor. Cancelar con cambios pide confirmación.
- **Escribir texto**: evento nuevo `TypeChar` (KEYEVENTF_UNICODE): escribe cualquier carácter sin depender
  de la distribución del teclado; el salto de línea pulsa Intro.
- **Posiciones relativas a una ventana**: la macro guarda el proceso y dónde estaba la esquina de su
  ventana; al reproducir, el ratón se desplaza lo que se haya movido. Si la ventana no está abierta, avisa
  y no reproduce. Formato de macro compatible: los campos nuevos son opcionales.
- Pruebas: nueva `tests/ui/macro-editor.ps1` (borrar, deshacer, insertar texto, cambiar posición, Probar,
  Guardar, reproducir, relativa a la diana y ventana ausente); `clics-reales.ps1` abre el editor y lo usa con
  el ratón de verdad. La diana del banco registra también el texto escrito.

## Versión 2.2.0 — Teclas, mini ventana y atajos de macro ✅
- **Acción** en Clics (antes "Botón"): izquierdo, derecho, central, **rueda** (abajo/arriba, 1-20 pasos) o
  **tecla** (`Engine/KeyInput.cs`: SendInput por código de escaneo; simple, doble o mantenida). La tecla va a
  la ventana activa y nunca a AutoClik (escribiría en sus casillas): con AutoClik delante, espera ("Cambia a
  la ventana de destino"). Con Tecla, la posición se atenúa y no se usa. Se guarda en el perfil.
- **Anti-AFK con tecla** (Espacio por defecto): activa la ventana y pulsa la tecla 60 ms en vez de clicar.
- **Macros**: velocidad "Otra" libre (×0,1-×20, acepta coma o punto; sustituye a ×10) y **atajo propio por
  macro** (menú → "Asignar atajo…", diálogo `Views/KeyDialog`), guardado en el archivo de la macro, con
  comprobación de choques con F6/F7/F8 y con otras macros. El atajo reproduce esa macro; otra vez, la para.
  El menú muestra la tecla de cada macro a la derecha.
- **Mini ventana flotante** (`Views/MiniHud`): solo con AutoClik oculto y algo en marcha; estado, contador
  y Detener (autoclicker) o vuelta, tiempo y "F8 para detener" (macros, traspasable para que la macro no la
  pulse). Se arrastra y recuerda el sitio. Ajustes → General → "Mini ventana al ocultar AutoClik".
- Descartado por el usuario: "parar si mueves el ratón" (interrumpiría demasiado).
- Pruebas: nueva `tests/ui/teclas.ps1` (tecla A x5 en la diana solo tras darle el foco, rueda -6, mini
  ventana y su Detener); anti-AFK con Espacio; macros con velocidad ×3 y atajo F9 desde el menú.

## Versión 2.1.0 — Macros ✅
- Nueva sección **Macros** (`Macros/`, `MainWindow.Macros.cs`), al nivel de TinyTask o mejor:
  - **Grabar** (F7): hooks de ratón y teclado de bajo nivel; movimientos cada 8 ms como mucho (y siempre el
    último antes de un clic), botones (también laterales), rueda vertical y horizontal, teclas con código de
    escaneo y bit de extendida. Ignora la entrada simulada, los clics sobre AutoClik y las teclas de los
    atajos; quita la espera inicial, los movimientos sueltos del final y las teclas modificadoras del final
    (el Ctrl de "Ctrl+F7"). Mantener una tecla graba solo la primera pulsación. Modos: todo, ratón, teclas.
    La ventana se oculta y sale la barra flotante (la de los puntos, con "no escribas contraseñas").
  - **Reproducir** (F8): hilo propio de alta prioridad con línea de tiempo absoluta (como el motor de clics);
    una vez, N veces, sin fin o durante X min; ×0,5-×10. Teclas por código de escaneo (juegos). Suelta todo
    lo pulsado al acabar cada vuelta y al parar. La ventana se oculta para que la macro no clique sobre
    AutoClik; F8, la esquina o Detener la paran. Un solo "piloto automático" a la vez (clics / macro).
  - **Biblioteca**: un .json por macro en `macros/` (eventos en una cadena compacta, no lista JSON);
    elegir, renombrar, duplicar, exportar/importar `.autoclik`, eliminar. Escritura atómica.
  - Atajos F7 y F8 configurables en Ajustes → Atajo (la captura de atajos se generalizó a los tres).
- Sin "convertir a .exe" (como TinyTask): el SAC de Windows bloquea .exe sin firmar.
- Pruebas: `tests/ui/macros.ps1` (grabar en la diana clic + A + B + rueda + clic, F7, reproducir 2 vueltas
  a x2: la diana recibe 6 clics, A B A B A B y 6 pasos de rueda; nada queda pulsado). La diana del banco
  ahora también cuenta teclas y rueda. `clics-reales.ps1` también pasa por Macros.

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
