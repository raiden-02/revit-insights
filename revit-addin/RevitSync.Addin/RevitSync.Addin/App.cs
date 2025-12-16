using Autodesk.Revit.UI;
using System.Reflection;

namespace RevitSync.Addin
{
    public class App : IExternalApplication
    {
        private static ApplyCommandHandler _handler;
        private static ExternalEvent _externalEvent;
        private static CommandPoller _poller;

        public Result OnStartup(UIControlledApplication app)
        {
            var panel = app.CreateRibbonPanel("RevitSync");

            string asmPath = Assembly.GetExecutingAssembly().Location;

            // Button for generate column grid
            var genGridBtnData = new PushButtonData(
                "GenerateColumnGrid",
                "Generate Column Grid",
                asmPath,
                "RevitSync.Addin.GenerateColumnGridCommand"
            )
            {
                ToolTip = "Generate test geometry quickly by placing a grid of structural columns in the active plan view crop."
            };

            // Export lightweight geometry snapshot
            var exportGeomBtnData = new PushButtonData(
                "ExportGeometry",
                "Export Geometry",
                asmPath,
                "RevitSync.Addin.ExportGeometryCommand"
            )
            {
                ToolTip = "Stream a lightweight 3D geometry snapshot (bounding boxes) to the local viewer."
            };

            panel.AddItem(genGridBtnData);
            panel.AddItem(exportGeomBtnData);

            // Two-way sync: poll web -> apply in Revit via ExternalEvent
            _handler = new ApplyCommandHandler();
            _externalEvent = ExternalEvent.Create(_handler);

            _poller = new CommandPoller(cmd =>
            {
                // store pending and raise
                _handler.Pending = cmd;
                _externalEvent.Raise();
            });
            _poller.Start();

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication app)
        {
            try { _poller?.Dispose(); } catch { }
            _poller = null;
            _externalEvent = null;
            _handler = null;
            return Result.Succeeded;
        }
    }
}
