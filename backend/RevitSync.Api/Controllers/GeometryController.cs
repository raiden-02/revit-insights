using Microsoft.AspNetCore.Mvc;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace RevitSync.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GeometryController : ControllerBase
    {
        // Lightweight primitive coming from Revit
        public class GeometryPrimitiveDto
        {
            public string Category { get; set; } = "";
            public string ElementId { get; set; } = ""; // Revit ElementId for selection/manipulation
            public bool IsWebCreated { get; set; } = false; // True if created via web UI
            public string Color { get; set; } = "#e5e7eb"; // color per category
            public double CenterX { get; set; }
            public double CenterY { get; set; }
            public double CenterZ { get; set; }
            public double SizeX { get; set; }
            public double SizeY { get; set; }
            public double SizeZ { get; set; }
            
            // Element properties extracted from Revit
            public Dictionary<string, string> Properties { get; set; } = new();
        }

        public class GeometrySnapshotDto
        {
            public string ProjectName { get; set; } = "";
            public DateTime TimestampUtc { get; set; }
            public List<GeometryPrimitiveDto> Primitives { get; set; } = new();
            public List<string> SelectedElementIds { get; set; } = new();
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
            GeometrySnapshotDto? snapshot = null;

            // If a projectName is provided, return latest snapshot for that project.
            if (!string.IsNullOrWhiteSpace(projectName))
            {
                if (!_latestByProject.TryGetValue(projectName, out snapshot))
                    return NotFound("No geometry snapshot for this project yet.");
            }
            else
            {
                // Otherwise return latest snapshot across all projects.
                if (_latestByProject.Count == 0)
                    return NotFound("No geometry snapshots yet.");

                snapshot = _latestByProject.Values
                    .OrderByDescending(s => s.TimestampUtc)
                    .FirstOrDefault();

                if (snapshot == null)
                    return NotFound("No geometry snapshots yet.");
            }

            // Compute ETag from ProjectName, TimestampUtc.Ticks, and Primitives.Count
            var etag = ComputeETag(snapshot);

            // Check If-None-Match header for conditional GET
            var ifNoneMatch = Request.Headers.IfNoneMatch.FirstOrDefault();
            if (!string.IsNullOrEmpty(ifNoneMatch) && ifNoneMatch == etag)
            {
                return StatusCode(304); // Not Modified
            }

            Response.Headers.ETag = etag;
            return Ok(snapshot);
        }

        private static string ComputeETag(GeometrySnapshotDto snapshot)
        {
            // Combine ProjectName, TimestampUtc.Ticks, and Primitives.Count into a hash
            var raw = $"{snapshot.ProjectName}:{snapshot.TimestampUtc.Ticks}:{snapshot.Primitives?.Count ?? 0}";
            
            var hash = raw.GetHashCode();
            
            // ETag must be wrapped in quotes per HTTP spec
            return $"\"{hash:X8}\"";
        }
    }
}

