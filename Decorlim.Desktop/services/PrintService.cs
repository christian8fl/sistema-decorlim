using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Decorlim.Desktop.Models;

namespace Decorlim.Desktop.Services
{
    public static class PrintService
    {
        public static void ImprimirComprobante(RegistrarVentaResponse venta, bool mostrarDialogo = true)
        {
            var doc = new FlowDocument
            {
                PageWidth = 300, // ~80mm ticket térmico o A4 ajustado
                PagePadding = new Thickness(14),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 10
            };

            // Encabezado Empresa
            var pHeader = new Paragraph { TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 0, 0, 6) };
            pHeader.Inlines.Add(new Run("DECORLIM\n") { FontSize = 14, FontWeight = FontWeights.Bold });
            pHeader.Inlines.Add(new Run("Pinturas, Acabados y Ferretería\n") { FontSize = 9 });
            pHeader.Inlines.Add(new Run("RUC: 1099999999001\n") { FontSize = 9 });
            pHeader.Inlines.Add(new Run("Cotacachi - Imbabura - Ecuador\n") { FontSize = 8.5, Foreground = Brushes.DimGray });
            doc.Blocks.Add(pHeader);

            // Tipo de Documento y Secuencial
            bool esFactura = string.Equals(venta.TipoDocumento, "Factura", StringComparison.OrdinalIgnoreCase);
            var pDoc = new Paragraph
            {
                TextAlignment = TextAlignment.Center,
                Background = Brushes.WhiteSmoke,
                Padding = new Thickness(4),
                Margin = new Thickness(0, 2, 0, 6)
            };
            pDoc.Inlines.Add(new Run(esFactura ? "FACTURA ELECTRÓNICA SRI\n" : "NOTA DE VENTA\n")
            {
                FontWeight = FontWeights.Bold,
                FontSize = 11
            });
            pDoc.Inlines.Add(new Run($"No. {venta.Secuencial}\n") { FontWeight = FontWeights.SemiBold });
            pDoc.Inlines.Add(new Run($"Fecha: {venta.FechaHora:dd/MM/yyyy HH:mm}\n") { FontSize = 8.5 });

            if (esFactura && !string.IsNullOrWhiteSpace(venta.ClaveAccesoSri))
            {
                pDoc.Inlines.Add(new Run("CLAVE DE ACCESO SRI:\n") { FontSize = 8, FontWeight = FontWeights.Bold });
                pDoc.Inlines.Add(new Run(venta.ClaveAccesoSri) { FontSize = 7.5, FontFamily = new FontFamily("Consolas") });
            }
            doc.Blocks.Add(pDoc);

            // Datos Completos del Cliente
            var pCliente = new Paragraph { Margin = new Thickness(0, 2, 0, 6), FontSize = 9 };
            pCliente.Inlines.Add(new Run("DATOS DEL CLIENTE\n") { FontWeight = FontWeights.Bold });
            pCliente.Inlines.Add(new Run($"Cliente: {venta.ClienteNombre}\n"));
            pCliente.Inlines.Add(new Run($"RUC/CI: {venta.ClienteIdentificacion}\n"));
            if (!string.IsNullOrWhiteSpace(venta.ClienteDireccion))
                pCliente.Inlines.Add(new Run($"Dirección: {venta.ClienteDireccion}\n"));
            if (!string.IsNullOrWhiteSpace(venta.ClienteTelefono) || !string.IsNullOrWhiteSpace(venta.ClienteCelular))
                pCliente.Inlines.Add(new Run($"Tel/Cel: {venta.ClienteTelefono} {venta.ClienteCelular}\n"));
            if (!string.IsNullOrWhiteSpace(venta.ClienteCorreo))
                pCliente.Inlines.Add(new Run($"Correo: {venta.ClienteCorreo}\n"));
            doc.Blocks.Add(pCliente);

            // Tabla de Productos
            var tabla = new Table { CellSpacing = 2, Margin = new Thickness(0, 4, 0, 6) };
            tabla.Columns.Add(new TableColumn { Width = new GridLength(32) });
            tabla.Columns.Add(new TableColumn { Width = new GridLength(148) });
            tabla.Columns.Add(new TableColumn { Width = new GridLength(45) });
            tabla.Columns.Add(new TableColumn { Width = new GridLength(50) });

            var rg = new TableRowGroup();
            var headerRow = new TableRow { Background = Brushes.LightGray };
            headerRow.Cells.Add(CrearCelda("Cant", true));
            headerRow.Cells.Add(CrearCelda("Descripción", true));
            headerRow.Cells.Add(CrearCelda("P.Unit", true, TextAlignment.Right));
            headerRow.Cells.Add(CrearCelda("Total", true, TextAlignment.Right));
            rg.Rows.Add(headerRow);

            foreach (var item in venta.Items)
            {
                var row = new TableRow();
                row.Cells.Add(CrearCelda(item.Cantidad.ToString(), false));
                row.Cells.Add(CrearCelda($"{item.Nombre} ({item.Presentacion})", false));
                row.Cells.Add(CrearCelda($"${item.PrecioUnitario:F2}", false, TextAlignment.Right));
                row.Cells.Add(CrearCelda($"${item.SubtotalLinea:F2}", false, TextAlignment.Right));
                rg.Rows.Add(row);
            }
            tabla.RowGroups.Add(rg);
            doc.Blocks.Add(tabla);

