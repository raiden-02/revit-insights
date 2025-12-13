using Autodesk.Revit.UI;
using System.Reflection;

namespace RevitInsights.Addin
{
    public class App : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication app)
        {
            var panel = app.CreateRibbonPanel("Revit Insights");

            string asmPath = Assembly.GetExecutingAssembly().Location;
            var btn = new PushButtonData(
                "ExportSummary",
                "Export Summary",
                asmPath,
                "RevitInsights.Addin.ExportCommand"
            );

            // Button for export summary
            var exportBtnData = new PushButtonData(
                "ExportSummary",
                "Export Summary",
                asmPath,
                "RevitInsights.Addin.ExportCommand"
            )
            {
                ToolTip = "Export model analytics (categories, counts) to the Revit Insights dashboard."
            };

           

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

           

            // Button for 3D massing generator
            var genMassingBtnData = new PushButtonData(
                "GenerateMassing",
                "Generate 3D Massing",
                asmPath,
                "RevitInsights.Addin.GenerateMassingCommand"
            )
            {
                ToolTip = "Generate a procedural 3D massing skyline inside the view's crop box."
            };

           // Run next web command
            var runWebCmdBtnData = new PushButtonData(
                "RunWebCommand",
                "Run Web Command",
                asmPath,
                "RevitInsights.Addin.RunWebCommand"
            )
            {
                ToolTip = "Fetch and execute the next queued command from the Revit Insights dashboard."
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



            panel.AddItem(exportBtnData);
            panel.AddItem(genGridBtnData);
            panel.AddItem(genMassingBtnData);
            panel.AddItem(runWebCmdBtnData);
            panel.AddItem(exportGeomBtnData);

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication app) => Result.Succeeded;
    }
}
