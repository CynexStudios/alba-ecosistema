#include <filesystem>
#include <iostream>
#include <string>
#include <vector>

#ifdef _WIN32
#include <process.h>
#else
#include <sys/types.h>
#include <sys/wait.h>
#include <unistd.h>
#endif

namespace fs = std::filesystem;

namespace {

std::string nombre_ejecutable(const std::string& base) {
#ifdef _WIN32
    return base + ".exe";
#else
    return base;
#endif
}

fs::path resolver_ruta_componente(const fs::path& dir_base, const std::string& subcarpeta, const std::string& binario) {
    fs::path directa = dir_base / subcarpeta / nombre_ejecutable(binario);
    if (fs::exists(directa)) {
        return directa;
    }

    // Soporte para bundle .app en macOS (Contents/MacOS/Alba.IDE)
    fs::path en_misma = dir_base / nombre_ejecutable(binario);
    if (fs::exists(en_misma)) {
        return en_misma;
    }

    return directa;
}

int ejecutar_proceso_seguro(const fs::path& ejecutable, const std::vector<std::string>& argumentos) {
    if (!fs::exists(ejecutable)) {
        std::cerr << "[Alba Host] No se encontró el binario requerido: " << ejecutable.string() << '\n';
        return 127;
    }

    std::vector<std::string> argv_copia;
    argv_copia.reserve(argumentos.size() + 1);
    argv_copia.push_back(ejecutable.string());
    for (const auto& a : argumentos) {
        argv_copia.push_back(a);
    }

    std::vector<char*> argv_ptr;
    argv_ptr.reserve(argv_copia.size() + 1);
    for (auto& s : argv_copia) {
        argv_ptr.push_back(s.data());
    }
    argv_ptr.push_back(nullptr);

#ifdef _WIN32
    intptr_t codigo = _spawnv(_P_WAIT, ejecutable.string().c_str(), argv_ptr.data());
    if (codigo == -1) {
        std::cerr << "[Alba Host] Error al iniciar el proceso en Windows.\n";
        return 1;
    }
    return static_cast<int>(codigo);
#else
    pid_t pid = fork();
    if (pid < 0) {
        std::cerr << "[Alba Host] Error al crear subproceso POSIX.\n";
        return 1;
    }
    if (pid == 0) {
        execv(ejecutable.c_str(), argv_ptr.data());
        _exit(127);
    }
    int estado = 0;
    if (waitpid(pid, &estado, 0) < 0) {
        return 1;
    }
    if (WIFEXITED(estado)) {
        return WEXITSTATUS(estado);
    }
    return 1;
#endif
}

void imprimir_ayuda() {
    std::cout << "Xynex NativeHost v5.0.0 (C++20 Portable Windows/macOS/Linux)\n"
              << "Uso:\n"
              << "  alba-host ejecutar <archivo.cibao|alb>   Ejecuta un programa en Cibao\n"
              << "  alba-host verificar <archivo.cibao|alb>  Verifica sintaxis sin ejecutar\n"
              << "  alba-host ide [carpeta]                  Inicia Xynex Code Editor\n"
              << "  alba-host --verificar-entorno            Verifica rutas portables del host\n"
              << "  alba-host version                        Muestra la versión del host\n";
}

} // namespace

int main(int argc, char* argv[]) {
    fs::path ruta_propia = (argc > 0 && argv[0] != nullptr)
        ? fs::weakly_canonical(fs::path(argv[0]))
        : fs::current_path();
    fs::path dir_base = ruta_propia.parent_path();

    if (argc < 2) {
        imprimir_ayuda();
        return 0;
    }

    std::string comando = argv[1];

    if (comando == "version" || comando == "--version") {
        std::cout << "Xynex NativeHost v5.0.0 (Cibao Programming Language)\n";
        return 0;
    }

    if (comando == "ayuda" || comando == "--help") {
        imprimir_ayuda();
        return 0;
    }

    if (comando == "--verificar-entorno") {
        fs::path cli_path = resolver_ruta_componente(dir_base, "cli", "alba");
        fs::path ide_path = resolver_ruta_componente(dir_base, "ide", "Alba.IDE");
        std::cout << "Directorio base: " << dir_base.string() << '\n'
                  << "CLI esperado:    " << cli_path.string() << '\n'
                  << "IDE esperado:    " << ide_path.string() << '\n';
        return 0;
    }

    if (comando == "ide") {
        fs::path ide_path = resolver_ruta_componente(dir_base, "ide", "Alba.IDE");
        std::vector<std::string> args_ide;
        for (int i = 2; i < argc; ++i) {
            args_ide.emplace_back(argv[i]);
        }
        return ejecutar_proceso_seguro(ide_path, args_ide);
    }

    fs::path cli_path = resolver_ruta_componente(dir_base, "cli", "alba");
    std::vector<std::string> args_cli;
    for (int i = 1; i < argc; ++i) {
        args_cli.emplace_back(argv[i]);
    }
    return ejecutar_proceso_seguro(cli_path, args_cli);
}
