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
				tracker?.Invoke($"Initialize & registering types of the assembly: {assembly.GetName().Name}", null);

				// repositories
				assembly.GetTypes()
					.Where(type => type.IsDefined(typeof(RepositoryAttribute), false))
					.ForEach(type =>
					{
						tracker?.Invoke($"Register the repository: {type.GetTypeName()}", null);
						RepositoryDefinition.Register(type);
					});

				// entities
				assembly.GetTypes()
					.Where(type => type.IsDefined(typeof(EntityAttribute), false))
					.ForEach(type =>
					{
						tracker?.Invoke($"Register the entity: {type.GetTypeName()}", null);
						EntityDefinition.Register(type);
					});
			}
			catch (ReflectionTypeLoadException ex)
			{
				if (ex.LoaderExceptions.FirstOrDefault(e => e is System.IO.FileNotFoundException) == null)
				{
					tracker?.Invoke($"Error occurred while registering types of the assembly: {assembly.GetName().Name}", ex);
					throw ex;
				}
			}
			catch (Exception ex)
			{
				tracker?.Invoke($"Error occurred while registering types of the assembly: {assembly.GetName().Name}", ex);
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
#if DEBUG
			RepositoryMediator.WriteLogs("Start to initialize repositories & entities [" + assemblies.Select(a => a.GetName().Name).ToString(", ") + "]");
#endif

			// initialize & register all types
			assemblies.ForEach(assembly => RepositoryStarter.Initialize(assembly, tracker));

			// read configuration and update
			if (ConfigurationManager.GetSection("net.vieapps.repositories") is AppConfigurationSectionHandler config)
				try
				{
					// update settings of data sources
					if (config.Section.SelectNodes("dataSources/dataSource") is XmlNodeList dataSourceNodes)
						foreach (XmlNode dataSourceNode in dataSourceNodes)
						{
							var dataSource = DataSource.FromJson(dataSourceNode.ToJson());
							if (!RepositoryMediator.DataSources.ContainsKey(dataSource.Name))
							{
								tracker?.Invoke($"Update setting of data-source [{dataSource.Name}]", null);
								RepositoryMediator.DataSources.Add(dataSource.Name, dataSource);
							}
						}

					// update settings of repositories
					if (config.Section.SelectNodes("repository") is XmlNodeList repositoryNodes)
						foreach (XmlNode repositoryNode in repositoryNodes)
						{
							// update repository
							RepositoryDefinition.Update(repositoryNode.ToJson(), tracker);

							// update repository entities
							if (repositoryNode.SelectNodes("entity") is XmlNodeList entityNodes)
								foreach (XmlNode repositoryEntityNode in entityNodes)
									EntityDefinition.Update(repositoryEntityNode.ToJson(), tracker);
						}

					// schemas (SQL)
					if ("true".IsEquals(config.Section.Attributes["ensureSchemas"]?.Value))
						Task.Run(async () =>
						{
							try
							{
								await RepositoryStarter.EnsureSqlSchemasAsync(tracker);
							}
							catch { }
						}).ConfigureAwait(false);

					// indexes (NoSQL)
					if ("true".IsEquals(config.Section.Attributes["ensureIndexes"]?.Value))
						Task.Run(async () =>
						{
							try
							{
								await RepositoryStarter.EnsureNoSqlIndexesAsync(tracker);
							}
							catch { }
						}).ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					tracker?.Invoke($"Error occurred while updating the repository: {ex.Message}", ex);
					throw ex;
				}

			else if (updateFromConfigurationFile)
				throw new ConfigurationErrorsException("Cannot find the configuration section named 'net.vieapps.repositories' in the configuration file");

			tracker?.Invoke($"Total of registered repositories: {RepositoryMediator.RepositoryDefinitions.Count}", null);
			tracker?.Invoke($"Total of registered entities: {RepositoryMediator.EntityDefinitions.Count}", null);
			tracker?.Invoke($"Total of registered data-sources: {RepositoryMediator.DataSources.Count}", null);
		}

		/// <summary>
		/// Initializes all types of all assemblies of the current app
		/// </summary>
		/// <param name="selector">The function to select assemblies to initialize</param>
		/// <param name="tracker">The tracker for tracking logs</param>
		public static void Initialize(Func<IEnumerable<Assembly>> selector = null, Action<string, Exception> tracker = null)
		{
			RepositoryStarter.Initialize(selector != null
				? selector()
				: new List<Assembly>() { Assembly.GetCallingAssembly() }.Concat(Assembly.GetCallingAssembly()
					.GetReferencedAssemblies()
					.Where(n => !n.Name.IsStartsWith("MsCorLib") && !n.Name.IsStartsWith("System") && !n.Name.IsStartsWith("Microsoft") && !n.Name.IsEquals("NETStandard")
						&& !n.Name.IsStartsWith("Newtonsoft") && !n.Name.IsStartsWith("WampSharp") && !n.Name.IsStartsWith("Castle.") && !n.Name.IsStartsWith("StackExchange.")
						&& !n.Name.IsStartsWith("MongoDB") && !n.Name.IsStartsWith("MySql") && !n.Name.IsStartsWith("Oracle") && !n.Name.IsStartsWith("Npgsql")
					)
					.Select(n => Assembly.Load(n))
				), 
				tracker
			);
		}

		static async Task EnsureSqlSchemasAsync(Action<string, Exception> tracker = null)
		{
			await RepositoryMediator.EntityDefinitions.ForEachAsync(async (definition, token) =>
			{
				var dataSource = RepositoryMediator.GetPrimaryDataSource(null, definition);
				if (dataSource != null && dataSource.Mode.Equals(RepositoryMode.SQL))
					try
					{
						tracker?.Invoke($"Ensure schemas: {definition.Type.ToString()} [{dataSource.Name} @ {dataSource.Mode} => {definition.TableName}]", null);
						await definition.EnsureSchemasAsync(dataSource);
					}
					catch (Exception ex)
					{
						tracker?.Invoke($"Error occurred while ensuring schemas: {ex.Message}", ex);
						RepositoryMediator.WriteLogs($"Cannot ensure schemas of SQL [{definition.Type.GetTypeName(true)}]: {ex.Message}", ex);
					}

				dataSource = RepositoryMediator.GetSecondaryDataSource(null, definition);
				if (dataSource != null && dataSource.Mode.Equals(RepositoryMode.SQL))
					try
					{
						tracker?.Invoke($"Ensure schemas: {definition.Type.ToString()} [{dataSource.Name} @ {dataSource.Mode} => {definition.TableName}]", null);
						await definition.EnsureSchemasAsync(dataSource);
					}
					catch (Exception ex)
					{
						tracker?.Invoke($"Error occurred while ensuring schemas: {ex.Message}", ex);
						RepositoryMediator.WriteLogs($"Cannot ensure schemas of SQL [{definition.Type.GetTypeName(true)}]: {ex.Message}", ex);
					}
			}, default(CancellationToken), true, false);
		}

		static async Task EnsureNoSqlIndexesAsync(Action<string, Exception> tracker = null)
		{
			await RepositoryMediator.EntityDefinitions.ForEachAsync(async (definition, ct) =>
			{
				var dataSource = RepositoryMediator.GetPrimaryDataSource(null, definition);
				if (dataSource != null && dataSource.Mode.Equals(RepositoryMode.NoSQL))
					try
					{
						tracker?.Invoke($"Ensure indexes: {definition.Type.ToString()} [{dataSource.Name} @ {dataSource.Mode} => {definition.CollectionName}]", null);
						await definition.EnsureIndexesAsync(dataSource);
					}
					catch (Exception ex)
					{
						tracker?.Invoke($"Error occurred while ensuring indexes: {ex.Message}", ex);
						RepositoryMediator.WriteLogs($"Cannot ensure indexes of NoSQL [{definition.Type.GetTypeName(true)}]: {ex.Message}", ex);
					}

				dataSource = RepositoryMediator.GetSecondaryDataSource(null, definition);
				if (dataSource != null && dataSource.Mode.Equals(RepositoryMode.NoSQL))
					try
					{
						tracker?.Invoke($"Ensure indexes: {definition.Type.ToString()} [{dataSource.Name} @ {dataSource.Mode} => {definition.CollectionName}]", null);
						await definition.EnsureIndexesAsync(dataSource);
					}
					catch (Exception ex)
					{
						tracker?.Invoke($"Error occurred while ensuring indexes: {ex.Message}", ex);
						RepositoryMediator.WriteLogs($"Cannot ensure indexes of NoSQL [{definition.Type.GetTypeName(true)}]: {ex.Message}", ex);
					}
			}, default(CancellationToken), true, false);
		}
	}
}