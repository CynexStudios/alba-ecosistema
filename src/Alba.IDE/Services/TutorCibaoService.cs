namespace Alba.IDE.Services;

public sealed record LeccionTutorCibao(
    int Numero,
    string Titulo,
    string Explicacion,
    string Reto,
    string CodigoEjemplo,
    string TextoEsperadoEnSalida);

/// <summary>
/// Bot tutor automático integrado en Xynex Code Editor 9.0 que guía a usuarios nuevos paso a paso
/// en la creación de su primer código en Cibao Code 9.0 usando todos los tipos, variables,
/// ciclos, funciones, clases y manejo de errores.
/// </summary>
public sealed class TutorCibaoService
{
    public IReadOnlyList<LeccionTutorCibao> Lecciones { get; } =
    [
        new(
            1,
            "1. Tu Primer Hola Mundo",
            "En Cibao Code 9.0 puedes mostrar mensajes en consola con `imprimir(...)` o `mostrar(...)`. Las cadenas van entre comillas dobles \"...\".",
            "Inserta el código, presiona F5 para ejecutarlo y comprueba el saludo.",
            """
            // Lección 1: Tu primer programa en Cibao Code 9.0
            mostrar("¡Hola Mundo desde Xynex Code Editor 9.0!")
            imprimir("Bienvenido a Cibao Code")
            """,
            "¡Hola Mundo"),
        new(
            2,
            "2. Variables, Constantes e Interpolación",
            "Usa `variable` para valores que cambian y `constante` para valores fijos. Con `$\"... {expr} ...\"` incrustas variables directo en el texto.",
            "Cambia tu nombre y edad, ejecuta y mira el texto interpolado.",
            """
            // Lección 2: Variables y Constantes
            constante VERSION = "9.0"
            variable nombre: texto = "Desarrollador"
            variable nivel: entero = 1
            nivel++

            mostrar($"Hola {nombre}, estás en Cibao {VERSION} (Nivel {nivel})")
            """,
            "Cibao 9.0"),
        new(
            3,
            "3. Todos los Tipos de Datos",
            "Cibao 9.0 incluye: `entero`, `decimal`, `texto`, `booleano` (`verdadero`/`falso`), `lista` (`[...]`), `diccionario` (`{ clave: valor }`) y `nulo`.",
            "Observa `tipo_de(...)` en cada tipo de dato nativo.",
            """
            // Lección 3: Todos los tipos de datos de Cibao 9.0
            variable edad: entero = 21
            variable altura: decimal = 1.78
            variable lenguaje: texto = "Cibao"
            variable activo: booleano = verdadero
            variable lenguajes: lista = ["Cibao", "Python", "HTML", "TypeScript"]
            variable perfil: diccionario = { usuario: "nuevo", puntos: 100 }
            variable sinValor = nulo

            mostrar($"entero={edad} ({tipo_de(edad)}), decimal={altura}, activo={activo}")
            mostrar($"lista={unir(lenguajes, ', ')}, usuario={perfil.usuario}, nulo={es_nulo(sinValor)}")
            """,
            "entero=21"),
        new(
            4,
            "4. Condicionales (`si`, `sino`, `elegir`)",
            "Toma decisiones con `si / sino_si / sino` o usa `elegir / caso / defecto` para múltiples opciones limpias.",
            "Prueba cambiar la nota o el día para ver distintas ramas.",
            """
            // Lección 4: Decisiones con si y elegir
            variable nota: entero = 95

            si nota >= 90 {
                mostrar("Calificación: Excelente (A)")
            } sino_si nota >= 75 {
                mostrar("Calificación: Aprobado (B)")
            } sino {
                mostrar("Calificación: Repaso recomendado")
            }

            variable rol = "creador"
            elegir rol {
                caso "admin", "creador" { mostrar("Acceso total habilitado") }
                defecto { mostrar("Acceso estándar") }
            }
            """,
            "Excelente"),
        new(
            5,
            "5. Ciclos y Rangos (`..` y `..=`)",
            "Con `1..5` generas [1,2,3,4] y con `1..=5` incluyes el 5. Recorre listas con `para x en lista` o usa `mientras`.",
            "Ejecuta el ciclo para sumar del 1 al 5.",
            """
            // Lección 5: Rangos 9.0 y ciclos
            variable total: entero = 0
            para n en 1..=5 {
                total += n
                mostrar($"Paso {n} -> acumulado = {total}")
            }
            mostrar($"Suma final = {total}")
            """,
            "Suma final = 15"),
        new(
            6,
            "6. Funciones y Lambdas Flecha (`=>`)",
            "Define funciones reutilizables con `funcion nombre(param: tipo): retorno` o expresiones cortas con `x => x * 2`.",
            "Ejecuta la función calcular y los filtros funcionales `.filtrar()` / `.mapear()`.",
            """
            // Lección 6: Funciones y Lambdas Flecha
            funcion saludar(persona: texto): texto {
                retornar $"¡Éxitos programando, {persona}!"
            }

            variable numeros = 1..=6
            variable pares = numeros.filtrar(n => n % 2 == 0)
            variable alCuadrado = pares.mapear(n => n * n)

            mostrar(saludar("Programador"))
            mostrar($"Pares al cuadrado: {unir(alCuadrado, ', ')}")
            """,
            "Pares al cuadrado: 4, 16, 36"),
        new(
            7,
            "7. Clases y Objetos (POO)",
            "Modela entidades reales con `clase`, atributos `publico`/`privado`, `constructor` y métodos.",
            "Crea una instancia con `nuevo` y llama su método.",
            """
            // Lección 7: Programación Orientada a Objetos
            clase Proyecto {
                publico nombre: texto
                privado version: decimal = 9.0

                constructor(nombre: texto) {
                    este.nombre = nombre
                }

                publico funcion resumen(): texto {
                    retornar $"Proyecto '{este.nombre}' v{este.version}"
                }
            }

            variable app = nuevo Proyecto("MiAppCibao")
            mostrar(app.resumen())
            """,
            "MiAppCibao"),
        new(
            8,
            "8. Errores (`intentar/capturar`) y Opcional (`?.` `??`)",
            "Protege tu app contra errores con `intentar / capturar` y evita fallos por valores `nulo` usando `?.` y `??`.",
            "Comprueba cómo se recupera de una clave o valor nulo sin caer.",
            """
            // Lección 8: Código robusto y seguro
            variable config = { tema: "oscuro", limite: nulo }
            variable puerto = config?.servidor?.puerto ?? 3000
            mostrar($"Puerto seguro seleccionado: {puerto}")

            intentar {
                variable fallo = 10 / 0
            } capturar (err) {
                mostrar($"Error capturado de forma segura: {err}")
            }
            """,
            "Puerto seguro seleccionado: 3000")
    ];

