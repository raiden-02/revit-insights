using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;

namespace RevitSync.Addin
{
    public static class GeometryExporter
    {
        private static readonly HttpClient _http = new HttpClient
        {
            BaseAddress = new Uri("http://localhost:5245/"),
            Timeout = TimeSpan.FromSeconds(5)
        };

        public class GeometryPrimitiveDto
        {
            public string Category { get; set; } = "";
            public string ElementId { get; set; } = "";
            public bool IsWebCreated { get; set; } = false;
            public string Color { get; set; } = "#e5e7eb"; // Hex color per category
            public double CenterX { get; set; }
            public double CenterY { get; set; }
            public double CenterZ { get; set; }
            public double SizeX { get; set; }
            public double SizeY { get; set; }
            public double SizeZ { get; set; }
            public Dictionary<string, string> Properties { get; set; } = new Dictionary<string, string>();
        }

        private static readonly Dictionary<BuiltInCategory, string> CategoryColors = new Dictionary<BuiltInCategory, string>
        {
            { BuiltInCategory.OST_Walls, "#4ade80" },              // Green - walls
            { BuiltInCategory.OST_Roofs, "#f97316" },              // Orange - roofs
            { BuiltInCategory.OST_Floors, "#94a3b8" },             // Slate gray - floors
            { BuiltInCategory.OST_StructuralColumns, "#60a5fa" },  // Blue - columns
            { BuiltInCategory.OST_StructuralFraming, "#a78bfa" },  // Purple - beams/framing
            { BuiltInCategory.OST_StructuralFoundation, "#78716c" }, // Stone gray - foundations
            { BuiltInCategory.OST_Windows, "#22d3ee" },            // Cyan - windows
            { BuiltInCategory.OST_Doors, "#fbbf24" },              // Amber - doors
            { BuiltInCategory.OST_CurtainWallPanels, "#67e8f9" },  // Light cyan - curtain panels
            { BuiltInCategory.OST_CurtainWallMullions, "#38bdf8" }, // Sky blue - curtain mullions/frames
            { BuiltInCategory.OST_Stairs, "#fb923c" },             // Light orange - stairs
            { BuiltInCategory.OST_Ramps, "#fdba74" },              // Peach - ramps
            { BuiltInCategory.OST_GenericModel, "#e879f9" },       // Fuchsia - generic/web-created
        };

        public class GeometrySnapshotDto
        {
            public string ProjectName { get; set; } = "";
            public DateTime TimestampUtc { get; set; }
            public List<GeometryPrimitiveDto> Primitives { get; set; } = new List<GeometryPrimitiveDto>();
            public List<string> SelectedElementIds { get; set; } = new List<string>();
        }

