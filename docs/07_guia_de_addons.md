# 7. Manual del Sistema de Addons (`/addons`)

## 7.1. Arquitectura de complementos

Los complementos residen en la carpeta `/addons/<id-addon>/` y permiten extender **Alba Studio** con soporte para otros lenguajes sin modificar el código fuente del IDE.

### Estructura estándar de un addon
```text
addons/
└── python/
    ├── manifest.json
    ├── syntax/
    │   └── python.xshd
    ├── snippets/
    │   └── snippets.json
    └── plugin.py
```

## 7.2. Especificación de `manifest.json`

| Campo | Tipo | Obligatorio | Descripción |
| :--- | :--- | :---: | :--- |
| `id` | `string` | Sí | Identificador único en minúsculas (`^[a-z0-9-]+$`) |
| `nombre` | `string` | Sí | Nombre visible en el gestor |
| `version` | `string` | Sí | Versión semántica `X.Y.Z` |
| `descripcion` | `string` | No | Descripción del complemento |
| `autor` | `string` | No | Autor o equipo |
| `lenguaje` | `string` | Sí | Nombre del lenguaje (ej. `Python`, `C#`, `JavaScript`, `C++`) |
| `extensiones` | `string[]` | Sí | Lista de extensiones (ej. `[".py"]`) |
| `ejecutable` | `string` | Sí | Binario autorizado en la lista blanca de seguridad |
| `scriptEntrada` | `string` | No | Ruta relativa al script adaptador dentro del addon |
| `sintaxis` | `string` | No | Ruta relativa al archivo `.xshd` de resaltado |
| `snippets` | `string` | No | Ruta relativa al archivo JSON de fragmentos de autocompletado |
| `dependencias` | `string[]` | No | Lista informativa de dependencias |

## 7.3. Política de seguridad (`ValidadorAddons`)

Un manifiesto JSON nunca puede ejecutar instrucciones arbitrarias ni comandos de shell:
1. **Lista blanca de ejecutables:** Solo se aceptan nombres simples de binarios autorizados (`python3`, `python`, `py`, `dotnet`, `node`, `gcc`, `g++`, `clang++`) sin rutas absolutas, espacios ni operadores de shell.
2. **Protección contra *Path Traversal*:** `sintaxis`, `snippets` y `scriptEntrada` deben resolverse estrictamente dentro de la carpeta propia del addon. Cualquier uso de `..` o rutas externas provoca el rechazo inmediato del complemento.
3. **Ejecución aislada:** El IDE inicia procesos con `UseShellExecute = false` y paso de argumentos mediante `ArgumentList`.

## 7.4. Administración desde Alba Studio

Desde el botón **Complementos** en la barra superior o lateral es posible:
- **Instalar desde carpeta:** Selecciona una carpeta con `manifest.json`; el validador la verifica y la copia al directorio escribible de complementos del usuario (`%APPDATA%\AlbaStudio\addons\<id>\` en Windows, `~/Library/Application Support/AlbaStudio/addons/<id>/` en macOS o `~/.config/AlbaStudio/addons/<id>/` en Linux), evitando fallos de permisos cuando el IDE está instalado en rutas del sistema.
- **Activar / Desactivar:** Persiste la preferencia en `addons-config.json` dentro del perfil de usuario (tanto para complementos integrados como instalados).
- **Desinstalar:** Elimina la carpeta del complemento instalado por el usuario. Los complementos integrados de solo lectura pueden desactivarse en cualquier momento.

## 7.5. Complemento incluido: Python 3 (`addons/python/`)

Incluye resaltado de sintaxis (`syntax/python.xshd`), fragmentos de código (`snippets/snippets.json`) y el adaptador `plugin.py`, que verifica errores de sintaxis con `ast.parse` antes de ejecutar scripts `.py` con codificación UTF-8.
