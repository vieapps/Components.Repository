#region Related components
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Xml;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
				tracker?.Invoke($"Initialize the assembly: {assembly.GetName().Name} [{types.Length} type(s)]", null);
				if (RepositoryMediator.IsDebugEnabled)
					RepositoryMediator.WriteLogs($"Initialize the assembly: {assembly.GetName().Name}", null);

				// repositories
				types.Where(type => type.IsDefined(typeof(RepositoryAttribute), false)).ForEach(type =>
				{
					tracker?.Invoke($"Register the repository: {type.GetTypeName()}", null);
					if (RepositoryMediator.IsDebugEnabled)
						RepositoryMediator.WriteLogs($"Register the repository: {type.GetTypeName()}", null);
					RepositoryDefinition.Register(type);
				});

				// entities
				types.Where(type => type.IsDefined(typeof(EntityAttribute), false)).ForEach(type =>
				{
					tracker?.Invoke($"Register the repository entity: {type.GetTypeName()}", null);
					if (RepositoryMediator.IsDebugEnabled)
						RepositoryMediator.WriteLogs($"Register the repository entity: {type.GetTypeName()}", null);
					EntityDefinition.Register(type);
				});

				// event handlers
				types.Where(type => type.IsDefined(typeof(EventHandlersAttribute), false))
					.Where(type => typeof(IPreCreateHandler).IsAssignableFrom(type) || typeof(IPostCreateHandler).IsAssignableFrom(type)
						|| typeof(IPreGetHandler).IsAssignableFrom(type) || typeof(IPostGetHandler).IsAssignableFrom(type)
						|| typeof(IPreUpdateHandler).IsAssignableFrom(type) || typeof(IPostUpdateHandler).IsAssignableFrom(type)
						|| typeof(IPreDeleteHandler).IsAssignableFrom(type) || typeof(IPostDeleteHandler).IsAssignableFrom(type))
					.ForEach(type =>
					{
						tracker?.Invoke($"Register the event-handler: {type.GetTypeName()}", null);
						if (RepositoryMediator.IsDebugEnabled)
							RepositoryMediator.WriteLogs($"Register the event-handler: {type.GetTypeName()}", null);
						RepositoryMediator.EventHandlers.Add(type);
					});
			}
			catch (ReflectionTypeLoadException ex)
			{
				if (ex.LoaderExceptions.FirstOrDefault(e => e is System.IO.FileNotFoundException) == null)
				{
					tracker?.Invoke($"Error occurred while initializing the assembly: {assembly.GetName().Name}", ex);
					RepositoryMediator.WriteLogs($"Error occurred while initializing the assembly: {assembly.GetName().Name}", ex);
					ex.LoaderExceptions.ForEach(exception => tracker?.Invoke(null, exception));
					throw ex;
				}
			}
			catch (Exception ex)
			{
				tracker?.Invoke($"Error occurred while initializing the assembly: {assembly.GetName().Name}", ex);
				RepositoryMediator.WriteLogs($"Error occurred while initializing the assembly: {assembly.GetName().Name}", ex);
				throw ex;
			}
		}

		/// <summary>
		/// Initializes all types in assemblies
		/// </summary>
		/// <param name="assemblies">The collection of assemblies</param>
		/// <param name="tracker">The tracker for tracking logs</param>
		/// <param name="updateFromConfigurationFile">true to update other settings from configuration file on the disc</param>
		public static void Initialize(IEnumerable<Assembly> assemblies, Action<string, Exception> tracker = null, bool updateFromConfigurationFile = true)
		{
			if (RepositoryMediator.IsDebugEnabled)
				RepositoryMediator.WriteLogs("Start to initialize repositories & entities [" + assemblies.Select(a => a.GetName().Name).ToString(", ") + "]");

			// initialize & register all types
			assemblies.ForEach(assembly => RepositoryStarter.Initialize(assembly, tracker));

			// read configuration and update
			if (ConfigurationManager.GetSection("net.vieapps.repositories") is AppConfigurationSectionHandler config)
				try
				{
					// update settings of data sources
					if (config.Section.SelectNodes("dataSources/dataSource") is XmlNodeList dataSourceNodes)
						RepositoryMediator.ConstructDataSources(dataSourceNodes.ToList(), tracker);

					// update settings of repositories
					if (config.Section.SelectNodes("repository") is XmlNodeList repositoryNodes)
						repositoryNodes.ToList().ForEach(repositoryNode =>
						{
							// update repository
							RepositoryDefinition.Update(repositoryNode.ToJson(), tracker);

							// update repository entities
							if (repositoryNode.SelectNodes("entity") is XmlNodeList entityNodes)
								entityNodes.ToList().ForEach(repositoryEntityNode => EntityDefinition.Update(repositoryEntityNode.ToJson(), tracker));
						});

					// default data sources
					RepositoryMediator.DefaultVersionDataSourceName = config.Section.Attributes["versionDataSource"]?.Value;
					RepositoryMediator.DefaultTrashDataSourceName = config.Section.Attributes["trashDataSource"]?.Value;

					// schemas (SQL)
					if ("true".IsEquals(config.Section.Attributes["ensureSchemas"]?.Value))
						Task.Run(async () => await RepositoryStarter.EnsureSqlSchemasAsync(tracker).ConfigureAwait(false)).ConfigureAwait(false);

					// indexes (NoSQL)
					if ("true".IsEquals(config.Section.Attributes["ensureIndexes"]?.Value))
						Task.Run(async () => await RepositoryStarter.EnsureNoSqlIndexesAsync(tracker).ConfigureAwait(false)).ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					tracker?.Invoke($"Error occurred while updating the repository: {ex.Message}", ex);
					RepositoryMediator.WriteLogs("Error occurred while updating the repository", ex);
					throw ex;
				}

			else if (updateFromConfigurationFile)
				throw new ConfigurationErrorsException("Cannot find the configuration section named 'net.vieapps.repositories' in the configuration file");

			tracker?.Invoke($"Total of registered repositories: {RepositoryMediator.RepositoryDefinitions.Count}", null);
			tracker?.Invoke($"Total of registered repository entities: {RepositoryMediator.EntityDefinitions.Count}", null);
			tracker?.Invoke($"Total of registered event-handlers: {RepositoryMediator.EventHandlers.Count}", null);
			tracker?.Invoke($"Total of registered data-sources: {RepositoryMediator.DataSources.Count}", null);
			tracker?.Invoke($"Default data-source for storing version contents: {RepositoryMediator.DefaultVersionDataSourceName ?? "(None)"}", null);
			tracker?.Invoke($"Default data-source for storing trash contents: {RepositoryMediator.DefaultTrashDataSourceName ?? "(None)"}", null);

			if (RepositoryMediator.IsDebugEnabled)
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
					.Where(n => !n.Name.IsStartsWith("api-ms") && !n.Name.IsStartsWith("clr") && !n.Name.IsStartsWith("mscor") && !n.Name.IsStartsWith("sos") && !n.Name.IsStartsWith("lib")
						&& !n.Name.IsStartsWith("System") && !n.Name.IsStartsWith("Microsoft") && !n.Name.IsStartsWith("Windows") && !n.Name.IsEquals("NETStandard")
						&& !n.Name.IsStartsWith("Newtonsoft") && !n.Name.IsStartsWith("WampSharp") && !n.Name.IsStartsWith("Castle.") && !n.Name.IsStartsWith("StackExchange.")
						&& !n.Name.IsStartsWith("MongoDB") && !n.Name.IsStartsWith("MySql") && !n.Name.IsStartsWith("Oracle") && !n.Name.IsStartsWith("Npgsql") && !n.Name.IsStartsWith("VIEApps.Components")
					)
					.Select(n => Assembly.Load(n))
					.ToList()
				),
				tracker
			);

		/// <summary>
		/// Constructs data-sources
		/// </summary>
		/// <param name="datasourceNodes"></param>
		/// <param name="tracker"></param>
		public static void ConstructDataSources(List<XmlNode> datasourceNodes, Action<string, Exception> tracker = null) => RepositoryMediator.ConstructDataSources(datasourceNodes, tracker);

		/// <summary>
		/// Constructs data-sources
		/// </summary>
		/// <param name="datasourceNodes"></param>
		/// <param name="tracker"></param>
		public static void ConstructDataSources(XmlNodeList datasourceNodes, Action<string, Exception> tracker = null) => RepositoryStarter.ConstructDataSources(datasourceNodes.ToList(), tracker);

		/// <summary>
		/// Constructs SQL database factory providers
		/// </summary>
		/// <param name="dbProviderFactoryNodes"></param>
		/// <param name="tracker"></param>
		public static void ConstructDbProviderFactories(List<XmlNode> dbProviderFactoryNodes, Action<string, Exception> tracker = null) => DbProviderFactories.ConstructDbProviderFactories(dbProviderFactoryNodes, tracker);

		/// <summary>
		/// Constructs SQL database factory providers
		/// </summary>
		/// <param name="dbProviderFactoryNodes"></param>
		/// <param name="tracker"></param>
		public static void ConstructDbProviderFactories(XmlNodeList dbProviderFactoryNodes, Action<string, Exception> tracker = null) => RepositoryStarter.ConstructDbProviderFactories(dbProviderFactoryNodes.ToList(), tracker);

		static async Task EnsureSqlSchemasAsync(Action<string, Exception> tracker = null)
		{
			await RepositoryMediator.EntityDefinitions.ForEachAsync(async (definition, cancellationToken) =>
			{
				var primaryDataSource = RepositoryMediator.GetPrimaryDataSource(null, definition);
				primaryDataSource = primaryDataSource != null && primaryDataSource.Mode.Equals(RepositoryMode.SQL)
					? primaryDataSource
					: null;
				if (primaryDataSource != null)
					try
					{
						tracker?.Invoke($"Ensure schemas of SQL: {definition.Type} [{primaryDataSource.Name} @ {primaryDataSource.Mode} => {definition.TableName}]", null);
						if (RepositoryMediator.IsDebugEnabled)
							RepositoryMediator.WriteLogs($"Ensure schemas of SQL: {definition.Type} [{primaryDataSource.Name} @ {primaryDataSource.Mode} => {definition.TableName}]", null);
						await definition.EnsureSchemasAsync(primaryDataSource, tracker, cancellationToken).ConfigureAwait(false);
					}
					catch (Exception ex)
					{
						tracker?.Invoke($"Error occurred while ensuring schemas of SQL: {definition.Type} [{primaryDataSource.Name} @ {primaryDataSource.Mode} => {definition.TableName}]", ex);
						RepositoryMediator.WriteLogs($"Error occurred while ensuring schemas of SQL: {definition.Type} [{primaryDataSource.Name} @ {primaryDataSource.Mode} => {definition.TableName}]", ex);
					}

				var secondaryDataSource = RepositoryMediator.GetSecondaryDataSource(null, definition);
				secondaryDataSource = secondaryDataSource != null && secondaryDataSource.Mode.Equals(RepositoryMode.SQL)
					? secondaryDataSource
					: null;
				if (secondaryDataSource != null)
					try
					{
						tracker?.Invoke($"Ensure schemas of SQL: {definition.Type} [{secondaryDataSource.Name} @ {secondaryDataSource.Mode} => {definition.TableName}]", null);
						if (RepositoryMediator.IsDebugEnabled)
							RepositoryMediator.WriteLogs($"Ensure schemas of SQL: {definition.Type} [{secondaryDataSource.Name} @ {secondaryDataSource.Mode} => {definition.TableName}]", null);
						await definition.EnsureSchemasAsync(secondaryDataSource, tracker, cancellationToken).ConfigureAwait(false);
					}
					catch (Exception ex)
					{
						tracker?.Invoke($"Error occurred while ensuring schemas of SQL: {definition.Type} [{secondaryDataSource.Name} @ {secondaryDataSource.Mode} => {definition.TableName}]", ex);
						RepositoryMediator.WriteLogs($"Error occurred while ensuring schemas of SQL: {definition.Type} [{secondaryDataSource.Name} @ {secondaryDataSource.Mode} => {definition.TableName}]", ex);
					}

				await RepositoryMediator.GetSyncDataSources(null, definition)
					.Where(dataSource => dataSource.Mode.Equals(RepositoryMode.SQL) && !dataSource.Name.IsEquals(primaryDataSource?.Name) && !dataSource.Name.IsEquals(secondaryDataSource?.Name))
					.ForEachAsync(async (dataSource, canceltoken) =>
					{
						try
						{
							tracker?.Invoke($"Ensure schemas of SQL: {definition.Type} [{dataSource.Name} @ {dataSource.Mode} => {definition.TableName}]", null);
							if (RepositoryMediator.IsDebugEnabled)
								RepositoryMediator.WriteLogs($"Ensure schemas of SQL: {definition.Type} [{dataSource.Name} @ {dataSource.Mode} => {definition.TableName}]", null);
							await definition.EnsureSchemasAsync(dataSource, tracker, cancellationToken).ConfigureAwait(false);
						}
						catch (Exception ex)
						{
							tracker?.Invoke($"Error occurred while ensuring schemas of SQL: {definition.Type} [{dataSource.Name} @ {dataSource.Mode} => {definition.TableName}]", ex);
							RepositoryMediator.WriteLogs($"Error occurred while ensuring schemas of SQL: {definition.Type} [{dataSource.Name} @ {dataSource.Mode} => {definition.TableName}]", ex);
						}
					}, cancellationToken, true, false).ConfigureAwait(false);
			}, CancellationToken.None, true, false).ConfigureAwait(false);
		}

		static async Task EnsureNoSqlIndexesAsync(Action<string, Exception> tracker = null)
		{
			await RepositoryMediator.EntityDefinitions.ForEachAsync(async (definition, cancellationToken) =>
			{
				var primaryDataSource = RepositoryMediator.GetPrimaryDataSource(null, definition);
				primaryDataSource = primaryDataSource != null && primaryDataSource.Mode.Equals(RepositoryMode.NoSQL)
					? primaryDataSource
					: null;
				if (primaryDataSource != null)
					try
					{
						tracker?.Invoke($"Ensure indexes of No SQL: {definition.Type} [{primaryDataSource.Name} @ {primaryDataSource.Mode} => {definition.CollectionName}]", null);
						if (RepositoryMediator.IsDebugEnabled)
							RepositoryMediator.WriteLogs($"Ensure indexes of No SQL: {definition.Type} [{primaryDataSource.Name} @ {primaryDataSource.Mode} => {definition.CollectionName}]", null);
						await definition.EnsureIndexesAsync(primaryDataSource, tracker, cancellationToken).ConfigureAwait(false);
					}
					catch (Exception ex)
					{
						tracker?.Invoke($"Error occurred while ensuring indexes of No SQL: {definition.Type} [{primaryDataSource.Name} @ {primaryDataSource.Mode} => {definition.CollectionName}]", ex);
						RepositoryMediator.WriteLogs($"Cannot ensure indexes of No SQL: {definition.Type} [{primaryDataSource.Name} @ {primaryDataSource.Mode} => {definition.CollectionName}]", ex);
					}

				var secondaryDataSource = RepositoryMediator.GetSecondaryDataSource(null, definition);
				secondaryDataSource = secondaryDataSource != null && secondaryDataSource.Mode.Equals(RepositoryMode.NoSQL)
					? secondaryDataSource
					: null;
				if (secondaryDataSource != null)
					try
					{
						tracker?.Invoke($"Ensure indexes of No SQL: {definition.Type} [{secondaryDataSource.Name} @ {secondaryDataSource.Mode} => {definition.CollectionName}]", null);
						if (RepositoryMediator.IsDebugEnabled)
							RepositoryMediator.WriteLogs($"Ensure indexes of No SQL: {definition.Type} [{secondaryDataSource.Name} @ {secondaryDataSource.Mode} => {definition.CollectionName}]", null);
						await definition.EnsureIndexesAsync(secondaryDataSource, tracker, cancellationToken).ConfigureAwait(false);
					}
					catch (Exception ex)
					{
						tracker?.Invoke($"Error occurred while ensuring indexes of No SQL: {definition.Type} [{secondaryDataSource.Name} @ {secondaryDataSource.Mode} => {definition.CollectionName}]", ex);
						RepositoryMediator.WriteLogs($"Cannot ensure indexes of No SQL: {definition.Type} [{secondaryDataSource.Name} @ {secondaryDataSource.Mode} => {definition.CollectionName}]", ex);
					}

				await RepositoryMediator.GetSyncDataSources(null, definition)
					.Where(dataSource => dataSource.Mode.Equals(RepositoryMode.NoSQL) && !dataSource.Name.IsEquals(primaryDataSource?.Name) && !dataSource.Name.IsEquals(secondaryDataSource?.Name))
					.ForEachAsync(async (dataSource, token) =>
					{
						try
						{
							tracker?.Invoke($"Ensure indexes of No SQL: {definition.Type} [{dataSource.Name} @ {dataSource.Mode} => {definition.CollectionName}]", null);
							if (RepositoryMediator.IsDebugEnabled)
								RepositoryMediator.WriteLogs($"Ensure indexes of No SQL: {definition.Type} [{dataSource.Name} @ {dataSource.Mode} => {definition.CollectionName}]", null);
							await definition.EnsureIndexesAsync(dataSource, tracker, cancellationToken).ConfigureAwait(false);
						}
						catch (Exception ex)
						{
							tracker?.Invoke($"Error occurred while ensuring indexes of No SQL: {definition.Type} [{dataSource.Name} @ {dataSource.Mode} => {definition.CollectionName}]", ex);
							RepositoryMediator.WriteLogs($"Cannot ensure indexes of No SQL: {definition.Type} [{dataSource.Name} @ {dataSource.Mode} => {definition.CollectionName}]", ex);
						}
					}, cancellationToken, true, false).ConfigureAwait(false);
			}, CancellationToken.None, true, false).ConfigureAwait(false);
		}
	}
}