#!/usr/bin/env bash
set -euo pipefail

VERSION="${1:-5.0.0}"
ARCH="amd64"
RID="linux-x64"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
DIST_DIR="${REPO_ROOT}/dist"
STAGE_DIR="${DIST_DIR}/stage-${RID}"

echo "==> Preparando empaquetado Linux para Xynex Code Editor & Cibao v${VERSION} (${RID})..."

rm -rf "${STAGE_DIR}"
mkdir -p "${STAGE_DIR}/ide" "${STAGE_DIR}/cli" "${DIST_DIR}"

# 1. Publicar IDE y CLI autocontenidos
dotnet publish "${REPO_ROOT}/src/Alba.IDE/Alba.IDE.csproj" \
    -c Release -r "${RID}" --self-contained true \
    -o "${STAGE_DIR}/ide" --nologo

dotnet publish "${REPO_ROOT}/src/Alba.CLI/Alba.CLI.csproj" \
    -c Release -r "${RID}" --self-contained true \
    -o "${STAGE_DIR}/cli" --nologo

chmod +x "${STAGE_DIR}/ide/Alba.IDE" "${STAGE_DIR}/cli/alba"
ln -sf "alba" "${STAGE_DIR}/cli/cibao"

# 2. Compilar host nativo C++20 si g++ está disponible
if command -v g++ >/dev/null 2>&1; then
    g++ -std=c++20 -O2 -static-libstdc++ -static-libgcc -DALBA_PLATFORM_LINUX=1 "${REPO_ROOT}/native-launcher/src/main.cpp" -o "${STAGE_DIR}/alba-host"
    chmod +x "${STAGE_DIR}/alba-host"
fi

# 3. Copiar addons, docs y ejemplos
cp -R "${REPO_ROOT}/addons" "${STAGE_DIR}/addons"
cp -R "${REPO_ROOT}/docs" "${STAGE_DIR}/docs"
cp -R "${REPO_ROOT}/examples" "${STAGE_DIR}/examples"

# 4. Archivo portable .tar.gz
TAR_OUT="${DIST_DIR}/XynexCodeEditor-${VERSION}-${RID}-portable.tar.gz"
tar -C "${STAGE_DIR}" -czf "${TAR_OUT}" .
echo "Archivo portable .tar.gz generado: ${TAR_OUT}"

# 5. Paquete .deb (Debian / Ubuntu / Mint / Pop!_OS)
if command -v dpkg-deb >/dev/null 2>&1; then
    DEB_ROOT="${DIST_DIR}/deb-pkg"
    rm -rf "${DEB_ROOT}"
    mkdir -p "${DEB_ROOT}/DEBIAN" \
             "${DEB_ROOT}/opt/xynex-code-editor" \
             "${DEB_ROOT}/usr/bin" \
             "${DEB_ROOT}/usr/share/applications" \
             "${DEB_ROOT}/usr/share/mime/packages" \
             "${DEB_ROOT}/usr/share/icons/hicolor/256x256/apps" \
             "${DEB_ROOT}/usr/share/icons/hicolor/128x128/apps"

    cp -R "${STAGE_DIR}/"* "${DEB_ROOT}/opt/xynex-code-editor/"
    ln -sf /opt/xynex-code-editor/ide/Alba.IDE "${DEB_ROOT}/usr/bin/xynex"
    ln -sf /opt/xynex-code-editor/ide/Alba.IDE "${DEB_ROOT}/usr/bin/alba-studio"
    ln -sf /opt/xynex-code-editor/cli/alba "${DEB_ROOT}/usr/bin/cibao"
    ln -sf /opt/xynex-code-editor/cli/alba "${DEB_ROOT}/usr/bin/alba"
    cp "${SCRIPT_DIR}/alba-studio.desktop" "${DEB_ROOT}/usr/share/applications/xynex-code-editor.desktop"
    cp "${SCRIPT_DIR}/alba-mime.xml" "${DEB_ROOT}/usr/share/mime/packages/cibao-mime.xml"
    cp "${REPO_ROOT}/assets/icons/alba-studio-256.png" "${DEB_ROOT}/usr/share/icons/hicolor/256x256/apps/xynex-code-editor.png"
    cp "${REPO_ROOT}/assets/icons/alba-studio-128.png" "${DEB_ROOT}/usr/share/icons/hicolor/128x128/apps/xynex-code-editor.png"

    cat > "${DEB_ROOT}/DEBIAN/control" <<EOF
