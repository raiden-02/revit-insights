using Microsoft.AspNetCore.Mvc;
using System.Collections.Concurrent;

namespace RevitSync.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CommandsController : ControllerBase
    {
        // Command coming from the web UI
        public class GeometryCommandDto
        {
            public string ProjectName { get; set; } = "";
            public string CommandId { get; set; } = Guid.NewGuid().ToString("N");
            public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

            // e.g. "ADD_BOXES"
            public string Type { get; set; } = "ADD_BOXES";

            public List<BoxDto> Boxes { get; set; } = new();
        }

        // Box in REVIT WORLD COORDINATES.
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

        // Per-project queue
        private static readonly ConcurrentDictionary<string, ConcurrentQueue<GeometryCommandDto>> _queues =
            new(StringComparer.OrdinalIgnoreCase);

        [HttpPost]
        public IActionResult Enqueue([FromBody] GeometryCommandDto cmd)
        {
            if (cmd == null) return BadRequest("Invalid payload.");
            if (string.IsNullOrWhiteSpace(cmd.ProjectName)) return BadRequest("ProjectName is required.");
            if (string.IsNullOrWhiteSpace(cmd.Type)) return BadRequest("Type is required.");
            if (cmd.Boxes == null || cmd.Boxes.Count == 0) return BadRequest("Boxes is required.");

            cmd.CreatedUtc = DateTime.UtcNow;

            var q = _queues.GetOrAdd(cmd.ProjectName, _ => new ConcurrentQueue<GeometryCommandDto>());
            q.Enqueue(cmd);

            return Ok(new { cmd.CommandId });
        }

        // Revit polls this. If projectName is omitted, it will dequeue from any queue.
        [HttpGet("next")]
        public IActionResult Dequeue([FromQuery] string? projectName = null)
        {
            if (!string.IsNullOrWhiteSpace(projectName))
            {
                if (!_queues.TryGetValue(projectName, out var q)) return NoContent();
                if (!q.TryDequeue(out var cmd)) return NoContent();
                return Ok(cmd);
            }

            // No project specified: try any queue 
            foreach (var kv in _queues)
            {
                var q = kv.Value;
                if (q.TryDequeue(out var cmd))
                    return Ok(cmd);
            }

            return NoContent();
        }
    }
}

