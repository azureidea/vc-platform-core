using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using VirtoCommerce.Platform.Core.Caching;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.StoreModule.Core.Model.Search;
using VirtoCommerce.StoreModule.Core.Services;
using VirtoCommerce.StoreModule.Data.Caching;
using VirtoCommerce.StoreModule.Data.Repositories;

namespace VirtoCommerce.StoreModule.Data.Services
{
    public class StoreSearchService : IStoreSearchService
    {
        private readonly Func<IStoreRepository> _repositoryFactory;
        private readonly IPlatformMemoryCache _platformMemoryCache;

        public StoreSearchService(Func<IStoreRepository> repositoryFactory, IPlatformMemoryCache platformMemoryCache)
        {
            _repositoryFactory = repositoryFactory;
            _platformMemoryCache = platformMemoryCache;
        }

        public async Task<GenericSearchResult<Store>> SearchStoresAsync(StoreSearchCriteria criteria)
        {
            var cacheKey = CacheKey.With(GetType(), "SearchStoresAsync", criteria.GetCacheKey());
            return await _platformMemoryCache.GetOrCreateExclusiveAsync(cacheKey, async (cacheEntry) =>
            {
                cacheEntry.AddExpirationToken(StoreSearchCacheRegion.CreateChangeToken());
                var result = new GenericSearchResult<Store>();
                using (var repository = _repositoryFactory())
                {
                    var query = repository.Stores;
                    if (!string.IsNullOrEmpty(criteria.Keyword))
                    {
                        query = query.Where(x => x.Name.Contains(criteria.Keyword) || x.Id.Contains(criteria.Keyword));
                    }
                    if (!criteria.StoreIds.IsNullOrEmpty())
                    {
                        query = query.Where(x => criteria.StoreIds.Contains(x.Id));
                    }
                    var sortInfos = criteria.SortInfos;
                    if (sortInfos.IsNullOrEmpty())
                    {
                        sortInfos = new[]
                        {
                            new SortInfo
                            {
                                SortColumn = "Name"
                            }
                        };
                    }

                    query = query.OrderBySortInfos(sortInfos);

                    result.TotalCount = await query.CountAsync();
                    var list = await query.Skip(criteria.Skip).Take(criteria.Take).ToArrayAsync();
                    result.Results = list.Select(x => x.ToModel(AbstractTypeFactory<Store>.TryCreateInstance())).ToList();
                }
                return result;
            });
        }
    }
}
