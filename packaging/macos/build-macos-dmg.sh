#!/usr/bin/env bash
set -euo pipefail

VERSION="${1:-5.0.0}"
ARCH="${2:-arm64}" # arm64 (Apple Silicon) o x64 (Intel)
RID="osx-${ARCH}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
DIST_DIR="${REPO_ROOT}/dist"
STAGE_DMG="${DIST_DIR}/dmg-${RID}"
APP_BUNDLE="${STAGE_DMG}/Xynex Code Editor.app"

echo "==> Empaquetando Xynex Code Editor v${VERSION} para macOS (${RID})..."

rm -rf "${STAGE_DMG}"
mkdir -p "${APP_BUNDLE}/Contents/MacOS/cli" "${APP_BUNDLE}/Contents/Resources" "${DIST_DIR}"

# 1. Publicar IDE y CLI autocontenidos
dotnet publish "${REPO_ROOT}/src/Alba.IDE/Alba.IDE.csproj" \
    -c Release -r "${RID}" --self-contained true \
    -o "${APP_BUNDLE}/Contents/MacOS" --nologo

dotnet publish "${REPO_ROOT}/src/Alba.CLI/Alba.CLI.csproj" \
    -c Release -r "${RID}" --self-contained true \
    -o "${APP_BUNDLE}/Contents/MacOS/cli" --nologo

chmod +x "${APP_BUNDLE}/Contents/MacOS/Alba.IDE" "${APP_BUNDLE}/Contents/MacOS/cli/alba"
ln -sf "alba" "${APP_BUNDLE}/Contents/MacOS/cli/cibao"

# 2. Copiar Info.plist, icono .icns y recursos en Contents/Resources
sed "s/@ALBA_VERSION@/${VERSION}/g" "${SCRIPT_DIR}/Info.plist.in" > "${APP_BUNDLE}/Contents/Info.plist"
cp "${REPO_ROOT}/assets/icons/alba-studio.icns" "${APP_BUNDLE}/Contents/Resources/alba-studio.icns"
cp -R "${REPO_ROOT}/addons" "${APP_BUNDLE}/Contents/Resources/addons"
cp -R "${REPO_ROOT}/docs" "${APP_BUNDLE}/Contents/Resources/docs"
cp -R "${REPO_ROOT}/examples" "${APP_BUNDLE}/Contents/Resources/examples"

# 3. Enlace estándar hacia Aplicaciones dentro del DMG
ln -s /Applications "${STAGE_DMG}/Applications"

# 4. Crear archivo .dmg (solo en macOS con hdiutil)
DMG_PATH="${DIST_DIR}/XynexCodeEditor-${VERSION}-${RID}.dmg"
rm -f "${DMG_PATH}"
if command -v hdiutil >/dev/null 2>&1; then
    hdiutil create -volname "Xynex Code Editor ${VERSION}" -srcfolder "${STAGE_DMG}" -ov -format UDZO "${DMG_PATH}"
    echo "DMG generado: ${DMG_PATH}"
else
    echo "hdiutil no disponible fuera de macOS. Bundle .app preparado en: ${APP_BUNDLE}"
fi
