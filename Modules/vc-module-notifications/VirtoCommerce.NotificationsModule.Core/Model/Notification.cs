using System.Collections.Generic;
using VirtoCommerce.NotificationsModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.NotificationsModule.Core.Model
{
    /// <summary>
    /// A parent class for Notification
    /// </summary>
    public abstract class Notification : AuditableEntity
    {
        /// <summary>
        /// For detecting owner
        /// </summary>
        public TenantIdentity TenantIdentity { get; set; }
        public bool IsActive { get; set; }

        /// <summary>
        /// Type of notifications, like Identifier
        /// </summary>
        private string _type;
        public string Type
        {
            get => !string.IsNullOrEmpty(_type) ? _type : this.GetType().Name;
            set => _type = value;
        }

        /// <summary>
        /// For detecting kind of notifications (email, sms and etc.)
        /// </summary>
        public string Kind { get; set; }
        public IList<NotificationTemplate> Templates { get; set; }

        public virtual NotificationMessage ToMessage(NotificationMessage message, INotificationTemplateRenderer render)
        {
            message.TenantIdentity = new TenantIdentity(message.TenantIdentity?.Id, message.TenantIdentity?.Type);
            message.NotificationType = Type;
            message.NotificationId = Id;

            return message;
        }
    }
}
