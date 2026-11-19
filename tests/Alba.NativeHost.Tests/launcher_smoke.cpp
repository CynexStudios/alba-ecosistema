#include <cassert>
#include <filesystem>
#include <iostream>
#include <string>

int main() {
    std::filesystem::path p = std::filesystem::current_path();
    assert(!p.empty());

#ifdef _WIN32
    std::string plataforma = "Windows";
#elif defined(__APPLE__)
    std::string plataforma = "macOS";
#else
    std::string plataforma = "Linux";
#endif

    std::cout << "[Alba.NativeHost.Tests] Smoke test C++20 OK en plataforma: "
              << plataforma << std::endl;
    return 0;
}
