# 3. Manual Completo del Lenguaje Alba (`v0.1.0`)

## 3.1. Hola Mundo y primer programa

Todo archivo de Alba utiliza la extensión `.alb` y codificación UTF-8. No es necesario escribir punto y coma al final de cada instrucción.

```alba
# Archivo: hola.alb
imprimir("¡Hola, mundo desde Alba!")
```

Para ejecutarlo desde consola:
```bash
alba ejecutar hola.alb
```

### Programa interactivo con `leer`
```alba
variable nombre = leer("¿Cómo te llamas? ")
imprimir("Bienvenido/a,", nombre)
```

---

## 3.2. Comentarios

```alba
# Comentario de una sola línea

/*
   Comentario de varias líneas.
   Nota: los comentarios multilínea no se anidan en v0.1.0.
*/
```

---

## 3.3. Variables y constantes

- **`variable`:** Crea una variable mutable. Puede inferir su tipo o declararlo explícitamente.
- **`constante`:** Crea un identificador inmutable; su inicialización con `=` es obligatoria.

```alba
variable edad = 20                  # Inferido como entero
variable precio: decimal = 19.95    # Tipado explícito
variable nombre: texto              # Valor por defecto: ""

constante PI: decimal = 3.14159
constante MAX_USUARIOS = 100
```

Si una variable se declara sin inicializador con anotación de tipo, recibe un valor inicial seguro:
- `entero` → `0`
- `decimal` → `0.0`
- `texto` → `""`
- `booleano` → `falso`
- `lista` → `[]`
- Tipo de clase o sin anotación → `nulo`

---

## 3.4. Tipos de datos y tipado gradual

Alba implementa los siguientes tipos en la v0.1.0:
- `entero`: Entero de 64 bits con signo (`42`, `-7`).
- `decimal`: Número en coma flotante de doble precisión (`3.14`, `0.5`).
- `texto`: Cadena UTF-8 delimitada por `"..."` o `'...'`. Soporta `\n`, `\t`, `\r`, `\\`, `\"`, `\'`.
- `booleano`: `verdadero` o `falso`.
- `lista`: Colección ordenada y mutable (`[1, "dos", verdadero]`).
- `nulo`: Ausencia de valor.

**Regla de compatibilidad:**
- Una variable con tipo explícito valida cada asignación en tiempo de ejecución.
- Se permite promoción automática sin pérdida de `entero` hacia `decimal`.
- Las conversiones explícitas entre tipos se realizan mediante `convertir_entero(x)`, `convertir_decimal(x)` y `convertir_texto(x)`.
- `tipo_de(x)` devuelve el nombre del tipo actual como `texto`.

---

## 3.5. Operadores y precedencia

De mayor a menor precedencia:
1. Agrupación y acceso: `( )`, `[ ]`, `.`
2. Instanciación: `nuevo`
3. Unarios: `-`, `no`
4. Potencia (asociativa a la derecha): `^`
5. Multiplicación, división y módulo: `*`, `/`, `%` (la división `/` siempre devuelve `decimal`)
6. Suma/concatenación y resta: `+`, `-` (si al menos un operando de `+` es `texto`, concatena)
7. Comparación: `<`, `<=`, `>`, `>=`
8. Igualdad: `==`, `!=`
9. Conjunción lógica (cortocircuito): `y`
10. Disyunción lógica (cortocircuito): `o`
11. Asignación (solo sentencias): `=`, `+=`, `-=`, `*=`, `/=`

> Las condiciones de `si`, `mientras`, `y`, `o`, `no` exigen valores estrictamente `booleano`.

---

## 3.6. Condicionales (`si`, `sino_si`, `sino`)

```alba
variable nota = 8.2

si nota >= 9.0 {
    imprimir("Sobresaliente")
} sino_si nota >= 7.0 {
    imprimir("Aprobado")
} sino {
    imprimir("Necesita mejorar")
}
```

---

## 3.7. Ciclos (`mientras`, `para`, `romper`, `continuar`)

```alba
variable contador = 0
mientras contador < 3 {
    imprimir("Cuenta:", contador)
    contador += 1
}

para i en rango(1, 6) {
    si i == 2 {
        continuar
    }
    si i == 5 {
        romper
    }
    imprimir("Valor:", i)
}
```

El ciclo `para ... en` admite valores de tipo `lista` y `texto` (iterando carácter por carácter). La función integrada `rango` admite `rango(fin)`, `rango(inicio, fin)` y `rango(inicio, fin, paso)`.

---

## 3.8. Funciones y closures

```alba
funcion sumar(a: entero, b: entero) -> entero {
    retornar a + b
}

funcion crear_multiplicador(factor: entero) {
    funcion multiplicar(x: entero) -> entero {
        retornar x * factor
    }
    retornar multiplicar
}

variable por_tres = crear_multiplicador(3)
imprimir(sumar(4, 5), por_tres(7))
```

---

## 3.9. Listas y funciones integradas

```alba
variable frutas: lista = ["manzana", "pera"]
agregar(frutas, "uva")
frutas[0] = "mango"
variable primera = eliminar(frutas, 0)
imprimir("Eliminada:", primera, "| Restantes:", longitud(frutas), frutas)
```

---

## 3.10. Clases, objetos y encapsulamiento

Alba v0.1.0 soporta clases con campos `publico`/`privado` (por defecto `publico`), `constructor`, métodos, instanciación con `nuevo` y referencia a la instancia actual con `este`:

```alba
clase Rectangulo {
    publico ancho: decimal
    privado alto: decimal

    constructor(ancho: decimal, alto: decimal) {
        este.ancho = ancho
        este.alto = alto
    }

    publico funcion area() -> decimal {
        retornar este.ancho * este.alto
    }
}

variable r = nuevo Rectangulo(4.0, 2.5)
imprimir("Área:", r.area())
```

> Nota: La herencia entre clases no forma parte de la v0.1.0.

---

## 3.11. Manejo de errores (`intentar`, `capturar`, `finalmente`, `lanzar`)

```alba
intentar {
    variable divisor = 0
    si divisor == 0 {
        lanzar "No se puede dividir entre cero."
    }
} capturar (err) {
    imprimir("Error capturado:", err)
} finalmente {
    imprimir("Limpieza ejecutada siempre.")
}
```

El bloque `capturar` intercepta tanto valores lanzados con `lanzar` como errores controlados del motor (por ejemplo, división por cero o índice fuera de rango).
