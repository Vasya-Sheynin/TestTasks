// YoutubeSummarize.Api/Controllers/SummaryController.cs
using Microsoft.AspNetCore.Mvc;
using System.Text;
using YoutubeSummarize.Application.Interfaces;

namespace YoutubeSummarize.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SummaryController : ControllerBase
{
    private readonly IYoutubeService _youtubeService;
    private readonly ISummarizationService _summarizationService;

    public SummaryController(IYoutubeService youtubeService, ISummarizationService summarizationService)
    {
        _youtubeService = youtubeService;
        _summarizationService = summarizationService;
    }

    [HttpGet("{youtubeVideoLink}")]
    public async Task<IActionResult> Get([FromRoute] string youtubeVideoLink)
    {
        try
        {
            var transcript = await _youtubeService.ExtractTranscriptAsync(youtubeVideoLink);
            var summary = await _summarizationService.SummarizeAsync(transcript);
            var bytes = Encoding.UTF8.GetBytes(summary);
            return File(bytes, "text/plain", "summary.txt");
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
