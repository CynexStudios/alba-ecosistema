# 1. Introducción, Filosofía y Estructura del Proyecto

## 1.1. ¿Qué es Alba?

**Alba** (extensión `.alb`, ecosistema **v2.0.0**) es un lenguaje de programación de propósito general diseñado íntegramente en **español natural**, acompañado de su propio motor de ejecución (`Alba.Engine`), su herramienta de consola (`Alba.CLI`), un lanzador nativo portable (`alba-host`) y su entorno de desarrollo oficial (**Alba Studio 2.0**).

## 1.2. Filosofía y objetivos

Alba busca eliminar las barreras iniciales del idioma inglés y de la sintaxis críptica sin sacrificar la estructura necesaria para construir software organizado:

- **Legibilidad inspirada en Python:** Sin punto y coma obligatorio al final de cada línea, operadores lógicos en español (`y`, `o`, `no`), ciclos `para ... en` e inferencia de tipos mediante `variable` y `constante`.
- **Estructura inspirada en C#:** Bloques delimitados explícitamente con llaves `{ ... }`, tipado explícito opcional (`: entero`, `: decimal`, `: texto`, `: booleano`, `: lista`), funciones tipadas opcionalmente (`-> tipo`), clases con `constructor`, referencia `este` y encapsulamiento (`publico`, `privado`).
- **Errores comprensibles:** Todo error léxico, sintáctico, de tipo o de ejecución reporta archivo, línea, columna, fragmento fuente y explicación en español, con diagnóstico en vivo dentro del editor.
- **Portabilidad real:** Todo el ecosistema compila y se empaqueta para **Windows (`win-x64`)**, **macOS (`osx-arm64` / `osx-x64`)** y **Linux (`linux-x64`)** desde un único flujo basado en **CMake**, **.NET 8** e instaladores nativos.

## 1.3. Estructura exacta del proyecto

```text
alba-ecosistema/
├── CMakeLists.txt                   # Orquestador multiplataforma Windows / macOS / Linux
├── CMakePresets.json                # Perfiles debug/release para CMake
├── Directory.Build.props            # Metadatos y versión centralizada (2.0.0)
├── AlbaEcosistema.sln               # Solución .NET 8
├── README.md / CHANGELOG.md / LICENSE
├── assets/                          # Logotipos, iconos, temas JSON y branding 2.0
├── packaging/                       # Instaladores nativos (.exe, .dmg, .deb/.rpm/.AppImage)
├── native-launcher/                 # Host nativo C++20 (alba-host)
├── src/
│   ├── Alba.Engine/                 # Lexer, AST, Parser, Diagnósticos e Intérprete
│   ├── Alba.CLI/                    # Ejecutor de línea de comandos (alba)
│   └── Alba.IDE/                    # IDE nativo Avalonia UI 11 (Alba Studio 2.0)
├── addons/
│   └── python/                      # Complemento oficial funcional para Python 3
├── examples/                        # Programas de ejemplo en .alb
├── tests/
│   ├── Alba.Engine.Tests/           # Pruebas unitarias Lexer, Parser, Intérprete y Errores
│   ├── Alba.IDE.Tests/              # Pruebas de proyectos, addons, temas, búsqueda y diagnóstico
│   └── Alba.NativeHost.Tests/       # Smoke test C++20 para CTest
└── docs/                            # Documentación técnica oficial
```
