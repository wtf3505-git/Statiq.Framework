﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Spectre.Cli;
using Statiq.Common;
using Statiq.Core;

namespace Statiq.App
{
    public class Bootstrapper : IBootstrapper
    {
        private readonly ClassCatalog _classCatalog = new ClassCatalog();

        private Func<CommandServiceTypeRegistrar, ICommandApp> _getCommandApp = x => new CommandApp(x);

        // Private constructor to force factory use which returns the interface to get access to default interface implementations
        private Bootstrapper(string[] arguments)
        {
            Arguments = arguments ?? throw new ArgumentNullException(nameof(arguments));
        }

        /// <inheritdoc/>
        public IClassCatalog ClassCatalog => _classCatalog;

        /// <inheritdoc/>
        public IConfiguratorCollection Configurators { get; } = new ConfiguratorCollection();

        /// <inheritdoc/>
        public string[] Arguments { get; }

        /// <inheritdoc/>
        public IBootstrapper SetDefaultCommand<TCommand>()
            where TCommand : class, ICommand
        {
            _getCommandApp = x => new CommandApp<TCommand>(x);
            return this;
        }

        /// <inheritdoc/>
        public async Task<int> RunAsync()
        {
            // Remove the synchronization context
            await default(SynchronizationContextRemover);

            // Populate the class catalog (if we haven't already)
            _classCatalog.Populate();

            // Run bootstrapper configurators first
            Configurators.Configure<IConfigurableBootstrapper>(this);
            Configurators.Configure<IBootstrapper>(this);

            // Create the service collection
            IServiceCollection serviceCollection = CreateServiceCollection() ?? new ServiceCollection();
            serviceCollection.TryAddSingleton<IConfigurableBootstrapper>(this);
            serviceCollection.TryAddSingleton<IBootstrapper>(this);
            serviceCollection.TryAddSingleton(_classCatalog);  // The class catalog is retrieved later for deferred logging once a service provider is built

            // Run configurators on the service collection
            ConfigurableServices configurableServices = new ConfigurableServices(serviceCollection);
            Configurators.Configure(configurableServices);

            // Add simple logging to make sure it's available in commands before the engine adds in
            serviceCollection.AddLogging();

            // Create the stand-alone command line service container and register a few types needed for the CLI
            CommandServiceTypeRegistrar registrar = new CommandServiceTypeRegistrar();
            registrar.RegisterInstance(typeof(IServiceCollection), serviceCollection);
            registrar.RegisterInstance(typeof(IBootstrapper), this);

            // Create the command line parser and run the command
            ICommandApp app = _getCommandApp(registrar);
            app.Configure(x =>
            {
                x.ValidateExamples();
                ConfigurableCommands configurableCommands = new ConfigurableCommands(x);
                Configurators.Configure(configurableCommands);
            });
            return await app.RunAsync(Arguments);
        }

        /// <summary>
        /// Creates a service collection for use by the bootstrapper.
        /// </summary>
        /// <remarks>
        /// Override to perform post-creation configuration or to use an alternate service collection type.
        /// </remarks>
        /// <returns>A service collection for use by the bootstrapper.</returns>
        protected virtual IServiceCollection CreateServiceCollection() => null;

        // Static factories

        /// <summary>
        /// Creates an empty bootstrapper without any default configuration.
        /// </summary>
        /// <remarks>
        /// Use this method when you want to fully customize the bootstrapper and engine.
        /// Otherwise use on of the <see cref="CreateDefault(string[])"/> overloads to
        /// create an initialize a bootstrapper with an initial set of default configurations.
        /// </remarks>
        /// <param name="args">The command line arguments.</param>
        /// <returns>The bootstrapper.</returns>
        public static IBootstrapper Create(string[] args) => new Bootstrapper(args);

        /// <summary>
        /// Creates a bootstrapper with a default configuration including logging, commands,
        /// shortcodes, and assembly scanning.
        /// </summary>
        /// <param name="args">The command line arguments.</param>
        /// <returns>The bootstrapper.</returns>
        public static IBootstrapper CreateDefault(string[] args) => Create(args).AddDefaults();

        /// <summary>
        /// Creates a bootstrapper with a default configuration including logging, commands,
        /// shortcodes, and assembly scanning while specifying an additional engine configurator.
        /// </summary>
        /// <typeparam name="TConfigurator">The type of engine configurator to use.</typeparam>
        /// <param name="args">The command line arguments.</param>
        /// <returns>The bootstrapper.</returns>
        public static IBootstrapper CreateDefault<TConfigurator>(string[] args)
            where TConfigurator : Common.IConfigurator<IEngine> =>
            Create(args).AddDefaults<TConfigurator>();

        /// <summary>
        /// Creates a bootstrapper with a default configuration including logging, commands,
        /// shortcodes, and assembly scanning while specifying a delegate to configure the engine.
        /// </summary>
        /// <param name="args">The command line arguments.</param>
        /// <param name="configureEngineAction">A delegate to configure the engine.</param>
        /// <returns>The bootstrapper.</returns>
        public static IBootstrapper CreateDefault(string[] args, Action<IEngine> configureEngineAction) =>
            Create(args).AddDefaults(configureEngineAction);

        /// <summary>
        /// Creates a bootstrapper with a default configuration including logging, commands,
        /// shortcodes, and assembly scanning while specifying an additional engine configurator.
        /// </summary>
        /// <param name="args">The command line arguments.</param>
        /// <param name="configurator">The engine configurator.</param>
        /// <returns>The bootstrapper.</returns>
        public static IBootstrapper CreateDefault(string[] args, Common.IConfigurator<IEngine> configurator) =>
            Create(args).AddDefaults(configurator);
    }
}
