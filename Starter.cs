#region Related components
using System;
using System.Xml;
using System.Linq;
using System.Reflection;
using System.Configuration;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using net.vieapps.Components.Utility;
#endregion

namespace net.vieapps.Components.Repository
{
	public static class RepositoryStarter
	{
		/// <summary>
		/// Initializes all types of the assembly
		/// </summary>
		/// <param name="assembly">The assembly for initializing</param>
		/// <param name="tracker">The tracker for tracking logs</param>
		public static void Initialize(Assembly assembly, Action<string, Exception> tracker = null)
		{
			try
			{
				var types = assembly.GetExportedTypes();
				tracker?.Invoke($"Initialize the assembly: {assembly.GetName().Name} with {types.Length} exported type(s)", null);
				if (tracker == null && RepositoryMediator.IsDebugEnabled)
					RepositoryMediator.WriteLogs($"Initialize the assembly: {assembly.GetName().Name} with {types.Length} exported type(s)", null);

				// repositories
				types.Where(type => type.IsDefined<RepositoryAttribute>(false)).ForEach(type =>
				{
					tracker?.Invoke($"Register the repository: {type.GetTypeName()}", null);
					if (tracker == null && RepositoryMediator.IsDebugEnabled)
						RepositoryMediator.WriteLogs($"Register the repository: {type.GetTypeName()}", null);
					RepositoryDefinition.Register(type, tracker);
				});

				// entities
				types.Where(type => type.IsDefined<EntityAttribute>(false)).ForEach(type =>
				{
					tracker?.Invoke($"Register the repository entity: {type.GetTypeName()}", null);
					if (tracker == null && RepositoryMediator.IsDebugEnabled)
						RepositoryMediator.WriteLogs($"Register the repository entity: {type.GetTypeName()}", null);
					EntityDefinition.Register(type);
				});

				// event handlers
				types.Where(type => type.IsDefined<EventHandlersAttribute>(false))
					.Where(type => typeof(IPreCreateHandler).IsAssignableFrom(type) || typeof(IPostCreateHandler).IsAssignableFrom(type)
						|| typeof(IPreGetHandler).IsAssignableFrom(type) || typeof(IPostGetHandler).IsAssignableFrom(type)
						|| typeof(IPreUpdateHandler).IsAssignableFrom(type) || typeof(IPostUpdateHandler).IsAssignableFrom(type)
						|| typeof(IPreDeleteHandler).IsAssignableFrom(type) || typeof(IPostDeleteHandler).IsAssignableFrom(type))
					.ForEach(type =>
					{
						tracker?.Invoke($"Register the event-handler: {type.GetTypeName()}", null);
						if (tracker == null && RepositoryMediator.IsDebugEnabled)
							RepositoryMediator.WriteLogs($"Register the event-handler: {type.GetTypeName()}", null);
						RepositoryMediator.EventHandlers.Add(type);
					});
			}
			catch (ReflectionTypeLoadException ex)
			{
				if (ex.LoaderExceptions.FirstOrDefault(e => e is System.IO.FileNotFoundException) == null)
				{
					tracker?.Invoke($"Error occurred while initializing the assembly: {assembly.GetName().Name}", ex);
					if (tracker == null)
						RepositoryMediator.WriteLogs($"Error occurred while initializing the assembly: {assembly.GetName().Name}", ex);
					ex.LoaderExceptions.ForEach(exception => tracker?.Invoke(null, exception));
					throw;
				}
			}
			catch (Exception ex)
			{
				tracker?.Invoke($"Error occurred while initializing the assembly: {assembly.GetName().Name}", ex);
				if (tracker == null)
					RepositoryMediator.WriteLogs($"Error occurred while initializing the assembly: {assembly.GetName().Name}", ex);
				throw;
			}
		}

