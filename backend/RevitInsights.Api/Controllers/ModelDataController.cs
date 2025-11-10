using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RevitInsights.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ModelDataController : ControllerBase
    {
        private readonly AppDb _db;
        public ModelDataController(AppDb db)
        {
            _db = db;
        }

        [HttpPost]
        public async Task<IActionResult> Ingest([FromBody] ModelSummary dto)
        {
            if (dto == null)
                return BadRequest("Invalid payload");

            _db.Summaries.Add(dto);
            await _db.SaveChangesAsync();
            return Ok(new { dto.Id });
        }

        [HttpGet("latest")]
        public async Task<IActionResult> Latest()
        {
            var s = await _db.Summaries
                .Include(x => x.Categories)
                .OrderByDescending(x => x.TimestampUtc)
                .FirstOrDefaultAsync();

            if (s == null)
                return NotFound("No data yet. Export from Revit first.");

            return Ok(s);
        }
    }
}