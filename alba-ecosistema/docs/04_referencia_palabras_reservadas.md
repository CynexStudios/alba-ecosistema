# 4. Referencia de Palabras Reservadas y Catálogo de Errores (Cibao `v5.0.0`)

## 4.1. Palabras reservadas de Cibao (`v5.0.0`)

| Categoría | Palabras reservadas | Estado |
| :--- | :--- | :--- |
| **Declaraciones** | `variable`, `constante` | Activas |
| **Tipos** | `entero`, `decimal`, `texto`, `booleano`, `lista`, `diccionario`, `vacio` | Activas |
| **Funciones** | `funcion`, `retornar` | Activas |
| **Condicionales y selección** | `si`, `sino`, `sino_si`, `elegir`, `caso`, `defecto` | Activas |
| **Ciclos** | `mientras`, `para`, `en`, `romper`, `continuar` | Activas |
| **Clases, herencia y POO** | `clase`, `extiende`, `constructor`, `nuevo`, `este`, `super`, `publico`, `privado` | Activas |
| **Módulos** | `importar`, `como`, `desde`, `modulo`, `exportar` | `importar` / `como` activos (`desde`, `modulo`, `exportar` reservados) |
| **Manejo de errores** | `intentar`, `capturar`, `finalmente`, `lanzar` | Activas |
| **Entrada y salida** | `imprimir`, `leer` | Activas |
| **Valores, lógica y tipos** | `verdadero`, `falso`, `nulo`, `y`, `o`, `no`, `es`, `??` | Activas |

## 4.2. Biblioteca estándar integrada

- **Núcleo y conversión:** `rango`, `longitud`, `tipo_de`, `convertir_entero`, `convertir_decimal`, `convertir_texto`
- **Listas y funcional (globales y por punto `.metodo()`):** `agregar`, `eliminar`, `mapear`, `filtrar`, `reducir`, `ordenar`, `invertir`, `contiene_elemento`, `indice_de`, `unir`
- **Diccionarios y JSON:** `claves`, `valores`, `contiene_clave`, `eliminar_clave`, `json_a_texto`, `texto_a_json`
- **Texto (globales y por punto `.metodo()`):** `mayusculas`, `minusculas`, `recortar`, `dividir`, `unir`, `reemplazar`, `contiene`, `empieza_con`, `termina_con`, `subtexto`
- **Matemáticas (`importar "matematicas" como mat` o globales):** `raiz`, `abs`, `redondear`, `piso`, `techo`, `minimo`, `maximo`, `seno`, `coseno`, `tangente`, `logaritmo`, `aleatorio`, `pi`, `euler` (`mat.PI`, `mat.E`)
- **Archivos seguros (`importar "archivos" como arch` o globales):** `leer_archivo`, `escribir_archivo`, `agregar_linea_archivo`, `existe_archivo`
- **Tiempo:** `tiempo_actual_ms`, `fecha_actual`, `esperar_ms`

## 4.3. Formato oficial y catálogo de códigos de error

Todos los errores del motor se muestran en español con archivo, línea, columna, código y línea fuente señalada con `^`:

- **Léxicos (`ALB-L001`–`ALB-L005`):** Cadena sin cerrar (`L001`), carácter inválido (`L002`), comentario `/*` sin cerrar (`L003`), escape inválido (`L004`), número mal formado (`L005`).
- **Sintácticos (`ALB-S001`–`ALB-S042`):** Errores estructurales de declaraciones, clases (`extiende`), bloques, selección `elegir` (`S040`/`S041`), cadenas interpoladas sin cerrar (`S039`) y literales diccionario (`S042`).
- **De Tipo (`ALB-T001`–`ALB-T010`):** Incompatibilidad de asignación (`T001`), parámetro (`T002`), retorno (`T003`), operador (`T004`), miembro (`T005`), indexación (`T006`/`T007`), iterable (`T008`), llamada no invocable (`T009`), clase inválida en `nuevo` (`T010`).
- **De Ejecución (`ALB-E001`–`ALB-E098`):** Redeclaración (`E001`), constante inmutable (`E002`), símbolo indefinido (`E003`), división por cero (`E005`), rango de lista/texto (`E006`), miembro inexistente (`E009`), acceso privado (`E010`), aridad incorrecta (`E011`), error de conversión/dominio (`E014`), error de herencia/`super` (`E015`), clave de diccionario ausente (`E016`), error de E/S en archivo (`E017`), excepción de usuario no capturada (`E090`), módulo no encontrado (`E097`), importación circular (`E098`).
