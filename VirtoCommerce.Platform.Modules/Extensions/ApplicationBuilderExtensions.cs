using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Smidge;
using Smidge.FileProcessors;
using Smidge.Models;
using Smidge.Nuglify;
using Smidge.Options;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Platform.Modules.Smidge;

namespace VirtoCommerce.Platform.Modules.Extensions
{
    public static class ApplicationBuilderExtensions
    {
        public static IApplicationBuilder UseModules(this IApplicationBuilder appBuilder)
        {
            using (var serviceScope = appBuilder.ApplicationServices.CreateScope())
            {
                var moduleManager = serviceScope.ServiceProvider.GetRequiredService<IModuleManager>();
                var modules = GetInstalledModules(serviceScope.ServiceProvider);
                var permissionsProvider = serviceScope.ServiceProvider.GetRequiredService<IPermissionsProvider>();
                foreach (var module in modules)
                {
                    //Register modules permissions defined in the module manifest
                    var modulePermissions = module.Permissions.SelectMany(x => x.Permissions).Select(x=> new Permission { Name = x.Id }).ToArray();
                    permissionsProvider.RegisterPermissions(modulePermissions);

                    moduleManager.PostInitializeModule(module, serviceScope.ServiceProvider);
                }
            }
            return appBuilder;
        }

        public static IApplicationBuilder UseModulesContent(this IApplicationBuilder appBuilder, IBundleManager bundles)
        {
            var env = appBuilder.ApplicationServices.GetService<IHostingEnvironment>();
            var modules = GetInstalledModules(appBuilder.ApplicationServices);
            var modulesOptions = appBuilder.ApplicationServices.GetRequiredService<IOptions<LocalStorageModuleCatalogOptions>>().Value;
            var cssBundleItems = modules.SelectMany(m => m.Styles).ToArray();
            var cssFiles = cssBundleItems.OfType<ManifestBundleFile>().Select(x => new CssFile(x.VirtualPath));

            cssFiles = cssFiles.Concat(cssBundleItems.OfType<ManifestBundleDirectory>().SelectMany(x => new WebFileFolder(modulesOptions.DiscoveryPath, x.VirtualPath)
                                                                                .AllWebFiles<CssFile>(x.SearchPattern, x.SearchSubdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)));

            var scriptBundleItems = modules.SelectMany(m => m.Scripts).ToArray();
            var jsFiles = scriptBundleItems.OfType<ManifestBundleFile>().Select(x => new JavaScriptFile(x.VirtualPath));
            //jsFiles = jsFiles.Concat(scriptBundleItems.OfType<ManifestBundleDirectory>().SelectMany(x => new WebFileFolder(modulesOptions.DiscoveryPath, x.VirtualPath)
            //                                                                    .AllWebFiles<JavaScriptFile>(x.SearchPattern, x.SearchSubdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)));
            foreach (var module in modules)
            {
                jsFiles = jsFiles.Concat(module.Scripts.OfType<ManifestBundleDirectory>().SelectMany((s =>
                {
                    var result = new WebFileFolder(modulesOptions.DiscoveryPath, s.VirtualPath)
                                    .AllWebFiles<JavaScriptFileVirtoCommerce>(s.SearchPattern
                                        , s.SearchSubdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
                    if (env.IsDevelopment())
                    {
                        foreach (var script in result)
                        {
                            script.FilePath = script.FilePath.Replace("~/", "");
                            string root = GetRootFolder(script.FilePath);
                            var requestPath = script.FilePath
                                .Replace(root, "Modules")
                                .Replace($"{module.ModuleName}Module.Web", $"$({module.ModuleName})");
                            script.FilePath = $"{requestPath}";
                        }
                    }
                    
                    return result;
                })));

                //appBuilder.UseStaticFiles(new StaticFileOptions()
                //{
                //    FileProvider = new PhysicalFileProvider(module.FullPhysicalPath),
                //    RequestPath = new PathString($"/sb/maps/$({ module.ModuleName })")
                //});
            }


            //TODO: Test minification and uglification for resulting bundles
            var options = bundles.DefaultBundleOptions;
            options.DebugOptions.FileWatchOptions.Enabled = true;
            options.DebugOptions.ProcessAsCompositeFile = false;
            options.DebugOptions.CompressResult = false;
            options.DebugOptions.CacheControlOptions = new CacheControlOptions() { EnableETag = false, CacheControlMaxAge = 0 };
            //options.ProductionOptions.ProcessAsCompositeFile = true;
            //options.ProductionOptions.FileWatchOptions.Enabled = true;
            //options.ProductionOptions.CacheControlOptions = new CacheControlOptions() { EnableETag = false, CacheControlMaxAge = 0 };
            //bundles.PipelineFactory.OnCreateDefault = (type, pipeline) => pipeline.Replace<JsMinifier, NuglifyJs>(bundles.PipelineFactory);

            bundles.Create("vc-modules-styles", cssFiles.ToArray())
               .WithEnvironmentOptions(options);

            bundles.Create("vc-modules-scripts"/*, bundles.PipelineFactory.Create<NuglifyJsVirtoCommerce>()*/, jsFiles.ToArray())
                   .WithEnvironmentOptions(options);


            return appBuilder;
        }

        static string GetRootFolder(string path)
        {
            while (true)
            {
                string temp = Path.GetDirectoryName(path);
                if (String.IsNullOrEmpty(temp))
                    break;
                path = temp;
            }
            return path;
        }

        private static IEnumerable<ManifestModuleInfo> GetInstalledModules(IServiceProvider serviceProvider)
        {
            var moduleCatalog = serviceProvider.GetRequiredService<ILocalModuleCatalog>();
            var allModules = moduleCatalog.Modules.OfType<ManifestModuleInfo>().ToArray();
            return moduleCatalog.CompleteListWithDependencies(allModules)
                .Where(x => x.State == ModuleState.Initialized)
                .OfType<ManifestModuleInfo>()
                .ToArray();
        }
    }

    /// <summary>
    /// Workaround  suggested by @josh-sachs  https://github.com/Shazwazza/Smidge/issues/47
    /// to allow use recursive directory search for content files
    /// </summary>
    internal class WebFileFolder
    {
        private readonly string _rootPath;
        private readonly string _path;

        public WebFileFolder(string rootPath, string path)
        {
            _rootPath = rootPath;
            _path = path;
        }

        public T[] AllWebFiles<T>(string pattern, SearchOption search) where T : IWebFile, new()
        {
            var result = Directory.GetFiles(Path.Combine(_rootPath, _path), pattern, search)
                 .Select(f => new T
                 {
                     FilePath = f.Replace(_rootPath, "~").Replace("\\", "/")
                 }).ToArray();
            return result;
        }

        public T[] AllWebFilesWithRequestRoot<T>(string pattern, SearchOption search) where T : IWebFile, new()
        {
            var fsPath = _path.Replace("~", _rootPath);
            return Directory.GetFiles(fsPath, pattern, search)
                .Select(f => new T
                {
                    FilePath = f.Replace(_rootPath, "~").Replace("\\", "/")
                }).ToArray();
        }
    }
}
