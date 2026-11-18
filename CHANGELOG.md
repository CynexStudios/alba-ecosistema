# Registro de cambios (Changelog)

Todos los cambios relevantes de **Alba Studio** y el **Lenguaje Alba** se documentan en este archivo siguiendo [Versionado Semántico](https://semver.org/lang/es/).

## [2.0.0] - 2026-11-18

### Añadido
- **Identidad visual y logotipo oficial:** Emblema propio de Alba Studio en `assets/` (`SVG`, `PNG`, `ICO`, `ICNS`) integrado en ventanas e instaladores.
- **Pantalla de inicio moderna:** Accesos rápidos, historial de proyectos/archivos recientes, novedades de versión y ejemplos directos.
- **Sistema de temas y sintaxis personalizable:** 3 temas incluidos (*Alba Crepúsculo*, *Alba Medianoche* y *Alba Amanecer*) + editor hexadecimal de colores de sintaxis persistente.
- **Espacio de trabajo renovado:** Menú superior completo, Activity Rail con Explorador (menú contextual con renombrar/duplicar/eliminar confirmado), Búsqueda global en proyecto (`Ctrl+Shift+F`), Addons y Herramientas.
- **Diagnóstico en vivo:** Detección en segundo plano de errores léxicos/sintácticos de Alba con subrayado ondulado en el editor, tooltip explicativo y panel **Problemas**.
- **Terminal integrada real:** Shell del sistema operativo (`powershell.exe` en Windows, `bash`/`sh` en Linux/macOS) dentro del panel inferior.
- **Productividad:** Paleta de comandos (`Ctrl+Shift+P`), apertura rápida de archivos (`Ctrl+P`), autoguardado configurable, recuperación de sesión, atajos configurables, notificaciones discretas (*toast*), visor integrado de documentación y registro de actividad.
- **Actualizaciones:** Comprobador transparente contra GitHub Releases (`CynexStudios/alba-ecosistema`) sin descargas ni instalaciones silenciosas.
- **Distribución multiplataforma:** Scripts e instaladores nativos para Windows (`.exe` con Inno Setup / ZIP), macOS (`.app` y `.dmg` para Apple Silicon e Intel) y Linux (`.deb`, `.rpm`, `.AppImage`), más workflows de CI/CD en GitHub Actions.

### Cambiado
- **Separación de datos de usuario y binarios:** Configuraciones, historial, sesiones y addons instalados por el usuario se almacenan en `%APPDATA%\AlbaStudio` / `Application Support` / `~/.config/AlbaStudio`, sin escribir dentro de carpetas de instalación de solo lectura.
- **CMake y NativeHost:** Actualizados a `2.0.0` añadiendo soporte oficial para macOS (`osx-arm64`, `osx-x64`).

## [0.1.0] - 2026-11-17

### Añadido
- Primera versión completa del motor `Alba.Engine` (Lexer, Parser, AST, Intérprete y diagnósticos `ALB-*`).
- CLI (`alba`) y lanzador nativo C++20 (`alba-host`).
- Primera versión de `Alba.IDE` en Avalonia UI 11 con explorador básico, consola interactiva y sistema de `/addons` con soporte inicial para Python.
- Manual completo de documentación en `docs/01..08_*.md`.
