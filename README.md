# AutoClik

Autoclicker para Windows 10/11: ligero (un solo .exe de ~200 KB), minimalista, preciso (error medio ≈ 0,1 ms, hasta 1000 clics/s)
y con tema claro/oscuro que sigue al de Windows y fondo translúcido en Windows 11. Idioma automático:
español si Windows está en español; inglés en cualquier otro idioma.

## Uso rápido

1. Abre `AutoClik.exe`. No necesita instalación: usa .NET Framework 4.8, que ya viene con
   Windows 10 (1903 o posterior) y Windows 11. Abre en medio segundo. Si lo abres otra vez,
   no se duplica: aparece delante la ventana que ya estaba abierta (también desde la bandeja).
2. En **Clics** (primer icono de la barra lateral) elige **Intervalo**, **Botón**, **Tipo** y **Repetir**.
3. Pulsa **Iniciar** o **F6**. Vuelve a pulsar **F6** para parar.

La ventana tiene una barra lateral con una sección por función: **Clics**, **Macros**, **Anti-AFK**
(solo si activas las funciones experimentales) y, abajo, **Ajustes**. Un punto verde en el icono indica que esa
función está en marcha.

La app no clica sobre su propia ventana, así que puedes iniciar con el ratón, mover el cursor
a donde quieras y volver para pulsar **Detener**.

## Funciones

| Dónde | Qué |
|---|---|
| **Intervalo** | Número + unidad (ms · s · min · h), desde 1 ms |
| **Acción** | Botón izquierdo, derecho o central; **rueda** (arriba o abajo, N pasos); o **tecla** (la que elijas; va a la ventana activa, nunca a AutoClik, y funciona en juegos) |
| **Tipo** | Simple, doble o mantener pulsado (duración en ms); también con teclas |
| **Repetir** | Sin fin, N veces (o N vueltas con puntos) o durante X segundos |
| **Posición → Cursor** | Clica donde esté el ratón |
| **Posición → Fijo** | Clica siempre en un punto; **Elegir** lo marca en pantalla |
| **Posición → Puntos** | Varios marcadores numerados que se recorren en orden |
| **Puntos → Añadir** | Cada clic en la pantalla añade un marcador; Esc termina |
| **Puntos → Grabar** | Graba tus clics reales (posición, botón y tiempos) y los convierte en puntos; si arrastras, se graba como arrastre (en línea recta) |
| **Puntos → Velocidad** | Reproduce la secuencia a ×0,5 · ×1 · ×2 · ×4 |
| **Marcador** | Arrástralo para moverlo; clic para editar botón, tipo, espera, orden o borrarlo |
| **Perfil** (barra de título) | Cambiar, crear (copia del actual), renombrar o eliminar configuraciones |
| **⚙ Ajustes → Atajo** | Clic en la tecla y pulsa otra (teclas, combinaciones o botones laterales del ratón). Modo *Alternar* o *Mantener*. También los atajos de grabar (F7) y reproducir (F8) macros |
| **⚙ Ajustes → Clics** | Variación aleatoria del intervalo, desvío del punto fijo, retardo de inicio, pausar si cambia la ventana activa, devolver el ratón a su sitio tras cada clic (posición fija o puntos) |
| **⚙ Ajustes → General** | Parar al llevar el ratón a una esquina, minimizar/cerrar a la bandeja, iniciar con Windows (oculto en la bandeja), **mini ventana** (con AutoClik oculto, una ventanita flotante con el estado y Detener), fondo translúcido |
| **Macros → Grabar** (F7) | Graba ratón (movimientos, clics, arrastres, rueda) y teclado con sus tiempos reales. La ventana se oculta y aparece una barra flotante; F7 o "Terminar" acaban. Se puede grabar todo, solo el ratón o solo las teclas. No graba los clics sobre AutoClik ni las teclas de los atajos, y quita la espera inicial |
| **Macros → Reproducir** (F8) | Una vez, N veces, sin fin o durante X minutos, a ×0,5 · ×1 · ×2 · ×4 u otra velocidad. Las teclas se envían como teclas físicas (funcionan en juegos). F8, la esquina de emergencia o Detener la paran, y nunca deja teclas ni botones pulsados |
| **Macros → Macro** | Biblioteca: elegir, **asignar un atajo propio** (F9 → "Farmeo"), renombrar, duplicar, exportar e importar (archivos `.autoclik`) o eliminar. Se guardan en `%AppData%\AutoClik\macros` |
| **Macros → Macro → Editar…** | **Editor de macros**: la macro como lista de pasos legibles ("Clic izquierdo en 878, 487", "Pulsar E (0,12 s)", "Escribir «hola»"). Borrar, subir/bajar, deshacer; cambiar la espera antes de cada paso, la tecla, cuánto se mantiene, la posición de un clic o un texto; **insertar** esperas, teclas, textos, clics y **esperar a un color** (la macro espera a que un punto tenga ese color, con un máximo, en vez de una espera fija); **limpiar** (acortar esperas largas, quitar movimientos del ratón); **Probar** sin guardar; y **posiciones relativas a una ventana** (si el juego se mueve, los clics lo siguen; si no está abierto, avisa) |
| **Programar** (reloj junto a Iniciar y Reproducir) | Empezar **ahora**, **a una hora** (si ya pasó, al día siguiente) o **dentro de** X minutos. **Al terminar** (solo si termina sola): nada, avisar (notificación de Windows), suspender o apagar el PC, con una cuenta atrás de 60 s y Cancelar. El reloj en color = algo programado; pulsarlo lo cancela. Se pierde al cerrar AutoClik (no al ocultarlo en la bandeja) |
| **Macros → Macro → Nueva cadena…** | **Cadenas de macros**: varias macros seguidas como una sola ("Abrir cofres ×3 → pausa 2 s → Vender → Volver"). Se reproducen, programan y llevan atajo igual que una macro; si cambias una macro, la cadena usa la versión nueva. Al renombrar una macro, las cadenas la siguen encontrando |
| **Macros → Velocidad** | ×0,5 · ×1 · ×2 · ×4 u **Otra** (libre, de ×0,1 a ×20) |
| **⚙ Ajustes → Experimental** | Muestra las funciones en pruebas (de momento, el Anti-AFK). Apagado por defecto |
| **Vigilar** (experimental, F10) | Reglas "cuando aparezca esto, haz aquello". Qué vigilar: **un color en un punto** o **una imagen** (la recortas arrastrando sobre la pantalla) en toda la pantalla o en una zona, con tolerancia. Qué hacer: clic encima, clic en otro punto, pulsar una tecla, iniciar los clics, reproducir una macro o **parar todo**. Una vez o cada vez que vuelva a aparecer (no repite mientras siga a la vista). Revisa la pantalla unas 5 veces por segundo; los juegos a pantalla completa "exclusiva" se ven negros (usa ventana o ventana sin bordes) |
| **Anti-AFK** (experimental) | Cada X minutos (1–18) trae al frente la ventana elegida (p. ej. Roblox) un instante, hace un clic en el punto elegido (o pulsa una tecla, p. ej. Espacio) y te devuelve a tu ventana con el ratón donde estaba. Espera a que dejes de usar el PC para no robarte teclas. **Probar ahora** hace un clic al momento |
| **⚙ Ajustes → Acerca de** | Versión y botón para abrir la carpeta de ajustes y del registro de errores |
| **📌** (barra de título) | Mantener la ventana siempre visible |