		/// <summary>
		/// Initializes all types in assemblies
		/// </summary>
		/// <param name="assemblies">The collection of assemblies</param>
		/// <param name="tracker">The tracker for tracking logs</param>
		/// <param name="updateFromConfigurationFile">true to update other settings from configuration file on the disc</param>
		/// <param name="config">The XML node that contains configuration</param>
		public static void Initialize(IEnumerable<Assembly> assemblies, Action<string, Exception> tracker = null, bool updateFromConfigurationFile = true, XmlNode config = null)
		{
			tracker?.Invoke($"Start to initialize repositories & entities [{assemblies.Select(a => a.GetName().Name).ToString(", ")}]", null);
			if (tracker == null && RepositoryMediator.IsDebugEnabled)
				RepositoryMediator.WriteLogs($"Start to initialize repositories & entities [{assemblies.Select(a => a.GetName().Name).ToString(", ")}]");

			// initialize & register all types
			assemblies.ForEach(assembly => RepositoryStarter.Initialize(assembly, tracker));

			// read configuration
			if (updateFromConfigurationFile && config == null)
			{
				if (ConfigurationManager.GetSection(UtilityService.GetAppSetting("Section:Repositories", "net.vieapps.repositories")) is AppConfigurationSectionHandler configSection)
					config = configSection.Section;
				if (config == null)
					throw new ConfigurationErrorsException("Cannot find the configuration section (might be named as 'net.vieapps.repositories') in the configuration file");
			}

			// update configuration
			if (config != null)
				try
				{
					// update settings of data sources
					if (config.SelectNodes("dataSources/dataSource") is XmlNodeList dataSourceNodes)
						RepositoryMediator.ConstructDataSources(dataSourceNodes.ToList(), tracker);

					// update settings of repositories
					if (updateFromConfigurationFile && config.SelectNodes("repository") is XmlNodeList repositoryNodes)
						repositoryNodes.ToList().ForEach(repositoryNode =>
						{
							// update repository
							var settingsJson = repositoryNode.ToJson();
							tracker?.Invoke($"Update settings of a repository => {settingsJson}", null);
							if (tracker == null && RepositoryMediator.IsDebugEnabled)
								RepositoryMediator.WriteLogs($"Update settings of a repository => {settingsJson}");
							RepositoryDefinition.Update(settingsJson, tracker);

							// update repository entities
							if (repositoryNode.SelectNodes("entity") is XmlNodeList entityNodes)
								entityNodes.ToList().ForEach(repositoryEntityNode =>
								{
									settingsJson = repositoryEntityNode.ToJson();
									tracker?.Invoke($"Update settings of a repository entity => {settingsJson}", null);
									if (tracker == null && RepositoryMediator.IsDebugEnabled)
										RepositoryMediator.WriteLogs($"Update settings of a repository entity => {settingsJson}");
									EntityDefinition.Update(settingsJson, tracker);
								});
						});

					// default data sources
					RepositoryMediator.DefaultVersionDataSourceName = config.Attributes["versionDataSource"]?.Value;
					RepositoryMediator.DefaultTrashDataSourceName = config.Attributes["trashDataSource"]?.Value;

					// ensure schemas (SQL)
					if ("true".IsEquals(config.Attributes["ensureSchemas"]?.Value))
						Task.Run(async () => await RepositoryMediator.EntityDefinitions.ForEachAsync(async (definition, cancellationToken) =>
						{
							var primaryDataSource = RepositoryMediator.GetPrimaryDataSource(null, definition);
							primaryDataSource = primaryDataSource != null && primaryDataSource.Mode.Equals(RepositoryMode.SQL)
								? primaryDataSource
								: null;
							await RepositoryStarter.EnsureSqlSchemasAsync(definition, primaryDataSource, tracker, cancellationToken).ConfigureAwait(false);

							var secondaryDataSource = RepositoryMediator.GetSecondaryDataSource(null, definition);
							secondaryDataSource = secondaryDataSource != null && secondaryDataSource.Mode.Equals(RepositoryMode.SQL)
								? secondaryDataSource
								: null;
							await RepositoryStarter.EnsureSqlSchemasAsync(definition, secondaryDataSource, tracker, cancellationToken).ConfigureAwait(false);

							await RepositoryMediator.GetSyncDataSources(null, definition)
								.Where(dataSource => dataSource.Mode.Equals(RepositoryMode.SQL) && !dataSource.Name.IsEquals(primaryDataSource?.Name) && !dataSource.Name.IsEquals(secondaryDataSource?.Name))
								.ForEachAsync(async (dataSource, token) => await RepositoryStarter.EnsureSqlSchemasAsync(definition, dataSource, tracker, token).ConfigureAwait(false), cancellationToken, true, false)
								.ConfigureAwait(false);
						}, CancellationToken.None, true, false)).ConfigureAwait(false);

					// ensure indexes (NoSQL)
					if ("true".IsEquals(config.Attributes["ensureIndexes"]?.Value))
						Task.Run(async () => await RepositoryMediator.EntityDefinitions.ForEachAsync(async (definition, cancellationToken) =>
						{
							var primaryDataSource = RepositoryMediator.GetPrimaryDataSource(null, definition);
							primaryDataSource = primaryDataSource != null && primaryDataSource.Mode.Equals(RepositoryMode.NoSQL)
								? primaryDataSource
								: null;
							await RepositoryStarter.EnsureNoSqlIndexesAsync(definition, primaryDataSource, tracker, cancellationToken).ConfigureAwait(false);

							var secondaryDataSource = RepositoryMediator.GetSecondaryDataSource(null, definition);
							secondaryDataSource = secondaryDataSource != null && secondaryDataSource.Mode.Equals(RepositoryMode.NoSQL)
								? secondaryDataSource
								: null;
							await RepositoryStarter.EnsureNoSqlIndexesAsync(definition, secondaryDataSource, tracker, cancellationToken).ConfigureAwait(false);

							await RepositoryMediator.GetSyncDataSources(null, definition)
								.Where(dataSource => dataSource.Mode.Equals(RepositoryMode.NoSQL) && !dataSource.Name.IsEquals(primaryDataSource?.Name) && !dataSource.Name.IsEquals(secondaryDataSource?.Name))
								.ForEachAsync(async (dataSource, token) => await RepositoryStarter.EnsureNoSqlIndexesAsync(definition, dataSource, tracker, token).ConfigureAwait(false), cancellationToken, true, false)
								.ConfigureAwait(false);
						}, CancellationToken.None, true, false)).ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					tracker?.Invoke($"Error occurred while updating the repository => {ex.Message}", ex);
					if (tracker == null)
						RepositoryMediator.WriteLogs($"Error occurred while updating the repository => {ex.Message}", ex);
					throw;
				}
			else
			{
				tracker?.Invoke("No configuration to update", null);
				if (tracker == null && RepositoryMediator.IsDebugEnabled)
					RepositoryMediator.WriteLogs("No configuration to update");
			}

			tracker?.Invoke($"Total of registered repositories: {RepositoryMediator.RepositoryDefinitions.Count}", null);
			tracker?.Invoke($"Total of registered repository entities: {RepositoryMediator.EntityDefinitions.Count}", null);
			tracker?.Invoke($"Total of registered event-handlers: {RepositoryMediator.EventHandlers.Count}", null);
			tracker?.Invoke($"Total of registered data-sources: {RepositoryMediator.DataSources.Count}", null);
			tracker?.Invoke($"Default data-source for storing version contents: {RepositoryMediator.DefaultVersionDataSourceName ?? "(None)"}", null);
			tracker?.Invoke($"Default data-source for storing trash contents: {RepositoryMediator.DefaultTrashDataSourceName ?? "(None)"}", null);

			if (tracker == null && RepositoryMediator.IsDebugEnabled)
			{
				RepositoryMediator.WriteLogs($"Total of registered repositories: {RepositoryMediator.RepositoryDefinitions.Count}", null);
				RepositoryMediator.WriteLogs($"Total of registered repository entities: {RepositoryMediator.EntityDefinitions.Count}", null);
				RepositoryMediator.WriteLogs($"Total of registered event-handlers: {RepositoryMediator.EventHandlers.Count}", null);
				RepositoryMediator.WriteLogs($"Total of registered data-sources: {RepositoryMediator.DataSources.Count}", null);
				RepositoryMediator.WriteLogs($"Default data-source for storing version contents: {RepositoryMediator.DefaultVersionDataSourceName ?? "(None)"}", null);
				RepositoryMediator.WriteLogs($"Default data-source for storing trash contents: {RepositoryMediator.DefaultTrashDataSourceName ?? "(None)"}", null);
			}
		}

