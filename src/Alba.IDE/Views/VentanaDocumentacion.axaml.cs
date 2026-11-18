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
            items.Add(new DocumentoItem("Referencia rápida de Alba", """
                # Lenguaje Alba v2.0.0 — Referencia Rápida

                variable nombre = "Alba"
                constante entero anio = 2026

                funcion saludar(texto quien) -> texto {
                    retornar "Hola, " + quien
                }

                si anio >= 2026 {
                    imprimir(saludar(nombre))
                } sino {
                    imprimir("Versión previa")
                }

                # Clases y manejo de errores:
                clase Persona {
                    publico texto nombre
                    constructor(texto nombre) {
                        este.nombre = nombre
                    }
                }
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
