using System.IO;
using System.Windows;
using CodeParser.Export;
using Contracts.Graph;
using CSharpCodeAnalyst.Resources;
using Microsoft.Win32;

namespace CSharpCodeAnalyst.Exports;

/// <summary>
///     Facade for various export formats.
/// </summary>
public static class Export
{
    /// <summary>
    ///     Svg export is special because it is a feature of the
    ///     Msagl graph library.
    /// </summary>
    public static void ToSvg(Action<FileStream>? svgExport)
    {
        if (svgExport is null)
        {
            return;
        }

        try
        {
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "SVG files (*.svg)|*.svg",
                Title = "Export to SVG"
            };

            if (saveFileDialog.ShowDialog() != true)
            {
                return;
            }

            using (var stream = new FileStream(saveFileDialog.FileName, FileMode.Create))
            {
                svgExport(stream);
            }
        }
        catch (Exception ex)
        {
            var message = string.Format(Strings.OperationFailed_Message, ex.Message);
            MessageBox.Show(message, Strings.Error_Title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public static void ToPng(FrameworkElement? canvas)
    {
        if (canvas is null)
        {
            return;
        }

        try
        {
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "PNG files (*.png)|*.png",
                Title = "Export to DGML"
            };

            if (saveFileDialog.ShowDialog() != true)
            {
                return;
            }

            ImageWriter.SaveToPng(canvas, saveFileDialog.FileName);
        }
        catch (Exception ex)
        {
            var message = string.Format(Strings.OperationFailed_Message, ex.Message);
            MessageBox.Show(message, Strings.Error_Title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public static void ToDgml(CodeGraph? exportGraph)
    {
        if (exportGraph is null) return;

        try
        {
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "DGML files (*.dgml)|*.dgml",
                Title = "Export to DGML"
            };

            if (saveFileDialog.ShowDialog() != true)
            {
                return;
            }

            var fileName = saveFileDialog.FileName;


            var exporter = new DgmlExport();
            exporter.Export(fileName, exportGraph);
        }
        catch (Exception ex)
        {
            var message = string.Format(Strings.OperationFailed_Message, ex.Message);
            MessageBox.Show(message, Strings.Error_Title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public static void ToPlantUml(CodeGraph? exportGraph)
    {
        if (exportGraph is null)
        {
            return;
        }

        try
        {
            var exporter = new PlantUmlExport();
            var plantUml = exporter.Export(exportGraph);

            Clipboard.SetText(plantUml);
            MessageBox.Show(Strings.ExportPlantUml_Success, Strings.ExportPlantUml_Title,
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            var message = string.Format(Strings.OperationFailed_Message, ex.Message);
            MessageBox.Show(message, Strings.Error_Title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public static void ToDsi(CodeGraph? codeGraph)
    {
        if (codeGraph is null)
        {
            return;
        }

        try
        {
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "DSI files (*.dsi)|*.dsi",
                Title = "Export to DSI"
            };

            if (saveFileDialog.ShowDialog() != true)
            {
                return;
            }

            var fileName = saveFileDialog.FileName;

            var exporter = new DsiExport();
            exporter.Export(fileName, codeGraph);
        }
        catch (Exception ex)
        {
            var message = string.Format(Strings.OperationFailed_Message, ex.Message);
            MessageBox.Show(message, Strings.Error_Title, MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}