Package: xynex-code-editor
Version: ${VERSION}
Section: devel
Priority: optional
Architecture: ${ARCH}
Maintainer: Jaderson Liriano (CynexStudios) <https://github.com/CynexStudios/alba-ecosistema>
Depends: libx11-6, libice6, libsm6, libfontconfig1
Description: Xynex Code Editor 5.0 — Lenguaje de programacion Cibao e IDE nativo
 Entorno de desarrollo integrado y motor autocontenido para programar en
 Cibao (.cibao / .alb). No requiere instalar .NET SDK adicional.
EOF

    cat > "${DEB_ROOT}/DEBIAN/postinst" <<'EOF'
#!/bin/sh
set -e
command -v update-desktop-database >/dev/null 2>&1 && update-desktop-database -q || true
command -v update-mime-database >/dev/null 2>&1 && update-mime-database /usr/share/mime || true
command -v gtk-update-icon-cache >/dev/null 2>&1 && gtk-update-icon-cache -q /usr/share/icons/hicolor || true
EOF
    chmod 0755 "${DEB_ROOT}/DEBIAN/postinst"

    DEB_OUT="${DIST_DIR}/XynexCodeEditor-${VERSION}-${RID}.deb"
    dpkg-deb --build "${DEB_ROOT}" "${DEB_OUT}"
    echo "Paquete .deb generado: ${DEB_OUT}"
fi

# 6. Paquete .rpm (Fedora / RHEL / openSUSE)
if command -v rpmbuild >/dev/null 2>&1; then
    RPM_TOP="${DIST_DIR}/rpmbuild"
    rm -rf "${RPM_TOP}"
    mkdir -p "${RPM_TOP}/"{BUILD,RPMS,SOURCES,SPECS,SRPMS}
    rpmbuild -bb "${SCRIPT_DIR}/alba-studio.spec" \
        --define "_topdir ${RPM_TOP}" \
        --define "_alba_version ${VERSION}" \
        --define "_stage_dir ${STAGE_DIR}" \
        --define "_pkg_dir ${SCRIPT_DIR}" \
        --define "_repo_root ${REPO_ROOT}"
    find "${RPM_TOP}/RPMS" -name "*.rpm" -exec cp {} "${DIST_DIR}/XynexCodeEditor-${VERSION}-${RID}.rpm" \;
    echo "Paquete .rpm generado en: ${DIST_DIR}/XynexCodeEditor-${VERSION}-${RID}.rpm"
fi

# 7. Paquete .AppImage portable
APPIMAGE_TOOL="${APPIMAGETOOL:-$(command -v appimagetool || true)}"
if [ -n "${APPIMAGE_TOOL}" ] && [ -x "${APPIMAGE_TOOL}" ]; then
    APPDIR="${DIST_DIR}/XynexCodeEditor.AppDir"
    rm -rf "${APPDIR}"
    mkdir -p "${APPDIR}/usr/lib/xynex-code-editor" "${APPDIR}/usr/bin"
    cp -R "${STAGE_DIR}/"* "${APPDIR}/usr/lib/xynex-code-editor/"
    cp "${SCRIPT_DIR}/alba-studio.desktop" "${APPDIR}/xynex-code-editor.desktop"
    cp "${REPO_ROOT}/assets/icons/alba-studio-256.png" "${APPDIR}/xynex-code-editor.png"

    cat > "${APPDIR}/AppRun" <<'EOF'
#!/bin/sh
HERE="$(dirname "$(readlink -f "$0")")"
exec "${HERE}/usr/lib/xynex-code-editor/ide/Alba.IDE" "$@"
EOF
    chmod +x "${APPDIR}/AppRun"

    ARCH=x86_64 "${APPIMAGE_TOOL}" "${APPDIR}" "${DIST_DIR}/XynexCodeEditor-${VERSION}-${RID}.AppImage"
    echo "Paquete .AppImage generado: ${DIST_DIR}/XynexCodeEditor-${VERSION}-${RID}.AppImage"
fi
