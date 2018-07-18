using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.CoreModule.Core.Seo;
using VirtoCommerce.CustomerModule.Core.Events;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.CustomerModule.Data.Model;
using VirtoCommerce.CustomerModule.Data.Repositories;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.DynamicProperties;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.Platform.Data.Infrastructure;

namespace VirtoCommerce.CustomerModule.Data.Services
{
    /// <summary>
    /// Abstract base class for all derived custom members services used IMemberRepository for persistent
    /// </summary>
    public abstract class MemberServiceBase : IMemberService
    {
        private readonly Func<IMemberRepository> _repositoryFactory;
        private readonly IEventPublisher _eventPublisher;
        private readonly IDynamicPropertyService _dynamicPropertyService;
        private readonly ISeoService _seoService;

        protected MemberServiceBase(Func<IMemberRepository> repositoryFactory,IEventPublisher eventPublisher, IDynamicPropertyService dynamicPropertyService, ISeoService seoService)
        {
            _repositoryFactory = repositoryFactory;
            _eventPublisher = eventPublisher;
            _dynamicPropertyService = dynamicPropertyService;
            _seoService = seoService;
        }
        
        #region IMemberService Members

        /// <summary>
        /// Return members by requested ids can be override for load extra data for resulting members
        /// </summary>
        /// <param name="memberIds"></param>
        /// <param name="responseGroup"></param>
        /// <param name="memberTypes"></param>
        /// <returns></returns>
        public virtual async Task<Member[]> GetByIdsAsync(string[] memberIds, string responseGroup = null, string[] memberTypes = null)
        {
            var retVal = new List<Member>();
            using (var repository = _repositoryFactory())
            {
                repository.DisableChangesTracking();
                //There is loading for all corresponding members conceptual model entities types
                //query performance when TPT inheritance used it is too slow, for improve performance we are passing concrete member types in to the repository
                var memberTypeInfos = AbstractTypeFactory<Member>.AllTypeInfos.Where(t => t.MappedType != null);
                if (memberTypes != null)
                {
                    memberTypeInfos = memberTypeInfos.Where(x => memberTypes.Any(mt => x.IsAssignableTo(mt)));
                }
                memberTypes = memberTypeInfos.Select(t => t.MappedType.AssemblyQualifiedName).Distinct().ToArray();

                var dataMembers = await repository.GetMembersByIdsAsync(memberIds, responseGroup, memberTypes);
                foreach (var dataMember in dataMembers)
                {
                    var member = AbstractTypeFactory<Member>.TryCreateInstance(dataMember.MemberType);
                    if (member != null)
                    {
                        dataMember.ToModel(member);
                        retVal.Add(member);
                    }
                }
            }

            var taskDynamicProperty = _dynamicPropertyService.LoadDynamicPropertyValuesAsync(retVal.ToArray<IHasDynamicProperties>());
            var taskSeo = _seoService.LoadSeoForObjectsAsync(retVal.OfType<ISeoSupport>().ToArray());
            await Task.WhenAll(taskDynamicProperty, taskSeo);

            return retVal.ToArray();
        }

        /// <summary>
        /// Create or update members in database
        /// </summary>
        /// <param name="members"></param>
        public virtual async Task SaveChangesAsync(Member[] members)
        {
            var pkMap = new PrimaryKeyResolvingMap();
            var changedEntries = new List<GenericChangedEntry<Member>>();

            using (var repository = _repositoryFactory())
            {
                var existingMemberEntities = await repository.GetMembersByIdsAsync(members.Where(m => !m.IsTransient()).Select(m => m.Id).ToArray());

                foreach (var member in members)
                {
                    var memberEntityType = AbstractTypeFactory<Member>.AllTypeInfos.Where(t => t.MappedType != null && t.IsAssignableTo(member.MemberType)).Select(t => t.MappedType).FirstOrDefault();
                    if (memberEntityType != null)
                    {
                        var dataSourceMember = AbstractTypeFactory<MemberDataEntity>.TryCreateInstance(memberEntityType.Name);
                        if (dataSourceMember != null)
                        {
                            dataSourceMember.FromModel(member, pkMap);

                            var dataTargetMember = existingMemberEntities.FirstOrDefault(m => m.Id == member.Id);
                            if (dataTargetMember != null)
                            {
                                changedEntries.Add(new GenericChangedEntry<Member>(member, dataTargetMember.ToModel(AbstractTypeFactory<Member>.TryCreateInstance(member.MemberType)), EntryState.Modified));
                                dataSourceMember.Patch(dataTargetMember);                               
                            }
                            else
                            {
                                repository.Add(dataSourceMember);
                                changedEntries.Add(new GenericChangedEntry<Member>(member, EntryState.Added));
                            }
                        }
                    }
                }
                //Raise domain events
                await _eventPublisher.Publish(new MemberChangingEvent(changedEntries));
                await repository.UnitOfWork.CommitAsync();
                pkMap.ResolvePrimaryKeys();
                await _eventPublisher.Publish(new MemberChangedEvent(changedEntries));
            }

            //TODO move to handler
            ////Save dynamic properties
            //foreach (var member in members)
            //{
            //    DynamicPropertyService.SaveDynamicPropertyValues(member);
            //}

            //CommerceService.UpsertSeoForObjects(members.OfType<ISeoSupport>().ToArray());
        }

        public virtual async Task DeleteAsync(string[] ids, string[] memberTypes = null)
        { 
            using (var repository = _repositoryFactory())
            {
                var members = await GetByIdsAsync(ids, null, memberTypes);
                if (!members.IsNullOrEmpty())
                {
                    var changedEntries = members.Select(x => new GenericChangedEntry<Member>(x, EntryState.Deleted));
                    await _eventPublisher.Publish(new MemberChangingEvent(changedEntries));

                    await repository.RemoveMembersByIdsAsync(members.Select(m => m.Id).ToArray());
                    await repository.UnitOfWork.CommitAsync();

                    //TODO move to handler
                    //foreach (var member in members)
                    //{
                    //    DynamicPropertyService.DeleteDynamicPropertyValues(member);
                    //    var seoObject = member as ISeoSupport;
                    //    if (seoObject != null)
                    //    {
                    //        CommerceService.DeleteSeoForObject(seoObject);
                    //    }
                    //}
                    await _eventPublisher.Publish(new MemberChangedEvent(changedEntries));
                }
            }
        }
        #endregion

        
    }
}