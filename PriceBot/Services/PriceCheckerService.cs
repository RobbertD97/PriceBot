using HtmlAgilityPack;
using PriceBot.Classes;
using System.Security.Authentication;

namespace PriceBot.Services
{
    public class PriceCheckerService
    {
        private readonly ProductService _productService;
        private readonly NotificationService _notificationService;
        private readonly KieskeurigService _kieskeurigService;
        private readonly HtmlWeb _htmlWeb;

        private readonly List<string> PotentiallyOutOfStockProducts = new();
        private readonly HashSet<string> OutOfStockNotifiedUrls = new();
        private readonly Dictionary<string, Product> lastKnownProducts = new();


        public PriceCheckerService(ProductService productService, NotificationService notificationService, KieskeurigService kieskeurigService, HtmlWeb htmlWeb)
        {
            _productService = productService;
            _notificationService = notificationService;
            _kieskeurigService = kieskeurigService;
            _htmlWeb = htmlWeb;
        }

        public async Task CheckAndUpdateProductPriceAsync(string url)
        {
            try
            {
                var bccHtml = await _htmlWeb.LoadFromWebAsync(url);

                var priceNodeBcc = _productService.GetPriceNode(bccHtml);
                if (priceNodeBcc == null)
                {
                    await HandleOutOfStockAsync(url);
                    return;
                }

                var product = CreateProduct(bccHtml, priceNodeBcc);
                if (product == null)
                {
                    return;
                }

                double? kieskeurigPrice = await _kieskeurigService.GetKieskeurigPrice(product.Ean!);

                await HandlePriceComparisonAsync(url, product, kieskeurigPrice);

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

        public async Task RecheckOutOfStockProductsAsync()
        {
            var urlsToRecheck = new List<string>(PotentiallyOutOfStockProducts);

            foreach (var url in urlsToRecheck)
            {
                var doc = await _htmlWeb.LoadFromWebAsync(url);
                HtmlNode? priceNodeBcc = _productService.GetPriceNode(doc);

                // If the price is found, the product is back in stock
                if (priceNodeBcc != null)
                {
                    OutOfStockNotifiedUrls.Remove(url);
                    PotentiallyOutOfStockProducts.Remove(url);
                }
            }
        }

        private async Task HandleOutOfStockAsync(string url)
        {
            // Only send notification when it hasn't been send already
            if (!OutOfStockNotifiedUrls.Contains(url))
            {
                await _notificationService.SendOutOfStockMessage(url);

                OutOfStockNotifiedUrls.Add(url);
                PotentiallyOutOfStockProducts.Add(url);
            }
        }

        private Product? CreateProduct(HtmlDocument bccHtml, HtmlNode priceNodeBcc)
        {
            var productTitle = _productService.GetProductTitle(bccHtml);
            var productPrice = _productService.GetProductPrice(priceNodeBcc);
            var productEan = _productService.GetProductEan(bccHtml);

            if (productPrice == null)
            {
                return null;
            }

            return new Product
            {
                Title = productTitle,
                Price = productPrice,
                Ean = productEan
            };
        }

        private async Task HandlePriceComparisonAsync(string url, Product product, double? kieskeurigPrice = null)
        {
            // Check if there is a last known product for the given URL
            if (lastKnownProducts.TryGetValue(url, out Product lastKnownProduct))
            {
                await NotifyPriceDropAsync(url, product, kieskeurigPrice, lastKnownProduct);
            }
            else // There is no last known product
            {
                PrintProductPriceInfo(product, kieskeurigPrice);
            }
        }

        private async Task NotifyPriceDropAsync(string url, Product product, double? kieskeurigPrice, Product lastKnownProduct)
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

                await _notificationService.SendDiscordNotification(message);
            }
            else // Last known price is greater than or equal to the current price
            {
                var message = $"Current price of '{product.Title}' is still €{product.Price}\n";
                if (kieskeurigPrice.HasValue) { message += $"Lowest price elsewhere: €{kieskeurigPrice.Value}\n"; }
                Console.WriteLine(message);
            }
        }
        private /*async*/ void PrintProductPriceInfo(Product product, double? kieskeurigPrice)
        {
            var message = $"Current price of '{product.Title}' is €{product.Price}\n";
            if (kieskeurigPrice.HasValue) { message += $"Lowest price elsewhere: €{kieskeurigPrice.Value}\n"; }
            Console.WriteLine(message);

            // FOR DEBUGGING PURPOSES
            // await _notificationService.SendDiscordNotification(message);
        }
    }
}