		/// <summary>
		/// Initializes all types of all assemblies of the current app
		/// </summary>
		/// <param name="selector">The function to select assemblies to initialize</param>
		/// <param name="tracker">The tracker for tracking logs</param>
		public static void Initialize(Func<IEnumerable<Assembly>> selector = null, Action<string, Exception> tracker = null)
			=> RepositoryStarter.Initialize(selector != null
				? selector()
				: new[] { Assembly.GetCallingAssembly() }.Concat(Assembly.GetCallingAssembly()
					.GetReferencedAssemblies()
					.Where(an => !an.Name.IsStartsWith("api-ms") && !an.Name.IsStartsWith("clr") && !an.Name.IsStartsWith("mscor") && !an.Name.IsStartsWith("sos") && !an.Name.IsStartsWith("lib")
						&& !an.Name.IsStartsWith("System") && !an.Name.IsStartsWith("Microsoft") && !an.Name.IsStartsWith("Windows") && !an.Name.IsEquals("NETStandard")
						&& !an.Name.IsStartsWith("Newtonsoft") && !an.Name.IsStartsWith("WampSharp") && !an.Name.IsStartsWith("Enyim.") && !an.Name.IsStartsWith("StackExchange.")
						&& !an.Name.IsStartsWith("Serilog") && !an.Name.IsStartsWith("MsgPack") && !an.Name.IsStartsWith("ExcelData")
						&& !an.Name.IsStartsWith("MongoDB") && !an.Name.IsStartsWith("MySql") && !an.Name.IsStartsWith("Npgsql")
						&& !an.Name.IsEndsWith(".Abstractions") && !an.Name.IsStartsWith("VIEApps.Components")
					)
					.Select(an => Assembly.Load(an))
				),
				tracker
			);

