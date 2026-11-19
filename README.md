<p align="center">
  <img src="alba-ecosistema/assets/branding/banner-github.png" alt="Xynex Code Editor &amp; Cibao 5.0 — CynexStudios" width="100%" />
</p>

<h1 align="center">Xynex Code Editor &amp; Cibao Programming Language (v5.0.0)</h1>

<p align="center">
  <strong>Lenguaje de programación moderno en español (<em>Cibao</em>, <code>.cibao</code> / <code>.alb</code>) y su entorno de desarrollo integrado multiplataforma (Windows y Linux; macOS bajo demanda).</strong>
</p>

<p align="center">
  Autor: <strong>Jaderson Liriano</strong> (<a href="https://github.com/CynexStudios">CynexStudios</a>) • Versión actual: <code>5.0.0</code> • Licencia: <a href="LICENSE">Apache 2.0</a>
</p>

---

## Características principales

- **Cibao Programming Language (`v5.0.0`, extensiones `.cibao` y `.alb`):**
  - **Diccionarios nativos (`diccionario`):** Literales `{ clave: valor }`, indexación `d["clave"]` o `d.clave`, iteración con `para k en d` y serialización JSON (`json_a_texto` / `texto_a_json`).
  - **Interpolación de texto (`$"..."` / `$'...'`):** Inserción directa de expresiones y acceso a propiedades (`$"Hola {usuario.nombre ?? 'Invitado'}"`).
  - **Orientación a objetos completa:** Clases, herencia con `extiende`, llamadas base con `super(...)` / `super.metodo()`, encapsulamiento `publico`/`privado`, polimorfismo e inspección `es`.
  - **Control de flujo y programación funcional:** Selección múltiple sin *fall-through* (`elegir` / `caso` / `defecto`), funciones anónimas con *closures* (`funcion(x) { ... }`), coalescencia nula (`??`) y métodos por punto (`.filtrar()`, `.mapear()`, `.reducir()`, `.ordenar()`, `.mayusculas()`, etc.).
  - **Módulos reales (`importar`):** Importación relativa de archivos `.cibao`/`.alb` con caché única, detección de ciclos (`ALB-E098`), alias (`como`) y módulos estándar integrados (`"matematicas"`, `"texto"`, `"json"`, `"archivos"`).
  - **100% retrocompatible:** Todos los scripts previos `.alb` continúan ejecutándose sin modificaciones.
- **Xynex Code Editor (`Alba.IDE` — Avalonia UI 11):**
  - Pantalla de bienvenida moderna, historial persistente de proyectos recientes y visor integrado de documentación (`Ayuda → Documentación`).
  - Resaltado sintáctico Cibao 5.0 (`alba.xshd`) + **autocompletado inteligente** + **diagnóstico en vivo** con subrayado rojo y panel **Problemas**.
  - Sistema de **3 temas** (*Crepúsculo*, *Medianoche*, *Amanecer*) y personalización hexadecimal de colores de sintaxis.
  - **Terminal integrada real** (PowerShell en Windows, `bash`/`sh` en Linux), **Paleta de Comandos** (`Ctrl+Shift+P`), apertura rápida (`Ctrl+P`) y búsqueda global (`Ctrl+Shift+F`).
  - Soporte políglota nativo y extensible (`/addons`): Python 3 integrado, Node.js, TypeScript, C#, C++, Rust, Go, Java, PHP, Ruby y HTML con vista previa Live Server.
- **CLI (`cibao` / `alba`) y Host Nativo C++20 (`alba-host`):** Ejecución y verificación por consola sin abrir el IDE.

---

## Tecnologías utilizadas

| Componente | Tecnología |
| :--- | :--- |
| Motor del lenguaje y CLI | C# 12 / .NET 8.0 (`Alba.Engine`, `Alba.CLI`) |
| Interfaz de escritorio (IDE) | Avalonia UI `11.2.1` + AvaloniaEdit `11.1.0` |
| Lanzador nativo portable | C++20 (`std::filesystem`, procesos nativos) |
| Orquestación multiplataforma | CMake ≥ 3.25 + CTest + Scripts locales (`scripts/`) |
| Empaquetado y CI/CD | Inno Setup 6 (Windows `.exe`/`.zip`), `.deb`/`.rpm`/`.AppImage`/`.tar.gz` (Linux), GitHub Actions |

---

## Descarga e instalación (usuarios finales)

Los instaladores oficiales son **autocontenidos** (`--self-contained true`): **no requieren instalar .NET SDK**.