        public class ExportResult
        {
            public bool Success { get; set; }
            public int PrimitiveCount { get; set; }
            public string ErrorMessage { get; set; }
        }

       
        // Export geometry from the document and POST to backend.
        public static ExportResult Export(Document doc, View view, bool showNoElementsAsSuccess = true, UIDocument uidoc = null)
        {
            if (doc == null)
            {
                return new ExportResult { Success = false, ErrorMessage = "No document" };
            }

            AppState.ActiveProjectName = doc.Title;
            string projectName = doc.Title;

            // Get current selection from Revit (for selection sync)
            var selectedElementIds = new List<string>();
            if (uidoc != null)
            {
                try
                {
                    var selection = uidoc.Selection.GetElementIds();
                    selectedElementIds = selection.Select(id => id.ToString()).ToList();
                }
                catch
                {
                    // Ignore selection errors
                }
            }

            // Categories for building massing visualization
            var categoriesToInclude = new[]
            {
                BuiltInCategory.OST_Walls,              // Exterior & interior walls (incl. curtain wall hosts)
                BuiltInCategory.OST_Roofs,              // Roof surfaces
                BuiltInCategory.OST_Floors,             // Floor slabs
                BuiltInCategory.OST_StructuralColumns,  // Vertical structural elements
                BuiltInCategory.OST_StructuralFraming,  // Beams, braces, trusses
                BuiltInCategory.OST_StructuralFoundation, // Footings, piles, slabs on grade
                BuiltInCategory.OST_Windows,            // Window openings
                BuiltInCategory.OST_Doors,              // Door openings
                BuiltInCategory.OST_CurtainWallPanels,  // Glass/curtain wall panels
                BuiltInCategory.OST_CurtainWallMullions, // Curtain wall frames/grids
                BuiltInCategory.OST_Stairs,             // Stair elements
                BuiltInCategory.OST_Ramps,              // Ramp elements
                BuiltInCategory.OST_GenericModel,       // Generic/custom elements (includes web-created)
            };

            var primitives = new List<GeometryPrimitiveDto>();

            foreach (BuiltInCategory bic in categoriesToInclude)
            {
                var catName = bic.ToString().Replace("OST_", "");

                FilteredElementCollector collector;
                if (view != null)
                {
                    collector = new FilteredElementCollector(doc, view.Id)
                        .OfCategory(bic)
                        .WhereElementIsNotElementType();
                }
                else
                {
                    collector = new FilteredElementCollector(doc)
                        .OfCategory(bic)
                        .WhereElementIsNotElementType();
                }

                string categoryColor = CategoryColors.ContainsKey(bic) ? CategoryColors[bic] : "#e5e7eb";

                foreach (var element in collector)
                {
                    BoundingBoxXYZ bbox = null;
                    if (view != null)
                        bbox = element.get_BoundingBox(view);
                    if (bbox == null)
                        bbox = element.get_BoundingBox(null);
                    if (bbox == null) continue;

                    XYZ min, max;
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
                        Color = categoryColor,
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
                return new ExportResult 
                { 
                    Success = showNoElementsAsSuccess, 
                    PrimitiveCount = 0,
                    ErrorMessage = showNoElementsAsSuccess ? null : "No elements found"
                };
            }

            var snapshot = new GeometrySnapshotDto
            {
                ProjectName = projectName,
                TimestampUtc = DateTime.UtcNow,
                Primitives = primitives,
                SelectedElementIds = selectedElementIds,
            };

            try
            {
                string json = JsonConvert.SerializeObject(snapshot);
                var resp = _http.PostAsync(
                    "api/geometry",
                    new StringContent(json, Encoding.UTF8, "application/json")
                ).Result;

                if (!resp.IsSuccessStatusCode)
                {
                    return new ExportResult 
                    { 
                        Success = false, 
                        ErrorMessage = $"HTTP {resp.StatusCode}" 
                    };
                }

                return new ExportResult { Success = true, PrimitiveCount = primitives.Count };
            }
            catch (Exception ex)
            {
                return new ExportResult { Success = false, ErrorMessage = ex.Message };
            }
        }

        private static Dictionary<string, string> ExtractElementProperties(Document doc, Element element)
        {
            var props = new Dictionary<string, string>();

            try
            {
                props["Name"] = element.Name ?? "";

                var fi = element as FamilyInstance;
                if (fi != null && fi.Symbol != null)
                {
                    props["Family"] = fi.Symbol.Family?.Name ?? "";
                    props["Type"] = fi.Symbol.Name ?? "";
                }
                else
                {
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

                AddParameterIfExists(props, element, BuiltInParameter.ALL_MODEL_MARK, "Mark");
                AddParameterIfExists(props, element, BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS, "Comments");
                AddParameterIfExists(props, element, BuiltInParameter.CURVE_ELEM_LENGTH, "Length");
                AddParameterIfExists(props, element, BuiltInParameter.HOST_AREA_COMPUTED, "Area");
                AddParameterIfExists(props, element, BuiltInParameter.HOST_VOLUME_COMPUTED, "Volume");
                AddParameterIfExists(props, element, BuiltInParameter.WALL_BASE_OFFSET, "Base Offset");
                AddParameterIfExists(props, element, BuiltInParameter.WALL_TOP_OFFSET, "Top Offset");
                AddParameterIfExists(props, element, BuiltInParameter.WALL_USER_HEIGHT_PARAM, "Unconnected Height");
                AddParameterIfExists(props, element, BuiltInParameter.FAMILY_BASE_LEVEL_OFFSET_PARAM, "Base Offset");
                AddParameterIfExists(props, element, BuiltInParameter.FAMILY_TOP_LEVEL_OFFSET_PARAM, "Top Offset");
                AddParameterIfExists(props, element, BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM, "Height Offset");
                AddParameterIfExists(props, element, BuiltInParameter.PHASE_CREATED, "Phase Created");
                AddParameterIfExists(props, element, BuiltInParameter.PHASE_DEMOLISHED, "Phase Demolished");

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
            }

            return props;
        }

        private static void AddParameterIfExists(Dictionary<string, string> props, Element element, BuiltInParameter bip, string displayName)
        {
            try
            {
                var param = element.get_Parameter(bip);
                if (param == null || !param.HasValue) return;

                var value = param.AsValueString();

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
