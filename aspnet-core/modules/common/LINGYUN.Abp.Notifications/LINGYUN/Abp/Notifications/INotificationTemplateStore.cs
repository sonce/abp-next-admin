﻿using System.Threading;
using System.Threading.Tasks;

namespace LINGYUN.Abp.Notifications;

public interface INotificationTemplateStore
{
    Task<string> GetOrNullAsync(string templateName, string culture = null, CancellationToken cancellationToken = default);
}