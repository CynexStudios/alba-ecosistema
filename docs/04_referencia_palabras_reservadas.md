# 04. Referencia Rápida de Palabras Reservadas, Operadores y Biblioteca Estándar (v9.0)

## Palabras Reservadas de Cibao 9.0

| Categoría | Palabras Clave |
|---|---|
| **Declaración** | `variable`, `constante`, `funcion`, `clase`, `constructor`, `extiende`, `nuevo`, `este`, `super`, `publico`, `privado`, `estatico` |
| **Control** | `si`, `sino_si`, `sino`, `mientras`, `para`, `en`, `repetir`, `detener`, `continuar`, `retornar`, `elegir`, `caso`, `defecto` |
| **Errores y Módulos** | `intentar`, `capturar`, `finalmente`, `lanzar`, `importar`, `como`, `exportar` |
| **Tipos de Datos** | `entero`, `decimal`, `texto`, `booleano`, `lista`, `diccionario`, `vacio`, `objeto` |
| **Valores y Lógicos** | `verdadero`, `falso`, `nulo`, `y`, `o`, `no`, `es` |

## Funciones Nativas Integradas

- **Entrada/Salida:** `imprimir(...)`, `mostrar(...)`, `leer(mensaje?)`
- **Inspección y Conversión:** `tipo_de(x)`, `es_nulo(x)`, `a_entero(x)`, `a_decimal(x)`, `a_texto(x)`, `a_booleano(x)`
- **Texto:** `longitud(t)`, `mayusculas(t)`, `minusculas(t)`, `recortar(t)`, `dividir(t, sep)`, `unir(lista, sep)`, `reemplazar(t, busca, nuevo)`, `contiene(t, sub)`, `empieza_con(t, pre)`, `termina_con(t, suf)`, `subtexto(t, ini, len?)`
- **Listas y Diccionarios:** `agregar(l, v)`, `quitar(l, idx)`, `insertar(l, idx, v)`, `ordenar(l)`, `invertir(l)`, `filtrar(l, fn)`, `mapear(l, fn)`, `reducir(l, fn, init)`, `claves(d)`, `valores(d)`, `tiene_clave(d, k)`
- **Matemáticas:** `abs(n)`, `raiz(n)`, `potencia(b, e)`, `redondear(n, dec?)`, `piso(n)`, `techo(n)`, `minimo(...)`, `maximo(...)`, `aleatorio(min?, max?)`, `seno(n)`, `coseno(n)`, `tangente(n)`
- **Archivos, JSON, Hash y Tiempo:** `leer_archivo(ruta)`, `escribir_archivo(ruta, contenido)`, `existe_archivo(ruta)`, `json_a_texto(obj)`, `texto_a_json(str)`, `hash_sha256(str)`, `tiempo_actual()`, `esperar(ms)`

## Módulos Estándar (`importar "nombre"`)

- `"matematicas"`: constantes `PI`, `E`, trigonometría y utilidades numéricas.
- `"texto"`: utilidades de cadenas y formato.
- `"json"`: conversión segura entre diccionarios/listas y texto JSON.
- `"archivos"`: lectura, escritura y comprobación de existencia.
