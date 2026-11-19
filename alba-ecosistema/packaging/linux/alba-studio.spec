Name:           xynex-code-editor
Version:        %{_alba_version}
Release:        1%{?dist}
Summary:        Xynex Code Editor 5.0 — Lenguaje de programación Cibao e IDE nativo
License:        Apache-2.0
URL:            https://github.com/CynexStudios/alba-ecosistema
BuildArch:      x86_64
AutoReqProv:    no

%description
Xynex Code Editor es el entorno de desarrollo integrado oficial del lenguaje de
programación Cibao (v5.0.0), creado por Jaderson Liriano (CynexStudios).
Incluye motor autocontenido compatible con .cibao y .alb, CLI (cibao / alba),
editor Avalonia UI y sistema de complementos extensible.

%install
mkdir -p %{buildroot}/opt/xynex-code-editor
mkdir -p %{buildroot}/usr/bin
mkdir -p %{buildroot}/usr/share/applications
mkdir -p %{buildroot}/usr/share/mime/packages
mkdir -p %{buildroot}/usr/share/icons/hicolor/256x256/apps

cp -r %{_stage_dir}/* %{buildroot}/opt/xynex-code-editor/
ln -sf /opt/xynex-code-editor/ide/Alba.IDE %{buildroot}/usr/bin/xynex
ln -sf /opt/xynex-code-editor/ide/Alba.IDE %{buildroot}/usr/bin/alba-studio
ln -sf /opt/xynex-code-editor/cli/alba %{buildroot}/usr/bin/cibao
ln -sf /opt/xynex-code-editor/cli/alba %{buildroot}/usr/bin/alba
install -m 644 %{_pkg_dir}/alba-studio.desktop %{buildroot}/usr/share/applications/xynex-code-editor.desktop
install -m 644 %{_pkg_dir}/alba-mime.xml %{buildroot}/usr/share/mime/packages/cibao-mime.xml
install -m 644 %{_repo_root}/assets/icons/alba-studio-256.png %{buildroot}/usr/share/icons/hicolor/256x256/apps/xynex-code-editor.png

%post
if command -v update-desktop-database >/dev/null 2>&1; then update-desktop-database -q || true; fi
if command -v update-mime-database >/dev/null 2>&1; then update-mime-database /usr/share/mime || true; fi
if command -v gtk-update-icon-cache >/dev/null 2>&1; then gtk-update-icon-cache -q /usr/share/icons/hicolor || true; fi

%postun
if command -v update-desktop-database >/dev/null 2>&1; then update-desktop-database -q || true; fi

%files
/opt/xynex-code-editor
/usr/bin/xynex
/usr/bin/alba-studio
/usr/bin/cibao
/usr/bin/alba
/usr/share/applications/xynex-code-editor.desktop
/usr/share/mime/packages/cibao-mime.xml
/usr/share/icons/hicolor/256x256/apps/xynex-code-editor.png
