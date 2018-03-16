﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using GDAXSharp.Utilities.Extensions;
using GDAXSharp.Authentication;
using GDAXSharp.HttpClient;
using GDAXSharp.Services.HttpRequest;
using GDAXSharp.Services.Products.Models;
using GDAXSharp.Services.Products.Models.Responses;
using GDAXSharp.Shared;
using GDAXSharp.Utilities;

namespace GDAXSharp.Services.Products
{
    public class ProductsService : AbstractService
    {
        private readonly IQueryBuilder queryBuilder;

        public ProductsService(
            IHttpClient httpClient,
            IHttpRequestMessageService httpRequestMessageService,
            IAuthenticator authenticator,
            IQueryBuilder queryBuilder)
                : base(httpClient, httpRequestMessageService, authenticator)
        {
            this.queryBuilder = queryBuilder;
        }

        public async Task<IEnumerable<Product>> GetAllProductsAsync()
        {
            return await MakeServiceCall<IEnumerable<Product>>(HttpMethod.Get, "/products");
        }

        public async Task<ProductsOrderBookResponse> GetProductOrderBookAsync(
			ProductType productId, 
			ProductLevel productLevel = ProductLevel.One)
        {
            var productsOrderBookJsonResponse = await MakeServiceCall<ProductsOrderBookJsonResponse>(HttpMethod.Get
                , $"/products/{productId.GetEnumMemberValue()}/book/?level={(int) productLevel}").ConfigureAwait(false); 
            var productOrderBookResponse = ConvertProductOrderBookResponse(productsOrderBookJsonResponse, productLevel);

            return productOrderBookResponse;
        }

        public async Task<ProductTicker> GetProductTickerAsync(ProductType productId)
        {
            return await MakeServiceCall<ProductTicker>(HttpMethod.Get, $"/products/{productId.GetEnumMemberValue()}/ticker").ConfigureAwait(false); 
        }

        public async Task<ProductStats> GetProductStatsAsync(ProductType productId)
        {
            return await MakeServiceCall<ProductStats>(HttpMethod.Get, $"/products/{productId.GetEnumMemberValue()}/stats").ConfigureAwait(false);
        }

        public async Task<IList<IList<ProductTrade>>> GetTradesAsync(
            ProductType productId,
            int limit = 100,
            int numberOfPages = 0)
        {
            var httpResponseMessage = await SendHttpRequestMessagePagedAsync<ProductTrade>(HttpMethod.Get, $"/products/{productId.GetEnumMemberValue()}/trades?limit={limit}", numberOfPages: numberOfPages);

            return httpResponseMessage;
        }

        public async Task<IList<Candle>> GetHistoricRatesAsync(
			ProductType productPair, 
			DateTime start, 
			DateTime end, 
			CandleGranularity granularity)
        {
            const int maxPeriods = 300;

            var rc = new List<Candle>();

            DateTime? batchEnd = end;
            DateTime batchStart;

            var maxBatchPeriod = (int)granularity * maxPeriods;

            do
            {
                if (batchEnd == null) {
                    break;
                }

                batchStart = batchEnd.Value.AddSeconds(-maxBatchPeriod);
                if (batchStart < start) batchStart = start;

                rc.AddRange(await GetHistoricRatesAsync(productPair, batchStart, batchEnd.Value, (int)granularity));

                batchEnd = rc.Last()?.Time;
            } while (batchStart > start);

            return rc;
        }

        private async Task<IList<Candle>> GetHistoricRatesAsync(
			ProductType productId, 
			DateTime start, 
			DateTime end, 
			int granularity)
        {
            var isoStart = start.ToString("s");
            var isoEnd = end.ToString("s");

            var queryString = queryBuilder.BuildQuery(
                new KeyValuePair<string, string>("start", isoStart),
                new KeyValuePair<string, string>("end", isoEnd),
                new KeyValuePair<string, string>("granularity", granularity.ToString()));

            return await MakeServiceCall<IList<Candle>>(HttpMethod.Get, $"/products/{productId.GetEnumMemberValue()}/candles" + queryString).ConfigureAwait(false);
        }

        private ProductsOrderBookResponse ConvertProductOrderBookResponse(
            ProductsOrderBookJsonResponse productsOrderBookJsonResponse,
            ProductLevel productLevel)
        {
            var askList = productsOrderBookJsonResponse.Asks.Select(product => product.ToArray()).Select(askArray => new Ask(Convert.ToDecimal(askArray[0], CultureInfo.InvariantCulture), Convert.ToDecimal(askArray[1], CultureInfo.InvariantCulture))
            {
                OrderId = productLevel == ProductLevel.Three
                    ? new Guid(askArray[2])
                    : (Guid?)null,
                NumberOfOrders = productLevel == ProductLevel.Three
                    ? (decimal?)null
                    : Convert.ToDecimal(askArray[2], CultureInfo.InvariantCulture)
            }).ToArray();

            var bidList = productsOrderBookJsonResponse.Bids.Select(product => product.ToArray()).Select(bidArray => new Bid(Convert.ToDecimal(bidArray[0], CultureInfo.InvariantCulture), Convert.ToDecimal(bidArray[1], CultureInfo.InvariantCulture))
            {
                OrderId = productLevel == ProductLevel.Three
                    ? new Guid(bidArray[2])
                    : (Guid?)null,
                NumberOfOrders = productLevel == ProductLevel.Three
                    ? (decimal?)null
                    : Convert.ToDecimal(bidArray[2], CultureInfo.InvariantCulture)
            });

            var productOrderBookResponse = new ProductsOrderBookResponse(productsOrderBookJsonResponse.Sequence, bidList, askList);
            return productOrderBookResponse;
        }
    }
}