### Seguridad

- **F6** (o tu atajo) detiene siempre, aunque la ventana esté oculta.
- **Esquina**: lleva el ratón a cualquier esquina de cualquier monitor y se para.
- **Pausar si cambia la ventana**: solo clica en la ventana donde empezaste.

### Dónde se guarda

Todo se guarda solo en `%AppData%\AutoClik\settings.json` (perfiles, puntos, atajo, posición
de la ventana). Para empezar de cero, borra ese archivo con la app cerrada. Los ajustes de versiones
anteriores (`%AppData%\Autoclicker`) se importan solos la primera vez. Si ocurre un error inesperado,
los detalles quedan en `error.log`, en esa misma carpeta.

### Si Windows lo bloquea

El .exe no está firmado digitalmente. En otro PC, SmartScreen puede mostrar "Windows protegió su PC"
(→ *Más información* → *Ejecutar de todas formas*), y si tiene activado el **Control inteligente de
aplicaciones** podría bloquearlo. La solución definitiva es firmar el .exe con un certificado de firma
de código.

## Compilar

Requisitos: SDK de .NET (para compilar; el resultado se ejecuta con .NET Framework 4.8).

```powershell
dotnet run --project src\Autoclicker          # ejecutar en desarrollo
powershell -File build.ps1                    # genera dist\AutoClik.exe (~200 KB)
powershell -NoProfile -File tests\run-all.ps1 # batería de pruebas (mueve el ratón ~1,5 min)
```

Opciones de línea de comandos (pruebas): `--theme=light|dark`, `--lang=es|en`, `--data=<carpeta>`,
`--record-injected`, `--antiafk-seconds=N`. `--tray` arranca oculto en la bandeja.
Antes de `run-all.ps1`, cierra cualquier AutoClik abierto (las pruebas pulsan F6).

Los textos de la interfaz están en `src/Autoclicker/Localization/Strings.cs` (español e inglés).

## Estructura

```
src/Autoclicker/
  Engine/       motor de clics (hilo dedicado, temporizador de alta resolución, SendInput)
  Input/        atajo global, parada en esquina, grabadora, anti-AFK
  Watch/        Vigilar: leer la pantalla, buscar imágenes, reglas y vigilante
  Macros/       macros: modelo, pasos del editor, biblioteca, grabadora (hooks) y reproductor
  Markers/      puntos y marcadores en pantalla
  Persistence/  perfiles y guardado en JSON
  Interop/      Win32: fondo translúcido, bandeja, logo, inicio con Windows
  Theme/        paletas claro/oscuro, estilos de los controles y formas del logo
  Views/        selector de posición, marcadores, editor de puntos, editor de macros, diálogos, barra de grabación
tests/
  Autoclicker.Bench/  precisión del motor y clics reales sobre una ventana de prueba
tools/make-icon.ps1   regenera el icono del .exe (Assets/app.ico)
```
