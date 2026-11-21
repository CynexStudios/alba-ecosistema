# Registro de Cambios (CHANGELOG)

## [9.0.0] — Gran Renovación Integral Xynex Code Editor 9.0 & Cibao Code 9.0

### Lenguaje Cibao Code 9.0
- Nuevos rangos nativos exclusivos (`1..5`) e inclusivos (`1..=5`).
- Lambdas flecha concisas (`x => x * 2`, `(a, b) => a + b`) y funciones de una línea con `=>`.
- Operador de potencia (`**`), operador de comprobación de tipo (`es`), acceso opcional encadenado (`?.`), incrementos (`++`, `--`) y bloques de texto multilínea (`"""..."""`).
- Mejora en el resaltado de sintaxis (`alba.xshd`) y autocompletado inteligente por contexto.

### IDE Visual Studio Pro 9.0 y Nuevas Capacidades
- **Interfaz renovada** con tema por defecto `Xynex Visual Studio Pro`, nuevo logo e icono, nuevo banner de bienvenida y paleta de alto contraste para listas, menús y pestañas.
- **Archivos sin carpeta:** Crea y ejecuta código directamente (`Archivo Sin Carpeta`) en 9 lenguajes sin obligar a seleccionar una carpeta primero.
- **IntelliSense visible y contextual:** Ventana de sugerencias de alto contraste con insignias de categoría (`FN`, `KW`, `TIPO`, `METODO`, `VAR`, `ADDON`), extracción de símbolos en vivo y disparadores inmediatos.
- **Detectores de error y runners nativos:** Análisis en tiempo real y ejecución nativa para Cibao 9.0, Python, HTML5, CSS3, JavaScript, TypeScript, JSON, C# y C++20.
- **Bot Tutor Automático (Desde Cero):** 8 lecciones interactivas integradas con inserción directa al editor y verificación automática de salida al ejecutar (`F5`).
- **Agente de IA conectado a tu propia IA:** Soporte multi-proveedor (Ollama local, Google Gemini, OpenAI / Groq / DeepSeek / OpenRouter, Anthropic Claude) con botón para aplicar código o crear archivos al instante.
- **Base de Datos SQLite y Seguridad Alta:** Registro/login de cuentas locales con PBKDF2-HMAC-SHA512, bloqueo tras 5 intentos fallidos, consultas parametrizadas y bóveda cifrada DPAPI / AES-256-GCM para credenciales de IA.
- **Tienda Online de Addons (API):** Descarga e instalación en 1 clic directamente dentro del editor.
- **Ajustes ampliados:** Fuente, tamaño, colores personalizados del editor, personalización completa de tokens de sintaxis y visibilidad de paneles.
- **Documentación desde cero:** 8 capítulos reescritos más manual HTML interactivo ejecutable desde el propio IDE.
- **Actualizador automático silencioso y empaquetado Linux/Arch/CachyOS/Windows:** Validación SHA-256 con actualización automática y compilación multiplataforma endurecida.
