# 4. Referencia de Palabras Reservadas y Catálogo de Errores

## 4.1. Palabras reservadas de Alba (`v0.1.0`)

| Categoría | Palabras reservadas | Estado en `v0.1.0` |
| :--- | :--- | :--- |
| **Declaraciones** | `variable`, `constante` | Activas |
| **Tipos** | `entero`, `decimal`, `texto`, `booleano`, `lista`, `vacio` | Activas |
| **Funciones** | `funcion`, `retornar` | Activas |
| **Condicionales** | `si`, `sino`, `sino_si` | Activas |
| **Ciclos** | `mientras`, `para`, `en`, `romper`, `continuar` | Activas |
| **Clases y objetos** | `clase`, `constructor`, `nuevo`, `este`, `publico`, `privado` | Activas |
| **Manejo de errores** | `intentar`, `capturar`, `finalmente`, `lanzar` | Activas |
| **Entrada y salida** | `imprimir`, `leer` | Activas |
| **Valores y lógica** | `verdadero`, `falso`, `nulo`, `y`, `o`, `no` | Activas |
| **Módulos (futuro)** | `importar`, `desde`, `como`, `modulo`, `exportar` | Reservadas en Lexer/Parser; ejecución diferida a `v0.2.0` (`ALB-E099`) |

## 4.2. Funciones estándar integradas

| Función | Firma / Uso | Descripción |
| :--- | :--- | :--- |
| `rango` | `rango(fin)` / `rango(ini, fin)` / `rango(ini, fin, paso)` | Genera una lista de enteros (máximo 1,000,000 elementos) |
| `longitud` | `longitud(lista \| texto)` | Devuelve la cantidad de elementos o caracteres (`entero`) |
| `tipo_de` | `tipo_de(valor)` | Devuelve el nombre del tipo del valor (`texto`) |
| `convertir_entero` | `convertir_entero(valor)` | Convierte `decimal`, `texto` o `booleano` a `entero` |
| `convertir_decimal` | `convertir_decimal(valor)` | Convierte `entero` o `texto` a `decimal` |
| `convertir_texto` | `convertir_texto(valor)` | Convierte cualquier valor a su representación `texto` |
| `agregar` | `agregar(lista, elemento)` | Agrega un elemento al final de la lista |
| `eliminar` | `eliminar(lista, indice)` | Elimina y retorna el elemento ubicado en `indice` |

## 4.3. Formato oficial y códigos de error

Todos los errores del motor se muestran en español con archivo, línea, columna, código y línea fuente señalada con `^`:

```text
ERROR SINTÁCTICO

Archivo: programa.alb
Línea: 12
Columna: 21

Descripción: [ALB-S030] Se esperaba una expresión después del operador de asignación '='.

Código:
12 | variable resultado =
                         ^
```

### Catálogo de códigos implementados
- **Léxicos (`ALB-L001`–`ALB-L005`):** Cadena sin cerrar (`L001`), carácter inválido como `!`/`&`/`|` (`L002`), comentario `/*` sin cerrar (`L003`), escape inválido (`L004`), número mal formado (`L005`).
- **Sintácticos (`ALB-S001`–`ALB-S037`):** Identificadores esperados, `constante` sin `=`, objetivo de asignación inválido (`S005`), errores en definición de clase (`S009`–`S014`), `retornar` fuera de función (`S025`), `romper`/`continuar` fuera de ciclo (`S026`/`S027`), `intentar` sin `capturar` ni `finalmente` (`S029`), `este` fuera de clase (`S033`).
- **De Tipo (`ALB-T001`–`ALB-T010`):** Incompatibilidad en asignación (`T001`), parámetro (`T002`), retorno (`T003`), operador/condición (`T004`), acceso de miembro en no-instancia (`T005`), indexación/lista (`T006`/`T007`), iterable no válido (`T008`), llamada no invocable (`T009`), clase inválida en `nuevo` (`T010`).
- **De Ejecución (`ALB-E001`–`ALB-E099`):** Redeclaración en mismo ámbito (`E001`), reasignación de constante (`E002`), símbolo no definido (`E003`), división/módulo por cero (`E005`), índice fuera de rango (`E006`), miembro inexistente (`E009`), acceso denegado a miembro `privado` (`E010`), aridad incorrecta (`E011`), error de conversión (`E014`), `lanzar` no capturado (`E090`), `importar` reservado para `v0.2.0` (`E099`).
