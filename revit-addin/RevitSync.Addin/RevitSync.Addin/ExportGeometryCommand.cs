using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;

namespace RevitSync.Addin
{
    [Transaction(TransactionMode.ReadOnly)]
    public class ExportGeometryCommand : IExternalCommand
    {
        private static readonly HttpClient http = new HttpClient
        {
            BaseAddress = new Uri("http://localhost:5245/")
        };

        private class GeometryPrimitiveDto
        {
            public string Category { get; set; } = "";
            public double CenterX { get; set; }
            public double CenterY { get; set; }
            public double CenterZ { get; set; }
            public double SizeX { get; set; }
            public double SizeY { get; set; }
            public double SizeZ { get; set; }
        }

        private class GeometrySnapshotDto
        {
            public string ProjectName { get; set; } = "";
            public DateTime TimestampUtc { get; set; }
            public List<GeometryPrimitiveDto> Primitives { get; set; } = new List<GeometryPrimitiveDto>();
        }

        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            UIDocument uidoc = data.Application.ActiveUIDocument;
            Document doc = uidoc?.Document;

            if (doc == null)
            {
                message = "No active document.";
                return Result.Failed;
            }

            // Set ActiveProjectName so poller can use project-specific dequeue
            AppState.ActiveProjectName = doc.Title;

            View view = uidoc.ActiveView;
            string projectName = doc.Title;

            var categoriesToInclude = new[]
            {
                BuiltInCategory.OST_Walls,
                BuiltInCategory.OST_StructuralColumns,
                BuiltInCategory.OST_Floors,
                BuiltInCategory.OST_GenericModel, // DirectShapes created from web
            };

            var primitives = new List<GeometryPrimitiveDto>();

            foreach (BuiltInCategory bic in categoriesToInclude)
            {
                var catName = bic.ToString().Replace("OST_", "");

                var collector = new FilteredElementCollector(doc, view.Id)
                    .OfCategory(bic)
                    .WhereElementIsNotElementType();

                foreach (var element in collector)
                {
                    BoundingBoxXYZ bbox = element.get_BoundingBox(view) ?? element.get_BoundingBox(null);
                    if (bbox == null) continue;

                    XYZ min;
                    XYZ max;
                    if (!TryGetWorldAabb(bbox, out min, out max))
                        continue;

                    if (min == null || max == null) continue;

                    double sizeX = max.X - min.X;
                    double sizeY = max.Y - min.Y;
                    double sizeZ = max.Z - min.Z;

                    if (sizeX <= 0 || sizeY <= 0 || sizeZ <= 0) continue;

                    double centerX = (min.X + max.X) / 2.0;
                    double centerY = (min.Y + max.Y) / 2.0;
                    double centerZ = (min.Z + max.Z) / 2.0;

                    primitives.Add(new GeometryPrimitiveDto
                    {
                        Category = catName,
                        CenterX = centerX,
                        CenterY = centerY,
                        CenterZ = centerZ,
                        SizeX = sizeX,
                        SizeY = sizeY,
                        SizeZ = sizeZ,
                    });
                }
            }

            if (primitives.Count == 0)
            {
                TaskDialog.Show("RevitSync", "No elements found for the selected categories in this view.");
                return Result.Succeeded;
            }

            var snapshot = new GeometrySnapshotDto
            {
                ProjectName = projectName,
                TimestampUtc = DateTime.UtcNow,
                Primitives = primitives,
            };

            string json = JsonConvert.SerializeObject(snapshot);
            var resp = http.PostAsync(
                "api/geometry",
                new StringContent(json, Encoding.UTF8, "application/json")
            ).Result;

            if (!resp.IsSuccessStatusCode)
            {
                message = $"Failed to upload geometry: {resp.StatusCode}";
                return Result.Failed;
            }

            TaskDialog.Show(
                "RevitSync",
                $"Exported {primitives.Count} geometry primitives for project '{projectName}'."
            );

            return Result.Succeeded;
        }

        private static bool TryGetWorldAabb(BoundingBoxXYZ bbox, out XYZ minWorld, out XYZ maxWorld)
        {
            minWorld = null;
            maxWorld = null;

            if (bbox == null || bbox.Min == null || bbox.Max == null)
                return false;

            Transform t = bbox.Transform ?? Transform.Identity;

            double minX = double.PositiveInfinity, minY = double.PositiveInfinity, minZ = double.PositiveInfinity;
            double maxX = double.NegativeInfinity, maxY = double.NegativeInfinity, maxZ = double.NegativeInfinity;

            XYZ bmin = bbox.Min;
            XYZ bmax = bbox.Max;

            double[] xs = { bmin.X, bmax.X };
            double[] ys = { bmin.Y, bmax.Y };
            double[] zs = { bmin.Z, bmax.Z };

            for (int xi = 0; xi < 2; xi++)
            for (int yi = 0; yi < 2; yi++)
            for (int zi = 0; zi < 2; zi++)
            {
                XYZ local = new XYZ(xs[xi], ys[yi], zs[zi]);
                XYZ w = t.OfPoint(local);

                if (w.X < minX) minX = w.X;
                if (w.Y < minY) minY = w.Y;
                if (w.Z < minZ) minZ = w.Z;

                if (w.X > maxX) maxX = w.X;
                if (w.Y > maxY) maxY = w.Y;
                if (w.Z > maxZ) maxZ = w.Z;
            }

            if (double.IsInfinity(minX) || double.IsInfinity(maxX))
                return false;

            minWorld = new XYZ(minX, minY, minZ);
            maxWorld = new XYZ(maxX, maxY, maxZ);
            return true;
        }
    }
}
