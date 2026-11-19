# 5. Arquitectura Interna del Intérprete y Guía de Desarrollo (`Cibao 5.0`)

## 5.1. Visión general de capas

El ecosistema separa estrictamente el motor del lenguaje (`Alba.Engine`), los adaptadores de ejecución (`Alba.CLI` y **Xynex Code Editor** / `Alba.IDE`), el lanzador nativo (`alba-host`) y la extensibilidad (`/addons`):

```text
+-------------------------------------------------------------------+
|          Xynex Code Editor (Alba.IDE - Avalonia UI 11)            |
|  Explorador | EditorCodigoAlba | ConsolaIdeIO | GestorAddons      |
+-------------------------------+-----------------------------------+
                                | IConsolaIO / AST / EjecutorAlba
+-------------------------------v-----------------------------------+
|                 Alba.Engine (.NET 8 — Cibao 5.0)                  |
|  AnalizadorLexico -> AnalizadorSintactico -> Interprete (partial) |
|  Diagnostics (UbicacionFuente, ErrorAlba, FormateadorErrores)     |
+-------------------------------^-----------------------------------+
                                | CLI / Host C++20
+-------------------------------+-----------------------------------+
|      Alba.CLI (cibao / alba)  <---  alba-host (C++20 / CMake)     |
+-------------------------------------------------------------------+
```

## 5.2. Flujo interno del motor (`Alba.Engine`)

1. **`AnalizadorLexico` (`Lexer/`):**
   - Recorre el código UTF-8 manteniendo línea y columna exactas (`1-based`).
   - Gestiona suprimir `SaltoLinea` dentro de `(...)` y `[...]`, además de una pila dedicada de interpolación (`$"..."` / `$'...'`) para expresiones `{...}` anidadas.
   - Emite `ResultadoLexico(Tokens, Errores)`.
2. **`AnalizadorSintactico` (`Parser/`):**
   - Analizador recursivo descendente con precedencia completa hasta coalescencia nula (`??`) y comprobación polimórfica de tipos (`es`).
   - Construye nodos AST inmutables (`Declaraciones.cs`, `Sentencias.cs`, `Expresiones.cs`).
   - Realiza validación contextual estática (`romper`/`continuar` solo en ciclos, `retornar` solo en funciones, `este`/`super` solo dentro de clases, `constante` con inicializador).
3. **`Interprete` (`Runtime/Interprete.cs` y `Runtime/Interprete.BibliotecaEstandar.cs`):**
   - Implementado como `partial class` para separar el núcleo de evaluación (ámbitos `Entorno`, herencia en `DefinicionClase`/`InstanciaAlba`, caché de módulos e importación circular `ALB-E098`, control `elegir`, lambdas, diccionarios) respecto a la biblioteca estándar ampliada (matemáticas, texto, funcional, JSON y archivos UTF-8).
   - Mantiene `_pilaClases` y `_pilaInstancias` para resolver `este`, `super(...)`, `super.metodo(...)` y encapsulamiento `privado` por clase declarante.
   - Delega toda E/S de consola en `IConsolaIO`.

## 5.3. Guía de extensión rápida

- **Agregar una nueva función estándar o método por punto:** Registrarla en `Interprete.BibliotecaEstandar.cs` y agregar su firma en `ServicioAutocompletado.cs` y `Resources/alba.xshd`.
- **Agregar una nueva palabra reservada o construcción sintáctica:**
  1. Añadir token en `TipoToken.cs` y registrarlo en `PalabrasReservadas.cs`.
  2. Crear nodo AST en `Ast/` y regla en `AnalizadorSintactico.cs`.
  3. Despachar la ejecución en `Interprete.cs`.
  4. Añadir pruebas unitarias en `tests/Alba.Engine.Tests/`.
- **Regla de arquitectura:** `Alba.Engine` nunca debe depender de `Alba.IDE`, `Avalonia` ni `System.Console`.
