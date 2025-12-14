using Autodesk.Revit.UI;
using System;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RevitInsights.Addin
{
    public class App : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication app)
        {
            var panel = app.CreateRibbonPanel("Revit Insights");

            string asmPath = Assembly.GetExecutingAssembly().Location;

            // Button for generate column grid
            var genGridBtnData = new PushButtonData(
                "GenerateColumnGrid",
                "Generate Column Grid",
                asmPath,
                "RevitInsights.Addin.GenerateColumnGridCommand"
            )
            {
                ToolTip = "Procedurally generate a column grid on the active level based on plan extents."
            };

            // Export lightweight geometry snapshot
            var exportGeomBtnData = new PushButtonData(
                "ExportGeometry",
                "Export Geometry",
                asmPath,
                "RevitInsights.Addin.ExportGeometryCommand"
            )
            {
                ToolTip = "Export a lightweight 3D geometry snapshot (bounding boxes) to the dashboard."
            };

            panel.AddItem(genGridBtnData);
            var exportBtn = panel.AddItem(exportGeomBtnData) as PushButton;
            if (exportBtn != null)
            {
                exportBtn.LargeImage = CreateExportGeometryIcon(32);
                exportBtn.Image = CreateExportGeometryIcon(16);
            }

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication app) => Result.Succeeded;

        private static ImageSource CreateExportGeometryIcon(int size)
        {
            // Programmatic WPF icon (no external files): dark tile + wireframe cube + arrow.
            // Works on .NET Framework 4.8 / C# 7.3.
            var group = new DrawingGroup();

            var bg = new RectangleGeometry(new System.Windows.Rect(0, 0, size, size), size * 0.18, size * 0.18);
            group.Children.Add(new GeometryDrawing(new SolidColorBrush(Color.FromRgb(2, 6, 23)), null, bg)); // #020617

            var stroke = new Pen(new SolidColorBrush(Color.FromRgb(96, 165, 250)), Math.Max(1.2, size * 0.06)); // blue
            stroke.StartLineCap = PenLineCap.Round;
            stroke.EndLineCap = PenLineCap.Round;
            stroke.LineJoin = PenLineJoin.Round;

            double m = size * 0.22;
            double w = size - 2 * m;
            double d = size * 0.18;

            // Front square
            var front = new RectangleGeometry(new System.Windows.Rect(m, m + d, w, w), size * 0.06, size * 0.06);
            group.Children.Add(new GeometryDrawing(null, stroke, front));

            // Back square (offset up-left)
            var back = new RectangleGeometry(new System.Windows.Rect(m + d, m, w, w), size * 0.06, size * 0.06);
            group.Children.Add(new GeometryDrawing(null, stroke, back));

            // Connecting edges
            group.Children.Add(new GeometryDrawing(null, stroke, new LineGeometry(new System.Windows.Point(m, m + d), new System.Windows.Point(m + d, m))));
            group.Children.Add(new GeometryDrawing(null, stroke, new LineGeometry(new System.Windows.Point(m + w, m + d), new System.Windows.Point(m + d + w, m))));
            group.Children.Add(new GeometryDrawing(null, stroke, new LineGeometry(new System.Windows.Point(m, m + d + w), new System.Windows.Point(m + d, m + w))));
            group.Children.Add(new GeometryDrawing(null, stroke, new LineGeometry(new System.Windows.Point(m + w, m + d + w), new System.Windows.Point(m + d + w, m + w))));

            // "Export" arrow (accent)
            var accent = new Pen(new SolidColorBrush(Color.FromRgb(249, 115, 22)), Math.Max(1.2, size * 0.06)); // orange
            accent.StartLineCap = PenLineCap.Round;
            accent.EndLineCap = PenLineCap.Round;
            accent.LineJoin = PenLineJoin.Round;

            var p1 = new System.Windows.Point(size * 0.62, size * 0.72);
            var p2 = new System.Windows.Point(size * 0.82, size * 0.72);
            group.Children.Add(new GeometryDrawing(null, accent, new LineGeometry(p1, p2)));
            group.Children.Add(new GeometryDrawing(null, accent, new LineGeometry(p2, new System.Windows.Point(size * 0.76, size * 0.66))));
            group.Children.Add(new GeometryDrawing(null, accent, new LineGeometry(p2, new System.Windows.Point(size * 0.76, size * 0.78))));

            group.Freeze();
            var img = new DrawingImage(group);
            img.Freeze();
            return img;
        }
    }
}
