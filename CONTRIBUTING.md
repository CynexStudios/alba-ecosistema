# Guía para Contribuir a Alba Studio y el Lenguaje Alba

¡Gracias por tu interés en contribuir al proyecto **Alba** de **CynexStudios**!

## Cómo empezar

1. Haz un *fork* del repositorio y crea una rama descriptiva:
   ```bash
   git checkout -b feat/mi-mejora
   ```
2. Requisitos locales para compilar desde código fuente:
   - SDK de .NET 8.0 (o superior con compatibilidad `net8.0`)
   - CMake ≥ 3.25 y compilador C++20 (MSVC, GCC o Clang)
3. Verifica que todas las pruebas pasen antes de abrir un Pull Request:
   ```bash
   dotnet test AlbaEcosistema.sln -c Release
   ```

## Reglas del proyecto

- Mantén la compatibilidad del motor `src/Alba.Engine` con el manual oficial (`docs/03_manual_del_lenguaje.md`).
- Evita romper la seguridad de `ValidadorAddons` (no permitas ejecución arbitraria ni rutas fuera del directorio del addon).
- En la interfaz Avalonia (`11.2.1`), verifica que todas las vistas sean compatibles con Windows, macOS y Linux y respeten los temas (`DynamicResource`).
- Al enviar contribuciones, aceptas que queden licenciadas bajo **Apache License 2.0**.
