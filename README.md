# Dota 2 Highlights AI

Cliente WPF (.NET 10) + servicio Python de IA (gRPC + ONNX Runtime GPU) para detectar y guardar highlights de Dota 2 automáticamente.

Ver `../GUIA-INSTALACION-DOTA2-HIGHLIGHTS.md` y `../PROYECTO-DOTA2-HIGHLIGHTS-CONTEXTO.md`.

## Estructura
- `src/DotaHighlights.Client` — cliente WPF (captura, buffer, UI).
- `src/dota2_ai` — servicio gRPC en Python (inferencia ONNX).
- `Installers/WiX` — instalador .msi.
- `.github/workflows` — CI/CD.
