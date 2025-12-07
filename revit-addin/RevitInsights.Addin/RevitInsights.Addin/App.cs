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

            panel.AddItem(exportBtnData);

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

            panel.AddItem(genGridBtnData);

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication app) => Result.Succeeded;
    }
}
