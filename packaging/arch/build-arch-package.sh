#!/usr/bin/env bash
set -euo pipefail

VERSION="${1:-9.0.0}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
DIST_DIR="${REPO_ROOT}/dist"
WORK_DIR="${DIST_DIR}/arch-build"

if ! command -v makepkg >/dev/null 2>&1; then
    echo "Error: 'makepkg' no está disponible. Ejecuta este script en Arch Linux / CachyOS o mediante Docker archlinux:latest." >&2
    exit 1
fi

echo "==> Compilando paquete nativo Arch Linux / CachyOS (.pkg.tar.zst) v${VERSION}..."

rm -rf "${WORK_DIR}"
mkdir -p "${WORK_DIR}" "${DIST_DIR}"
cp "${SCRIPT_DIR}/PKGBUILD" "${WORK_DIR}/PKGBUILD"

# Ajustar pkgver dinámicamente si se solicita una versión distinta
sed -i "s/^pkgver=.*/pkgver=${VERSION}/" "${WORK_DIR}/PKGBUILD"

export XYNEX_REPO_ROOT="${REPO_ROOT}"
export PKGEXT=".pkg.tar.zst"

(
    cd "${WORK_DIR}"
    makepkg -f --noconfirm
)

PKG_FILE="$(find "${WORK_DIR}" -maxdepth 1 -name '*.pkg.tar.zst' | head -n 1)"
if [ -z "${PKG_FILE}" ] || [ ! -f "${PKG_FILE}" ]; then
    echo "Error: No se generó el archivo .pkg.tar.zst en ${WORK_DIR}." >&2
    exit 1
fi

OUT_FILE="${DIST_DIR}/XynexCodeEditor-${VERSION}-arch-cachyos-x86_64.pkg.tar.zst"
cp "${PKG_FILE}" "${OUT_FILE}"
echo "Paquete nativo Arch / CachyOS generado: ${OUT_FILE}"
