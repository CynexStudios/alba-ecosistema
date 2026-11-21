<img width="1280" height="720" alt="banner-github" src="https://github.com/user-attachments/assets/a6fca9ea-c26e-42cf-98b1-12fe0b16d518" />

# Xynex Code Editor 9.0 & Cibao Code 9.0

**IDE profesional multi-lenguaje estilo Visual Studio** y lenguaje de programación oficial **Cibao Code 9.0** (`CynexStudios/alba-ecosistema`).

## Novedades de la Versión 9.0

- **Nueva Interfaz Visual Studio Pro 9.0:** Tema de alto contraste, nuevo logo, nuevo banner integrado, inicio limpio sin código repetitivo y acceso inmediato a **Archivo Sin Carpeta**.
- **Cibao Code 9.0 Mejorado:** Extensiones `.cibao` (100% compatible con `.alb`), rangos nativos `1..5` y `1..=5`, lambdas flecha `x => x * 2`, operador de potencia `**`, acceso opcional seguro `?.`, coalescencia nula `??`, comprobación de tipo `es` y cadenas multilínea `"""..."""`.
- **Bot Tutor Automático Cibao 9.0:** 8 lecciones interactivas desde cero dentro del IDE (Hola Mundo, Variables, los 7 Tipos de Datos, Condicionales, Ciclos/Rangos, Funciones/Lambdas, Clases/POO y Manejo de Errores) con validación al ejecutar (`F5`).
- **Agente de IA Propio del Usuario:** Conecta tu propia IA (**Ollama local gratuita**, **Google Gemini**, **OpenAI / Groq / DeepSeek** o **Anthropic Claude**) para escribir, explicar y corregir código directamente en el editor.
- **Base de Datos SQLite de Cuentas y Seguridad Alta:** Registro e inicio de sesión local con **PBKDF2-HMAC-SHA512**, bloqueo contra fuerza bruta, consultas parametrizadas, bóveda cifrada DPAPI/AES-256-GCM y protección contra Path Traversal.
- **Tienda Online de Addons (API):** Descarga e instala extensiones y soporte en **1 clic** sin descargas manuales.
- **IntelliSense de Alto Contraste & Detectores Nativos:** Autocompletado contextual con insignias visuales + detectores nativos en tiempo real para **Cibao, Python, HTML, CSS, JavaScript, TypeScript, JSON, C# y C++**.
- **Vista Previa Web/HTML Mejorada & Ajustes Completos:** Renderizado mejorado de tarjetas/navegación/tablas/imágenes + modo ventana Web App (`--app=`) + configuración completa de fuente, tamaño, colores del editor y sintaxis.
- **Documentación Interactiva Desde Cero:** Nuevo manual completo en Markdown (`/docs`) y aplicación HTML interactiva integrada dentro del editor.
- **Actualizador Automático Silencioso:** Comprueba releases, valida **SHA-256** e instala nuevas versiones automáticamente al iniciarse, con paquetes nativos para **Windows (Instalador y Portable)**, **Arch Linux / CachyOS (`.pkg.tar.zst`)**, **Debian/Ubuntu (`.deb`)**, **Fedora (`.rpm`)**, **AppImage** y **Linux Portable Universal (`.tar.gz`)**.

## Estructura del Repositorio

- `src/Alba.Engine`: Léxico, parser, AST e intérprete de Cibao Code 9.0.
- `src/Alba.CLI`: Intérprete de línea de comandos (`alba` / `cibao`).
- `src/Alba.IDE`: Xynex Code Editor 9.0 (Avalonia UI 11 + AvaloniaEdit + SQLite).
- `addons/`: Catálogo oficial en línea (`catalogo.json`) y manifiestos de complementos.
- `docs/`: Manual completo en 8 capítulos + documentación HTML interactiva (`docs/html/documentacion_cibao.html`).
- `packaging/`: Instaladores nativos para Windows, Arch Linux / CachyOS, Debian/Ubuntu, Fedora, AppImage y macOS.
- `tests/`: Pruebas unitarias de `Alba.Engine.Tests` y `Alba.IDE.Tests`.
