using Microsoft.AspNetCore.Mvc;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace RevitInsights.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GeometryController : ControllerBase
    {
        // Lightweight primitive coming from Revit
        public class GeometryPrimitiveDto
        {
            public string Category { get; set; } = "";
            public double CenterX { get; set; }
            public double CenterY { get; set; }
            public double CenterZ { get; set; }
            public double SizeX { get; set; }
            public double SizeY { get; set; }
            public double SizeZ { get; set; }
        }

        public class GeometrySnapshotDto
        {
            public string ProjectName { get; set; } = "";
            public DateTime TimestampUtc { get; set; }
            public List<GeometryPrimitiveDto> Primitives { get; set; } = new();
        }

        // In-memory store: latest snapshot per project
        private static readonly ConcurrentDictionary<string, GeometrySnapshotDto> _latestByProject =
            new(StringComparer.OrdinalIgnoreCase);

        [HttpPost]
        public IActionResult Ingest([FromBody] GeometrySnapshotDto dto)
        {
            if (string.IsNullOrEmpty(dto.ProjectName))
                return BadRequest("Project name is required");
            
            dto.TimestampUtc = DateTime.UtcNow;
            _latestByProject[dto.ProjectName] = dto;
            return Ok();
        }

        [HttpGet("latest")]
        public IActionResult Latest([FromQuery] string? projectName = null)
        {
            // If a projectName is provided, return latest snapshot for that project.
            if (!string.IsNullOrWhiteSpace(projectName))
            {
                if (_latestByProject.TryGetValue(projectName, out var snapshot))
                    return Ok(snapshot);

                return NotFound("No geometry snapshot for this project yet.");
            }

            // Otherwise return latest snapshot across all projects.
            if (_latestByProject.Count == 0)
                return NotFound("No geometry snapshots yet.");

            var latest = _latestByProject.Values
                .OrderByDescending(s => s.TimestampUtc)
                .FirstOrDefault();

            return latest == null ? NotFound("No geometry snapshots yet.") : Ok(latest);
        }
    }
}

