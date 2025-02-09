// This file is automatically generated by ABP framework to use MVC Controllers from CSharp
using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Http.Client;
using Volo.Abp.Http.Modeling;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Http.Client.ClientProxying;
using LINGYUN.Abp.Dapr.ServiceInvocation;
using LINGYUN.Abp.Dapr;
using System.Linq;
using Volo.Abp;
using System.Collections.Generic;
using Volo.Abp.Reflection;
using System.Net.Http;
using LINGYUN.Abp.Dapr.Client.ClientProxying;

// ReSharper disable once CheckNamespace
namespace LINGYUN.Abp.Dapr.ServiceInvocation.ClientProxies;

[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(ITestAppService), typeof(TestClientProxy))]
public partial class TestClientProxy : DaprClientProxyBase<ITestAppService>, ITestAppService
{
    public virtual async Task<ListResultDto<NameValue>> GetAsync()
    {
        return await RequestAsync<ListResultDto<NameValue>>(nameof(GetAsync));
    }

    public virtual async Task<NameValue> UpdateAsync(int inctement)
    {
        return await RequestAsync<NameValue>(nameof(UpdateAsync), new ClientProxyRequestTypeValue
        {
            { typeof(int), inctement }
        });
    }

    public virtual async Task<TestNeedWrapObject> GetWrapedAsync(string name)
    {
        return await RequestAsync<TestNeedWrapObject>(nameof(GetWrapedAsync), new ClientProxyRequestTypeValue
        {
            { typeof(string), name }
        });
    }
}
