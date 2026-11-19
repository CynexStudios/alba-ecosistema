using System.Text;
using Alba.IDE.Services;
using Avalonia.Controls;

namespace Alba.IDE.Views;

public partial class VentanaDocumentacion : Window
{
    private sealed record DocumentoItem(string Titulo, string Contenido)
    {
        public override string ToString() => Titulo;
    }

    public VentanaDocumentacion()
    {
        InitializeComponent();
        CargarDocumentos();
    }

    private void CargarDocumentos()
    {
        var items = new List<DocumentoItem>();
        string? dirDocs = RutasUsuarioAlba.BuscarDirectorioInstalacion("docs");

        if (!string.IsNullOrWhiteSpace(dirDocs) && Directory.Exists(dirDocs))
        {
            foreach (string archivo in Directory.GetFiles(dirDocs, "*.md").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                string nombre = Path.GetFileNameWithoutExtension(archivo).Replace('_', ' ');
                string texto = File.ReadAllText(archivo, Encoding.UTF8);
                items.Add(new DocumentoItem(nombre, texto));
            }
        }

        if (items.Count == 0)
        {
            items.Add(new DocumentoItem("Referencia rápida de Cibao 5.0", """
                # Cibao Programming Language v5.0.0 — Referencia Rápida

                # Variables, interpolación y coalescencia nula:
                variable ciudad: texto = "Santiago"
                variable alias = nulo ?? "Desarrollador"
                imprimir($"Bienvenido {alias} desde {ciudad.mayusculas()}!")

                # Diccionarios y JSON:
                variable perfil: diccionario = { nombre: "Ana", rol: "Ingeniera" }
                perfil["pais"] = "República Dominicana"
                imprimir(json_a_texto(perfil))

                # Control de flujo elegir / caso / defecto:
                elegir perfil.rol {
                    caso "Ingeniera", "Arquitecta":
                        imprimir("Acceso técnico completo")
                    defecto:
                        imprimir("Acceso estándar")
                }

                # Programación funcional y lambdas:
                variable numeros = [1, 2, 3, 4, 5]
                variable pares_dobles = numeros.filtrar(funcion(n) { retornar n % 2 == 0 })
                                               .mapear(funcion(n) { retornar n * 10 })

                # Clases, herencia y polimorfismo:
                clase Persona {
                    publico texto nombre
                    constructor(texto nombre) {
                        este.nombre = nombre
                    }
                    publico funcion saludar() -> texto {
                        retornar $"Hola, soy {este.nombre}"
                    }
                }

                clase Ingeniero extiende Persona {
                    publico texto especialidad
                    constructor(texto nombre, texto especialidad) {
                        super(nombre)
                        este.especialidad = especialidad
                    }
                    publico funcion saludar() -> texto {
                        retornar $"{super.saludar()} ({este.especialidad})"
                    }
                }

                # Módulos:
                importar "matematicas" como mat
                imprimir($"Raíz de 144 = {mat.raiz(144)}")
                """));
        }

        ListaDocumentos.ItemsSource = items;
        ListaDocumentos.SelectedIndex = 0;
    }

    private void AlSeleccionarDocumento(object? sender, SelectionChangedEventArgs e)
    {
        if (ListaDocumentos.SelectedItem is DocumentoItem doc)
        {
            TxtContenidoDoc.Text = doc.Contenido;
        }
    }
}
