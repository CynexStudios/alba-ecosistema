# 6. Manual del IDE: Alba Studio (`v2.0.0`)

## 6.1. Pantalla de Inicio (Bienvenida)

Al abrir **Alba Studio 2.0** sin proyecto activo (o mediante **Archivo → Pantalla de inicio**), accedes a:
- **Acciones rápidas:** *Crear nuevo proyecto Alba*, *Abrir proyecto o carpeta*, *Abrir archivo de código*, *Documentación* y *Temas y Ajustes*.
- **Recientes:** Historial persistente de proyectos y archivos abiertos recientemente, con apertura directa o eliminación de la lista.
- **Novedades y ejemplos:** Resumen de la versión `v2.0.0` y acceso directo al ejemplo `01_hola_mundo.alb`.

---

## 6.2. Espacio de trabajo renovado

1. **Barra superior integrada:**
   - Logotipo oficial de Alba Studio, menús (`Archivo`, `Editar`, `Ver`, `Ejecutar`, `Herramientas`, `Ayuda`), acceso central a la **Paleta de Comandos (`Ctrl+Shift+P`)** y botones rápidos (`Verificar`, `Ejecutar F5`, `Detener`, `Inicio`, `Ajustes`).
2. **Activity Rail y barra lateral colapsable (`Ctrl+B`):**
   - **Explorador:** Árbol de archivos con menú contextual (clic derecho: nuevo archivo, nueva carpeta, renombrar, duplicar, eliminar con confirmación).
   - **Buscar en Proyecto (`Ctrl+Shift+F`):** Búsqueda global en todos los archivos con salto directo a línea y columna.
   - **Complementos (Addons):** Lista de addons validados y acceso al gestor completo.
   - **Herramientas:** Verificación completa de todos los `.alb` del proyecto, terminal en carpeta, manual y explorador del sistema.
3. **Área de edición (`EditorCodigoAlba`):**
   - Pestañas múltiples con menú contextual (*Cerrar*, *Cerrar otras*, *Cerrar todas*), barra de ruta (*breadcrumbs*), resaltado dinámico por tema, numeración de líneas, autocompletado (`Ctrl+Space`), zoom (`Ctrl+Rueda`) y **diagnóstico en vivo** con subrayado rojo y tooltip informativo en errores `.alb`.
4. **Panel inferior con pestañas (`Ctrl+J`):**
   - **Consola:** Salida del programa y barra interactiva para `leer(...)`.
   - **Problemas:** Lista en vivo de errores de sintaxis/léxico; doble clic para saltar al código.
   - **Terminal:** Shell real del sistema (`powershell.exe` en Windows, `bash`/`sh` en Linux/macOS) en la carpeta del proyecto.
   - **Registro:** Historial con marca de tiempo de eventos del IDE.
5. **Barra de estado inferior:**
   - Estado e indicador de ejecución, conteo de errores, lenguaje, cursor (`Ln X, Col Y`), botón rápido de cambio de tema y versión `v2.0.0`.

---

## 6.3. Temas, productividad y actualizaciones

Desde **Ajustes** puedes:
- Elegir entre **Alba Crepúsculo**, **Alba Medianoche** y **Alba Amanecer**, o personalizar colores hexadecimales de sintaxis.
- Activar **Autoguardado periódico** y **Recuperación de sesión**.
- Editar atajos de teclado y comprobar manualmente actualizaciones oficiales en GitHub Releases (`CynexStudios/alba-ecosistema`).

---

## 6.4. Atajos de teclado predeterminados (configurables)

| Atajo | Acción |
| :--- | :--- |
| `Ctrl + Shift + P` | Abrir Paleta de Comandos |
| `Ctrl + P` | Apertura rápida de archivos del proyecto |
| `Ctrl + S` | Guardar archivo activo |
| `Ctrl + F` | Buscar / Reemplazar en archivo activo |
| `Ctrl + Shift + F` | Buscar en todo el proyecto |
| `Ctrl + B` | Mostrar / ocultar barra lateral |
| `Ctrl + J` | Mostrar / ocultar panel inferior |
| `Ctrl + Shift + B` | Verificar sintaxis del archivo activo |
| `Ctrl + Space` | Autocompletado |
| `F5` | Guardar y ejecutar el archivo activo |
