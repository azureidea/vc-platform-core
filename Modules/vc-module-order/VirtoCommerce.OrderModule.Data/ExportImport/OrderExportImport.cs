using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using VirtoCommerce.Domain.Inventory.Model.Search;
using VirtoCommerce.InventoryModule.Core.Model;
using VirtoCommerce.InventoryModule.Core.Model.Search;
using VirtoCommerce.OrderModule.Core.Model;
using VirtoCommerce.OrderModule.Core.Model.Search;
using VirtoCommerce.OrderModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.ExportImport;

namespace VirtoCommerce.OrderModule.Data.ExportImport
{
    public sealed class OrderExportImport : IExportSupport, IImportSupport
    {
        private readonly ICustomerOrderSearchService _customerOrderSearchService;
        private readonly ICustomerOrderService _customerOrderService;
        private readonly JsonSerializer _serializer;
        private const int _batchSize = 50;

        public OrderExportImport(ICustomerOrderSearchService customerOrderSearchService, ICustomerOrderService customerOrderService)
        {
            _customerOrderSearchService = customerOrderSearchService;
            _customerOrderService = customerOrderService;
            _serializer = new JsonSerializer
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Ignore
            };
        }

        public async Task ExportAsync(Stream outStream, ExportImportOptions options, Action<ExportImportProgressInfo> progressCallback,
            ICancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var progressInfo = new ExportImportProgressInfo { Description = "The orders are loading" };
            progressCallback(progressInfo);

            using (var sw = new StreamWriter(outStream, Encoding.UTF8))
            using (var writer = new JsonTextWriter(sw))
            {
                writer.WriteStartObject();

                var orders = await _customerOrderSearchService.SearchCustomerOrdersAsync(new CustomerOrderSearchCriteria{ Take = int.MaxValue });
                writer.WritePropertyName("OrderTotalCount");
                writer.WriteValue(orders.TotalCount);

                writer.WritePropertyName("Orders");
                writer.WriteStartArray();

                foreach (var order in orders.Results)
                {
                    _serializer.Serialize(writer, order);
                }

                writer.WriteEndArray();

                writer.WriteEndObject();
                writer.Flush();
            }
        }

        public async Task ImportAsync(Stream inputStream, ExportImportOptions options, Action<ExportImportProgressInfo> progressCallback,
            ICancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var progressInfo = new ExportImportProgressInfo();
            var orderTotalCount = 0;

            using (var streamReader = new StreamReader(inputStream))
            using (var reader = new JsonTextReader(streamReader))
            {
                while (reader.Read())
                {
                    if (reader.TokenType == JsonToken.PropertyName)
                    {
                        if (reader.Value.ToString() == "OrderTotalCount")
                        {
                            orderTotalCount = reader.ReadAsInt32() ?? 0;
                        }
                        else if (reader.Value.ToString() == "Orders")
                        {
                            var orders = new List<CustomerOrder>();
                            var orderCount = 0;
                            while (reader.TokenType != JsonToken.EndArray)
                            {
                                var order = _serializer.Deserialize<CustomerOrder>(reader);
                                orders.Add(order);
                                orderCount++;

                                reader.Read();
                            }

                            for (int i = 0; i < orderCount; i += _batchSize)
                            {
                                await _customerOrderService.SaveChangesAsync(orders.Skip(i).Take(_batchSize).ToArray());

                                if (orderCount > 0)
                                {
                                    progressInfo.Description = $"{ i } of { orderCount } orders imported";
                                }
                                else
                                {
                                    progressInfo.Description = $"{ i } fulfillment centers imported";
                                }
                                progressCallback(progressInfo);
                            }

                        }
                    }
                }
            }
        }
    }
}
