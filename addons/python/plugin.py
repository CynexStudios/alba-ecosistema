#!/usr/bin/env python3
"""
Adaptador controlado del complemento de Python para Alba Studio.
Valida sintaxis mediante ast.parse antes de ejecutar el script objetivo en UTF-8.
"""

import ast
import os
import runpy
import sys


def validar_sintaxis(ruta_archivo: str) -> tuple[bool, str]:
    with open(ruta_archivo, "r", encoding="utf-8") as archivo:
        codigo = archivo.read()
    try:
        ast.parse(codigo, filename=ruta_archivo)
        return True, "Sintaxis Python válida."
    except SyntaxError as err:
        detalle = (
            f"ERROR SINTÁCTICO (Python)\n"
            f"Archivo: {os.path.basename(ruta_archivo)}\n"
            f"Línea: {err.lineno or 1}\n"
            f"Columna: {err.offset or 1}\n"
            f"Descripción: {err.msg}"
        )
        return False, detalle


def ejecutar_archivo(ruta_archivo: str) -> int:
    valido, mensaje = validar_sintaxis(ruta_archivo)
    if not valido:
        print(mensaje, file=sys.stderr, flush=True)
        return 1

    try:
        sys.argv = [ruta_archivo]
        runpy.run_path(ruta_archivo, run_name="__main__")
        return 0
    except SystemExit as exit_exc:
        codigo = exit_exc.code
        return codigo if isinstance(codigo, int) else 0
    except Exception as exc:
        print(f"ERROR DE EJECUCIÓN (Python): {exc}", file=sys.stderr, flush=True)
        return 1


def main() -> int:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    if hasattr(sys.stderr, "reconfigure"):
        sys.stderr.reconfigure(encoding="utf-8")

    if len(sys.argv) < 3:
        print("Uso: plugin.py <ejecutar|verificar> <archivo.py>", file=sys.stderr)
        return 1

    accion = sys.argv[1].lower()
    ruta_archivo = os.path.abspath(sys.argv[2])

    if not os.path.isfile(ruta_archivo):
        print(f"Archivo no encontrado: {ruta_archivo}", file=sys.stderr)
        return 1

    if accion == "verificar":
        valido, mensaje = validar_sintaxis(ruta_archivo)
        print(mensaje if valido else mensaje, file=sys.stdout if valido else sys.stderr)
        return 0 if valido else 1

    if accion == "ejecutar":
        return ejecutar_archivo(ruta_archivo)

    print(f"Acción no soportada: {accion}", file=sys.stderr)
    return 1


if __name__ == "__main__":
    sys.exit(main())
