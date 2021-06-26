using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Io;
using FreeGameIsAFreeGame.Core;
using FreeGameIsAFreeGame.Core.Models;
using NLog;

namespace FreeGameIsAFreeGame.Scraper.Ubisoft
{
    public class UbisoftScraper : IScraper
    {
        private const string URL = "https://store.ubi.com/us/search/?lang=en_US&cgid=free-offers&prefn1=freeOfferProductType&prefv1=Giveaway&categoryslot=true&format=ajax";
        private IBrowsingContext context;
        private ILogger logger;

        string IScraper.Identifier => "UbisoftFree";

        /// <inheritdoc />
        public Task Initialize(CancellationToken token)
        {
            context = BrowsingContext.New(Configuration.Default
                .WithDefaultLoader()
                .WithDefaultCookies());

            logger = LogManager.GetLogger(GetType().FullName);

            return Task.CompletedTask;
        }

        public async Task<IEnumerable<IDeal>> Scrape(CancellationToken token)
        {
            List<IDeal> deals = new List<IDeal>();

            DocumentRequest request = DocumentRequest.Get(Url.Create(URL));
            IDocument document = await context.OpenAsync(request, token);
            token.ThrowIfCancellationRequested();

            IHtmlElement body = document.Body;

            IEnumerable<IHtmlListItemElement> items = body.QuerySelectorAll<IHtmlListItemElement>(".grid-tile");
            foreach (IHtmlListItemElement element in items)
            {
                IHtmlDivElement titleElement = element.QuerySelector<IHtmlDivElement>(".card-title");
                string title = titleElement.TextContent.Trim();

                IHtmlAnchorElement linkElement = element.QuerySelector<IHtmlAnchorElement>(".thumb-link");
                IHtmlImageElement imageElement = element.QuerySelector<IHtmlImageElement>(".product_image");
                IHtmlDivElement priceElement = element.QuerySelector<IHtmlDivElement>(".card-price");
                IHtmlDivElement availabilityElement = element.QuerySelector<IHtmlDivElement>(".product-availability-label");
                string offerStartDate = availabilityElement.GetAttribute("data-freeofferstartdate");
                string offerEndDate = availabilityElement.GetAttribute("data-freeofferenddate");

                bool hasStartDate = DateTime.TryParseExact(offerStartDate, "ddd MMM dd HH:mm:ss Z yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime startDate);
                bool hasEndDate = DateTime.TryParseExact(offerEndDate, "ddd MMM dd HH:mm:ss Z yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime endDate);

                if (!hasStartDate && !hasEndDate)
                {
                    logger.Info($"{title} has no start or end date");
                    continue;
                }

                DateTime now = DateTime.Now;
                if ((hasStartDate && now < startDate) || (hasEndDate && now > endDate))
                {
                    logger.Info($"{title} is not active right now");
                    continue;
                }

                string price = priceElement.TextContent.Trim();
                if (price.ToLower() != "free to play")
                {
                    logger.Info($"{title} is not free");
                    continue;
                }

                deals.Add(new Deal
                {
                    Discount = 100,
                    End = hasEndDate ? endDate : (DateTime?) null,
                    Start = hasStartDate ? startDate : (DateTime?) null,
                    Title = title,
                    Link = $"https://store.ubi.com/{linkElement.GetAttribute("href")}",
                    Image = imageElement.GetAttribute("data-desktop-src")
                });
            }

            return deals;
        }

        /// <inheritdoc />
        public Task Dispose()
        {
            context?.Dispose();
            return Task.CompletedTask;
        }
    }
}
