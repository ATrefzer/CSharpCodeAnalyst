using System.Diagnostics;
using System.IO;
using System.Windows;
using CodeGraph.Export;
using CSharpCodeAnalyst.Common;
using CSharpCodeAnalyst.Resources;

namespace CSharpCodeAnalyst.Export;

/// <summary>
///     Facade for various export formats.
/// </summary>
public class Exporter
{
    private readonly IUserNotification _ui;

    public Exporter(IUserNotification ui)
    {
        _ui = ui;
    }

    /// <summary>
    ///     Svg export is special because it is a feature of the
    ///     Msagl graph library.
    /// </summary>
    public void ToSvg(Action<FileStream>? svgExport)
    {
        if (svgExport is null)
        {
            return;
        }

        try
        {
            var fileName = _ui.ShowSaveFileDialog("SVG files (*.svg)|*.svg", "Export to SVG");
            if (string.IsNullOrEmpty(fileName))
            {
                return;
            }

            using var stream = new FileStream(fileName, FileMode.Create);
            svgExport(stream);
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    public void ToBitmapClipboard(FrameworkElement? canvas)
    {
        if (canvas is null)
        {
            return;
        }

        try
        {
            ImageWriter.CopyToClipboard(canvas);
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void ShowError(Exception ex)
    {
        Trace.TraceError(ex.ToString());
        var message = string.Format(Strings.OperationFailed_Message, ex.Message);
        _ui.ShowError(message);
    }

    public void ToPng(FrameworkElement? canvas)
    {
        if (canvas is null)
        {
            return;
        }

        try
        {
            var fileName = _ui.ShowSaveFileDialog("PNG files (*.png)|*.png", "Export to PNG");
            if (string.IsNullOrEmpty(fileName))
            {
                return;
            }

            ImageWriter.SaveToPng(canvas, fileName);
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    public void ToDgml(CodeGraph.Graph.CodeGraph? exportGraph)
    {
        if (exportGraph is null) return;

        try
        {
            var fileName = _ui.ShowSaveFileDialog("DGML files (*.dgml)|*.dgml", "Export to DGML");
            if (string.IsNullOrEmpty(fileName))
            {
                return;
            }

            DgmlExport.Export(fileName, exportGraph);
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    public void ToPlantUml(CodeGraph.Graph.CodeGraph? exportGraph)
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
            _ui.ShowSuccess(Strings.ExportPlantUml_Success);
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    public void ToDsi(CodeGraph.Graph.CodeGraph? codeGraph)
    {
        if (codeGraph is null)
        {
            return;
        }

        try
        {
            var fileName = _ui.ShowSaveFileDialog("DSI files (*.dsi)|*.dsi", "Export to DSI");
            if (string.IsNullOrEmpty(fileName))
            {
                return;
            }

            DsiExport.Export(fileName, codeGraph);
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private static void RunDsiViewer(string filePath)
    {
        var executablePath = @"ExternalApplications\\DsmSuite.DsmViewer.View.exe";

        using var process = new Process();
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            Arguments = filePath,
            UseShellExecute = false,
            RedirectStandardOutput = false,
            CreateNoWindow = true
        };

        process.StartInfo = startInfo;
        process.Start();
    }

    public void ToPlainText(CodeGraph.Graph.CodeGraph? graph)
    {
        if (graph is null)
        {
            return;
        }

        try
        {
            var fileName = _ui.ShowSaveFileDialog("TXT files (*.txt)|*.txt", "Export to TXT");
            if (string.IsNullOrEmpty(fileName))
            {
                return;
            }

            CodeGraphSerializer.SerializeToFile(graph, fileName);
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }
}