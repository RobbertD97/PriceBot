using System.Text.Json;

namespace PriceBot.Helpers
{
    internal static class JsonHelper
    {
        public static async Task<List<string>> GetUrlsToTrack()
        {
            var filePath = "urls-to-track.json";

            if (File.Exists(filePath))
            {
                var jsonString = await File.ReadAllTextAsync(filePath);
                var urlsToTrack = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(jsonString)!;
                return urlsToTrack["urls"];
            }
            else
            {
                Console.WriteLine($"Warning: {filePath} file not found. No URLs to track.");
                return new List<string>();
            }
        }
    }
}