using Microsoft.Extensions.DependencyInjection;
using PriceBot.Services;
using PriceBot.Helpers;
using HtmlAgilityPack;

namespace PriceBot
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var serviceProvider = new ServiceCollection()
                .AddTransient<ProductService>()
                .AddTransient<KieskeurigService>()
                .AddTransient<NotificationService>()
                .AddTransient<PriceCheckerService>()
                .AddHttpClient()
                .AddSingleton<HtmlWeb>()
                .BuildServiceProvider();

            var priceCheckerService = serviceProvider.GetRequiredService<PriceCheckerService>();

            Console.OutputEncoding = System.Text.Encoding.UTF8;

            var urls = await JsonHelper.GetUrlsToTrack();

            while (true)
            {
                foreach (var url in urls)
                {
                    await priceCheckerService.CheckAndUpdateProductPriceAsync(url);
                }

                await priceCheckerService.RecheckOutOfStockProductsAsync();

                Console.WriteLine("\n");
                await Task.Delay(TimeSpan.FromMinutes(1));
            }
        }
    }
}
