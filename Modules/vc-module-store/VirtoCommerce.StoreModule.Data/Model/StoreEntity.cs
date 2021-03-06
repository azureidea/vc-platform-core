using System;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.StoreModule.Core.Model;

namespace VirtoCommerce.StoreModule.Data.Model
{
    public class StoreEntity : AuditableEntity
    {
        public StoreEntity()
        {
            Languages = new NullCollection<StoreLanguageEntity>();
            Currencies = new NullCollection<StoreCurrencyEntity>();
            PaymentMethods = new NullCollection<StorePaymentMethodEntity>();
            ShippingMethods = new NullCollection<StoreShippingMethodEntity>();
            TaxProviders = new NullCollection<StoreTaxProviderEntity>();
            TrustedGroups = new NullCollection<StoreTrustedGroupEntity>();
            FulfillmentCenters = new NullCollection<StoreFulfillmentCenterEntity>();
        }

        [Required]
        [StringLength(128)]
        public string Name { get; set; }

        [StringLength(256)]
        public string Description { get; set; }

        [StringLength(256)]
        public string Url { get; set; }

        public int StoreState { get; set; }

        [StringLength(128)]
        public string TimeZone { get; set; }

        [StringLength(128)]
        public string Country { get; set; }

        [StringLength(128)]
        public string Region { get; set; }

        [StringLength(128)]
        public string DefaultLanguage { get; set; }

        [StringLength(64)]
        public string DefaultCurrency { get; set; }

        [StringLength(128)]
        [Required]
        public string Catalog { get; set; }

        public int CreditCardSavePolicy { get; set; }

        [StringLength(128)]
        public string SecureUrl { get; set; }

        [StringLength(128)]
        public string Email { get; set; }

        [StringLength(128)]
        public string AdminEmail { get; set; }

        public bool DisplayOutOfStock { get; set; }

        [StringLength(128)]
        public string FulfillmentCenterId { get; set; }
        [StringLength(128)]
        public string ReturnsFulfillmentCenterId { get; set; }

        #region Navigation Properties

        public virtual ObservableCollection<StoreLanguageEntity> Languages { get; set; }

        public virtual ObservableCollection<StoreCurrencyEntity> Currencies { get; set; }
        public virtual ObservableCollection<StoreTrustedGroupEntity> TrustedGroups { get; set; }

        public virtual ObservableCollection<StorePaymentMethodEntity> PaymentMethods { get; set; }
        public virtual ObservableCollection<StoreShippingMethodEntity> ShippingMethods { get; set; }
        public virtual ObservableCollection<StoreTaxProviderEntity> TaxProviders { get; set; }

        public virtual ObservableCollection<StoreFulfillmentCenterEntity> FulfillmentCenters { get; set; }
        #endregion

        public virtual Store ToModel(Store store)
        {
            if (store == null)
                throw new ArgumentNullException(nameof(store));

            store.Id = Id;
            store.AdminEmail = AdminEmail;
            store.Catalog = Catalog;
            store.Country = Country;
            store.CreatedBy = CreatedBy;
            store.CreatedDate = CreatedDate;
            store.DefaultCurrency = DefaultCurrency;
            store.DefaultLanguage = DefaultLanguage;
            store.Description = Description;
            store.DisplayOutOfStock = DisplayOutOfStock;
            store.Email = Email;
            store.ModifiedBy = ModifiedBy;
            store.ModifiedDate = ModifiedDate;
            store.Name = Name;
            store.Region = Region;
            store.SecureUrl = SecureUrl;
            store.TimeZone = TimeZone;
            store.Url = Url;
            store.MainFulfillmentCenterId = FulfillmentCenterId;
            store.MainReturnsFulfillmentCenterId = ReturnsFulfillmentCenterId;

            store.StoreState = EnumUtility.SafeParse<StoreState>(StoreState.ToString(), Core.Model.StoreState.Open);
            store.Languages = Languages.Select(x => x.LanguageCode).ToList();
            store.Currencies = Currencies.Select(x => x.CurrencyCode).ToList();
            store.TrustedGroups = TrustedGroups.Select(x => x.GroupName).ToList();
            store.AdditionalFulfillmentCenterIds = FulfillmentCenters.Where(x => x.Type == FulfillmentCenterType.Main).Select(x => x.FulfillmentCenterId).ToList();
            store.ReturnsFulfillmentCenterIds = FulfillmentCenters.Where(x => x.Type == FulfillmentCenterType.Returns).Select(x => x.FulfillmentCenterId).ToList();
            return store;
        }

