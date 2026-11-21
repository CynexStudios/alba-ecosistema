# 02. Instalación Multiplataforma, Requisitos y Actualización Automática (v9.0)

Xynex Code Editor 9.0 publica binarios autocontenidos (no necesitas instalar previamente el runtime de .NET en tu equipo para ejecutar Xynex ni programas Cibao).

## 1. Windows 10 / 11 (x64)

- **Instalador (`XynexCodeEditor-9.0.0-win-x64-setup.exe`):** Incluye asistente visual renovado, registro de extensiones `.cibao` / `.alb`, acceso en Menú Inicio y soporte para actualización silenciosa automática.
- **Portable (`XynexCodeEditor-9.0.0-win-x64-portable.zip`):** Descomprime en cualquier carpeta o USB y ejecuta `Xynex.exe` o `ide/Alba.IDE.exe`.

## 2. Arch Linux y CachyOS (Nativo)

- **Paquete Pacman nativo (`.pkg.tar.zst`):**
  ```bash
  sudo pacman -U XynexCodeEditor-9.0.0-1-x86_64.pkg.tar.zst
  ```
  Instala el binario nativo en `/opt/xynex-code-editor`, el comando global `xynex`, el compilador CLI `cibao` / `alba`, los íconos y la entrada de escritorio `.desktop`.
- **Portable para todas las distribuciones (`.tar.gz`):** Funciona en CachyOS, Arch, Manjaro, EndeavourOS, Debian, Ubuntu, Fedora y openSUSE sin requerir root:
  ```bash
  tar -xzf XynexCodeEditor-9.0.0-linux-x64-portable.tar.gz
  cd xynex-code-editor-portable
  ./xynex.sh
  ```

## 3. Ubuntu / Debian / Mint, Fedora / openSUSE y AppImage

- **Debian / Ubuntu / Mint:** `sudo apt install ./XynexCodeEditor-9.0.0-linux-amd64.deb`
- **Fedora / openSUSE / RHEL:** `sudo dnf install ./XynexCodeEditor-9.0.0-1.x86_64.rpm`
- **AppImage Universal:** `chmod +x XynexCodeEditor-9.0.0-x86_64.AppImage && ./XynexCodeEditor-9.0.0-x86_64.AppImage`

## 4. Actualizador Automático Integrado 9.0

Al iniciar, Xynex 9.0 consulta las releases oficiales en GitHub (`CynexStudios/alba-ecosistema`):
1. Detecta si tu instalación es Windows Setup, Windows Portable, Arch/CachyOS `pacman`, `.deb`, `.rpm`, `AppImage` o Linux Portable.
2. Descarga en segundo plano el paquete exacto y verifica su suma criptográfica en `SHA256SUMS.txt`.
3. Con la opción **Actualización silenciosa automática** activada (por defecto en 9.0), aplica la actualización de forma transparente y relanza el IDE actualizado.
