using System.Text.Json;

namespace PriceBot.Services;

public class NotificationService
{
    private const string PricebotWebhookUrl = "https://discord.com/api/webhooks/1128422331950321785/T7iUiECrfnVcJn_h7yR-xcjZY1UlG_oS7XjhojwWQLnK4-sQAK-oKNBEdVQw4L5Ut8LZ";
    private const string OutOfStockWebhookUrl = "https://discord.com/api/webhooks/1133762623670849559/dnGsicqo95ELYIS2FoEzNiRzIPk1AWeK4v74Kz4CAyQHLq-aHP6t7bqC8rPVe9ITJ7t6";

    private readonly HttpClient _client;

    public NotificationService(HttpClient client)
    {
        _client = client;
    }

    public async Task SendDiscordNotification(string content, string webhookUrl = PricebotWebhookUrl)
    {
        var payload = new
        {
            content = content!
        };

        var serializedPayload = JsonSerializer.Serialize(payload);
        var stringContent = new StringContent(serializedPayload, System.Text.Encoding.UTF8, "application/json");

        await _client.PostAsync(webhookUrl, stringContent);
    }

    public async Task SendOutOfStockMessage(string url)
    {
        var productNumber = url[^6..];
        var message = $"Couldn't find price information on the page: " +
            $"{url}\n" +
            $"Internal number: {productNumber}\n";

        Console.WriteLine(message);
        await SendDiscordNotification(message, OutOfStockWebhookUrl);
    }
}