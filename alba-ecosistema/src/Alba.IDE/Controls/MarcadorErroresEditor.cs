using Alba.IDE.Models;
using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace Alba.IDE.Controls;

/// <summary>
/// Dibuja subrayados ondulados bajo errores léxicos y sintácticos detectados en vivo.
/// </summary>
public sealed class MarcadorErroresEditor : IBackgroundRenderer
{
    private readonly List<ProblemaCodigo> _problemas = [];
    private readonly Pen _lapizError = new(new SolidColorBrush(Color.Parse("#EF4444")), 1.4);

    public KnownLayer Layer => KnownLayer.Selection;

    public void ActualizarProblemas(IEnumerable<ProblemaCodigo> problemas)
    {
        _problemas.Clear();
        _problemas.AddRange(problemas);
    }

    public ProblemaCodigo? ObtenerProblemaEnLinea(int linea) =>
        _problemas.FirstOrDefault(p => p.Linea == linea);

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (!textView.VisualLinesValid || _problemas.Count == 0 || textView.Document is null)
        {
            return;
        }

        TextDocument doc = textView.Document;
        foreach (ProblemaCodigo p in _problemas)
        {
            if (p.Linea < 1 || p.Linea > doc.LineCount)
            {
                continue;
            }

            DocumentLine lineaDoc = doc.GetLineByNumber(p.Linea);
            int offsetInicio = lineaDoc.Offset + Math.Clamp(p.Columna - 1, 0, Math.Max(0, lineaDoc.Length));
            int longitud = Math.Max(1, Math.Min(Math.Max(3, p.Longitud), Math.Max(1, lineaDoc.EndOffset - offsetInicio)));
            if (offsetInicio >= doc.TextLength)
            {
                offsetInicio = Math.Max(0, lineaDoc.Offset);
                longitud = Math.Max(1, Math.Min(4, lineaDoc.Length));
            }

            var segmento = new TextSegment
            {
                StartOffset = offsetInicio,
                Length = Math.Min(longitud, Math.Max(0, doc.TextLength - offsetInicio))
            };

            foreach (Rect rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, segmento))
            {
                double y = rect.Bottom - 1.5;
                double xInicio = rect.Left;
                double xFin = Math.Max(rect.Right, rect.Left + 10);

                var geo = new StreamGeometry();
                using (StreamGeometryContext ctx = geo.Open())
                {
                    ctx.BeginFigure(new Point(xInicio, y), isFilled: false);
                    bool arriba = true;
                    for (double x = xInicio + 3; x <= xFin; x += 3)
                    {
                        ctx.LineTo(new Point(x, arriba ? y - 2 : y + 1));
                        arriba = !arriba;
                    }
                    ctx.EndFigure(isClosed: false);
                }
                drawingContext.DrawGeometry(null, _lapizError, geo);
            }
        }
    }
}
