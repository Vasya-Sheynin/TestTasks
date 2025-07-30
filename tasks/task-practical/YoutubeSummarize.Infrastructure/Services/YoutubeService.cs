using YoutubeExplode;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.ClosedCaptions;

namespace YoutubeSummarize.Application.Interfaces;

public class YoutubeService : IYoutubeService
{
    public async Task<string> ExtractTranscriptAsync(string videoUrl)
    {
        var youtube = new YoutubeClient();
        var videoId = VideoId.Parse(videoUrl);
        var trackManifest = await youtube.Videos.ClosedCaptions.GetManifestAsync(videoId);

        var trackInfo = trackManifest.GetByLanguage("en") ?? trackManifest.Tracks.FirstOrDefault();
        if (trackInfo == null)
            throw new Exception("No captions found for this video.");

        var track = await youtube.Videos.ClosedCaptions.GetAsync(trackInfo);
        return string.Join(" ", track.Captions.Select(c => c.Text));
    }
}