		/// <summary>
		/// Constructs data-sources
		/// </summary>
		/// <param name="datasourceNodes"></param>
		/// <param name="tracker"></param>
		public static void ConstructDataSources(List<XmlNode> datasourceNodes, Action<string, Exception> tracker = null)
			=> RepositoryMediator.ConstructDataSources(datasourceNodes, tracker);

		/// <summary>
		/// Constructs data-sources
		/// </summary>
		/// <param name="datasourceNodes"></param>
		/// <param name="tracker"></param>
		public static void ConstructDataSources(XmlNodeList datasourceNodes, Action<string, Exception> tracker = null)
			=> RepositoryStarter.ConstructDataSources(datasourceNodes.ToList(), tracker);

		/// <summary>
		/// Constructs SQL database factory providers
		/// </summary>
		/// <param name="dbProviderFactoryNodes"></param>
		/// <param name="tracker"></param>
		public static void ConstructDbProviderFactories(List<XmlNode> dbProviderFactoryNodes, Action<string, Exception> tracker = null)
			=> DbProvider.ConstructDbProviderFactories(dbProviderFactoryNodes, tracker);

		/// <summary>
		/// Constructs SQL database factory providers
		/// </summary>
		/// <param name="dbProviderFactoryNodes"></param>
		/// <param name="tracker"></param>
		public static void ConstructDbProviderFactories(XmlNodeList dbProviderFactoryNodes, Action<string, Exception> tracker = null)
			=> RepositoryStarter.ConstructDbProviderFactories(dbProviderFactoryNodes.ToList(), tracker);

		/// <summary>
		/// Ensures schemas of an entity in SQL database
		/// </summary>
		/// <param name="definition"></param>
		/// <param name="dataSource"></param>
		/// <param name="tracker"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public static async Task EnsureSqlSchemasAsync(EntityDefinition definition, DataSource dataSource, Action<string, Exception> tracker = null, CancellationToken cancellationToken = default)
		{
			if (definition != null && dataSource != null && dataSource.Mode.Equals(RepositoryMode.SQL))
				try
				{
					tracker?.Invoke($"Ensure schemas of SQL: {definition.Type} [{dataSource.Name} @ {dataSource.Mode} => {definition.TableName}]", null);
					if (tracker == null && RepositoryMediator.IsDebugEnabled)
						RepositoryMediator.WriteLogs($"Ensure schemas of SQL: {definition.Type} [{dataSource.Name} @ {dataSource.Mode} => {definition.TableName}]", null);
					await definition.EnsureSchemasAsync(dataSource, tracker, cancellationToken).ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					tracker?.Invoke($"Error occurred while ensuring schemas of SQL: {definition.Type} [{dataSource.Name} @ {dataSource.Mode} => {definition.TableName}]", ex);
					if (tracker == null)
						RepositoryMediator.WriteLogs($"Error occurred while ensuring schemas of SQL: {definition.Type} [{dataSource.Name} @ {dataSource.Mode} => {definition.TableName}]", ex);
				}
		}

		/// <summary>
		/// Ensures indexes of an entity in No SQL database
		/// </summary>
		/// <param name="definition"></param>
		/// <param name="dataSource"></param>
		/// <param name="tracker"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public static async Task EnsureNoSqlIndexesAsync(EntityDefinition definition, DataSource dataSource, Action<string, Exception> tracker = null, CancellationToken cancellationToken = default)
		{
			if (definition != null && dataSource != null && dataSource.Mode.Equals(RepositoryMode.NoSQL))
				try
				{
					tracker?.Invoke($"Ensure indexes of No SQL: {definition.Type} [{dataSource.Name} @ {dataSource.Mode} => {definition.CollectionName}]", null);
					if (tracker == null && RepositoryMediator.IsDebugEnabled)
						RepositoryMediator.WriteLogs($"Ensure indexes of No SQL: {definition.Type} [{dataSource.Name} @ {dataSource.Mode} => {definition.CollectionName}]", null);
					await definition.EnsureIndexesAsync(dataSource, tracker, cancellationToken).ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					tracker?.Invoke($"Error occurred while ensuring indexes of No SQL: {definition.Type} [{dataSource.Name} @ {dataSource.Mode} => {definition.CollectionName}]", ex);
					if (tracker == null)
						RepositoryMediator.WriteLogs($"Cannot ensure indexes of No SQL: {definition.Type} [{dataSource.Name} @ {dataSource.Mode} => {definition.CollectionName}]", ex);
				}
		}
	}
}