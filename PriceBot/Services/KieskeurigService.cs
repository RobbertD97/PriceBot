using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PriceBot.Services
{
    public class KieskeurigService
    {
        private const string BaseUrlKieskeurig = "https://www.kieskeurig.nl/search?q=%24"; // 'Dollar sign' to limit search to one result
        private const string PriceXPathKieskeurig = "//div[@class='product-tile__price']//strong";
        private const string ErrorMessageXPath = "//div[contains(@class, 'error-message')]";

        private readonly ProductService _productService;
        private readonly HtmlWeb _htmlWeb;
        
        public KieskeurigService(ProductService productService, HtmlWeb htmlWeb)
        {
            _productService = productService;
            _htmlWeb = htmlWeb;
        }

        public async Task<double?> GetKieskeurigPrice(string ean)
        {
            var web = new HtmlWeb();
            var url = $"{BaseUrlKieskeurig}{ean}";
            var doc = await web.LoadFromWebAsync(url);

            HtmlNode? priceNode = _productService.GetPriceNode(doc, PriceXPathKieskeurig);
            HtmlNode? errorMessageNode = doc.DocumentNode.SelectSingleNode(ErrorMessageXPath);

            if (priceNode != null && string.IsNullOrEmpty(errorMessageNode.InnerHtml))
            {
                return _productService.GetProductPrice(priceNode);
            }

            return null;
        }
    }
}
