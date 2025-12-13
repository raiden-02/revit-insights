using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;

namespace RevitInsights.Addin
{
    [Transaction(TransactionMode.ReadOnly)]
    public class RunWebCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string message, Autodesk.Revit.DB.ElementSet elements)
        {
            TaskDialog.Show("Revit Insights", "Run Web Command is not implemented yet.");
            return Result.Succeeded;
        }
    }
}


