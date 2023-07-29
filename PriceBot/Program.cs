using System;
using System.Globalization;
using System.Security.Authentication;
using System.Text.Json;
using HtmlAgilityPack;

public partial class Program
{
    static readonly HttpClient client = new();
    static readonly Dictionary<string, Product> lastKnownProducts = new();
    static readonly List<string> PotentiallyOutOfStockProducts = new();
    static readonly HashSet<string> OutOfStockNotifiedUrls = new();


    private const string PricebotWebhookUrl = "https://discord.com/api/webhooks/1128422331950321785/T7iUiECrfnVcJn_h7yR-xcjZY1UlG_oS7XjhojwWQLnK4-sQAK-oKNBEdVQw4L5Ut8LZ";
    private const string OutOfStockWebhookUrl = "https://discord.com/api/webhooks/1133762623670849559/dnGsicqo95ELYIS2FoEzNiRzIPk1AWeK4v74Kz4CAyQHLq-aHP6t7bqC8rPVe9ITJ7t6";

    private const string TitleXPath = "//h1[@id='page_title']";
    private const string PriceXPathBcc = "//section[contains(@class, 'productoffer')]//span[contains(@class, 'priceblock__price--salesprice')]";
    private const string EanXPath = "//tr[th[contains(text(), 'EAN')]]/td";

    private const string BaseUrlKieskeurig = "https://www.kieskeurig.nl/search?q=%24"; // 'Dollar sign' to limit search to one result
    private const string PriceXPathKieskeurig = "//div[@class='product-tile__price']//strong";
    private static readonly string ErrorMessageXPath = "//div[contains(@class, 'error-message')]";

    static async Task Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        var urls = await GetUrlsToTrack();

