# 3. Manual Completo del Lenguaje Cibao (`v5.0.0`)

## 3.1. Hola Mundo y primer programa

Todo archivo de **Cibao** (*Cibao Programming Language*) utiliza la extensión oficial `.cibao` (y conserva compatibilidad total con `.alb`) y codificación UTF-8. No es necesario escribir punto y coma al final de cada instrucción.

```cibao
# Archivo: hola.cibao
variable lugar = "el Cibao"
imprimir($"¡Hola mundo desde {lugar.mayusculas()}!")
```

Para ejecutarlo desde consola:
```bash
alba ejecutar hola.cibao
```

### Programa interactivo con `leer`
```cibao
variable nombre = leer("¿Cómo te llamas? ")
imprimir($"Bienvenido/a, {nombre}")
```

---

## 3.2. Comentarios

```cibao
# Comentario de una sola línea

/*
   Comentario de varias líneas.
*/
```

---

## 3.3. Variables, constantes y coalescencia nula (`??`)

- **`variable`:** Crea una variable mutable. Puede inferir su tipo o declararlo explícitamente.
- **`constante`:** Crea un identificador inmutable; su inicialización con `=` es obligatoria.
- **`??`:** Devuelve el operando izquierdo si es distinto de `nulo`; en caso contrario evalúa y devuelve el derecho.

```cibao
variable edad = 20                     # Inferido como entero
variable precio: decimal = 19.95       # Tipado explícito
variable nombre: texto                 # Valor por defecto: ""
variable apodo = nulo ?? "Invitado"    # Resultado: "Invitado"

constante PI_LOCAL: decimal = 3.14159
```

Si una variable se declara sin inicializador con anotación de tipo, recibe un valor inicial seguro:
- `entero` → `0`
- `decimal` → `0.0`
- `texto` → `""`
- `booleano` → `falso`
- `lista` → `[]`
- `diccionario` → `{}`
- Tipo de clase o sin anotación → `nulo`

---

## 3.4. Tipos de datos, interpolación y diccionarios

Cibao implementa los siguientes tipos en la `v5.0.0`:
- `entero`: Entero de 64 bits con signo (`42`, `-7`).
- `decimal`: Número en coma flotante de doble precisión (`3.14`, `0.5`).
- `texto`: Cadena UTF-8 delimitada por `"..."` o `'...'`. Con prefijo `$"..."` o `$'...'` admite interpolación directa de expresiones `{expresion}` y escapes `\{` / `\}`.
- `booleano`: `verdadero` o `falso`.
- `lista`: Colección ordenada y mutable (`[1, "dos", verdadero]`).
- `diccionario`: Mapa clave-valor (`{ nombre: "Ana", edad: 28 }`), accesible vía `d["nombre"]` o `d.nombre`.
- `funcion`: Funciones nombradas y funciones anónimas (`funcion(x) { retornar x * 2 }`).
- `nulo`: Ausencia de valor.

```cibao
variable usuario: diccionario = {
    nombre: "Carlos",
    "ciudad": "Santiago"
}
usuario["activo"] = verdadero

para clave en usuario {
    imprimir($"{clave} => {usuario[clave]}")
}
```

---

## 3.5. Operadores y precedencia

De mayor a menor precedencia:
1. Agrupación y acceso: `( )`, `[ ]`, `.`
2. Instanciación: `nuevo`
3. Unarios: `-`, `no`
4. Potencia (asociativa a la derecha): `^`
5. Multiplicación, división y módulo: `*`, `/`, `%` (la división `/` siempre devuelve `decimal`)
6. Suma/concatenación y resta: `+`, `-`
7. Comparación y comprobación de tipo: `<`, `<=`, `>`, `>=`, `es`
8. Igualdad: `==`, `!=`
9. Conjunción lógica (cortocircuito): `y`
10. Disyunción lógica (cortocircuito): `o`
11. Coalescencia nula (cortocircuito): `??`
12. Asignación (solo sentencias): `=`, `+=`, `-=`, `*=`, `/=`

---

## 3.6. Condicionales (`si`, `sino_si`, `sino`) y `elegir`

```cibao
variable dia = "lunes"

elegir dia {
    caso "sabado", "domingo":
        imprimir("Fin de semana")
    caso "lunes":
        imprimir("Inicio de semana")
    defecto:
        imprimir("Día laboral")
}
```

> A diferencia de `switch` tradicional, `elegir` no hace *fall-through*: al coincidir un `caso`, ejecuta su bloque y sale automáticamente.

---

## 3.7. Ciclos (`mientras`, `para`, `romper`, `continuar`)

```cibao
para i en rango(1, 6) {
    si i == 2 { continuar }
    si i == 5 { romper }
    imprimir($"Valor: {i}")
}
```

El ciclo `para ... en` admite `lista`, `diccionario` (iterando claves) y `texto` (iterando caracteres).

---

## 3.8. Funciones, lambdas y programación funcional

```cibao
variable nums = [1, 2, 3, 4, 5, 6]
variable resultado = nums
    .filtrar(funcion(x: entero) -> booleano { retornar x % 2 == 0 })
    .mapear(funcion(x: entero) -> entero { retornar x * x })

imprimir(resultado)   # [4, 16, 36]
```

---

## 3.9. Clases, herencia (`extiende`, `super`) y polimorfismo (`es`)

```cibao
clase Vehiculo {
    publico marca: texto

    constructor(marca: texto) {
        este.marca = marca
    }

    publico funcion describir() -> texto {
        retornar $"Vehículo {este.marca}"
    }
}

clase Automovil extiende Vehiculo {
    publico puertas: entero

    constructor(marca: texto, puertas: entero) {
        super(marca)
        este.puertas = puertas
    }

    publico funcion describir() -> texto {
        retornar $"{super.describir()} de {este.puertas} puertas"
    }
}

variable auto: Vehiculo = nuevo Automovil("Toyota", 4)
imprimir(auto es Vehiculo, auto es Automovil, auto.describir())
```

---

## 3.10. Módulos e importaciones reales (`importar`)

Cibao soporta importar archivos `.cibao` / `.alb` relativos al archivo en ejecución, con caché única y prevención de dependencias circulares, además de módulos virtuales de la biblioteca estándar:

```cibao
importar "./utilidades.cibao" como util
importar "matematicas" como mat
importar "json" como json

imprimir($"Raíz: {mat.raiz(81)}")
imprimir(json.json_a_texto({ ok: verdadero, version: 5 }))
```

---

## 3.11. Manejo de errores (`intentar`, `capturar`, `finalmente`, `lanzar`)

```cibao
intentar {
    lanzar "No se pudo completar la operación."
} capturar (err) {
    imprimir($"Error capturado: {err}")
} finalmente {
    imprimir("Limpieza garantizada.")
}
```
