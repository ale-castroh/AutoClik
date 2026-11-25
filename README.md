# AutoClik

Autoclicker para Windows 10/11: ligero (un solo .exe de ~200 KB), minimalista, preciso (error medio ≈ 0,1 ms, hasta 1000 clics/s)
y con tema claro/oscuro que sigue al de Windows. Idioma automático: español si Windows está en
español; inglés en cualquier otro idioma.

## Uso rápido

1. Abre `AutoClik.exe`. No necesita instalación: usa .NET Framework 4.8, que ya viene con
   Windows 10 (1903 o posterior) y Windows 11. Abre en medio segundo. Si lo abres otra vez,
   no se duplica: aparece delante la ventana que ya estaba abierta (también desde la bandeja).
2. Elige **Intervalo**, **Botón**, **Tipo** y **Repetir**.
3. Pulsa **Iniciar** o **F6**. Vuelve a pulsar **F6** para parar.

La app no clica sobre su propia ventana, así que puedes iniciar con el ratón, mover el cursor
a donde quieras y volver para pulsar **Detener**.

## Funciones

| Dónde | Qué |
|---|---|
| **Intervalo** | Número + unidad (ms · s · min · h), desde 1 ms |
| **Botón** | Izquierdo, derecho o central |
| **Tipo** | Simple, doble o mantener pulsado (duración en ms) |
| **Repetir** | Sin fin, N veces (o N vueltas con puntos) o durante X segundos |
| **Posición → Cursor** | Clica donde esté el ratón |
| **Posición → Fijo** | Clica siempre en un punto; **Elegir** lo marca en pantalla |
| **Posición → Puntos** | Varios marcadores numerados que se recorren en orden |
| **Puntos → Añadir** | Cada clic en la pantalla añade un marcador; Esc termina |
| **Puntos → Grabar** | Graba tus clics reales (posición, botón y tiempos) y los convierte en puntos; si arrastras, se graba como arrastre (en línea recta) |
| **Puntos → Velocidad** | Reproduce la secuencia a ×0,5 · ×1 · ×2 · ×4 |
| **Marcador** | Arrástralo para moverlo; clic para editar botón, tipo, espera, orden o borrarlo |
| **Perfil** (barra de título) | Cambiar, crear (copia del actual), renombrar o eliminar configuraciones |
| **⚙ Ajustes → Atajo** | Clic en la tecla y pulsa otra (teclas, combinaciones o botones laterales del ratón). Modo *Alternar* o *Mantener* |
| **⚙ Ajustes → Clics** | Variación aleatoria del intervalo, desvío del punto fijo, retardo de inicio, pausar si cambia la ventana activa, devolver el ratón a su sitio tras cada clic (posición fija o puntos) |
| **⚙ Ajustes → General** | Parar al llevar el ratón a una esquina, minimizar/cerrar a la bandeja, iniciar con Windows (oculto en la bandeja) |
| **⚙ Ajustes → Experimental → Anti-AFK** | Cada X minutos (1–18) trae al frente la ventana elegida (p. ej. Roblox) un instante, hace un clic en el punto elegido y te devuelve a tu ventana con el ratón donde estaba. Espera a que dejes de usar el PC para no robarte teclas |
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

Opciones de línea de comandos (pruebas): `--theme=light|dark`, `--lang=es|en`, `--data=<carpeta>`.
Antes de `run-all.ps1`, cierra cualquier AutoClik abierto (las pruebas pulsan F6).

Los textos de la interfaz están en `src/Autoclicker/Localization/Strings.cs` (español e inglés).

## Estructura

```
src/Autoclicker/
  Engine/       motor de clics (hilo dedicado, temporizador de alta resolución, SendInput)
  Input/        atajo global, parada en esquina, grabadora
  Markers/      puntos y marcadores en pantalla
  Persistence/  perfiles y guardado en JSON
  Interop/      Win32: Mica, bandeja, iconos
  Theme/        paletas claro/oscuro y estilos
  Views/        selector de posición, marcadores, editor, diálogos, barra de grabación
tests/
  Autoclicker.Bench/  precisión del motor y clics reales sobre una ventana de prueba
tools/make-icon.ps1   regenera el icono
```
