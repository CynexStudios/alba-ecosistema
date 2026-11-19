# 1. Introducción, Filosofía y Estructura del Proyecto (`v5.0.0`)

## 1.1. ¿Qué es Cibao y Xynex Code Editor?

**Cibao** (*Cibao Programming Language*, extensión principal `.cibao` y retrocompatibilidad total con `.alb`, versión **5.0.0**) es un lenguaje de programación de propósito general diseñado en **español natural**, nacido de las raíces de su creador **Jaderson Liriano (`CynexStudios`)**.

Se distribuye junto con su motor (`Alba.Engine`), su CLI dual (`cibao` / `alba`), su lanzador nativo portable (`alba-host` en C++20) y su entorno oficial de desarrollo: **Xynex Code Editor**.

## 1.2. Filosofía y objetivos

Cibao combina la expresividad moderna con una sintaxis clara en español sin renunciar a la ingeniería estructurada:

- **Legibilidad natural:** Sin punto y coma obligatorio, operadores lógicos en español (`y`, `o`, `no`), inferencia de tipos, interpolación `$"..."`, diccionarios nativos `{ clave: valor }` y coalescencia nula `??`.
- **Estructura sólida y orientada a objetos:** Bloques `{ ... }`, tipado gradual opcional (`: entero`, `: decimal`, `: texto`, `: booleano`, `: lista`, `: diccionario`), funciones y lambdas tipadas opcionalmente (`-> tipo`), clases con herencia (`extiende`, `super`, `es`) y encapsulamiento (`publico`, `privado`).
- **Modularidad y programación funcional:** Importación real de módulos (`importar "..." como ...`) y métodos funcionales integrados (`.filtrar()`, `.mapear()`, `.reducir()`, `.ordenar()`).
- **Errores comprensibles:** Todo error léxico, sintáctico, de tipo o de ejecución reporta archivo, línea, columna, fragmento y explicación detallada en español dentro de la consola y en vivo en el editor.
- **Ecosistema abierto y multiplataforma:** Compila e instala nativamente en **Windows (`win-x64`)** y **Linux (`linux-x64`)** (con soporte opcional para macOS bajo demanda) mediante **.NET 8**, **CMake** e instaladores autocontenidos.

## 1.3. Estructura exacta del proyecto

```text
alba-ecosistema/
├── .github/workflows/               # CI multiplataforma y Release automático (Windows + Linux)
├── CMakeLists.txt                   # Orquestador multiplataforma (.NET 8 + C++20)
├── CMakePresets.json                # Perfiles debug/release para CMake
├── Directory.Build.props            # Metadatos y versión centralizada (5.0.0)
├── AlbaEcosistema.sln               # Solución .NET 8
├── README.md / CHANGELOG.md / LICENSE
├── assets/                          # Logotipos, iconos, temas JSON y branding
├── packaging/                       # Instaladores nativos (.exe, .deb/.rpm/.AppImage/.dmg)
├── scripts/                         # Scripts unificados compilar-todo.ps1 / .sh
├── native-launcher/                 # Host nativo C++20 (alba-host)
├── src/
│   ├── Alba.Engine/                 # Lexer, AST, Parser, Diagnósticos e Intérprete Cibao 5.0
│   ├── Alba.CLI/                    # Ejecutor de línea de comandos (cibao / alba)
│   └── Alba.IDE/                    # Xynex Code Editor (Avalonia UI 11)
├── addons/                          # Complementos integrados (Python 3, etc.)
├── examples/                        # Programas de ejemplo en .cibao y .alb
├── tests/
│   ├── Alba.Engine.Tests/           # Pruebas unitarias Lexer, Parser, Runtime y Cibao 5.0
│   ├── Alba.IDE.Tests/              # Pruebas del IDE, addons, temas, búsqueda y diagnósticos
│   └── Alba.NativeHost.Tests/       # Smoke test C++20 para CTest
└── docs/                            # Documentación técnica oficial
```
