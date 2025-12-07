using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using System;
using System.Linq;

namespace RevitInsights.Addin
{
    [Transaction(TransactionMode.Manual)]
    public class GenerateColumnGridCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            UIApplication uiapp = data.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc?.Document;

            if (doc == null)
            {
                message = "No active document.";
                return Result.Failed;
            }

            View activeView = uidoc.ActiveView;

            // 1) Determine level to place columns on
            Level level = GetLevelFromView(activeView, doc);
            if (level == null)
            {
                TaskDialog.Show("Revit Insights", "Could not determine a level for the active view.");
                return Result.Failed;
            }

            // 2) Determine plan extents (bounding box) from the view's crop box
            BoundingBoxXYZ bbox = GetPlanBoundingBox(activeView);
            if (bbox == null)
            {
                TaskDialog.Show(
                    "Revit Insights",
                    "Could not determine the plan extents. Make sure the active view has an active crop box.");
                return Result.Failed;
            }

            XYZ min = bbox.Min;
            XYZ max = bbox.Max;

            // 3) Choose spacing (in feet – Revit internal units are feet)
            double spacingFeet = 20.0; // ~6m
            double minX = min.X + spacingFeet;
            double maxX = max.X - spacingFeet;
            double minY = min.Y + spacingFeet;
            double maxY = max.Y - spacingFeet;

            if (minX >= maxX || minY >= maxY)
            {
                TaskDialog.Show("Revit Insights", "Floor area too small for the chosen spacing.");
                return Result.Failed;
            }

            // 4) Find a column family symbol to use
            FamilySymbol columnSymbol = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_StructuralColumns)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .FirstOrDefault();

            if (columnSymbol == null)
            {
                TaskDialog.Show("Revit Insights", "Could not find a structural column family symbol in this project.");
                return Result.Failed;
            }

            int placedCount = 0;

            using (Transaction tx = new Transaction(doc, "Generate Column Grid"))
            {
                tx.Start();

                // Ensure the symbol is active
                if (!columnSymbol.IsActive)
                {
                    columnSymbol.Activate();
                    doc.Regenerate();
                }

                // 5) Generate grid of points and place family instances
                for (double x = minX; x <= maxX; x += spacingFeet)
                {
                    for (double y = minY; y <= maxY; y += spacingFeet)
                    {
                        XYZ point = new XYZ(x, y, level.Elevation);
                        doc.Create.NewFamilyInstance(point, columnSymbol, level, StructuralType.Column);
                        placedCount++;
                    }
                }

                tx.Commit();
            }

            TaskDialog.Show(
                "Revit Insights",
                $"Generated column grid on level '{level.Name}'.\nColumns placed: {placedCount}");

            return Result.Succeeded;
        }

        private Level GetLevelFromView(View view, Document doc)
        {
            // If it's a plan view, get the generated level
            if (view is ViewPlan vp && vp.GenLevel != null)
                return vp.GenLevel;

            // If it's a section, pick nearest level by Z
            if (view is ViewSection vs && vs.Origin != null)
            {
                double elevation = vs.Origin.Z;
                return new FilteredElementCollector(doc)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .OrderBy(l => Math.Abs(l.Elevation - elevation))
                    .FirstOrDefault();
            }

            // Fallback: lowest level
            return new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => l.Elevation)
                .FirstOrDefault();
        }

        private BoundingBoxXYZ GetPlanBoundingBox(View view)
        {
            // Use the view's crop box only. No collectors, no extra filters.
            if (view != null && view.CropBoxActive && view.CropBox != null)
            {
                return view.CropBox;
            }

            return null;
        }
    }
}
