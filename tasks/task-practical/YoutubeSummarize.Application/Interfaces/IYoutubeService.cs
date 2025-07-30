namespace YoutubeSummarize.Application.Interfaces;

// YoutubeSummarize.Application/Interfaces/IYoutubeService.cs
public interface IYoutubeService
{
    Task<string> ExtractTranscriptAsync(string videoUrl);
}