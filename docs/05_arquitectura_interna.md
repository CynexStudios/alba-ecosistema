# 05. Arquitectura Interna, Seguridad y Base de Datos de Cuentas (v9.0)

El ecosistema Xynex 9.0 está estructurado en capas limpias y desacopladas dentro de `.NET 8` + `Avalonia UI 11`.

## 1. Motor del Lenguaje (`Alba.Engine`)

1. **`AnalizadorLexico`:** Convierte el código fuente `.cibao` / `.alb` en tokens tipados, con soporte para interpolación `$""`, multilínea `"""`, operadores `..`, `..=`, `=>`, `?.`, `??`, `**`, `++`, `--` y literales españoles.
2. **`AnalizadorSintactico`:** Parser recursivo descendente con recuperación de errores que produce un AST tipado (`Declaraciones`, `Expresiones`) y diagnósticos exactos de línea y columna.
3. **`Interprete` & `BibliotecaEstandar`:** Ejecución directa con entornos léxicos anidados, herencia dinámica (`extiende`, `super`), clausuras, resolución segura de módulos relativos y prevención de bucles/recursión descontrolada.

## 2. Arquitectura de Seguridad 9.0 (`ServicioSeguridad` y `ServicioBaseDatosUsuarios`)

- **Base de datos embebida SQLite (`xynex_accounts.db`):** Todas las consultas utilizan exclusivamente **parámetros enlazados** (`@u`, `@c`, `@id`) evitando inyección SQL.
- **Hashing criptográfico robusto:** Las contraseñas de usuario se derivan con **PBKDF2-HMAC-SHA512** (120,000 iteraciones + sal aleatoria de 256 bits) y verificación en tiempo constante (`CryptographicOperations.FixedTimeEquals`).
- **Bloqueo ante fuerza bruta:** Tras 5 intentos fallidos consecutivos, la cuenta se protege automáticamente durante 5 minutos.
- **Bóveda cifrada para API Keys de IA:** Las claves del usuario se cifran en reposo mediante **Windows DPAPI** en Windows o **AES-256-GCM** con clave maestra protegida (`0600`) en Linux/macOS.
- **Validación de rutas y URLs:** Prevención de *Path Traversal* en addons (`ValidadorAddons.TryResolverRutaSegura`) y filtro estricto de URLs (`EsUrlHttpSegura`: HTTPS o `localhost`/`127.0.0.1` únicamente para Ollama local).
- **Verificación de actualizaciones:** Los paquetes descargados desde GitHub Releases se validan por **SHA-256** antes de su ejecución silenciosa.
