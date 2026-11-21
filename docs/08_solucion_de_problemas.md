# 08. Solución de Problemas, Compilación Nativa (Linux/Arch/CachyOS/Windows) y FAQ (v9.0)

## 1. Preguntas Frecuentes de Uso

- **¿Necesito crear una carpeta para probar un código?**
  No. Usa **Archivo Sin Carpeta** en la pantalla inicial o el botón `+ Sin Carpeta` de la barra superior. Escribe tu código y presiona `F5`.
- **¿Por qué no veo mis claves de IA en texto plano?**
  Por diseño de seguridad, las API Keys se cifran antes de guardarse en SQLite (`xynex_accounts.db`). Ni los repositorios Git ni los logs del editor exponen tus secretos.
- **¿Cómo funciona la actualización automática?**
  Cuando `CynexStudios/alba-ecosistema` publica una versión superior a `9.0.0`, Xynex descarga el instalador/portable adecuado a tu sistema operativo, verifica su SHA-256 y, con la opción silenciosa activa, lo instala y reinicia el editor automáticamente.

## 2. Compilación Nativa por Plataforma

### Windows x64
```powershell
./scripts/compilar-todo.ps1 -Version "9.0.0"
```
Genera `XynexCodeEditor-9.0.0-win-x64-portable.zip`, el instalador Inno Setup `XynexCodeEditor-9.0.0-win-x64-setup.exe` y los binarios nativos.

### Linux (Portable Universal, Debian/Ubuntu `.deb`, Fedora `.rpm` y `.AppImage`)
```bash
bash packaging/linux/build-linux-packages.sh 9.0.0
```
El paquete `.tar.gz` es autocontenido y funciona en **CachyOS, Arch Linux, Ubuntu, Debian, Fedora, Mint y openSUSE** ejecutando `./xynex.sh`.

### Arch Linux y CachyOS (`.pkg.tar.zst`)
```bash
bash packaging/arch/build-arch-package.sh 9.0.0
sudo pacman -U dist/XynexCodeEditor-9.0.0-1-x86_64.pkg.tar.zst
```
En `xynex-code-editor` se incluye la compilación `net8.0` `linux-x64` autocontenida, escritorio `.desktop`, íconos PNG de alta resolución y enlaces directos `xynex`, `cibao` y `alba`.
