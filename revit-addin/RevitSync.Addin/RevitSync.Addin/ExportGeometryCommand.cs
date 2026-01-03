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
            public string ElementId { get; set; } = ""; // Revit ElementId for selection/manipulation
            public bool IsWebCreated { get; set; } = false; // True if created via web UI
            public double CenterX { get; set; }
            public double CenterY { get; set; }
            public double CenterZ { get; set; }
            public double SizeX { get; set; }
            public double SizeY { get; set; }
            public double SizeZ { get; set; }
            
            // Element properties extracted from Revit
            public Dictionary<string, string> Properties { get; set; } = new Dictionary<string, string>();
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

                    // Check if this is a web-created DirectShape
                    bool isWebCreated = false;
                    var ds = element as DirectShape;
                    if (ds != null && ds.ApplicationId == "RevitSync")
                    {
                        isWebCreated = true;
                    }

                    var properties = ExtractElementProperties(doc, element);

                    primitives.Add(new GeometryPrimitiveDto
                    {
                        Category = catName,
                        ElementId = element.Id.ToString(),
                        IsWebCreated = isWebCreated,
                        CenterX = centerX,
                        CenterY = centerY,
                        CenterZ = centerZ,
                        SizeX = sizeX,
                        SizeY = sizeY,
                        SizeZ = sizeZ,
                        Properties = properties,
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

        /// Extracts key properties from a Revit element.
        private static Dictionary<string, string> ExtractElementProperties(Document doc, Element element)
        {
            var props = new Dictionary<string, string>();

            try
            {
                // 1. Element Name (every element has this)
                props["Name"] = element.Name ?? "";

                // 2. Family and Type for FamilyInstance elements (Columns, Furniture, etc.)
                var fi = element as FamilyInstance;
                if (fi != null && fi.Symbol != null)
                {
                    props["Family"] = fi.Symbol.Family?.Name ?? "";
                    props["Type"] = fi.Symbol.Name ?? "";
                }
                else
                {
                    // For system families (Walls, Floors), get Type from GetTypeId()
                    var typeId = element.GetTypeId();
                    if (typeId != null && typeId != ElementId.InvalidElementId)
                    {
                        var typeElem = doc.GetElement(typeId);
                        if (typeElem != null)
                        {
                            props["Type"] = typeElem.Name ?? "";
                        }
                    }
                }

                // 3. Level - resolve from BuiltInParameter.LEVEL_PARAM or INSTANCE_SCHEDULE_ONLY_LEVEL_PARAM
                var levelParam = element.get_Parameter(BuiltInParameter.LEVEL_PARAM)
                              ?? element.get_Parameter(BuiltInParameter.INSTANCE_SCHEDULE_ONLY_LEVEL_PARAM)
                              ?? element.get_Parameter(BuiltInParameter.FAMILY_LEVEL_PARAM);
                if (levelParam != null && levelParam.HasValue)
                {
                    var levelId = levelParam.AsElementId();
                    if (levelId != null && levelId != ElementId.InvalidElementId)
                    {
                        var level = doc.GetElement(levelId) as Level;
                        if (level != null)
                        {
                            props["Level"] = level.Name;
                        }
                    }
                }

                // 4. Key Built-in Parameters with formatted display values
                AddParameterIfExists(props, element, BuiltInParameter.ALL_MODEL_MARK, "Mark");
                AddParameterIfExists(props, element, BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS, "Comments");
                
                // Geometry parameters (Length, Area, Volume)
                AddParameterIfExists(props, element, BuiltInParameter.CURVE_ELEM_LENGTH, "Length");
                AddParameterIfExists(props, element, BuiltInParameter.HOST_AREA_COMPUTED, "Area");
                AddParameterIfExists(props, element, BuiltInParameter.HOST_VOLUME_COMPUTED, "Volume");
                
                // Wall-specific
                AddParameterIfExists(props, element, BuiltInParameter.WALL_BASE_OFFSET, "Base Offset");
                AddParameterIfExists(props, element, BuiltInParameter.WALL_TOP_OFFSET, "Top Offset");
                AddParameterIfExists(props, element, BuiltInParameter.WALL_USER_HEIGHT_PARAM, "Unconnected Height");
                
                // Column-specific
                AddParameterIfExists(props, element, BuiltInParameter.FAMILY_BASE_LEVEL_OFFSET_PARAM, "Base Offset");
                AddParameterIfExists(props, element, BuiltInParameter.FAMILY_TOP_LEVEL_OFFSET_PARAM, "Top Offset");
                
                // Floor-specific
                AddParameterIfExists(props, element, BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM, "Height Offset");

                // 5. Phase (Created / Demolished)
                AddParameterIfExists(props, element, BuiltInParameter.PHASE_CREATED, "Phase Created");
                AddParameterIfExists(props, element, BuiltInParameter.PHASE_DEMOLISHED, "Phase Demolished");

                // 6. Workset (for workshared projects)
                if (doc.IsWorkshared)
                {
                    var worksetParam = element.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM);
                    if (worksetParam != null && worksetParam.HasValue)
                    {
                        var worksetId = worksetParam.AsInteger();
                        var workset = doc.GetWorksetTable().GetWorkset(new WorksetId(worksetId));
                        if (workset != null)
                        {
                            props["Workset"] = workset.Name;
                        }
                    }
                }
            }
            catch
            {
                // Swallow errors - property extraction should not fail export
            }

            return props;
        }

        /// Helper to add a BuiltInParameter value if it exists and has a value.
        private static void AddParameterIfExists(Dictionary<string, string> props, Element element, BuiltInParameter bip, string displayName)
        {
            try
            {
                var param = element.get_Parameter(bip);
                if (param == null || !param.HasValue) return;

                var value = param.AsValueString();
                
                // Fallback to raw value if AsValueString is empty
                if (string.IsNullOrEmpty(value))
                {
                    switch (param.StorageType)
                    {
                        case StorageType.String:
                            value = param.AsString();
                            break;
                        case StorageType.Integer:
                            value = param.AsInteger().ToString();
                            break;
                        case StorageType.Double:
                            value = param.AsDouble().ToString("F2");
                            break;
                        case StorageType.ElementId:
                            value = param.AsElementId()?.ToString() ?? "";
                            break;
                    }
                }

                if (!string.IsNullOrEmpty(value))
                {
                    props[displayName] = value;
                }
            }
            catch
            {
                // Ignore parameter extraction errors
            }
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
