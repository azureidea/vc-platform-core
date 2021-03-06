using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using VirtoCommerce.Platform.Core.Caching;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Exceptions;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.Platform.Data.Model;
using VirtoCommerce.Platform.Data.Repositories;

namespace VirtoCommerce.Platform.Data.Settings
{
    /// <summary>
    /// Provide next functionality to working with settings
    /// - Load setting metainformation from module manifest and database 
    /// - Deep load all settings for entity
    /// - Mass update all entity settings
    /// </summary>
    public class SettingsManager : ISettingsManager, ISettingsRegistrar
    {
        private readonly Func<IPlatformRepository> _repositoryFactory;
        private readonly IMemoryCache _memoryCache;
        private readonly Dictionary<string, SettingDescriptor> _registeredSettings = new Dictionary<string, SettingDescriptor>(StringComparer.OrdinalIgnoreCase);

        public SettingsManager(Func<IPlatformRepository> repositoryFactory, IMemoryCache memoryCache)
        {
            _repositoryFactory = repositoryFactory;
            _memoryCache = memoryCache;
        }


        #region ISettingsRegistrar Members
        public void RegisterSettingsForType(IEnumerable<SettingDescriptor> settings, string typeName)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<SettingDescriptor> GetSettingsForType(string typeName)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<SettingDescriptor> AllRegisteredSettings => _registeredSettings.Values;

        public void RegisterSettings(IEnumerable<SettingDescriptor> settings, string moduleId = null)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }
            foreach (var setting in settings)
            {
                setting.ModuleId = moduleId;
                _registeredSettings[setting.Name] = setting;
            }
        }
        #endregion
        #region ISettingsManager Members

        public virtual async Task<ObjectSettingEntry> GetObjectSettingAsync(string name, string objectType = null, string objectId = null)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }
            return (await GetObjectSettingsAsync(new[] { name }, objectType, objectId)).FirstOrDefault();
        }

        public virtual async Task<IEnumerable<ObjectSettingEntry>> GetObjectSettingsAsync(IEnumerable<string> names, string objectType = null, string objectId = null)
        {
            if (names == null)
            {
                throw new ArgumentNullException(nameof(names));
            }
            var cacheKey = CacheKey.With(GetType(), "GetSettingByNamesAsync", string.Join(";", names));
            var result = await _memoryCache.GetOrCreateExclusiveAsync(cacheKey, async (cacheEntry) =>
            {
                var resultObjectSettings = new List<ObjectSettingEntry>();
                var dbStoredSettings = new List<SettingEntity>();

                //Try to load setting value from DB
                using (var repository = _repositoryFactory())
                {
                    //try to load setting from db
                    dbStoredSettings.AddRange(await repository.GetObjectSettingsAsync(objectType, objectId));
                }

                foreach (var name in names)
                {
                    _registeredSettings.TryGetValue(name, out var settingDescriptor);
                    if (settingDescriptor == null)
                    {
                        throw new PlatformException($"Setting with name {name} not registered");
                    }
                    var objectSetting = new ObjectSettingEntry(settingDescriptor);

                    var dbSetting = dbStoredSettings.FirstOrDefault(x => x.Name.EqualsInvariant(name));
                    if (dbSetting != null)
                    {
                        objectSetting = dbSetting.ToModel(objectSetting);
                    }
                    resultObjectSettings.Add(objectSetting);

                    //Add cache  expiration token for setting
                    cacheEntry.AddExpirationToken(SettingsCacheRegion.CreateChangeToken(objectSetting));
                }
                return resultObjectSettings;
            });
            return result;
        }

        public virtual async Task RemoveObjectSettingsAsync(IEnumerable<ObjectSettingEntry> objectSettings)
        {
            if (objectSettings == null)
            {
                throw new ArgumentNullException(nameof(objectSettings));
            }
            using (var repository = _repositoryFactory())
            {
                foreach (var objectSetting in objectSettings)
                {
                    var dbSetting = repository.Settings.FirstOrDefault(x => x.Name == objectSetting.Name && x.ObjectType == objectSetting.ObjectType && x.ObjectId == objectSetting.ObjectId);
                    if (dbSetting != null)
                    {
                        repository.Remove(dbSetting);
                    }
                }
                await repository.UnitOfWork.CommitAsync();
                ClearCache(objectSettings);
            }
        }

        public virtual async Task SaveObjectSettingsAsync(IEnumerable<ObjectSettingEntry> objectSettings)
        {
            if (objectSettings == null)
            {
                throw new ArgumentNullException(nameof(objectSettings));
            }

            using (var repository = _repositoryFactory())
            {
                var settingNames = objectSettings.Select(x => x.Name).Distinct().ToArray();
                var alreadyExistDbSettings = (await repository.Settings
                    .Include(s => s.SettingValues)
                    .Where(x => settingNames.Contains(x.Name))
                    .ToListAsync());

                foreach (var setting in objectSettings)
                {
                    var modifiedEntity = AbstractTypeFactory<SettingEntity>.TryCreateInstance().FromModel(setting);
                    //we need to convert resulting DB entities to model to use valueObject equals
                    var originalEntity = alreadyExistDbSettings.Where(x => x.Name == setting.Name)
                                                               .FirstOrDefault(x => x.ToModel(AbstractTypeFactory<ObjectSettingEntry>.TryCreateInstance()).Equals(setting));

                    if (originalEntity != null)
                    {
                        modifiedEntity.Patch(originalEntity);
                    }
                    else
                    {
                        repository.Add(modifiedEntity);
                    }
                }

                await repository.UnitOfWork.CommitAsync();
            }

            ClearCache(objectSettings);
        }

        #endregion

        protected virtual void ClearCache(IEnumerable<ObjectSettingEntry> objectSettings)
        {
            //Clear setting from cache
            foreach (var setting in objectSettings)
            {
                SettingsCacheRegion.ExpireSetting(setting);
            }
        }


    }
}
