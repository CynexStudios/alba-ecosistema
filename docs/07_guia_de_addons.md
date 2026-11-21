# 07. Guía de Complementos y Tienda Online (API 9.0)

Xynex Code Editor 9.0 incluye **conexión en línea directa mediante API** para descargar e instalar complementos sin tener que bajar carpetas manualmente desde el navegador.

## 1. Descarga en 1 Clic desde el Editor

1. Haz clic en **Addons API** en la barra superior (o en el icono de descargas de la barra izquierda).
2. En la pestaña **Descargar en Línea (API)** verás el catálogo oficial sincronizado desde GitHub (`addons/catalogo.json`), con soporte inmediato para Cibao 9.0, Python, HTML/CSS/Web, TypeScript/JS, C#, C++20 y más.
3. Escribe el lenguaje que buscas y pulsa **Descargar e Instalar en 1 clic**.
4. El resaltado de sintaxis XSHD, snippets e integración de autocompletado se activan automáticamente.

## 2. Crear un Complemento Propio (`manifest.json`)

Si deseas distribuir un complemento personalizado en carpeta local, incluye un archivo `manifest.json` válido:

```json
{
  "id": "mi-lenguaje-addon",
  "nombre": "Soporte Mi Lenguaje",
  "version": "9.0.0",
  "autor": "Tu Nombre",
  "descripcion": "Resaltado y ejecución nativa.",
  "lenguaje": "MiLenguaje",
  "extensiones": [".ext"],
  "ejecutable": "interprete",
  "argumentos": ["{archivo}"],
  "archivoSintaxis": "sintaxis.xshd",
  "palabrasClave": ["funcion", "retornar"],
  "snippets": [
    {
      "prefijo": "hola",
      "etiqueta": "Programa inicial",
      "codigo": "mostrar(\"Hola!\")"
    }
  ]
}
```

Por seguridad, `ValidadorAddons` bloquea rutas relativas fuera de la carpeta del complemento (`..`), scripts remotos no autorizados o ejecutables con inyección de shell.
