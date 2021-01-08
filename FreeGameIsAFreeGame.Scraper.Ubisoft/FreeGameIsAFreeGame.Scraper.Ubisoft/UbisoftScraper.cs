using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
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
        private const int OFFSET = 16;

        string IScraper.Identifier => "UbisoftFree";
        string IScraper.DisplayName => "Ubisoft Store";

        private IBrowsingContext context;
        private ILogger logger;

        public UbisoftScraper()
        {
            context = BrowsingContext.New(Configuration.Default
                .WithDefaultLoader()
                .WithDefaultCookies());

            logger = LogManager.GetLogger(GetType().FullName);
        }

        public async Task<IEnumerable<IDeal>> Scrape(CancellationToken token)
        {
            List<IDeal> deals = new List<IDeal>();

            int start = 0;
            while (true)
            {
                await Task.Delay(1500);

                logger.Info($"Getting items {start} to {start + OFFSET}");
                IHtmlElement pageBody = await GetPageBody(start, token);
                if (token.IsCancellationRequested)
                    return null;

                List<IDeal> parsedDeals = ParsePageBody(pageBody);
                if (parsedDeals.Count == 0)
                {
                    logger.Info("No deals found, most likely the end of the catalog");
                    break;
                }

                foreach (IDeal parsedDeal in parsedDeals)
                {
                    if (parsedDeal.Discount != 100)
                        continue;

                    deals.Add(parsedDeal);
                }

                start += OFFSET;
            }

            return deals;
        }

        private async Task<IHtmlElement> GetPageBody(int start, CancellationToken token)
        {
            logger.Debug("Getting page body from index {start}", start);
            Url url = Url.Create(GetUrl(start));
            DocumentRequest request = DocumentRequest.Get(url);
            IDocument document = await context.OpenAsync(request, token);
            return token.IsCancellationRequested ? null : document.Body;
        }

        private List<IDeal> ParsePageBody(IHtmlElement body)
        {
            List<IDeal> deals = new List<IDeal>();

            IEnumerable<IHtmlListItemElement> listItems = body.QuerySelectorAll<IHtmlListItemElement>("li.grid-tile");
            foreach (IHtmlListItemElement listItem in listItems)
            {
                IHtmlDivElement discountElement = listItem.QuerySelector<IHtmlDivElement>("div.deal-percentage");
                IHtmlDivElement giveawayElement = listItem.QuerySelector<IHtmlDivElement>("div.giveaway");
                IHtmlImageElement imageElement = listItem.QuerySelector<IHtmlImageElement>("img.product_image");
                IHtmlAnchorElement linkElement = listItem.QuerySelector<IHtmlAnchorElement>("a.button");
                IHtmlDivElement titleElement = listItem.QuerySelector<IHtmlDivElement>("div.card-title");
                IHtmlDivElement editionElement = listItem.QuerySelector<IHtmlDivElement>("div.card-subtitle");

                if (discountElement == null && giveawayElement == null)
                    continue;

                try
                {
                    string discount = string.Empty;
                    if (giveawayElement != null)
                    {
                        discount = "100";
                    }
                    else
                    {
                        string discountText = discountElement.Text();
                        discount = Regex.Match(discountText, "\\d+").ToString().Trim();
                    }

                    string img = imageElement.GetAttribute("data-desktop-src");
                    string link = linkElement.Href;
                    string title = $"{titleElement.Text().Trim()}";
                    if (editionElement != null)
                    {
                        title += $" {editionElement.Text().Trim()}";
                    }

                    Deal deal = new Deal()
                    {
                        Discount = int.Parse(discount),
                        Image = img,
                        Title = title,
                        Link = link,
                        Start = null,
                        End = null
                    };

                    deals.Add(deal);
                }
                catch (Exception e)
                {
                    logger.Error(e);
                }
            }

            return deals;
        }

        private string GetUrl(int start)
        {
            return
                $"https://store.ubi.com/us/video-games/?lang=en_US&prefn1=productTypeCategoryRefinementString&prefv1=Video%20Game&sz={OFFSET}&format=ajax&start={start}";
        }
    }
}
