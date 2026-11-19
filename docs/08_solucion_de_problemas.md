# 8. Guía de Solución de Problemas (`v5.0.0`)

## 8.1. Problemas durante la compilación

### `dotnet` no encontrado al ejecutar `cmake --preset` o `scripts/compilar-todo`
- **Causa:** El SDK de .NET 8 no está instalado o no figura en el `PATH`.
- **Solución:** Instala .NET 8.0 SDK y verifica con `dotnet --version` en una nueva terminal antes de compilar. Si solo quieres usar el editor o generar los instaladores en la nube, puedes usar GitHub Actions (`release.yml`).

### Error de compilador C++20 en CMake
- **Causa:** Falta compilador C++20 (`g++` en Linux o Visual Studio 2022 con carga de trabajo C++ en Windows).
- **Solución:**
  - En Ubuntu/Debian: `sudo apt install build-essential cmake`.
  - En Windows: Instala el componente *Desarrollo para el escritorio con C++* en Visual Studio 2022.

---

## 8.2. Problemas al ejecutar programas Cibao (`.cibao` / `.alb`)

### `ERROR LÉXICO [ALB-L002]: Carácter inesperado '!'` (o `&` / `|`)
- **Causa:** Uso de operadores lógicos estilo C/C#/JavaScript (`!`, `&&`, `||`).
- **Solución:** Cibao utiliza palabras en español: `no`, `y`, `o`. Para desigualdad usa `!=`.

### `ERROR DE TIPO [ALB-T004]: Se esperaba una condición 'booleano'`
- **Causa:** Se pasó un `entero`, `texto` o `nulo` directamente en `si` o `mientras` (por ejemplo `si x {`).
- **Solución:** Escribe una comparación explícita, como `si x != 0 {`, `si nombre != "" {` o `si valor es texto {`.

### `ERROR DE EJECUCIÓN [ALB-E010]: Acceso denegado al miembro privado`
- **Causa:** Se intentó leer o modificar un campo o método marcado `privado` desde fuera de la clase declarante (incluso desde una subclase derivada).
- **Solución:** Expón un método `publico` en la clase base o cambia la visibilidad del miembro a `publico`.

### `ERROR DE EJECUCIÓN [ALB-E097]` o `[ALB-E098]` en `importar`
- **Causa:** El archivo importado no existe en la ruta relativa al script (`E097`) o existe una dependencia mutua circular A ↔ B (`E098`).
- **Solución:** Verifica el nombre/extensión `.cibao` o `.alb` relativo al archivo actual y elimina ciclos entre módulos.

---

## 8.3. Problemas con Xynex Code Editor y complementos

### El complemento de Python o un compilador externo indica que no encuentra el intérprete
- **Causa:** `python3`, `node`, `g++`, `rustc` o `go` no están instalados o no figuran en el `PATH` del sistema.
- **Solución:** Instala la herramienta deseada y asegúrate de agregarla al `PATH` (en Windows marca *Add to PATH* durante la instalación).

### Un addon instalado aparece como inválido en VentanaAddons
- **Causa:** El `manifest.json` incumple el esquema, apunta a un archivo fuera de su carpeta (`..`) o declara un ejecutable no permitido en la lista blanca de `ValidadorAddons`.
- **Solución:** Revisa el mensaje en **Seguridad** dentro de **Complementos** y corrige las rutas relativas o el ejecutable.

### La ejecución en Xynex Code Editor parece pausada esperando
- **Causa:** El programa invocó `leer(...)` y está esperando que ingreses texto en la consola integrada.
- **Solución:** Escribe el valor en la barra inferior de entrada en la consola integrada y presiona `Enter` (o pulsa **Detener**).