        while (true)
        {
            foreach (var url in urls)
            {
                await CheckPrice(url);
            }

            await CheckPotentiallyOutOfStockProducts();


            Console.WriteLine("\n");
            await Task.Delay(TimeSpan.FromMinutes(30));
        }
    }

    private static async Task CheckPrice(string url)
    {
        var web = new HtmlWeb();

        try
        {
            var bccHtml = await web.LoadFromWebAsync(url);

            HtmlNode? priceNodeBcc = GetPriceNode(bccHtml, PriceXPathBcc);

            if (priceNodeBcc == null)
            {
                if (!OutOfStockNotifiedUrls.Contains(url))
                {
                    await SendOutOfStockMessage(url);
                    OutOfStockNotifiedUrls.Add(url);
                    PotentiallyOutOfStockProducts.Add(url);
                }
                return;
            }

            var productTitle = GetProductTitle(bccHtml);
            var productEan = GetProductEan(bccHtml);
            var productPrice = GetProductPrice(priceNodeBcc);

            if (productPrice == null)
            {
                return;
            }

            double? kieskeurigPrice = await GetKieskeurigPrice(productEan);

            var product = new Product
            {
                Title = productTitle,
                Price = productPrice,
                Ean = productEan
            };

            await CompareAndLogPriceInfoAsync(url, product, kieskeurigPrice);

            lastKnownProducts[url] = product;
        }
        catch (HttpRequestException ex) when (ex.InnerException is AuthenticationException)
        {
            Console.WriteLine($"SSL connection could not be established. Exception: {ex.InnerException.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
    }

    private static async Task CheckPotentiallyOutOfStockProducts()
    {
        var urlsToRecheck = new List<string>(PotentiallyOutOfStockProducts);

        foreach (var url in urlsToRecheck)
        {
            var web = new HtmlWeb();
            var doc = await web.LoadFromWebAsync(url);
            HtmlNode? priceNodeBcc = GetPriceNode(doc, PriceXPathBcc);

            if (priceNodeBcc != null) // If the price is found, the product is back in stock
            {
                PotentiallyOutOfStockProducts.Remove(url); // Remove from potentially out-of-stock list
            }
        }
    }

    private static HtmlNode? GetPriceNode(HtmlDocument doc, string pricePath)
    {
        // Find the price on the page (this might change based on the structure of the website)
        var priceNode = doc.DocumentNode.SelectSingleNode(pricePath);

        if (priceNode == null || priceNode.InnerText.Contains("Dit product is uit het assortiment"))
        {
            return null;
        }

        // Remove potential unwanted span
        HtmlNode? unwantedSpan = priceNode.SelectSingleNode("./span[@class='sr-only']");
        if (unwantedSpan != null)
        {
            priceNode.RemoveChild(unwantedSpan);
        }

        return priceNode;
    }

    private static string GetProductTitle(HtmlDocument doc)
    {
        var titleNode = doc.DocumentNode.SelectSingleNode(TitleXPath);
        var productTitle = titleNode.InnerText;

        return productTitle;
    }

    private static double? GetProductPrice(HtmlNode priceNode)
    {
        var priceString = priceNode.InnerText.Trim();

        // Remove non-numeric characters except comma and dot.
        priceString = new string(priceString
            .Where(c => char.IsDigit(c) || c == ',' || c == '.')
            .ToArray());

        // Assume that the last comma is the decimal separator and remaining dots are thousands separators.
        // First, replace all dots with nothing.
        priceString = priceString.Replace(".", "");

        // Then replace the last comma with a dot.
        int lastCommaIndex = priceString.LastIndexOf(",");
        if (lastCommaIndex != -1)
        {
            priceString = priceString.Remove(lastCommaIndex, 1).Insert(lastCommaIndex, ".");
        }

        try
        {
            if (double.TryParse(priceString, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out double price))
            {
                return price;
            }

            throw new FormatException($"Unable to parse '{priceString}' as a double");
        }
        catch (FormatException)
        {
            Console.WriteLine($"An error occurred while parsing {priceString} to a double\n");
            return null;
        }
    }

    private static string GetProductEan(HtmlDocument doc)
    {
        var eanNode = doc.DocumentNode.SelectSingleNode(EanXPath);
        var productEan = eanNode.InnerText;

        return productEan;
    }

    private static async Task<double?> GetKieskeurigPrice(string ean)
    {
        var web = new HtmlWeb();
        var url = $"{BaseUrlKieskeurig}{ean}";
        var doc = await web.LoadFromWebAsync(url);

        HtmlNode? priceNode = GetPriceNode(doc, PriceXPathKieskeurig);
        HtmlNode? errorMessageNode = doc.DocumentNode.SelectSingleNode(ErrorMessageXPath);

        if (priceNode != null && string.IsNullOrEmpty(errorMessageNode.InnerHtml))
        {
            return GetProductPrice(priceNode);
        }

        return null;
    }

    private static async Task CompareAndLogPriceInfoAsync(string url, Product product, double? kieskeurigPrice = null)
    {
        if (lastKnownProducts.TryGetValue(url, out Product lastKnownProduct))
        {
            await CheckPriceDropAndNotifyAsync(url, product, kieskeurigPrice, lastKnownProduct);
        }
        else // There is no last known product
        {
            var message = $"Current price of '{product.Title}' is €{product.Price}\n";
            if (kieskeurigPrice.HasValue) { message += $"Lowest price elsewhere: €{kieskeurigPrice.Value}\n"; }
            Console.WriteLine(message);
        }
    }

    private static async Task CheckPriceDropAndNotifyAsync(string url, Product product, double? kieskeurigPrice, Product lastKnownProduct)
    {
        // If the new price is lower than the last known price, send a notification.
        if (product.Price < lastKnownProduct.Price)
        {
            var productNumber = url[^6..];

            string message =
                $"Price of '{product.Title}' has dropped from €{lastKnownProduct.Price} to €{product.Price}!\n" +
                $"URL: {url}\n" +
                $"Internal number: {productNumber}\n";

            if (kieskeurigPrice.HasValue) { message += $"Lowest price elsewhere: €{kieskeurigPrice.Value}"; }
            Console.WriteLine($"{message}");

            await SendDiscordNotification(message, PricebotWebhookUrl);
        }
        else // Last known price is greater than or equal to the current price
        {
            var message = $"Current price of '{product.Title}' is still €{product.Price}\n";
            if (kieskeurigPrice.HasValue) { message += $"Lowest price elsewhere: €{kieskeurigPrice.Value}\n"; }
            Console.WriteLine(message);
        }
    }

    private static async Task SendOutOfStockMessage(string url)
    {
        var productNumber = url[^6..];
        var message = $"Couldn't find price information on the page: " +
            $"{url}\n" +
            $"Internal number: {productNumber}\n";

        Console.WriteLine(message);
        await SendDiscordNotification(message, OutOfStockWebhookUrl);
    }

    private static async Task SendDiscordNotification(string content, string webhookUrl)
    {
        var payload = new
        {
            content = content!
        };

        var serializedPayload = JsonSerializer.Serialize(payload);
        var stringContent = new StringContent(serializedPayload, System.Text.Encoding.UTF8, "application/json");

        await client.PostAsync(webhookUrl, stringContent);
    }

    private static async Task<List<string>> GetUrlsToTrack()
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
