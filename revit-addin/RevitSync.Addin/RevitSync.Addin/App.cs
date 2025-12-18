using Autodesk.Revit.UI;
using System;
using System.IO;
using System.Reflection;
using System.Windows.Media.Imaging;

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
            string asmDir = Path.GetDirectoryName(asmPath);

            string iconPath = Path.Combine(asmDir, "Icons", "revitsync-icon.png");
            BitmapSource largeIcon = LoadIcon(iconPath, 32);  // 32x32 for large button
            BitmapSource smallIcon = LoadIcon(iconPath, 16);  // 16x16 for small button

            // Button for generate column grid
            var genGridBtnData = new PushButtonData(
                "GenerateColumnGrid",
                "Generate\nColumn Grid",
                asmPath,
                "RevitSync.Addin.GenerateColumnGridCommand"
            )
            {
                ToolTip = "Generate test geometry quickly by placing a grid of structural columns in the active plan view crop.",
                LargeImage = largeIcon,
                Image = smallIcon
            };

            // Export lightweight geometry snapshot
            var exportGeomBtnData = new PushButtonData(
                "ExportGeometry",
                "Export\nGeometry",
                asmPath,
                "RevitSync.Addin.ExportGeometryCommand"
            )
            {
                ToolTip = "Stream a lightweight 3D geometry snapshot (bounding boxes) to the local viewer.",
                LargeImage = largeIcon,
                Image = smallIcon
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

        private static BitmapSource LoadIcon(string path, int size)
        {
            if (!File.Exists(path))
                return null;

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.DecodePixelWidth = size;   // Resize width
            bitmap.DecodePixelHeight = size;  // Resize height
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze(); // Required for cross-thread access

            return bitmap;
        }
    }
}