            // Totales y Forma de Pago
            var pTotales = new Paragraph { TextAlignment = TextAlignment.Right, Margin = new Thickness(0, 4, 0, 6) };
            pTotales.Inlines.Add(new Run($"Subtotal: ${venta.Subtotal:F2}\n"));
            if (esFactura)
            {
                pTotales.Inlines.Add(new Run($"IVA (15%): ${venta.Iva:F2}\n"));
            }
            pTotales.Inlines.Add(new Run($"TOTAL A PAGAR: ${venta.Total:F2}\n") { FontSize = 12, FontWeight = FontWeights.Bold });
            doc.Blocks.Add(pTotales);

            var pPago = new Paragraph { FontSize = 9, Margin = new Thickness(0, 2, 0, 8) };
            pPago.Inlines.Add(new Run($"Forma de Pago: {venta.MetodoPago}\n") { FontWeight = FontWeights.SemiBold });
            if (!string.IsNullOrWhiteSpace(venta.ReferenciaPago))
            {
                pPago.Inlines.Add(new Run($"Ref. Transferencia: {venta.ReferenciaPago}\n"));
            }
            if (esFactura && !string.IsNullOrWhiteSpace(venta.ClienteCorreo))
            {
                pPago.Inlines.Add(new Run($"Comprobante enviado a: {venta.ClienteCorreo}\n") { Foreground = Brushes.DimGray });
            }
            doc.Blocks.Add(pPago);

            var pPie = new Paragraph { TextAlignment = TextAlignment.Center, FontSize = 8.5, Foreground = Brushes.DimGray };
            pPie.Inlines.Add(new Run("¡Gracias por su compra en DECORLIM!"));
            doc.Blocks.Add(pPie);

            EjecutarImpresion(doc, $"Comprobante_{venta.Secuencial}", mostrarDialogo);
        }

        public static void ImprimirEtiquetasLote(IEnumerable<EtiquetaLoteItem> lote, bool mostrarPrecioMayorista, bool mostrarDialogo = true)
        {
            var doc = new FlowDocument
            {
                PagePadding = new Thickness(16),
                ColumnWidth = 220,
                ColumnGap = 12,
                FontFamily = new FontFamily("Segoe UI")
            };

            foreach (var item in lote)
            {
                for (int c = 0; c < item.Copias; c++)
                {
                    var imgBarcode = new Image
                    {
                        Source = BarcodeService.GenerarImagenCodigoBarras(item.CodigoBarras, 195, 48),
                        Width = 195,
                        Height = 48,
                        Stretch = Stretch.Uniform
                    };

                    var stack = new StackPanel { Margin = new Thickness(4) };
                    stack.Children.Add(new TextBlock
                    {
                        Text = "DECORLIM",
                        FontWeight = FontWeights.Bold,
                        FontSize = 9,
                        HorizontalAlignment = HorizontalAlignment.Center
                    });
                    stack.Children.Add(new TextBlock
                    {
                        Text = $"{item.Nombre} ({item.Presentacion})",
                        FontSize = 8.5,
                        TextWrapping = TextWrapping.Wrap,
                        TextAlignment = TextAlignment.Center,
                        Margin = new Thickness(0, 1, 0, 2)
                    });
                    stack.Children.Add(imgBarcode);

                    string textoPrecio = mostrarPrecioMayorista && item.PrecioMayorista > 0
                        ? $"PVP: ${item.PrecioMinorista:F2}  |  May: ${item.PrecioMayorista:F2}"
                        : $"PVP: ${item.PrecioMinorista:F2}";

                    stack.Children.Add(new TextBlock
                    {
                        Text = textoPrecio,
                        FontWeight = FontWeights.Bold,
                        FontSize = 10,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 2, 0, 0)
                    });

                    var border = new Border
                    {
                        BorderBrush = Brushes.Black,
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(4),
                        Padding = new Thickness(4),
                        Margin = new Thickness(0, 0, 0, 8),
                        Child = stack
                    };

                    doc.Blocks.Add(new BlockUIContainer(border));
                }
            }

            EjecutarImpresion(doc, "Etiquetas_DECORLIM", mostrarDialogo);
        }

        private static TableCell CrearCelda(string texto, bool negrita, TextAlignment align = TextAlignment.Left)
        {
            var p = new Paragraph(new Run(texto))
            {
                Margin = new Thickness(0),
                FontSize = 8.5,
                TextAlignment = align,
                FontWeight = negrita ? FontWeights.Bold : FontWeights.Normal
            };
            return new TableCell(p);
        }

        private static void EjecutarImpresion(FlowDocument doc, string tituloTrabajo, bool mostrarDialogo)
        {
            var printDialog = new PrintDialog();
            if (!mostrarDialogo || printDialog.ShowDialog() == true)
            {
                IDocumentPaginatorSource idpSource = doc;
                printDialog.PrintDocument(idpSource.DocumentPaginator, tituloTrabajo);
            }
        }
    }
}