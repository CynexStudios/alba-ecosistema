# Política de Seguridad

## Versiones soportadas

| Versión | Soporte de seguridad |
| :--- | :--- |
| `2.0.x` | Soportada |
| `0.1.x` | Sin soporte (actualizar a 2.0.0) |

## Modelo de seguridad del IDE y Addons

- **Validador de complementos (`ValidadorAddons`):** Todo addon en `/addons` o instalado por el usuario se valida contra lista blanca de ejecutables y prevención de *path traversal* antes de poder ejecutarse.
- **Actualizaciones transparentes:** Alba Studio 2.0 nunca descarga ni ejecuta binarios en segundo plano de manera silenciosa; las actualizaciones requieren confirmación explícita y se descargan desde los lanzamientos oficiales en GitHub Releases.

## Reportar una vulnerabilidad

Si detectas una vulnerabilidad en el motor `Alba.Engine`, el validador de addons o los instaladores:
1. **No la publiques inicialmente como un issue público.**
2. Utiliza la pestaña **Security -> Advisories** del repositorio `CynexStudios/alba-ecosistema` en GitHub para enviar un reporte privado con los pasos para reproducirla.
