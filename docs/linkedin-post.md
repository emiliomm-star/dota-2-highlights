# Borradores para LinkedIn

> Copia, ajusta el tono a tu voz y **acompáñalo SIEMPRE de un vídeo/GIF** de un highlight capturado + su edición Pro Montage. El vídeo es lo que genera alcance.

---

## Versión A — Post principal (recomendada)

🎮 Llevo semanas construyendo un proyecto personal que me tenía muy enganchado: **una app que captura y edita SOLA mis mejores jugadas de Dota 2.**

La idea nació de algo simple: cuando haces una gran jugada, ya pasó. Grabar toda la partida y editar a mano es un peñazo, y al final pierdes tus highlights.

Así que construí una app (C# / .NET 10) que corre en segundo plano y:

🔴 Graba siempre en un buffer en RAM → puede guardar lo que YA ocurrió.
🧠 Detecta sola los momentos épicos usando la integración oficial de Dota 2 (Game State Integration): doble/triple kills, escapes clutch a baja vida, teamfights, rachas…
🎬 Genera automáticamente varias ediciones "con aura" (slow-mo en el kill, zoom, flashes al ritmo de tu música, versión vertical 9:16 para TikTok/Reels…), con overlays del héroe y el tipo de jugada.

Todo esto **sin apretar un solo botón.**

Lo más divertido no fueron los efectos, sino las decisiones de ingeniería: diseñar el sistema alrededor de una única interfaz (`IHighlightTrigger`) para poder enchufar nuevas formas de detección sin reescribir nada, hacer la detección de beats de la música en C# puro, o pelearme con NVENC y ffmpeg hasta que todo el pipeline volaba en la GPU.

Es un **MVP funcional** y ya captura highlights reales. Lo siguiente: detección por visión (ONNX) para jugadas que no son kills, y un LLM para auto-generar títulos y hashtags.

👉 Está en GitHub (link en comentarios) y **busco feedback y gente a la que le apetezca sumarse.** ¿Qué jugada de Dota crees que debería detectar y no lo hace todavía?

#gamedev #csharp #dotnet #dota2 #machinelearning #softwareengineering #buildinpublic #proyectopersonal

---

## Versión B — Corta (para quien lee poco)

🎮 Mi último proyecto personal: una app en **C# / .NET 10** que **captura y edita SOLA** tus mejores jugadas de Dota 2.

Corre en segundo plano, detecta los momentos épicos con la integración oficial del juego (kills, escapes clutch, teamfights…) y genera varias ediciones listas para TikTok/Reels — slow-mo, zoom, flashes al beat de tu música, versión vertical… todo sin tocar un botón.

MVP funcional, en GitHub 👇. Busco feedback y colaboradores.
¿Qué le añadirías?

#csharp #dotnet #gamedev #dota2 #buildinpublic

---

## Ideas de "carrete" (varios posts, build in public)

1. **El teaser**: el vídeo del antes/después (gameplay → Pro Montage). "¿Y si tu PC editara tus highlights por ti?"
2. **La decisión técnica**: por qué usé la integración oficial (GSI) en vez de IA de visión para detectar kills. Pragmatismo > sobre-ingeniería.
3. **El buffer**: el truco de "grabar el pasado" — cómo guardas algo que ya ocurrió.
4. **El pipeline de edición**: cómo convertí ffmpeg en un motor de estilos componibles.
5. **El reto**: la detección de beats en C# puro para sincronizar los flashes con la música.
6. **El roadmap**: qué falta (visión ONNX, LLM para captions) y llamada a colaborar.
