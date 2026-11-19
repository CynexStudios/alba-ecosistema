# 2. Requisitos, Instalación, Compilación y Distribución (`v5.0.0`)

## 2.1. Requisitos del sistema

### Para usuarios finales (Instaladores autocontenidos)
Los paquetes generados para **Xynex Code Editor 5.0.0** son autocontenidos (`--self-contained true`): **no requieren** instalar el SDK de .NET ni herramientas de compilación.
- **Windows:** Windows 10 / 11 (`win-x64`).
- **Linux:** Distribuciones `linux-x64` modernas con X11 o Wayland (`libx11-6`, `libfontconfig1`).
- **macOS (opcional bajo demanda):** macOS 12+ (`osx-arm64` / `osx-x64`).
- **Opcional:** Python 3, Node.js, GCC/Clang, Rust, Go o JDK si deseas ejecutar proyectos en esos lenguajes desde el IDE.

### Para compilar desde código fuente
| Componente | Windows (PC) | Linux |
| :--- | :--- | :--- |
| **SDK .NET** | .NET 8.0 SDK | .NET 8.0 SDK |
| **CMake** | CMake ≥ 3.25 | CMake ≥ 3.25 |
| **Compilador C++20** | Visual Studio 2022/2026 (MSVC x64) | GCC 11+ o Clang 14+ |

---

## 2.2. Compilación y pruebas rápidas

### Con scripts unificados (recomendado)
```powershell
# Windows (compila solución, ejecuta xUnit y genera .exe/.zip si Inno Setup 6 está instalado)
.\scripts\compilar-todo.ps1 -Version 5.0.0
```
```bash
# Linux (compila solución, ejecuta xUnit y genera .tar.gz / .deb / .rpm / .AppImage)
./scripts/compilar-todo.sh 5.0.0
```

### Con CMake o .NET CLI directamente
```powershell
# Windows
dotnet test AlbaEcosistema.sln -c Release
cmake --preset windows-auto
cmake --build --preset windows-auto
ctest --preset windows-auto
```
```bash
# Linux
dotnet test AlbaEcosistema.sln -c Release
cmake --preset linux-release
cmake --build --preset linux-release
ctest --preset linux-release
```

---

## 2.3. Generación de instaladores nativos y Releases en GitHub

- **Windows (Inno Setup 6 + ZIP portable):**
  `.\packaging\windows\build-windows-installer.ps1 -Version 5.0.0`
- **Arch Linux / CachyOS (`.pkg.tar.zst` nativo con `pacman`):**
  ```bash
  # Instalar desde GitHub Releases:
  sudo pacman -U XynexCodeEditor-5.0.0-arch-cachyos-x86_64.pkg.tar.zst

  # O compilar nativamente desde código fuente:
  ./packaging/arch/build-arch-package.sh 5.0.0
  # También disponible con makepkg directo:
  cd packaging/arch && makepkg -si
  ```
- **Otras distros Linux (`.tar.gz`, `.deb`, `.rpm` y `.AppImage`):**
  `./packaging/linux/build-linux-packages.sh 5.0.0`
- **macOS (opcional bajo demanda):**
  `./packaging/macos/build-macos-dmg.sh 5.0.0 arm64`

### Publicación automática desde GitHub Actions
Si no cuentas con herramientas de empaquetado local:
1. Sube tus cambios a GitHub (asegurándote de incluir la carpeta oculta `.github/workflows/`).
2. Crea una etiqueta/Release `v5.0.0` (o lanza **Release Multiplataforma** desde **Actions → Run workflow**).
3. El flujo generará y adjuntará automáticamente `XynexCodeEditor-5.0.0-win-x64-setup.exe`, `portable.zip`, `arch-cachyos-x86_64.pkg.tar.zst`, `.deb`, `.rpm`, `.AppImage`, `.tar.gz` y `SHA256SUMS.txt`.

### Actualizador integrado en Xynex Code Editor
Desde **Ayuda → Comprobar actualizaciones...**, la barra de estado o **Ajustes → Actualizaciones**, el IDE detecta nuevas versiones en GitHub Releases, selecciona automáticamente el paquete de tu sistema operativo (`.exe`, `.zip`, `.pkg.tar.zst`, `.deb`, `.rpm`, `.AppImage` o `.tar.gz`), verifica su firma **SHA-256**, aplica la actualización y reinicia el editor conservando tus ajustes y proyectos.

---

## 2.4. Separación de datos del usuario

Xynex Code Editor separa estrictamente los binarios instalados respecto a los datos del usuario:
- **Configuración, historial, sesiones y addons instalados:**
  - Windows: `%APPDATA%\XynexCodeEditor\`
  - macOS: `~/Library/Application Support/XynexCodeEditor/`
  - Linux: `~/.config/XynexCodeEditor/`
- **Proyectos:** Por defecto en `Documentos\ProyectosCibao\` (o la carpeta elegida por el usuario).
