#!/usr/bin/env bash
set -euo pipefail

VERSION="${1:-5.0.0}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

echo "=== Compilación Integral: Xynex Code Editor & Cibao v${VERSION} ==="
cd "${REPO_ROOT}"

dotnet restore AlbaEcosistema.sln
dotnet build AlbaEcosistema.sln -c Release --no-restore
dotnet test AlbaEcosistema.sln -c Release --no-build --verbosity normal

chmod +x packaging/linux/build-linux-packages.sh
./packaging/linux/build-linux-packages.sh "${VERSION}"

if command -v makepkg >/dev/null 2>&1; then
    echo "==> Entorno Arch Linux / CachyOS detectado: generando paquete nativo .pkg.tar.zst..."
    chmod +x packaging/arch/build-arch-package.sh
    ./packaging/arch/build-arch-package.sh "${VERSION}"
fi

echo "=== Empaquetado Linux completado en '${REPO_ROOT}/dist' ==="
