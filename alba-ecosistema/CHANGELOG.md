# Registro de cambios (Changelog)

Todos los cambios relevantes de **Xynex Code Editor** y **Cibao Programming Language** se documentan en este archivo siguiendo [Versionado Semántico](https://semver.org/lang/es/).

## [5.0.0] - 2026-11-19

### Añadido
- **Nueva identidad oficial del ecosistema:** Lenguaje renombrado a **Cibao** (*Cibao Programming Language*, extensión principal `.cibao` con 100% compatibilidad `.alb`) e IDE oficial **Xynex Code Editor**, creados por **Jaderson Liriano (`CynexStudios`)**.
- **Diccionarios nativos (`diccionario`):** Literales `{ clave: valor }` y `{ "clave": valor }`, acceso/mutación `d["k"]` y `d.k`, iteración de claves con `para k en d` y soporte JSON (`json_a_texto`, `texto_a_json`).
- **Interpolación de texto (`$"..."` y `$'...'`):** Evaluación directa de expresiones embebidas `{expr}`, interpolación anidada y escapes `\{`/`\}`.
- **Herencia y polimorfismo en POO:** Clases derivadas con `extiende`, llamadas a constructores y métodos base con `super(...)` / `super.metodo()`, comprobación de tipos en ejecución con `es` y preservación estricta del encapsulamiento `privado`.
- **Módulos reales (`importar`):** Resolución relativa de archivos `.cibao`/`.alb`, caché de carga única, detección de ciclos (`ALB-E098`), espacios de nombres (`como alias`) y módulos estándar virtuales (`"matematicas"`, `"texto"`, `"json"`, `"archivos"`).
- **Control de flujo y programación funcional:** Sentencia `elegir` / `caso` / `defecto` sin *fall-through*, funciones anónimas de primera clase con *closures* (`funcion(...) { ... }`) y operador de coalescencia nula (`??`).
- **Métodos integrados por punto y Biblioteca Estándar ampliada:** Métodos `.longitud()`, `.agregar()`, `.filtrar()`, `.mapear()`, `.reducir()`, `.ordenar()`, `.mayusculas()`, `.dividir()`, funciones matemáticas trigonométricas/logarítmicas y E/S de archivos UTF-8 seguros.
- **Integración total en Xynex Code Editor:** Resaltado `alba.xshd` actualizado, autocompletado de Cibao 5.0, paso de ruta activa para `importar` relativo, soporte multi-lenguaje en terminal integrada y panel **Problemas**.
- **Empaquetado y CI/CD optimizados:** Instaladores `XynexCodeEditor-5.0.0` para Windows (`.exe`/`.zip`) y Linux (`.deb`/`.rpm`/`.AppImage`/`.tar.gz`) con comandos `cibao` y `alba`, matriz CI enfocada en Windows + Linux (macOS bajo demanda manual) y scripts unificados `scripts/compilar-todo.*`.

## [2.0.0] - 2026-11-18

### Añadido
- Pantalla de inicio moderna, historial persistente de proyectos recientes, sistema de 3 temas personalizables y diagnósticos en vivo.
- Terminal integrada real, paleta de comandos (`Ctrl+Shift+P`), búsqueda global en proyectos y autoguardado con recuperación de sesión.
- Distribución multiplataforma con Inno Setup 6 y empaquetado nativo Linux/macOS.

## [0.1.0] - 2026-11-17

### Añadido
- Primera versión completa del motor `Alba.Engine` (Lexer, Parser, AST, Intérprete y diagnósticos `ALB-*`).
- CLI inicial (`alba`), lanzador nativo C++20 (`alba-host`) e IDE inicial en Avalonia UI 11.
