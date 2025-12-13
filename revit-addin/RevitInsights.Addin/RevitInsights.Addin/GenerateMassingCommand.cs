using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RevitInsights.Addin
{
    [Transaction(TransactionMode.Manual)]
    public class GenerateMassingCommand : IExternalCommand
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

            // 1) Determine level to place massing on
            Level level = GetLevelFromView(activeView, doc);
            if (level == null)
            {
                TaskDialog.Show("Revit Insights", "Could not determine a level for the active view.");
                return Result.Failed;
            }

            // 2) Get plan extents from crop box
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

            // 3) Grid spacing and tower footprint (feet – Revit internal units)
            double spacingFeet = 80.0;          // distance between tower centers
            double towerWidthFeet = 40.0;       // X dimension
            double towerDepthFeet = 40.0;       // Y dimension

            // shrink extents a bit so towers stay inside crop
            double minX = min.X + spacingFeet;
            double maxX = max.X - spacingFeet;
            double minY = min.Y + spacingFeet;
            double maxY = max.Y - spacingFeet;

            if (minX >= maxX || minY >= maxY)
            {
                TaskDialog.Show("Revit Insights", "Area too small for the chosen spacing.");
                return Result.Failed;
            }

            // 4) Tower height range (in feet)
            double minHeightFeet = 60.0;   // ~ 5 stories
            double maxHeightFeet = 240.0;  // ~ 20 stories

            var rand = new Random();
            int towerCount = 0;

            using (Transaction tx = new Transaction(doc, "Generate 3D Massing"))
            {
                tx.Start();

                // Loop over a grid inside the crop box
                for (double x = minX; x <= maxX; x += spacingFeet)
                {
                    for (double y = minY; y <= maxY; y += spacingFeet)
                    {
                        double heightFeet = Lerp(minHeightFeet, maxHeightFeet, rand.NextDouble());

                        // Center of tower footprint on plan (at level elevation)
                        double baseZ = level.Elevation;
                        XYZ center = new XYZ(x, y, baseZ);

                        // Build a rectangular profile around the center
                        IList<CurveLoop> profile = CreateRectangleProfile(
                            center,
                            towerWidthFeet,
                            towerDepthFeet
                        );

                        // Extrude up in Z to create a Solid
                        Solid solid = GeometryCreationUtilities.CreateExtrusionGeometry(
                            profile,
                            XYZ.BasisZ,
                            heightFeet
                        );

                        // Create DirectShape in a massing / generic category
                        ElementId catId;
                        try
                        {
                            // Some templates/projects may not expose the Mass category
                            catId = doc.Settings.Categories.get_Item(BuiltInCategory.OST_Mass).Id;
                        }
                        catch
                        {
                            // Fallback to Generic Models if Mass category is unavailable
                            catId = new ElementId(BuiltInCategory.OST_GenericModel);
                        }

                        DirectShape ds = DirectShape.CreateElement(doc, catId);
                        ds.SetShape(new List<GeometryObject> { solid });
                        ds.ApplicationId = "RevitInsights";
                        ds.ApplicationDataId = Guid.NewGuid().ToString();

                        towerCount++;
                    }
                }

                tx.Commit();
            }

            TaskDialog.Show(
                "Revit Insights",
                $"Generated procedural 3D massing on level '{level.Name}'.\nTowers created: {towerCount}");

            return Result.Succeeded;
        }

        private static double Lerp(double a, double b, double t)
        {
            return a + (b - a) * t;
        }

        /// <summary>
        /// Creates a rectangular CurveLoop profile centered at 'center' on the given level elevation.
        /// Width is along X, depth is along Y.
        /// </summary>
        private static IList<CurveLoop> CreateRectangleProfile(XYZ center, double width, double depth)
        {
            double halfW = width / 2.0;
            double halfD = depth / 2.0;

            XYZ p1 = new XYZ(center.X - halfW, center.Y - halfD, center.Z);
            XYZ p2 = new XYZ(center.X + halfW, center.Y - halfD, center.Z);
            XYZ p3 = new XYZ(center.X + halfW, center.Y + halfD, center.Z);
            XYZ p4 = new XYZ(center.X - halfW, center.Y + halfD, center.Z);

            var loop = new CurveLoop();
            loop.Append(Line.CreateBound(p1, p2));
            loop.Append(Line.CreateBound(p2, p3));
            loop.Append(Line.CreateBound(p3, p4));
            loop.Append(Line.CreateBound(p4, p1));

            return new List<CurveLoop> { loop };
        }

        private Level GetLevelFromView(View view, Document doc)
        {
            // Similar logic as in GenerateColumnGridCommand

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
            // Use the view's crop box only
            if (view != null && view.CropBoxActive && view.CropBox != null)
            {
                return view.CropBox;
            }

            return null;
        }
    }
}
