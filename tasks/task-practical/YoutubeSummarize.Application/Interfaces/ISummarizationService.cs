namespace YoutubeSummarize.Application.Interfaces;

// YoutubeSummarize.Application/Interfaces/ISummarizationService.cs
public interface ISummarizationService
{
    Task<string> SummarizeAsync(string text);
}