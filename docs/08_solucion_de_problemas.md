# 8. Guía de Solución de Problemas

## 8.1. Problemas durante la compilación

### `dotnet` no encontrado al ejecutar `cmake --preset`
- **Causa:** El SDK de .NET 8 no está instalado o no figura en el `PATH`.
- **Solución:** Instala .NET 8.0 SDK y verifica con `dotnet --version` en una nueva terminal antes de ejecutar `cmake`.

### Error de compilador C++20 en CMake
- **Causa:** Falta compilador C++20 (`g++` en Linux o Visual Studio 2022 con carga de trabajo C++ en Windows).
- **Solución:**
  - En Ubuntu/Debian: `sudo apt install build-essential cmake`.
  - En Windows: Instala el componente *Desarrollo para el escritorio con C++* en Visual Studio 2022.

---

## 8.2. Problemas al ejecutar programas Alba

### `ERROR LÉXICO [ALB-L002]: Carácter inesperado '!'` (o `&` / `|`)
- **Causa:** Uso de operadores lógicos estilo C/C#/JavaScript (`!`, `&&`, `||`).
- **Solución:** Alba utiliza palabras en español: `no`, `y`, `o`. Para desigualdad usa `!=`.

### `ERROR DE TIPO [ALB-T004]: Se esperaba una condición 'booleano'`
- **Causa:** Se pasó un `entero`, `texto` o `nulo` directamente en `si` o `mientras` (por ejemplo `si x {`).
- **Solución:** Escribe una comparación explícita, como `si x != 0 {` o `si nombre != "" {`.

### `ERROR DE EJECUCIÓN [ALB-E010]: Acceso denegado al miembro privado`
- **Causa:** Se intentó leer o modificar un campo o método marcado `privado` desde fuera de la clase.
- **Solución:** Expón un método `publico` en la clase (p. ej. `obtener_saldo()`) o cambia la visibilidad del miembro a `publico`.

---

## 8.3. Problemas con el IDE y complementos

### El complemento de Python indica que no encuentra el intérprete
- **Causa:** `python3` / `python` no está instalado o no está en el `PATH` del sistema.
- **Solución:** Instala Python 3.10+ (en Windows marca *Add Python to PATH* durante la instalación). Alba Studio intenta automáticamente `python` y `py` en Windows si `python3` no existe.

### Un addon instalado aparece como inválido en VentanaAddons
- **Causa:** El `manifest.json` incumple el esquema, apunta a un archivo fuera de su carpeta (`..`) o declara un ejecutable no permitido en la lista blanca de `ValidadorAddons`.
- **Solución:** Revisa el mensaje en el campo **Seguridad** dentro de **Complementos** y corrige las rutas relativas o el ejecutable.

### La ejecución en Alba Studio parece pausada esperando
- **Causa:** El programa invocó `leer(...)` y está esperando que ingreses texto en la consola integrada.
- **Solución:** Escribe el valor en la barra inferior verde/azul de la consola integrada y presiona `Enter` (o pulsa **Detener** para cancelar la ejecución).
