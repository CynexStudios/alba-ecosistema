# 03. Manual Completo del Lenguaje Cibao Code 9.0 (Desde Cero)

Este manual explica **todo el lenguaje Cibao 9.0 paso a paso**, desde tu primera línea hasta programación modular y orientada a objetos.

## 1. Hola Mundo y Comentarios

```cibao
// Comentario de una sola línea
/* Comentario de
   varias líneas */

mostrar("¡Hola Mundo desde Cibao Code 9.0!")
imprimir("Imprime texto sin problemas")
variable tuNombre = leer("Ingresa tu nombre: ")
mostrar($"Bienvenido, {tuNombre}!")
```

## 2. Variables y Constantes

- **`variable`**: crea un espacio de memoria que puedes modificar después.
- **`constante`**: valor inmutable que no se puede reasignar tras su creación.

```cibao
constante EMPRESA: texto = "Cynex Studios"
variable puntos: entero = 10
puntos += 5
puntos++
mostrar($"{EMPRESA} — Puntos actuales: {puntos}")
```

## 3. Los 7 Tipos de Datos Nativos

| Tipo | Palabra clave | Valores ejemplo |
|---|---|---|
| Entero | `entero` | `0`, `42`, `-15` |
| Decimal | `decimal` | `3.1416`, `9.0`, `-0.5` |
| Texto | `texto` | `"Hola"`, `$"Suma: {1 + 2}"`, `"""Multilínea"""` |
| Booleano | `booleano` | `verdadero`, `falso` |
| Lista | `lista` | `["a", "b", "c"]`, `[1, 2, 3]` |
| Diccionario | `diccionario` | `{ nombre: "Ana", activo: verdadero }` |
| Nulo | `nulo` | `nulo` |

Usa `tipo_de(valor)` para inspeccionar el tipo real en tiempo de ejecución y `valor es lista` para validar tipos en expresiones lógicas.

## 4. Operadores Aritméticos, Lógicos, Potencia, Opcional y Rangos

- **Aritméticos:** `+`, `-`, `*`, `/`, `%`, y **potencia `**`** (`2 ** 8` devuelve `256`).
- **Comparación:** `==`, `!=`, `<`, `<=`, `>`, `>=`.
- **Lógicos:** `y` (`&&`), `o` (`||`), `no` (`!`).
- **Opcional seguro:** `perfil?.contacto?.correo ?? "sin-correo@ejemplo.com"`.
- **Rangos (9.0):** `1..5` equivale a `[1, 2, 3, 4]`; `1..=5` equivale a `[1, 2, 3, 4, 5]`.

## 5. Control de Flujo

### Condicional `si` / `sino_si` / `sino`
```cibao
variable bateria: entero = 18
si bateria > 50 {
    mostrar("Batería óptima")
} sino_si bateria >= 15 {
    mostrar("Ahorro de energía recomendado")
} sino {
    mostrar("Conectar cargador")
}
```

### Selector `elegir` / `caso` / `defecto`
```cibao
variable dia = "viernes"
elegir dia {
    caso "lunes", "martes":
        mostrar("Inicio de semana")
    caso "viernes" {
        mostrar("¡Casi fin de semana!")
    }
    defecto:
        mostrar("Día laboral regular")
}
```

### Ciclos `para`, `mientras` y `repetir`
```cibao
para numero en 1..=5 {
    si numero == 3 { continuar }
    mostrar($"Número: {numero}")
}

variable intentos = 0
mientras intentos < 3 {
    intentos++
}

repetir 2 {
    mostrar("Repetición rápida")
}
```

## 6. Funciones, Parámetros por Defecto y Lambdas Flecha (`=>`)

```cibao
funcion calcularTotal(precio: decimal, impuesto: decimal = 0.18): decimal {
    retornar precio + (precio * impuesto)
}

// Expresión concisa de una línea con flecha =>
funcion doble(x: entero): entero => x * 2

// Lambdas en métodos funcionales de lista
variable valores = 1..=8
variable filtrados = valores
    .filtrar(n => n % 2 == 0)
    .mapear((n) => n ** 2)

mostrar($"Total={calcularTotal(100.0)} | Pares^2={unir(filtrados, ', ')}")
```

## 7. Programación Orientada a Objetos (POO)

```cibao
clase Persona {
    publico nombre: texto
    privado documento: texto

    constructor(nombre: texto, documento: texto) {
        este.nombre = nombre
        este.documento = documento
    }

    publico funcion presentarse(): texto {
        retornar $"Hola, soy {este.nombre}"
    }
}

clase Desarrollador extiende Persona {
    publico lenguaje: texto

    constructor(nombre: texto, documento: texto, lenguaje: texto) {
        super(nombre, documento)
        este.lenguaje = lenguaje
    }

    publico funcion presentarse(): texto {
        retornar $"{super.presentarse()} y programo en {este.lenguaje}"
    }
}

variable dev = nuevo Desarrollador("Lucía", "001", "Cibao 9.0")
mostrar(dev.presentarse())
```

## 8. Manejo Seguro de Errores y Módulos

```cibao
importar "matematicas" como mat

intentar {
    mostrar($"Raíz cuadrada de 144 = {mat.raiz(144)}")
    lanzar "Demostración de error controlado"
} capturar (err) {
    mostrar($"Capturado: {err}")
} finalmente {
    mostrar("Limpieza ejecutada siempre")
}
```