    public string ResponderPreguntaRapida(string mensajeUsuario, int indiceLeccionActual)
    {
        string q = (mensajeUsuario ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(q))
        {
            return "Escribe qué quieres aprender (ej: 'variables', 'tipos', 'funciones', 'bucles', 'clases', 'web') o pulsa 'Cargar lección en editor'.";
        }

        if (q.Contains("variable") || q.Contains("constante"))
            return "Usa `variable x: entero = 10` para valores modificables y `constante PI = 3.14` para constantes. Haz clic en la Lección 2 para insertarlo en el editor.";

        if (q.Contains("tipo") || q.Contains("dato") || q.Contains("lista") || q.Contains("diccionario"))
            return "Cibao 9.0 soporta `entero`, `decimal`, `texto`, `booleano`, `lista` (`[1,2]`), `diccionario` (`{clave: 1}`) y `nulo`. Mira la Lección 3 para probarlos todos.";

        if (q.Contains("funcion") || q.Contains("lambda") || q.Contains("flecha"))
            return "Las funciones se crean con `funcion sumar(a: entero, b: entero): entero { retornar a + b }` o en una línea con `funcion doble(x) => x * 2`.";

        if (q.Contains("bucle") || q.Contains("ciclo") || q.Contains("para") || q.Contains("mientras") || q.Contains("rango"))
            return "Con rangos puedes escribir `para i en 1..=10 { mostrar(i) }` o usar `mientras condicion { ... }`. Revisa la Lección 5.";

        if (q.Contains("clase") || q.Contains("objeto") || q.Contains("poo"))
            return "Declara clases con `clase Persona { publico nombre: texto; constructor(nombre: texto) { este.nombre = nombre } }` e instancia con `nuevo Persona(\"Ana\")`.";

        if (q.Contains("error") || q.Contains("intentar") || q.Contains("nulo") || q.Contains("??"))
            return "Usa `intentar { ... } capturar (e) { ... }` para errores y `usuario?.perfil?.nombre ?? \"Invitado\"` para evitar nulos.";

        LeccionTutorCibao leccion = Lecciones[Math.Clamp(indiceLeccionActual, 0, Lecciones.Count - 1)];
        return $"Ahora estás en '{leccion.Titulo}'. {leccion.Explicacion}\nConsejo: {leccion.Reto}";
    }

    public bool VerificarSalidaLeccion(LeccionTutorCibao leccion, string salidaConsola) =>
        !string.IsNullOrWhiteSpace(salidaConsola) &&
        salidaConsola.Contains(leccion.TextoEsperadoEnSalida, StringComparison.OrdinalIgnoreCase);
}
