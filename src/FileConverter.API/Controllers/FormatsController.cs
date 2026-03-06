using FileConverter.Application;
using FileConverter.Application.DTOs;
using FileConverter.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace FileConverter.API.Controllers;

[ApiController]
[Route("api/v1/formats")]
public class FormatsController : ControllerBase
{
    [HttpGet]
    public IActionResult GetAllFormats()
    {
        var all = SupportedConversions.GetAll();
        var result = all.Select(kv => new FormatInfoResponse
        {
            Format = kv.Key.ToString().ToLower(),
            Category = SupportedConversions.GetCategory(kv.Key).ToString(),
            TargetFormats = kv.Value.Select(f => f.ToString().ToLower()).ToList()
        }).ToList();

        return Ok(result);
    }

    [HttpGet("{ext}/targets")]
    public IActionResult GetTargets(string ext)
    {
        var format = SupportedConversions.ParseFormat(ext);
        if (format == null) return NotFound(new { error = $"Unknown format: {ext}" });

        var targets = SupportedConversions.GetTargets(format.Value)
            .Select(f => f.ToString().ToLower()).ToList();

        return Ok(new { format = format.Value.ToString().ToLower(), targets });
    }

    [HttpGet("{sourceExt}/options")]
    public IActionResult GetOptions(string sourceExt, [FromQuery] string? target = null)
    {
        var sourceFormat = SupportedConversions.ParseFormat(sourceExt);
        if (sourceFormat == null) return NotFound(new { error = $"Unknown format: {sourceExt}" });

        if (string.IsNullOrEmpty(target))
        {
            // Return options for the first available target
            var firstTarget = SupportedConversions.GetTargets(sourceFormat.Value).FirstOrDefault();
            var options = FormatOptionsProvider.GetOptions(sourceFormat.Value, firstTarget);
            return Ok(options);
        }

        var targetFormat = SupportedConversions.ParseFormat(target);
        if (targetFormat == null) return NotFound(new { error = $"Unknown target format: {target}" });

        return Ok(FormatOptionsProvider.GetOptions(sourceFormat.Value, targetFormat.Value));
    }
}
