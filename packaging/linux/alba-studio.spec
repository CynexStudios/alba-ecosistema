Name:           alba-studio
Version:        %{_alba_version}
Release:        1%{?dist}
Summary:        Alba Studio 2.0 — Lenguaje de programación en español e IDE nativo
License:        Apache-2.0
URL:            https://github.com/CynexStudios/alba-ecosistema
BuildArch:      x86_64
AutoReqProv:    no

%description
Alba Studio es el entorno de desarrollo integrado oficial del lenguaje de
programación en español Alba. Incluye motor Alba.Engine autocontenido,
CLI (alba), editor Avalonia UI y sistema de complementos extensible.

%install
mkdir -p %{buildroot}/opt/alba-studio
mkdir -p %{buildroot}/usr/bin
mkdir -p %{buildroot}/usr/share/applications
mkdir -p %{buildroot}/usr/share/mime/packages
mkdir -p %{buildroot}/usr/share/icons/hicolor/256x256/apps

cp -r %{_stage_dir}/* %{buildroot}/opt/alba-studio/
ln -sf /opt/alba-studio/ide/Alba.IDE %{buildroot}/usr/bin/alba-studio
ln -sf /opt/alba-studio/cli/alba %{buildroot}/usr/bin/alba
install -m 644 %{_pkg_dir}/alba-studio.desktop %{buildroot}/usr/share/applications/alba-studio.desktop
install -m 644 %{_pkg_dir}/alba-mime.xml %{buildroot}/usr/share/mime/packages/alba-mime.xml
install -m 644 %{_repo_root}/assets/icons/alba-studio-256.png %{buildroot}/usr/share/icons/hicolor/256x256/apps/alba-studio.png

%post
if command -v update-desktop-database >/dev/null 2>&1; then update-desktop-database -q || true; fi
if command -v update-mime-database >/dev/null 2>&1; then update-mime-database /usr/share/mime || true; fi
if command -v gtk-update-icon-cache >/dev/null 2>&1; then gtk-update-icon-cache -q /usr/share/icons/hicolor || true; fi

%postun
if command -v update-desktop-database >/dev/null 2>&1; then update-desktop-database -q || true; fi

%files
/opt/alba-studio
/usr/bin/alba-studio
/usr/bin/alba
/usr/share/applications/alba-studio.desktop
/usr/share/mime/packages/alba-mime.xml
/usr/share/icons/hicolor/256x256/apps/alba-studio.png
