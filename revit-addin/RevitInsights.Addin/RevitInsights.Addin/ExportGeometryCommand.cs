using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;

namespace RevitInsights.Addin
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

            if (doc == null) {
                message = "No active document.";
                return Result.Failed;
            }

            View view = uidoc.ActiveView;
            string projectName = doc.Title;

            // Categories to stream as boxes
            var categoriesToInclude = new[]
            {
                BuiltInCategory.OST_Walls,
                BuiltInCategory.OST_StructuralColumns,
                BuiltInCategory.OST_Floors,
            };

            // Collect elements and build bounding box primitives
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
                    if(bbox == null) continue;

                    XYZ min = bbox.Min;
                    XYZ max = bbox.Max;

                    // Skip degenerate boxes (zero size)
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
                TaskDialog.Show("Revit Insights", "No elements found for the selected categories in this view.");
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
                "Revit Insights",
                $"Exported {primitives.Count} geometry primitives for project '{projectName}'."
            );
            
            return Result.Succeeded;
        }
    }
}
