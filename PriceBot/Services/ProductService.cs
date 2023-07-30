using HtmlAgilityPack;
using System.Globalization;
using System.Security.Authentication;
using PriceBot.Classes;

namespace PriceBot.Services
{
    public class ProductService
    {
        private readonly HtmlWeb _web = new();

        private const string TitleXPath = "//h1[@id='page_title']";
        private const string PriceXPathBcc = "//section[contains(@class, 'productoffer')]//span[contains(@class, 'priceblock__price--salesprice')]";
        private const string EanXPath = "//tr[th[contains(text(), 'EAN')]]/td";

        public HtmlNode? GetPriceNode(HtmlDocument doc, string pricePath = PriceXPathBcc)
        {
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

        public string GetProductTitle(HtmlDocument doc)
        {
            var titleNode = doc.DocumentNode.SelectSingleNode(TitleXPath);
            var productTitle = titleNode.InnerText;

            return productTitle;
        }

        public double? GetProductPrice(HtmlNode priceNode)
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

        public string GetProductEan(HtmlDocument doc)
        {
            var eanNode = doc.DocumentNode.SelectSingleNode(EanXPath);
            var productEan = eanNode.InnerText;

            return productEan;
        }

    }
}
