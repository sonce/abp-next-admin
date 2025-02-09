using LINGYUN.Abp.Cli.ServiceProxying;
using LINGYUN.Abp.Cli.ServiceProxying.CSharp;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Cli.Args;
using Volo.Abp.Cli.Commands;
using Volo.Abp.DependencyInjection;

using VoloGenerateProxyArgs = Volo.Abp.Cli.ServiceProxying.GenerateProxyArgs;

namespace LINGYUN.Abp.Cli.Commands;

public class GenerateProxyCommand : IConsoleCommand, ITransientDependency
{
    public const string Name = "generate-proxy";

    protected string CommandName => Name;

    protected IServiceScopeFactory ServiceScopeFactory { get; }

    public GenerateProxyCommand(
        IServiceScopeFactory serviceScopeFactory)
    {
        ServiceScopeFactory = serviceScopeFactory;
    }

    public async Task ExecuteAsync(CommandLineArgs commandLineArgs)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var serviceProxyGenerator = scope.ServiceProvider.GetRequiredService<CSharpServiceProxyGenerator>();

            await serviceProxyGenerator.GenerateProxyAsync(BuildArgs(commandLineArgs));
        }
    }

    private VoloGenerateProxyArgs BuildArgs(CommandLineArgs commandLineArgs)
    {
        var provider = commandLineArgs.Options.GetOrNull(Options.Provider.Short, Options.Provider.Long);
        var url = commandLineArgs.Options.GetOrNull(Options.Url.Short, Options.Url.Long);
        var target = commandLineArgs.Options.GetOrNull(Options.Target.Long);
        var module = commandLineArgs.Options.GetOrNull(Options.Module.Short, Options.Module.Long) ?? "app";
        var output = commandLineArgs.Options.GetOrNull(Options.Output.Short, Options.Output.Long);
        var apiName = commandLineArgs.Options.GetOrNull(Options.ApiName.Short, Options.ApiName.Long);
        var source = commandLineArgs.Options.GetOrNull(Options.Source.Short, Options.Source.Long);
        var workDirectory = commandLineArgs.Options.GetOrNull(Options.WorkDirectory.Short, Options.WorkDirectory.Long) ?? Directory.GetCurrentDirectory();
        var folder = commandLineArgs.Options.GetOrNull(Options.Folder.Long);

        return new GenerateProxyArgs(CommandName, workDirectory, module, url, output, target, apiName, source, folder, provider, commandLineArgs.Options);
    }

    public string GetUsageInfo()
    {
        var sb = new StringBuilder();

        sb.AppendLine("");
        sb.AppendLine("Usage:");
        sb.AppendLine("");
        sb.AppendLine($"  labp {CommandName}");
        sb.AppendLine("");
        sb.AppendLine("Options:");
        sb.AppendLine("");
        sb.AppendLine("-m|--module <module-name>                         (default: 'app') The name of the backend module you wish to generate proxies for.");
        sb.AppendLine("-wd|--working-directory <directory-path>          Execution directory.");
        sb.AppendLine("-u|--url <url>                                    API definition URL from.");
        sb.AppendLine("-p|--provider <client-proxy-provider>             The client proxy provider(http, dapr).");
        sb.AppendLine("See the documentation for more info: https://docs.abp.io/en/abp/latest/CLI");

        sb.AppendLine("");
        sb.AppendLine("Examples:");
        sb.AppendLine("");
        sb.AppendLine("  labp generate-proxy");
        sb.AppendLine("  labp generate-proxy -p dapr");
        sb.AppendLine("  labp generate-proxy -m identity -o Pages/Identity/client-proxies.js -url https://localhost:44302/");
        sb.AppendLine("  labp generate-proxy --folder MyProxies/InnerFolder -url https://localhost:44302/");

        return sb.ToString();
    }

    public string GetShortDescription()
    {
        return "Generates client service proxies and DTOs to consume HTTP APIs.";
    }

    public static class Options
    {
        public static class Provider
        {
            public const string Short = "p";
            public const string Long = "provider";
        }

        public static class Module
        {
            public const string Short = "m";
            public const string Long = "module";
        }

        public static class ApiName
        {
            public const string Short = "a";
            public const string Long = "api-name";
        }

        public static class Source
        {
            public const string Short = "s";
            public const string Long = "source";
        }
        public static class Output
        {
            public const string Short = "o";
            public const string Long = "output";
        }

        public static class Target
        {
            public const string Long = "target";
        }

        public static class Prompt
        {
            public const string Short = "p";
            public const string Long = "prompt";
        }

        public static class Folder
        {
            public const string Long = "folder";
        }

        public static class Url
        {
            public const string Short = "u";
            public const string Long = "url";
        }

        public static class WorkDirectory
        {
            public const string Short = "wd";
            public const string Long = "working-directory";
        }
    }
}
