﻿using LINGYUN.Abp.Notifications;
using LY.MicroService.RealtimeMessage.BackgroundJobs;
using LY.MicroService.RealtimeMessage.MultiTenancy;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.Json;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TextTemplating;
using Volo.Abp.Uow;

namespace LY.MicroService.RealtimeMessage.EventBus.Distributed
{
    /// <summary>
    /// 订阅通知发布事件,统一发布消息
    /// </summary>
    /// <remarks>
    /// 作用在于SignalR客户端只会与一台服务器建立连接,
    /// 只有启用了SignlR服务端的才能真正将消息发布到客户端
    /// </remarks>
    public class NotificationEventHandler :
        IDistributedEventHandler<NotificationEto<NotificationData>>,
        IDistributedEventHandler<NotificationEto<NotificationTemplate>>,
        ITransientDependency
    {
        /// <summary>
        /// Reference to <see cref="ILogger<DefaultNotificationDispatcher>"/>.
        /// </summary>
        public ILogger<NotificationEventHandler> Logger { get; set; }
        /// <summary>
        /// Reference to <see cref="AbpNotificationsPublishOptions"/>.
        /// </summary>
        protected AbpNotificationsPublishOptions Options { get; }
        /// <summary>
        /// Reference to <see cref="ICurrentTenant"/>.
        /// </summary>
        protected ICurrentTenant CurrentTenant { get; }
        /// <summary>
        /// Reference to <see cref="ITenantConfigurationCache"/>.
        /// </summary>
        protected ITenantConfigurationCache TenantConfigurationCache { get; }
        /// <summary>
        /// Reference to <see cref="IJsonSerializer"/>.
        /// </summary>
        protected IJsonSerializer JsonSerializer { get; }
        /// <summary>
        /// Reference to <see cref="IBackgroundJobManager"/>.
        /// </summary>
        protected IBackgroundJobManager BackgroundJobManager { get; }
        /// <summary>
        /// Reference to <see cref="ITemplateRenderer"/>.
        /// </summary>
        protected ITemplateRenderer TemplateRenderer { get; }
        /// <summary>
        /// Reference to <see cref="INotificationStore"/>.
        /// </summary>
        protected INotificationStore NotificationStore { get; }
        /// <summary>
        /// Reference to <see cref="IStringLocalizerFactory"/>.
        /// </summary>
        protected IStringLocalizerFactory StringLocalizerFactory { get; }
        /// <summary>
        /// Reference to <see cref="INotificationDefinitionManager"/>.
        /// </summary>
        protected INotificationDefinitionManager NotificationDefinitionManager { get; }
        /// <summary>
        /// Reference to <see cref="INotificationSubscriptionManager"/>.
        /// </summary>
        protected INotificationSubscriptionManager NotificationSubscriptionManager { get; }
        /// <summary>
        /// Reference to <see cref="INotificationPublishProviderManager"/>.
        /// </summary>
        protected INotificationPublishProviderManager NotificationPublishProviderManager { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="NotificationEventHandler"/> class.
        /// </summary>
        public NotificationEventHandler(
            ICurrentTenant currentTenant,
            ITenantConfigurationCache tenantConfigurationCache,
            IJsonSerializer jsonSerializer,
            ITemplateRenderer templateRenderer,
            IBackgroundJobManager backgroundJobManager,
            IStringLocalizerFactory stringLocalizerFactory,
            IOptions<AbpNotificationsPublishOptions> options,
            INotificationStore notificationStore,
            INotificationDefinitionManager notificationDefinitionManager,
            INotificationSubscriptionManager notificationSubscriptionManager,
            INotificationPublishProviderManager notificationPublishProviderManager)
        {
            Options = options.Value;
            TenantConfigurationCache = tenantConfigurationCache;
            CurrentTenant = currentTenant;
            JsonSerializer = jsonSerializer;
            TemplateRenderer = templateRenderer;
            BackgroundJobManager = backgroundJobManager;
            StringLocalizerFactory = stringLocalizerFactory;
            NotificationStore = notificationStore;
            NotificationDefinitionManager = notificationDefinitionManager;
            NotificationSubscriptionManager = notificationSubscriptionManager;
            NotificationPublishProviderManager = notificationPublishProviderManager;

            Logger = NullLogger<NotificationEventHandler>.Instance;
        }

        [UnitOfWork]
        public async virtual Task HandleEventAsync(NotificationEto<NotificationTemplate> eventData)
        {
            var notification = await NotificationDefinitionManager.GetOrNullAsync(eventData.Name);
            if (notification == null)
            {
                return;
            }

            if (notification.NotificationType == NotificationType.System)
            {
                var allActiveTenants = await TenantConfigurationCache.GetTenantsAsync();

                foreach (var activeTenant in allActiveTenants)
                {
                    await SendToTenantAsync(activeTenant.Id, notification, eventData);
                }
            }
            else
            {
                await SendToTenantAsync(eventData.TenantId, notification, eventData);
            }
        }

        protected async virtual Task SendToTenantAsync(
            Guid? tenantId, 
            NotificationDefinition notification,
            NotificationEto<NotificationTemplate> eventData)
        {
            using (CurrentTenant.Change(tenantId))
            {
                var notificationInfo = new NotificationInfo
                {
                    Name = notification.Name,
                    TenantId = tenantId,
                    Severity = eventData.Severity,
                    Type = notification.NotificationType,
                    CreationTime = eventData.CreationTime,
                    Lifetime = notification.NotificationLifetime,
                };
                notificationInfo.SetId(eventData.Id);

                var title = notification.DisplayName.Localize(StringLocalizerFactory);

                var message = await TemplateRenderer.RenderAsync(
                    templateName: eventData.Data.Name,
                    model: eventData.Data.ExtraProperties,
                    cultureName: eventData.Data.Culture,
                    globalContext: new Dictionary<string, object>
                    {
                        { "$notification", notification.Name },
                        { "$formUser", eventData.Data.FormUser },
                        { "$notificationId", eventData.Id },
                        { "$title", title.ToString() },
                        { "$creationTime", eventData.CreationTime.ToString("yyyy-MM-dd HH:mm:ss") },
                    });

                var notificationData = new NotificationData();
                notificationData.WriteStandardData(
                    title: title,
                    message: message,
                    createTime: eventData.CreationTime,
                    formUser: eventData.Data.FormUser);
                notificationData.ExtraProperties.AddIfNotContains(eventData.Data.ExtraProperties);

                notificationInfo.Data = notificationData;

                Logger.LogDebug($"Persistent notification {notificationInfo.Name}");

                // 持久化通知
                await NotificationStore.InsertNotificationAsync(notificationInfo);

                var providers = Enumerable.Reverse(NotificationPublishProviderManager.Providers);

                // 过滤用户指定提供者
                if (eventData.UseProviders.Any())
                {
                    providers = providers.Where(p => eventData.UseProviders.Contains(p.Name));
                }
                else if (notification.Providers.Any())
                {
                    providers = providers.Where(p => notification.Providers.Contains(p.Name));
                }

                await PublishFromProvidersAsync(providers, eventData.Users, notificationInfo);
            }
        }

        [UnitOfWork]
        public async virtual Task HandleEventAsync(NotificationEto<NotificationData> eventData)
        {
            var notification = await NotificationDefinitionManager.GetOrNullAsync(eventData.Name);
            if (notification == null)
            {
                return;
            }

            if (notification.NotificationType == NotificationType.System)
            {
                var allActiveTenants = await TenantConfigurationCache.GetTenantsAsync();

                foreach (var activeTenant in allActiveTenants)
                {
                    await SendToTenantAsync(activeTenant.Id, notification, eventData);
                }
            }
            else
            {
                await SendToTenantAsync(eventData.TenantId, notification, eventData);
            }
        }

        protected async virtual Task SendToTenantAsync(
            Guid? tenantId,
            NotificationDefinition notification,
            NotificationEto<NotificationData> eventData)
        {
            using (CurrentTenant.Change(tenantId))
            {
                var notificationInfo = new NotificationInfo
                {
                    Name = notification.Name,
                    CreationTime = eventData.CreationTime,
                    Data = eventData.Data,
                    Severity = eventData.Severity,
                    Lifetime = notification.NotificationLifetime,
                    TenantId = tenantId,
                    Type = notification.NotificationType
                };
                notificationInfo.SetId(eventData.Id);

                // TODO: 可以做成一个接口来序列化消息
                notificationInfo.Data = NotificationDataConverter.Convert(notificationInfo.Data);

                Logger.LogDebug($"Persistent notification {notificationInfo.Name}");

                // 持久化通知
                await NotificationStore.InsertNotificationAsync(notificationInfo);

                var providers = Enumerable.Reverse(NotificationPublishProviderManager.Providers);

                // 过滤用户指定提供者
                if (eventData.UseProviders.Any())
                {
                    providers = providers.Where(p => eventData.UseProviders.Contains(p.Name));
                }
                else if (notification.Providers.Any())
                {
                    providers = providers.Where(p => notification.Providers.Contains(p.Name));
                }

                await PublishFromProvidersAsync(providers, eventData.Users, notificationInfo);
            }
        }

        /// <summary>
        /// 指定提供者发布通知
        /// </summary>
        /// <param name="providers">提供者列表</param>
        /// <param name="notificationInfo">通知信息</param>
        /// <returns></returns>
        protected async Task PublishFromProvidersAsync(
            IEnumerable<INotificationPublishProvider> providers,
            IEnumerable<UserIdentifier> users,
            NotificationInfo notificationInfo)
        {
            // 检查是够已订阅消息
            Logger.LogDebug($"Gets a list of user subscriptions {notificationInfo.Name}");

            // 获取用户订阅列表
            var userSubscriptions = await NotificationSubscriptionManager
                    .GetUsersSubscriptionsAsync(notificationInfo.TenantId, notificationInfo.Name, users);

            users = userSubscriptions.Select(us => new UserIdentifier(us.UserId, us.UserName));

            if (users.Any())
            {
                // 持久化用户通知
                Logger.LogDebug($"Persistent user notifications {notificationInfo.Name}");
                await NotificationStore
                    .InsertUserNotificationsAsync(
                        notificationInfo,
                        users.Select(u => u.UserId));

                // 2020-11-02 fix bug, 多个发送提供者处于同一个工作单元之下,不能把删除用户订阅写入到单个通知提供者完成事件中
                // 而且为了确保一致性,删除订阅移动到发布通知之前
                if (notificationInfo.Lifetime == NotificationLifetime.OnlyOne)
                {
                    // 一次性通知在发送完成后就取消用户订阅
                    await NotificationStore
                        .DeleteUserSubscriptionAsync(
                            notificationInfo.TenantId,
                            users,
                            notificationInfo.Name);
                }

                // 发布通知
                foreach (var provider in providers)
                {
                    await PublishAsync(provider, notificationInfo, users);
                }
            }
        }
        /// <summary>
        /// 发布通知
        /// </summary>
        /// <param name="provider">通知发布者</param>
        /// <param name="notificationInfo">通知信息</param>
        /// <param name="subscriptionUserIdentifiers">订阅用户列表</param>
        /// <returns></returns>
        protected async Task PublishAsync(
            INotificationPublishProvider provider,
            NotificationInfo notificationInfo,
            IEnumerable<UserIdentifier> subscriptionUserIdentifiers)
        {
            try
            {
                Logger.LogDebug($"Sending notification with provider {provider.Name}");
                var notifacationDataMapping = Options.NotificationDataMappings
                        .GetMapItemOrDefault(provider.Name, notificationInfo.Name);
                if (notifacationDataMapping != null)
                {
                    notificationInfo.Data = notifacationDataMapping.MappingFunc(notificationInfo.Data);
                }
                // 发布
                await provider.PublishAsync(notificationInfo, subscriptionUserIdentifiers);

                Logger.LogDebug($"Send notification {notificationInfo.Name} with provider {provider.Name} was successful");
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Send notification error with provider {provider.Name}");
                Logger.LogWarning($"Error message:{ex.Message}");

                Logger.LogTrace(ex, $"Send notification error with provider { provider.Name}");

                Logger.LogDebug($"Send notification error, notification {notificationInfo.Name} entry queue");
                // 发送失败的消息进入后台队列
                await BackgroundJobManager.EnqueueAsync(
                    new NotificationPublishJobArgs(
                        notificationInfo.GetId(),
                        provider.GetType().AssemblyQualifiedName,
                        subscriptionUserIdentifiers.ToList(),
                        notificationInfo.TenantId));
            }
        }
    }
}
