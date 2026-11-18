# 5. Arquitectura Interna del Intérprete y Guía de Desarrollo

## 5.1. Visión general de capas

El ecosistema separa estrictamente la lógica lingüística (`Alba.Engine`), los adaptadores de ejecución (`Alba.CLI` y `Alba.IDE`), el lanzador portable (`alba-host`) y la extensibilidad (`/addons`):

```text
+---------------------------------------------------------------+
|               Alba Studio (Alba.IDE - Avalonia 11)            |
|  Explorador | EditorCodigoAlba | ConsolaIdeIO | GestorAddons  |
+---------------------------+-----------------------------------+
                            | IConsolaIO / AST / EjecutorAlba
+---------------------------v-----------------------------------+
|                   Alba.Engine (.NET 8)                        |
|  AnalizadorLexico -> AnalizadorSintactico -> Interprete       |
|  Diagnostics (UbicacionFuente, ErrorAlba, FormateadorErrores) |
+---------------------------^-----------------------------------+
                            | CLI / Host C++20
+---------------------------+-----------------------------------+
|          Alba.CLI (alba)  <---  alba-host (C++20 / CMake)     |
+---------------------------------------------------------------+
```

## 5.2. Flujo interno del motor (`Alba.Engine`)

1. **`AnalizadorLexico` (`Lexer/`):**
   - Recorre el texto fuente UTF-8 carácter por carácter manteniendo línea y columna (1-based).
   - Rastrea `_profundidadParentesis` y `_profundidadCorchetes` para suprimir `SaltoLinea` dentro de `(...)` y `[...]`.
   - Emite `ResultadoLexico(Tokens, Errores)`.
2. **`AnalizadorSintactico` (`Parser/`):**
   - Analizador recursivo descendente con 11 niveles de precedencia.
   - Construye nodos inmutables `NodoAst` (`Expresion`, `Sentencia`, `Declaracion`, `ProgramaAst`).
   - Realiza validación contextual temprana (`romper`/`continuar` solo dentro de ciclos, `retornar` solo en funciones, `este` solo en clases, `constante` con inicializador).
   - Ante errores sintácticos, sincroniza hasta el siguiente límite de instrucción (`Sincronizar`) controlando `_profundidadBloque`.
3. **`Interprete` (`Runtime/`):**
   - Mantiene `_entornoGlobal` (con biblioteca estándar) y una cadena de `Entorno` léxicos para bloques y *closures*.
   - Usa pilas `_pilaClases` y `_pilaInstancias` para resolver `este` y aplicar encapsulamiento `privado`/`publico`.
   - Implementa `retornar`, `romper`, `continuar` y `lanzar` mediante excepciones internas controladas (`SenalRetornar`, `SenalRomper`, `SenalContinuar`, `ExcepcionLanzadaUsuario`).
   - Delega toda lectura y escritura en `IConsolaIO`.

## 5.3. Guía de desarrollo y extensión del motor

- **Agregar una nueva función estándar:** Registrarla en `Interprete.RegistrarBibliotecaEstandar()` usando `RegistrarNativa(...)` y añadirla en `ServicioAutocompletado.FuncionesAlba` y `Resources/alba.xshd`.
- **Agregar una nueva palabra reservada:**
  1. Añadir el token en `TipoToken.cs` y mapearla en `PalabrasReservadas.cs`.
  2. Implementar el nodo AST en `Ast/` y su regla en `AnalizadorSintactico.cs`.
  3. Implementar la ejecución en `Interprete.cs`.
  4. Añadir pruebas en `LexerTests.cs`, `ParserTests.cs` e `InterpreteTests.cs`.
- **Regla de arquitectura:** `Alba.Engine` nunca debe depender de `Alba.IDE`, `Avalonia` ni `System.Console`.
