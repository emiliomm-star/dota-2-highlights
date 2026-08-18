# 🎬 Dota 2 Highlights AI

> Captura **automáticamente** tus mejores jugadas de Dota 2 y las convierte en clips editados listos para redes — sin apretar un solo botón.

**Estado:** MVP funcional · **Plataforma:** Windows 11 · **Stack:** C# / .NET 10 (WPF) · ffmpeg + NVENC · Dota 2 Game State Integration

---

## 💡 El problema

Cuando haces una gran jugada en Dota 2, ya pasó. Grabar toda la partida y editar a mano los momentos buenos es tedioso, y la mayoría de la gente simplemente **pierde sus highlights**.

## 🎯 La idea de producto

Una app que corre en segundo plano mientras juegas, **detecta sola** los momentos épicos, guarda el clip **hacia atrás** (los segundos previos + el desenlace) y te entrega **varias ediciones "con aura"** listas para subir a TikTok / Reels / YouTube — con tu propia música si quieres.

La visión final es una pequeña **"tienda" de capturadores** por juego (hoy solo Dota 2), donde cada juego es una app dentro del launcher.

---

## ✅ Qué hace HOY (MVP)

**Captura inteligente**
- Grabación continua en un **buffer circular en RAM** (siempre tiene los últimos ~25 s), así puede guardar lo que *ya* ocurrió.
- Captura de pantalla moderna con **Windows.Graphics.Capture** (Direct3D 11), codificada por hardware con **NVENC**.

**Detección automática (sin botones)** vía **Game State Integration** oficial de Dota 2:
- 🔪 **Multi-kills** — doble / triple / ultra / rampage (con escalada: si un doble se vuelve triple, sube el nivel en un solo clip).
- 🛡️ **Jugadas sin kill** — escapes clutch (sobrevivir a baja vida), teamfights (asistencias), rachas de kills (Mega Kill → Godlike).
- Cada clip incluye un **post-roll** para capturar el desenlace, no solo el momento previo.

**Editor automático** — por cada highlight genera 5-6 versiones con ffmpeg + NVENC:
- 🔥 **Pro Montage** — slow-mo suave en el kill + zoom con shake + flash + grade *teal-orange* + viñeta + grano + barras de cine.
- 📱 **Vertical 9:16** — recorte para TikTok / Shorts / Reels.
- 🥁 **Beat Sync** — flashes sincronizados con los golpes de *tu* música (detección de beats propia).
- 🐢 Slow-mo · 🔍 Zoom · 🎞️ Cinematic.
- **Overlays con datos reales**: quema "RAMPAGE", el nombre del héroe y el contador de kills usando la info de la partida.
- **Tu música**: carga un `.mp3`/`.wav` y se mezcla en las ediciones (en bucle + recortada al clip).

**Otros**
- UI tipo **launcher / tienda** (WPF, MVVM).
- Atajo global **F9** como respaldo manual.
- Logging (Serilog), instalador **WiX**, **CI/CD** con GitHub Actions.

---

## 🏗️ Arquitectura y decisiones de ingeniería

El corazón del diseño es una interfaz simple:

```
IHighlightTrigger  →  "¡guarda los últimos N segundos!"
```

Todo lo que puede pedir un highlight implementa esa interfaz: el hotkey F9, el detector de multi-kills, el de jugadas sin-kill y —en el futuro— un detector por visión IA. **Añadir una nueva forma de detectar = una clase nueva, sin tocar el resto.**

Algunas decisiones que valió la pena tomar:
- **GSI antes que visión IA para los kills**: Dota ya expone los eventos exactos por su integración oficial → detección precisa y determinista sin entrenar nada.
- **Buffer del *pasado***: la clave para "guardar lo que ya pasó" es grabar siempre; el gatillo solo decide *cuándo* volcar.
- **Editor como *pipeline* de presets**: cada efecto es un fragmento de filtro ffmpeg componible → escalar a más estilos es trivial.
- **Detección de beats en C# puro** (análisis de energía del audio), sin dependencias pesadas.
- Un par de *gotchas* reales resueltos: puertos reservados por Hyper-V/WSL (se usa un `TcpListener` en un puerto seguro), y el escapado de rutas de fuente de ffmpeg en Windows para los overlays.

### Stack técnico
| Área | Tecnología |
|---|---|
| Cliente escritorio | C# / .NET 10, **WPF**, CommunityToolkit.MVVM |
| Captura | Windows.Graphics.Capture, **Vortice.Direct3D11** |
| Vídeo | **ffmpeg** + **NVENC** (H.264 por GPU) |
| Datos de juego | Dota 2 **Game State Integration** (HTTP local) |
| Logging / Instalador / CI | Serilog · **WiX v7** · GitHub Actions |
| Futuro (IA de visión) | Servicio Python + **ONNX Runtime** (gRPC), dockerizado |

---

## 🚧 Qué falta / Roadmap

- 🔊 **Captura de audio del juego** (hoy los clips son mudos salvo la música añadida).
- 👁️ **Detección por visión (ONNX)** para jugadas puramente visuales que GSI no ve (esquivar un skillshot, un combo espectacular).
- 🤖 **LLM** para auto-generar títulos, descripciones y hashtags al publicar.
- ⭐ **Recomendación que aprende** qué estilo de edición prefieres.
- 🎯 Auto-reframe inteligente para la vertical, persistencia de configuración, más juegos en la tienda.

---

## 🤝 Contribuir

Es un proyecto personal en evolución y **toda ayuda / feedback es bienvenida** — desde ideas de detección hasta efectos de edición o mejoras de arquitectura. Abre un issue o escríbeme.

## 📄 Licencia

**GNU AGPL-3.0** — código abierto para que lo veas, aprendas y contribuyas, pero cualquier
copia o derivado (incluido si se ofrece como servicio) **debe permanecer también open source**.
Es decir: míralo y aprende, pero no puede convertirse en un producto cerrado de terceros.

© 2026 Emilio Muñoz Monterrey. Ver [LICENSE](LICENSE).
