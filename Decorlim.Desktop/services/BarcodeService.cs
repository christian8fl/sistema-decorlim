using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace Decorlim.Desktop.Services
{
    public static class BarcodeService
    {
        public static ImageSource GenerarImagenCodigoBarras(string texto, double ancho = 220, double alto = 55)
        {
            string codigo = string.IsNullOrWhiteSpace(texto) ? "DEC-00000" : texto.Trim().ToUpperInvariant();

            // Generar secuencia determinística de barras con marcas de inicio y fin
            var barras = new List<int> { 2, 1, 1, 2, 1, 2 }; // Inicio
            int checksum = 0;

            for (int i = 0; i < codigo.Length; i++)
            {
                int val = codigo[i];
                checksum += val * (i + 1);
                // Patrón de 4 módulos por carácter (barra, espacio, barra, espacio)
                barras.Add(((val >> 6) & 3) + 1);
                barras.Add(((val >> 4) & 3) + 1);
                barras.Add(((val >> 2) & 3) + 1);
                barras.Add((val & 3) + 1);
            }

            int dv = checksum % 10;
            barras.Add((dv % 3) + 1);
            barras.Add(((dv + 1) % 3) + 1);
            barras.Add(2); barras.Add(1); barras.Add(2); barras.Add(1); barras.Add(2); // Fin

            int totalUnidades = 0;
            foreach (int b in barras) totalUnidades += b;

            double unidadAncho = Math.Max(1.2, (ancho - 16) / totalUnidades);
            double anchoReal = (totalUnidades * unidadAncho) + 16;

            var visual = new DrawingVisual();
            using (DrawingContext dc = visual.RenderOpen())
            {
                dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, anchoReal, alto));

                double x = 8;
                double altoBarras = alto - 18;
                bool esBarraNegra = true;

                foreach (int unidades in barras)
                {
                    double w = unidades * unidadAncho;
                    if (esBarraNegra)
                    {
                        dc.DrawRectangle(Brushes.Black, null, new Rect(x, 4, w, altoBarras));
                    }
                    x += w;
                    esBarraNegra = !esBarraNegra;
                }

                var formattedText = new FormattedText(
                    codigo,
                    System.Globalization.CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    new Typeface(new FontFamily("Consolas"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                    11,
                    Brushes.Black,
                    1.0);

                double textX = Math.Max(4, (anchoReal - formattedText.Width) / 2);
                dc.DrawText(formattedText, new Point(textX, alto - 14));
            }

            var drawingGroup = visual.Drawing;
            var image = new DrawingImage(drawingGroup);
            image.Freeze();
            return image;
        }
    }
}