        public virtual StoreEntity FromModel(Store store, PrimaryKeyResolvingMap pkMap)
        {
            if (store == null)
                throw new ArgumentNullException(nameof(store));

            pkMap.AddPair(store, this);

            Id = store.Id;
            AdminEmail = store.AdminEmail;
            Catalog = store.Catalog;
            Country = store.Country;
            CreatedBy = store.CreatedBy;
            CreatedDate = store.CreatedDate;
            DefaultCurrency = store.DefaultCurrency;
            DefaultLanguage = store.DefaultLanguage;
            Description = store.Description;
            DisplayOutOfStock = store.DisplayOutOfStock;
            Email = store.Email;
            ModifiedBy = store.ModifiedBy;
            ModifiedDate = store.ModifiedDate;
            Name = store.Name;
            Region = store.Region;
            SecureUrl = store.SecureUrl;
            TimeZone = store.TimeZone;
            Url = store.Url;
            StoreState = (int)store.StoreState;

            if (store.DefaultCurrency != null)
            {
                DefaultCurrency = store.DefaultCurrency.ToString();
            }
            if (store.MainFulfillmentCenterId != null)
            {
                FulfillmentCenterId = store.MainFulfillmentCenterId;
            }
            if (store.MainReturnsFulfillmentCenterId != null)
            {
                ReturnsFulfillmentCenterId = store.MainReturnsFulfillmentCenterId;
            }
            if (store.Languages != null)
            {
                Languages = new ObservableCollection<StoreLanguageEntity>(store.Languages.Select(x => new StoreLanguageEntity
                {
                    LanguageCode = x
                }));
            }

            if (store.Currencies != null)
            {
                Currencies = new ObservableCollection<StoreCurrencyEntity>(store.Currencies.Select(x => new StoreCurrencyEntity
                {
                    CurrencyCode = x.ToString()
                }));
            }

            if (store.TrustedGroups != null)
            {
                TrustedGroups = new ObservableCollection<StoreTrustedGroupEntity>(store.TrustedGroups.Select(x => new StoreTrustedGroupEntity
                {
                    GroupName = x
                }));
            }

            if (store.ShippingMethods != null)
            {
                ShippingMethods = new ObservableCollection<StoreShippingMethodEntity>(store.ShippingMethods.Select(x => AbstractTypeFactory<StoreShippingMethodEntity>.TryCreateInstance().FromModel(x, pkMap)));
            }
            if (store.PaymentMethods != null)
            {
                PaymentMethods = new ObservableCollection<StorePaymentMethodEntity>(store.PaymentMethods.Select(x => AbstractTypeFactory<StorePaymentMethodEntity>.TryCreateInstance().FromModel(x, pkMap)));
            }
            if (store.TaxProviders != null)
            {
                TaxProviders = new ObservableCollection<StoreTaxProviderEntity>(store.TaxProviders.Select(x => AbstractTypeFactory<StoreTaxProviderEntity>.TryCreateInstance().FromModel(x, pkMap)));
            }

            FulfillmentCenters = new ObservableCollection<StoreFulfillmentCenterEntity>();
            if (store.AdditionalFulfillmentCenterIds != null)
            {

                FulfillmentCenters.AddRange(store.AdditionalFulfillmentCenterIds.Select(fc => new StoreFulfillmentCenterEntity
                {
                    FulfillmentCenterId = fc,
                    Name = fc,
                    StoreId = store.Id,
                    Type = FulfillmentCenterType.Main
                }));
            }
            if (store.ReturnsFulfillmentCenterIds != null)
            {
                FulfillmentCenters.AddRange(store.ReturnsFulfillmentCenterIds.Select(fc => new StoreFulfillmentCenterEntity
                {
                    FulfillmentCenterId = fc,
                    Name = fc,
                    StoreId = store.Id,
                    Type = FulfillmentCenterType.Returns
                }));
            }

            return this;
        }

