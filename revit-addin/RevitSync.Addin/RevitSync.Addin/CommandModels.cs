using System.Collections.Generic;

namespace RevitSync.Addin
{
    public class GeometryCommandDto
    {
        public string ProjectName { get; set; } = "";
        public string CommandId { get; set; } = "";
        public string Type { get; set; } = "ADD_BOXES";
        public List<BoxDto> Boxes { get; set; } = new List<BoxDto>();
    }

    public class BoxDto
    {
        public string Category { get; set; } = "WebBox";
        public double CenterX { get; set; }
        public double CenterY { get; set; }
        public double CenterZ { get; set; }
        public double SizeX { get; set; }
        public double SizeY { get; set; }
        public double SizeZ { get; set; }
    }
}