- **Descargas oficiales:** [GitHub Releases — CynexStudios/alba-ecosistema](https://github.com/CynexStudios/alba-ecosistema/releases)

### Windows (`win-x64`)
1. Descarga `XynexCodeEditor-5.0.0-win-x64-setup.exe` (o `XynexCodeEditor-5.0.0-win-x64-portable.zip`).
2. Ejecuta el instalador guiado para instalar **Xynex Code Editor**, asociar archivos `.cibao` y `.alb` y añadir los comandos `cibao` y `alba` a tu `PATH`.

### Linux (`linux-x64`)
- **Debian / Ubuntu / Mint:**
  ```bash
  sudo apt install ./XynexCodeEditor-5.0.0-linux-x64.deb
  ```
- **Fedora / RHEL:**
  ```bash
  sudo dnf install ./XynexCodeEditor-5.0.0-linux-x64.rpm
  ```
- **Universal portable (AppImage o Tarball):**
  ```bash
  chmod +x XynexCodeEditor-5.0.0-linux-x64.AppImage
  ./XynexCodeEditor-5.0.0-linux-x64.AppImage
  ```

*(Nota: la compilación automatizada para macOS está diferida a ejecución manual bajo demanda en GitHub Actions para mantener ágil la matriz principal Windows + Linux).*

---

## Cómo publicar un Release en GitHub sin compilar localmente

El flujo `.github/workflows/release.yml` compila y publica automáticamente los instaladores en la nube:
1. Sube tus cambios a la rama `main` en GitHub.
2. En GitHub, ve a **Tags / Releases → Draft a new release**, crea la etiqueta `v5.0.0` (o usa la pestaña **Actions → Release Multiplataforma → Run workflow**).
3. GitHub Actions ejecutará pruebas unitarias, compilará Windows y Linux y adjuntará automáticamente `.exe`, `.zip`, `.deb`, `.rpm`, `.AppImage`, `.tar.gz` y `SHA256SUMS.txt`.

---

## Compilación desde código fuente (desarrolladores)

### Requisitos
- **SDK de .NET 8.0**
- **CMake ≥ 3.25** y **Compilador C++20** (Visual Studio / MSVC en Windows, GCC 11+ en Linux)

### Comandos rápidos
```powershell
# Windows: compilar, probar y empaquetar (.exe / .zip)
.\scripts\compilar-todo.ps1 -Version 5.0.0
```
```bash
# Linux: compilar, probar y empaquetar (.deb / .rpm / .AppImage / .tar.gz)
./scripts/compilar-todo.sh 5.0.0
```
O mediante Solution / CMake:
```bash
dotnet test AlbaEcosistema.sln -c Release
cmake --preset windows-auto   # o linux-release
cmake --build --preset windows-auto
```

---

## Ejemplo de código en Cibao (`v5.0.0`)

```cibao
importar "matematicas" como mat

clase Persona {
    publico nombre: texto
    constructor(nombre: texto) { este.nombre = nombre }
}

clase Desarrollador extiende Persona {
    publico lenguaje: texto
    constructor(nombre: texto, lenguaje: texto) {
        super(nombre)
        este.lenguaje = lenguaje
    }
    publico funcion presentar() -> texto {
        retornar $"Soy {este.nombre} y programo en {este.lenguaje}"
    }
}

variable dev = nuevo Desarrollador("Jaderson", "Cibao 5.0")
variable datos: diccionario = { ciudad: "Santiago", activo: verdadero }
variable nota = datos["estado"] ?? "Listo"

imprimir(dev.presentar(), "|", nota, "| Raíz(144):", mat.raiz(144))
```

---

## Estructura del proyecto

```text
alba-ecosistema/
├── .github/workflows/        # CI multiplataforma y Release automático (Windows + Linux)
├── addons/                   # Complementos integrados (Python 3, etc.)
├── assets/                   # Logotipos, iconos (.ico/.icns/.png), branding y temas
├── docs/                     # Documentación completa (01..08)
├── examples/                 # Programas de ejemplo en .cibao y .alb
├── native-launcher/          # Host nativo portable C++20 (alba-host)
├── packaging/                # Instaladores Windows (Inno Setup), Linux (.deb/.rpm/.AppImage) y macOS
├── scripts/                  # Scripts unificados de compilación y empaquetado local
├── src/
│   ├── Alba.Engine/          # Motor Cibao 5.0 (Lexer, AST, Parser, Runtime, Stdlib)
│   ├── Alba.CLI/             # Herramienta de línea de comandos (cibao / alba)
│   └── Alba.IDE/             # Xynex Code Editor (Avalonia UI 11)
├── tests/                    # Pruebas unitarias (Motor, Cibao 5.0, IDE y Host C++)
├── AlbaEcosistema.sln
├── CMakeLists.txt
├── CMakePresets.json
└── Directory.Build.props     # Versionado centralizado (5.0.0)
```

---

## Documentación, Licencia y Soporte

- **Documentación completa:** Carpeta [`docs/`](docs/01_introduccion_y_filosofia.md) o dentro de **Xynex Code Editor** en **Ayuda → Documentación**.
- **Licencia:** Distribuido bajo **Apache License 2.0** ([`LICENSE`](LICENSE) y [`NOTICE`](NOTICE)). Copyright © 2026 **Jaderson Liriano (`CynexStudios`)**.
- **Soporte:** Consulta [`SUPPORT.md`](SUPPORT.md) o abre una incidencia en [GitHub Issues](https://github.com/CynexStudios/alba-ecosistema/issues).

---

## English Summary

**Xynex Code Editor 5.0.0** & **Cibao Programming Language** (`.cibao` / `.alb`), created by **Jaderson Liriano (`CynexStudios`)**, is an open-source Spanish-syntax programming language and cross-platform native desktop IDE built on **.NET 8**, **Avalonia UI 11**, **AvaloniaEdit**, and a portable **C++20** host. Cibao 5.0 features native dictionaries, string interpolation (`$"..."`), single-inheritance OOP (`extiende`, `super`, `es`), real relative modules (`importar`), `elegir` pattern selection, anonymous lambdas with closures, null-coalescing (`??`), JSON & file I/O helpers, and automated Windows/Linux installers via GitHub Actions under the **Apache 2.0 License**.
