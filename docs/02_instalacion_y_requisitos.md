# 2. Requisitos, Instalación, Compilación y Distribución (`v2.0.0`)

## 2.1. Requisitos del sistema

### Para usuarios finales (Instaladores nativos)
Los paquetes generados para **Alba Studio 2.0.0** son autocontenidos (`--self-contained true`): **no requieren** instalar el SDK de .NET ni herramientas de compilación.
- **Windows:** Windows 10 / 11 (x64).
- **macOS:** macOS 12+ (Apple Silicon `osx-arm64` o Intel `osx-x64`).
- **Linux:** Distribuciones x64 modernas con X11 o Wayland (`libx11`, `libfontconfig1`).
- **Opcional:** Python 3 en el `PATH` para utilizar el complemento incluido de Python.

### Para compilar desde código fuente
| Componente | Windows (PC) | macOS | Linux |
| :--- | :--- | :--- | :--- |
| **SDK .NET** | .NET 8.0 SDK (o compatible `net8.0`) | .NET 8.0 SDK | .NET 8.0 SDK |
| **CMake** | CMake ≥ 3.25 | CMake ≥ 3.25 | CMake ≥ 3.25 |
| **Compilador C++20** | Visual Studio 2022/2026 (MSVC x64) | Xcode / Clang 14+ | GCC 11+ o Clang 14+ |

---

## 2.2. Compilación portable con CMake

### Windows (`win-x64`)
```powershell
cd alba-ecosistema
cmake --preset windows-auto
cmake --build --preset windows-auto
ctest --preset windows-auto
```

### Linux (`linux-x64`)
```bash
cd alba-ecosistema
cmake --preset linux-release
cmake --build --preset linux-release
ctest --preset linux-release
```

### macOS (`osx-arm64` Apple Silicon / `osx-x64` Intel)
```bash
cd alba-ecosistema
cmake --preset macos-arm64-release    # o macos-x64-release
cmake --build --preset macos-arm64-release
ctest --preset macos-arm64-release
```

Los binarios resultantes se generan en `build/<preset>/bin/Release/`:
- `alba-host` / `alba-host.exe` (lanzador nativo C++20)
- `cli/alba` / `cli/alba.exe` (CLI autónomo)
- `ide/Alba.IDE` / `ide/Alba.IDE.exe` (IDE Alba Studio 2.0)
- `addons/`, `docs/`, `examples/`

---

## 2.3. Generación de instaladores nativos

- **Windows (`.exe` con Inno Setup 6 y ZIP portable):**
  ```powershell
  .\packaging\windows\build-windows-installer.ps1
  ```
- **macOS (`.app` y `.dmg`):**
  ```bash
  ./packaging/macos/build-macos-dmg.sh 2.0.0 arm64   # o x64
  ```
- **Linux (`.deb`, `.rpm` y `.AppImage`):**
  ```bash
  ./packaging/linux/build-linux-packages.sh 2.0.0
  ```

---

## 2.4. Separación de datos del usuario

Alba Studio 2.0 nunca guarda configuraciones ni proyectos dentro de carpetas de instalación (`Program Files`, `/Applications`, `/opt`):
- **Configuración, recientes, sesión y addons de usuario:**
  - Windows: `%APPDATA%\AlbaStudio\`
  - macOS: `~/Library/Application Support/AlbaStudio/`
  - Linux: `~/.config/AlbaStudio/`
- **Proyectos:** Por defecto en `Documentos\ProyectosAlba\` (o la carpeta elegida por el usuario).
