using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;

namespace RevitSync.Addin
{
    public class ApplyCommandHandler : IExternalEventHandler
    {
        // Set by poller thread, read on UI thread in Execute()
        public GeometryCommandDto Pending;

        public void Execute(UIApplication app)
        {
            var cmd = Pending;
            Pending = null;
            if (cmd == null) return;

            var doc = app.ActiveUIDocument?.Document;
            if (doc == null) return;

            try
            {
                if (cmd.Type == "ADD_BOXES")
                {
                    using (var tx = new Transaction(doc, $"RevitSync: {cmd.Type}"))
                    {
                        tx.Start();

                        foreach (var b in cmd.Boxes)
                            CreateBoxDirectShape(doc, cmd, b);

                        tx.Commit();
                    }
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("RevitSync", $"Failed to apply command '{cmd.Type}'.\n{ex.Message}");
            }
        }

        public string GetName() => "RevitSync Apply Command Handler";

        private void CreateBoxDirectShape(Document doc, GeometryCommandDto cmd, BoxDto b)
        {
            // Create DirectShape in Generic Models
            var ds = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));

            // IDs help you later add MOVE/DELETE
            ds.ApplicationId = "RevitSync";
            ds.ApplicationDataId = $"{cmd.CommandId}:{b.Category ?? "WebBox"}";

            // Box extents
            double hx = Math.Abs(b.SizeX) / 2.0;
            double hy = Math.Abs(b.SizeY) / 2.0;
            double hz = Math.Abs(b.SizeZ) / 2.0;

            // avoid degenerate solids
            if (hx <= 1e-6 || hy <= 1e-6 || hz <= 1e-6) return;

            var center = new XYZ(b.CenterX, b.CenterY, b.CenterZ);
            var min = center - new XYZ(hx, hy, hz);
            var max = center + new XYZ(hx, hy, hz);

            // Create a box solid by extruding a rectangle in XY from min.Z to max.Z
            var baseLoop = RectangleLoop(min.X, min.Y, max.X, max.Y, min.Z);
            var solid = GeometryCreationUtilities.CreateExtrusionGeometry(
                new List<CurveLoop> { baseLoop },
                XYZ.BasisZ,
                (max.Z - min.Z)
            );

            ds.SetShape(new List<GeometryObject> { solid });
        }

        private CurveLoop RectangleLoop(double x0, double y0, double x1, double y1, double z)
        {
            var p00 = new XYZ(x0, y0, z);
            var p10 = new XYZ(x1, y0, z);
            var p11 = new XYZ(x1, y1, z);
            var p01 = new XYZ(x0, y1, z);

            var loop = new CurveLoop();
            loop.Append(Line.CreateBound(p00, p10));
            loop.Append(Line.CreateBound(p10, p11));
            loop.Append(Line.CreateBound(p11, p01));
            loop.Append(Line.CreateBound(p01, p00));
            return loop;
        }
    }
}