        public virtual void Patch(StoreEntity target)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            target.AdminEmail = AdminEmail;
            target.Catalog = Catalog;
            target.Country = Country;
            target.DefaultCurrency = DefaultCurrency;
            target.DefaultLanguage = DefaultLanguage;
            target.Description = Description;
            target.DisplayOutOfStock = DisplayOutOfStock;
            target.Email = Email;
            target.ModifiedBy = ModifiedBy;
            target.ModifiedDate = ModifiedDate;
            target.Name = Name;
            target.Region = Region;
            target.SecureUrl = SecureUrl;
            target.TimeZone = TimeZone;
            target.Url = Url;
            target.StoreState = (int)StoreState;
            target.FulfillmentCenterId = FulfillmentCenterId;
            target.ReturnsFulfillmentCenterId = ReturnsFulfillmentCenterId;

            if (!Languages.IsNullCollection())
            {
                var languageComparer = AnonymousComparer.Create((StoreLanguageEntity x) => x.LanguageCode);
                Languages.Patch(target.Languages, languageComparer,
                                      (sourceLang, targetLang) => targetLang.LanguageCode = sourceLang.LanguageCode);
            }
            if (!Currencies.IsNullCollection())
            {
                var currencyComparer = AnonymousComparer.Create((StoreCurrencyEntity x) => x.CurrencyCode);
                Currencies.Patch(target.Currencies, currencyComparer,
                                      (sourceCurrency, targetCurrency) => targetCurrency.CurrencyCode = sourceCurrency.CurrencyCode);
            }
            if (!TrustedGroups.IsNullCollection())
            {
                var trustedGroupComparer = AnonymousComparer.Create((StoreTrustedGroupEntity x) => x.GroupName);
                TrustedGroups.Patch(target.TrustedGroups, trustedGroupComparer,
                                      (sourceGroup, targetGroup) => sourceGroup.GroupName = targetGroup.GroupName);
            }

            if (!PaymentMethods.IsNullCollection())
            {
                var paymentComparer = AnonymousComparer.Create((StorePaymentMethodEntity x) => x.Code);
                PaymentMethods.Patch(target.PaymentMethods, paymentComparer,
                                      (sourceMethod, targetMethod) => sourceMethod.Patch(targetMethod));
            }
            if (!ShippingMethods.IsNullCollection())
            {
                var shippingComparer = AnonymousComparer.Create((StoreShippingMethodEntity x) => x.Code);
                ShippingMethods.Patch(target.ShippingMethods, shippingComparer,
                                      (sourceMethod, targetMethod) => sourceMethod.Patch(targetMethod));
            }
            if (!TaxProviders.IsNullCollection())
            {
                var shippingComparer = AnonymousComparer.Create((StoreTaxProviderEntity x) => x.Code);
                TaxProviders.Patch(target.TaxProviders, shippingComparer,
                                      (sourceProvider, targetProvider) => sourceProvider.Patch(targetProvider));
            }
            if (!FulfillmentCenters.IsNullCollection())
            {
                var fulfillmentCenterComparer = AnonymousComparer.Create((StoreFulfillmentCenterEntity fc) => $"{fc.FulfillmentCenterId}-{fc.Type}");
                FulfillmentCenters.Patch(target.FulfillmentCenters, fulfillmentCenterComparer,
                                      (sourceFulfillmentCenter, targetFulfillmentCenter) => sourceFulfillmentCenter.Patch(targetFulfillmentCenter));
            }
        }
    }
}
