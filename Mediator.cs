#region Related components
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Data;
using System.Data.Common;
using System.Xml;
using System.Xml.Linq;

using MongoDB.Driver;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using net.vieapps.Components.Caching;
using net.vieapps.Components.Utility;
using net.vieapps.Components.Security;
#endregion

namespace net.vieapps.Components.Repository
{
	/// <summary>
	/// Allow to use the Repository functionality without direct reference to <see cref="RepositoryBase"/> class
	/// </summary>
	public static class RepositoryMediator
	{

#if DEBUG || PROCESSLOGS
		public static Dictionary<string, RepositoryDefinition> RepositoryDefinitions = new Dictionary<string, RepositoryDefinition>();
		public static Dictionary<string, EntityDefinition> EntityDefinitions = new Dictionary<string, EntityDefinition>();
		public static Dictionary<string, DataSource> DataSources = new Dictionary<string, DataSource>();
		public static List<Type> EventHandlers = new List<Type>();
#else
		internal static Dictionary<Type, RepositoryDefinition> RepositoryDefinitions = new Dictionary<Type, RepositoryDefinition>();
		internal static Dictionary<Type, EntityDefinition> EntityDefinitions = new Dictionary<Type, EntityDefinition>();
		internal static Dictionary<string, DataSource> DataSources = new Dictionary<string, DataSource>(StringComparer.OrdinalIgnoreCase);
		internal static List<Type> EventHandlers = new List<Type>();
#endif

		/// <summary>
		/// Gets the name of data-source that will be used as default storage of version contents
		/// </summary>
		public static string DefaultVersionDataSourceName { get; internal set; }

		/// <summary>
		/// Gets the name of data-source that will be used as default storage of trash contents
		/// </summary>
		public static string DefaultTrashDataSourceName { get; internal set; }

		#region Definitions
		/// <summary>
		/// Gets the repository definition that matched with the type
		/// </summary>
		/// <param name="type"></param>
		/// <returns></returns>
		public static RepositoryDefinition GetRepositoryDefinition(Type type)
		{
			return type != null
				? RepositoryMediator.RepositoryDefinitions[type]
				: null;
		}

		/// <summary>
		/// Gets the repository definition that matched with the type name
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <returns></returns>
		public static RepositoryDefinition GetRepositoryDefinition<T>() where T : class
		{
			return RepositoryMediator.GetRepositoryDefinition(typeof(T));
		}

		/// <summary>
		/// Gets the repository entity definition that matched with the type
		/// </summary>
		/// <param name="type"></param>
		/// <returns></returns>
		public static EntityDefinition GetEntityDefinition(Type type)
		{
			return type != null
				? RepositoryMediator.EntityDefinitions[type]
				: null;
		}

		/// <summary>
		/// Gets the repository entity definition that matched with the type of a class
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <returns></returns>
		public static EntityDefinition GetEntityDefinition<T>() where T : class
		{
			return RepositoryMediator.GetEntityDefinition(typeof(T)) ?? throw new InformationNotFoundException($"The entity definition [{typeof(T).GetTypeName()}] is not found");
		}
		#endregion

		#region Runtime repositories & entities
		/// <summary>
		/// Gets the runtime repositories (means business modules)  of a system (means an organization)
		/// </summary>
		/// <param name="systemID"></param>
		/// <returns></returns>
		public static List<IRepository> GetRuntimeRepositories(string systemID)
		{
			if (string.IsNullOrWhiteSpace(systemID))
				return null;

			var repositories = new List<IRepository>();
			RepositoryMediator.RepositoryDefinitions
				.Where(info => info.Value.RuntimeRepositories != null && info.Value.RuntimeRepositories.Count > 0)
				.ForEach(info => repositories = repositories.Concat(info.Value.RuntimeRepositories.Where(data => data.Value.SystemID.IsEquals(systemID)).Select(data => data.Value)).ToList());

			return repositories;
		}

		/// <summary>
		/// Gets the runtime repository (means business module) by identity
		/// </summary>
		/// <param name="repositoryID"></param>
		/// <returns></returns>
		public static IRepository GetRuntimeRepository(string repositoryID)
		{
			if (string.IsNullOrWhiteSpace(repositoryID))
				return null;

			var repositories = RepositoryMediator.RepositoryDefinitions
				.Where(info => info.Value.RuntimeRepositories.ContainsKey(repositoryID))
				.Select(info => info.Value.RuntimeRepositories)
				.FirstOrDefault();

			return repositories?[repositoryID];
		}

		/// <summary>
		/// Gets the runtime repository entity (means business content-type) by identity
		/// </summary>
		/// <param name="entityID"></param>
		/// <returns></returns>
		public static IRepositoryEntity GetRuntimeRepositoryEntity(string entityID)
		{
			if (string.IsNullOrWhiteSpace(entityID))
				return null;

			var entities = RepositoryMediator.EntityDefinitions
				.Where(info => info.Value.RuntimeEntities.ContainsKey(entityID))
				.Select(info => info.Value.RuntimeEntities)
				.FirstOrDefault();

			return entities?[entityID];
		}
		#endregion

		#region Data Source
		internal static void ConstructDataSources(XmlNodeList nodes, Action<string, Exception> tracker = null)
		{
			foreach (XmlNode node in nodes)
			{
				var dataSource = DataSource.FromJson(node.ToJson());
				if (!RepositoryMediator.DataSources.ContainsKey(dataSource.Name))
				{
					tracker?.Invoke($"Update settings of data-source [{dataSource.Name}] - {!string.IsNullOrWhiteSpace(dataSource.ConnectionString)}", null);
					RepositoryMediator.DataSources.Add(dataSource.Name, dataSource);
				}
			}
		}

		/// <summary>
		/// Gets the primary data source
		/// </summary>
		/// <param name="name">The string that presents name of a data source</param>
		/// <returns></returns>
		public static DataSource GetDataSource(string name)
		{
			return !string.IsNullOrWhiteSpace(name) && RepositoryMediator.DataSources.ContainsKey(name)
				? RepositoryMediator.DataSources[name]
				: null;
		}

		/// <summary>
		/// Gets the primary data source
		/// </summary>
		/// <param name="aliasTypeName">The string that presents alias of a type name</param>
		/// <param name="definition">The definition of a repository entity</param>
		/// <returns></returns>
		public static DataSource GetPrimaryDataSource(string aliasTypeName, EntityDefinition definition)
		{
			if (definition == null)
				return null;

			var dataSource = definition.PrimaryDataSource;
			if (dataSource == null)
			{
				var parent = !string.IsNullOrWhiteSpace(aliasTypeName)
					? RepositoryMediator.GetRepositoryDefinition(Type.GetType(aliasTypeName))
					: definition.RepositoryDefinition;

				if (parent != null)
					dataSource = parent.PrimaryDataSource;
			}

			if (dataSource == null)
				throw new RepositoryOperationException($"The primary data-source named '{definition.PrimaryDataSourceName}' (of '{definition.Type.GetTypeName()}') is not found");
			return dataSource;
		}

		/// <summary>
		/// Gets the primary data source
		/// </summary>
		/// <param name="context">The working context of a repository entity</param>
		/// <returns></returns>
		public static DataSource GetPrimaryDataSource(this RepositoryContext context)
		{
			return RepositoryMediator.GetPrimaryDataSource(context.AliasTypeName, context.EntityDefinition);
		}

		/// <summary>
		/// Gets the primary data source
		/// </summary>
		/// <param name="definition">The definition of a repository entity</param>
		/// <returns></returns>
		public static DataSource GetPrimaryDataSource(this EntityDefinition definition)
		{
			return RepositoryMediator.GetPrimaryDataSource(null, definition);
		}

		/// <summary>
		/// Gets the primary data source
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="aliasTypeName">The string that presents alias of a type name</param>
		/// <returns></returns>
		public static DataSource GetPrimaryDataSource<T>(string aliasTypeName = null)
		{
			return RepositoryMediator.GetPrimaryDataSource(aliasTypeName, RepositoryMediator.GetEntityDefinition(typeof(T)));
		}

		/// <summary>
		/// Gets the secondary data source
		/// </summary>
		/// <param name="aliasTypeName">The string that presents alias of a type name</param>
		/// <param name="definition">The definition of a repository entity</param>
		/// <returns></returns>
		public static DataSource GetSecondaryDataSource(string aliasTypeName, EntityDefinition definition)
		{
			if (definition == null)
				return null;

			var dataSource = definition.SecondaryDataSource;
			if (dataSource == null)
			{
				var parent = !string.IsNullOrWhiteSpace(aliasTypeName)
					? RepositoryMediator.GetRepositoryDefinition(Type.GetType(aliasTypeName))
					: definition.RepositoryDefinition;

				if (parent != null)
					dataSource = parent.SecondaryDataSource;
			}
			return dataSource;
		}

		/// <summary>
		/// Gets the secondary data source
		/// </summary>
		/// <param name="context">The working context of a repository entity</param>
		/// <returns></returns>
		public static DataSource GetSecondaryDataSource(this RepositoryContext context)
		{
			return RepositoryMediator.GetSecondaryDataSource(context.AliasTypeName, context.EntityDefinition);
		}

		/// <summary>
		/// Gets the secondary data source
		/// </summary>
		/// <param name="definition">The definition of a repository entity</param>
		/// <returns></returns>
		public static DataSource GetSecondaryDataSource(this EntityDefinition definition)
		{
			return RepositoryMediator.GetSecondaryDataSource(null, definition);
		}

		/// <summary>
		/// Gets the secondary data source
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="aliasTypeName">The string that presents alias of a type name</param>
		/// <returns></returns>
		public static DataSource GetSecondaryDataSource<T>(string aliasTypeName = null)
		{
			return RepositoryMediator.GetSecondaryDataSource(aliasTypeName, RepositoryMediator.GetEntityDefinition(typeof(T)));
		}

		/// <summary>
		/// Gets the sync data sources
		/// </summary>
		/// <param name="aliasTypeName">The string that presents alias of a type name</param>
		/// <param name="definition">The definition of a repository entity</param>
		/// <returns></returns>
		public static List<DataSource> GetSyncDataSources(string aliasTypeName, EntityDefinition definition)
		{
			if (definition == null)
				return new List<DataSource>();

			var dataSources = definition.SyncDataSources;
			if (dataSources == null)
			{
				var parent = !string.IsNullOrWhiteSpace(aliasTypeName)
					? RepositoryMediator.GetRepositoryDefinition(Type.GetType(aliasTypeName))
					: definition.RepositoryDefinition;

				if (parent != null)
					dataSources = parent.SyncDataSources;
			}
			return dataSources;
		}

		/// <summary>
		/// Gets the sync data sources
		/// </summary>
		/// <param name="context">The working context of a repository entity</param>
		/// <returns></returns>
		public static List<DataSource> GetSyncDataSources(this RepositoryContext context)
		{
			return RepositoryMediator.GetSyncDataSources(context.AliasTypeName, context.EntityDefinition);
		}

		/// <summary>
		/// Gets the sync data sources
		/// </summary>
		/// <param name="definition">The definition of a repository entity</param>
		/// <returns></returns>
		public static List<DataSource> GetSyncDataSources(this EntityDefinition definition)
		{
			return RepositoryMediator.GetSyncDataSources(null, definition);
		}

		/// <summary>
		/// Gets the sync data sources
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="aliasTypeName">The string that presents alias of a type name</param>
		/// <returns></returns>
		public static List<DataSource> GetSyncDataSources<T>(string aliasTypeName = null)
		{
			return RepositoryMediator.GetSyncDataSources(aliasTypeName, RepositoryMediator.GetEntityDefinition(typeof(T)));
		}

		/// <summary>
		/// Gets the data source that use to store versioning contents
		/// </summary>
		/// <param name="aliasTypeName">The string that presents alias of a type name</param>
		/// <param name="definition">The definition of a repository entity</param>
		/// <returns></returns>
		public static DataSource GetVersionDataSource(string aliasTypeName, EntityDefinition definition)
		{
			var dataSource = definition?.VersionDataSource;
			if (dataSource == null && definition != null)
			{
				var parent = !string.IsNullOrWhiteSpace(aliasTypeName)
					? RepositoryMediator.GetRepositoryDefinition(Type.GetType(aliasTypeName))
					: definition.RepositoryDefinition;

				if (parent != null)
					dataSource = parent.VersionDataSource;
			}

			return dataSource ?? RepositoryMediator.GetDataSource(RepositoryMediator.DefaultVersionDataSourceName);
		}

		/// <summary>
		/// Gets the data source that use to store versioning contents
		/// </summary>
		/// <param name="context">The working context of a repository entity</param>
		/// <returns></returns>
		public static DataSource GetVersionDataSource(this RepositoryContext context)
		{
			return RepositoryMediator.GetVersionDataSource(context.AliasTypeName, context.EntityDefinition);
		}

		/// <summary>
		/// Gets the data source that use to store versioning contents
		/// </summary>
		/// <param name="definition">The definition of a repository entity</param>
		/// <returns></returns>
		public static DataSource GetVersionDataSource(this EntityDefinition definition)
		{
			return RepositoryMediator.GetVersionDataSource(null, definition);
		}

		/// <summary>
		/// Gets the data source that use to store versioning contents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="aliasTypeName">The string that presents alias of a type name</param>
		/// <returns></returns>
		public static DataSource GetVersionDataSource<T>(string aliasTypeName = null)
		{
			return RepositoryMediator.GetVersionDataSource(aliasTypeName, RepositoryMediator.GetEntityDefinition(typeof(T)));
		}

		/// <summary>
		/// Gets the data source that use to store trash contents
		/// </summary>
		/// <param name="aliasTypeName">The string that presents alias of a type name</param>
		/// <param name="definition">The definition of a repository entity</param>
		/// <returns></returns>
		public static DataSource GetTrashDataSource(string aliasTypeName, EntityDefinition definition)
		{
			var dataSource = definition?.TrashDataSource;
			if (dataSource == null && definition != null)
			{
				var parent = !string.IsNullOrWhiteSpace(aliasTypeName)
					? RepositoryMediator.GetRepositoryDefinition(Type.GetType(aliasTypeName))
					: definition.RepositoryDefinition;

				if (parent != null)
					dataSource = parent.TrashDataSource;
			}

			return dataSource ?? RepositoryMediator.GetDataSource(RepositoryMediator.DefaultTrashDataSourceName);
		}

		/// <summary>
		/// Gets the data source that use to store trash contents
		/// </summary>
		/// <param name="context">The working context of a repository entity</param>
		/// <returns></returns>
		public static DataSource GetTrashDataSource(this RepositoryContext context)
		{
			return RepositoryMediator.GetTrashDataSource(context.AliasTypeName, context.EntityDefinition);
		}

		/// <summary>
		/// Gets the data source that use to store trash contents
		/// </summary>
		/// <param name="definition">The definition of a repository entity</param>
		/// <returns></returns>
		public static DataSource GetTrashDataSource(this EntityDefinition definition)
		{
			return RepositoryMediator.GetTrashDataSource(null, definition);
		}

		/// <summary>
		/// Gets the data source that use to store trash contents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="aliasTypeName">The string that presents alias of a type name</param>
		/// <returns></returns>
		public static DataSource GetTrashDataSource<T>(string aliasTypeName = null)
		{
			return RepositoryMediator.GetTrashDataSource(aliasTypeName, RepositoryMediator.GetEntityDefinition(typeof(T)));
		}
		#endregion

		#region Connection String
		/// <summary>
		/// Gets the settings of the connection string of the data source (for working with SQL/NoSQL database)
		/// </summary>
		/// <param name="name">The string that presents name of a connection string</param>
		/// <returns></returns>
		public static ConnectionStringSettings GetConnectionStringSettings(string name)
		{
			return !string.IsNullOrWhiteSpace(name)
				? ConfigurationManager.ConnectionStrings[name]
				: null;
		}

		/// <summary>
		/// Gets the settings of the connection string of the data source (for working with SQL/NoSQL database)
		/// </summary>
		/// <param name="dataSource">The data source</param>
		/// <returns></returns>
		public static ConnectionStringSettings GetConnectionStringSettings(this DataSource dataSource)
		{
			return RepositoryMediator.GetConnectionStringSettings(dataSource?.ConnectionStringName);
		}

		/// <summary>
		/// Gets the connection string of the data source (for working with SQL/NoSQL database)
		/// </summary>
		/// <param name="dataSource">The data source</param>
		/// <returns></returns>
		public static string GetConnectionString(this DataSource dataSource)
		{
			return dataSource.ConnectionString ?? RepositoryMediator.GetConnectionStringSettings(dataSource)?.ConnectionString;
		}
		#endregion

		#region Validate
		internal static bool Validate(EntityDefinition definition, Dictionary<string, object> stateData)
		{
			var changed = false;

			// standard properties
			foreach (var attribute in definition.Attributes)
			{
				if (attribute.IsIgnored() || !attribute.CanRead || !attribute.CanWrite)
					continue;

				object value = stateData[attribute.Name];

				if (value == null)
				{
					if (attribute.Name.Equals(definition.PrimaryKey))
						throw new InformationRequiredException("The value of the primary-key is required");
					else if (attribute.NotNull)
						throw new InformationRequiredException($"The value of the {(attribute.IsPublic ? "property" : "attribute")} named '{attribute.Name}' is required (doesn't allow null)");
				}

				else if (attribute.Type.IsStringType() && !attribute.IsCLOB)
				{
					if (attribute.NotEmpty && string.IsNullOrWhiteSpace(value as string))
						throw new InformationRequiredException($"The value of the {(attribute.IsPublic ? "property" : "attribute")} named '{attribute.Name}' is required (doesn't allow empty or null)");

					var maxLength = attribute.MaxLength > 0 ? attribute.MaxLength : 4000;
					if ((value as string).Length > maxLength)
					{
						changed = true;
						stateData[attribute.Name] = (value as string).Left(maxLength);
					}
				}
			}

			// extended properties
			// .....

			return changed;
		}
		#endregion

		#region Create
		/// <summary>
		/// Creates new instance of object in repository
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="object">The object to create new instance in repository</param>
		public static bool Create<T>(RepositoryContext context, DataSource dataSource, T @object) where T : class
		{
			try
			{
				// update context
				context.Operation = RepositoryOperation.Create;
				context.EntityDefinition = RepositoryMediator.GetEntityDefinition<T>();

				// validate & re-update object
				var currentState = context.SetCurrentState(@object);
				if (RepositoryMediator.Validate(context.EntityDefinition, currentState))
				{
					// re-update object
					currentState.ForEach(data =>
					{
						if (data.Key.StartsWith("ExtendedProperties."))
							(@object as IBusinessEntity).ExtendedProperties[data.Key.Replace("ExtendedProperties.", "")] = data.Value;
						else
							@object.SetAttributeValue(data.Key, data.Value);
					});

					// update state
					context.SetCurrentState(@object, currentState);
				}

				// call pre-create handlers
				if (context.CallPreCreateHandlers(@object, false))
					return false;

				// prepare data source
				dataSource = dataSource ?? context.GetPrimaryDataSource();

				// create
				if (dataSource.Mode.Equals(RepositoryMode.NoSQL))
					context.Create<T>(dataSource, @object);
				else if (dataSource.Mode.Equals(RepositoryMode.SQL))
					context.Create<T>(dataSource, @object);

				// update in cache storage
				if (context.EntityDefinition.Cache != null)
#if DEBUG || PROCESSLOGS
					if (context.EntityDefinition.CacheStorage.Set(@object))
						RepositoryMediator.WriteLogs("CREATE: Add the object into the cache storage successful [" + @object.GetCacheKey(false) + "]");
#else
					context.EntityDefinition.Cache.Set(@object);
#endif

				// call post-handlers
				context.CallPostCreateHandlers(@object, false);
				return true;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				throw new RepositoryOperationException($"Error occurred while creating new object [{typeof(T).ToString()}]", ex);
			}
		}

		/// <summary>
		/// Creates new instance of object in repository
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object to create new instance in repository</param>
		public static void Create<T>(RepositoryContext context, string aliasTypeName, T @object) where T : class
		{
			context.AliasTypeName = aliasTypeName;
			if (RepositoryMediator.Create<T>(context, context.GetPrimaryDataSource(), @object))
			{
				// secondary data source
				var secondaryDataSource = context.GetSecondaryDataSource();
				if (secondaryDataSource != null)
					Task.Run(() =>
					{
						try
						{
							if (secondaryDataSource.Mode.Equals(RepositoryMode.NoSQL))
								context.Create<T>(secondaryDataSource, @object);
							else if (secondaryDataSource.Mode.Equals(RepositoryMode.SQL))
								context.Create<T>(secondaryDataSource, @object);
						}
						catch { }
					}).ConfigureAwait(false);

				// sync data sources
				context.GetSyncDataSources()
					.ForEach(syncDataSource =>
					{
						Task.Run(() =>
						{
							try
							{
								if (syncDataSource.Mode.Equals(RepositoryMode.NoSQL))
									context.Create<T>(syncDataSource, @object);
								else if (syncDataSource.Mode.Equals(RepositoryMode.SQL))
									context.Create<T>(syncDataSource, @object);
							}
							catch { }
						}).ConfigureAwait(false);
					});
			}
		}

		/// <summary>
		/// Creates new instance of object in repository
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object to create instance in repository</param>
		public static void Create<T>(string aliasTypeName, T @object) where T : class
		{
			using (var context = new RepositoryContext())
			{
				RepositoryMediator.Create(context, aliasTypeName, @object);
			}
		}

		/// <summary>
		/// Creates new instance of object in repository
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="object">The object to create instance in repository</param>
		/// <param name="cancellationToken">The cancellation token</param>
		public static async Task<bool> CreateAsync<T>(RepositoryContext context, DataSource dataSource, T @object, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			try
			{
				// update context
				context.Operation = RepositoryOperation.Create;
				context.EntityDefinition = RepositoryMediator.GetEntityDefinition<T>();

				// validate & re-update object
				var currentState = context.SetCurrentState(@object);
				if (RepositoryMediator.Validate(context.EntityDefinition, currentState))
				{
					// re-update object
					currentState.ForEach(data =>
					{
						if (data.Key.StartsWith("ExtendedProperties."))
							(@object as IBusinessEntity).ExtendedProperties[data.Key.Replace("ExtendedProperties.", "")] = data.Value;
						else
							@object.SetAttributeValue(data.Key, data.Value);
					});

					// update state
					context.SetCurrentState(@object, currentState);
				}

				// call pre-handlers
				if (await context.CallPreCreateHandlersAsync(@object, false, cancellationToken).ConfigureAwait(false))
					return false;

				// prepare data source
				dataSource = dataSource ?? context.GetPrimaryDataSource();

				// create
				if (dataSource.Mode.Equals(RepositoryMode.NoSQL))
					await context.CreateAsync<T>(dataSource, @object, null, cancellationToken).ConfigureAwait(false);
				else if (dataSource.Mode.Equals(RepositoryMode.SQL))
					await context.CreateAsync<T>(dataSource, @object, cancellationToken).ConfigureAwait(false);

				// update in cache storage
				if (context.EntityDefinition.Cache != null)
#if DEBUG || PROCESSLOGS
					if (await context.EntityDefinition.CacheStorage.SetAsync(@object).ConfigureAwait(false))
						RepositoryMediator.WriteLogs("CREATE: Add the object into the cache storage successful [" + @object.GetCacheKey(false) + "]");
#else
					await context.EntityDefinition.Cache.SetAsync(@object).ConfigureAwait(false);
#endif

				// call post-handlers
				await context.CallPostCreateHandlersAsync(@object, false, cancellationToken).ConfigureAwait(false);
				return true;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				throw new RepositoryOperationException($"Error occurred while creating new object [{typeof(T).ToString()}]", ex);
			}
		}

		/// <summary>
		/// Creates new instance of object in repository
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object to create instance in repository</param>
		/// <param name="cancellationToken">The cancellation token</param>
		public static async Task CreateAsync<T>(RepositoryContext context, string aliasTypeName, T @object, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			// process
			context.AliasTypeName = aliasTypeName;
			if (await RepositoryMediator.CreateAsync<T>(context, context.GetPrimaryDataSource(), @object, cancellationToken).ConfigureAwait(false))
			{
				// secondary data source
				var tasks = new List<Task>();
				var secondaryDataSource = context.GetSecondaryDataSource();
				if (secondaryDataSource != null)
					tasks.Add(Task.Run(async () =>
					{
						try
						{
							if (secondaryDataSource.Mode.Equals(RepositoryMode.NoSQL))
								await context.CreateAsync<T>(secondaryDataSource, @object, cancellationToken).ConfigureAwait(false);
							else if (secondaryDataSource.Mode.Equals(RepositoryMode.SQL))
								await context.CreateAsync<T>(secondaryDataSource, @object, cancellationToken).ConfigureAwait(false);
						}
						catch { }
					}));

				// sync data sources
				context.GetSyncDataSources()
					.ForEach(syncDataSource =>
					{
						tasks.Add(Task.Run(async () =>
						{
							try
							{
								if (syncDataSource.Mode.Equals(RepositoryMode.NoSQL))
									await context.CreateAsync<T>(syncDataSource, @object, cancellationToken).ConfigureAwait(false);
								else if (syncDataSource.Mode.Equals(RepositoryMode.SQL))
									await context.CreateAsync<T>(syncDataSource, @object, cancellationToken).ConfigureAwait(false);
							}
							catch { }
						}));
					});

				// force al tasks to run
				var task = Task.Run(async () => await Task.WhenAll(tasks).ConfigureAwait(false));
			}
		}

		/// <summary>
		/// Creates new instance of object in repository
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object to create instance in repository</param>
		public static async Task CreateAsync<T>(string aliasTypeName, T @object, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			using (var context = new RepositoryContext())
			{
				await RepositoryMediator.CreateAsync(context, aliasTypeName, @object, cancellationToken).ConfigureAwait(false);
			}
		}
		#endregion

		#region Get
		/// <summary>
		/// Gets the instance of a object from repository
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="id">The string that present identity (primary-key)</param>
		/// <param name="callHandlers">true to call event-handlers before processing</param>
		/// <param name="processCache">true to process cache (first check existed object, then update cache)</param>
		/// <returns></returns>
		public static T Get<T>(RepositoryContext context, DataSource dataSource, string id, bool callHandlers = true, bool processCache = true) where T : class
		{
			try
			{
				// prepare
				if (callHandlers)
				{
					context.Operation = RepositoryOperation.Get;
					context.EntityDefinition = RepositoryMediator.GetEntityDefinition<T>();
				}

				// call pre-handlers
				if (callHandlers && context.CallPreGetHandlers<T>(id))
					return null;

				// get cached object
				var @object = processCache && context.EntityDefinition.Cache != null
					? context.EntityDefinition.Cache.Fetch<T>(id)
					: null;

#if DEBUG || PROCESSLOGS
				if (@object != null)
					RepositoryMediator.WriteLogs("GET: The cached object is found [" + @object.GetCacheKey(false) + "]");
#endif

				// load from data store if got no cached
				if (@object == null)
				{
					dataSource = dataSource ?? context.GetPrimaryDataSource();
					@object = dataSource.Mode.Equals(RepositoryMode.NoSQL)
						? context.Get<T>(dataSource, id, null)
						: dataSource.Mode.Equals(RepositoryMode.SQL)
							? context.Get<T>(dataSource, id)
							: null;

					// when the object is not found, then get instance from secondary source
					if (@object == null)
					{
						var secondaryDataSource = context.GetSecondaryDataSource();
						if (secondaryDataSource != null && !secondaryDataSource.Name.IsEquals(dataSource.Name))
						{
							@object = secondaryDataSource.Mode.Equals(RepositoryMode.NoSQL)
								? context.Get<T>(secondaryDataSource, id, null)
								: secondaryDataSource.Mode.Equals(RepositoryMode.SQL)
									? context.Get<T>(secondaryDataSource, id)
									: null;

							// re-create object at primary data source
							if (@object == null)
							{
								if (dataSource.Mode.Equals(RepositoryMode.NoSQL))
									context.Create<T>(dataSource, @object);
								else if (dataSource.Mode.Equals(RepositoryMode.SQL))
									context.Create<T>(dataSource, @object);
							}
						}
					}

					// update into cache storage
					if (@object != null && processCache && context.EntityDefinition.Cache != null)
#if DEBUG || PROCESSLOGS
						if (context.EntityDefinition.CacheStorage.Set(@object))
							RepositoryMediator.WriteLogs("GET: Add the object into the cache storage successful [" + @object.GetCacheKey(false) + "]");
#else
						context.EntityDefinition.Cache.Set(@object);
#endif
				}

				// update state & call post-handlers
				if (callHandlers && @object != null)
				{
					context.SetCurrentState(@object);
					context.CallPostGetHandlers(@object);
				}

				// return the instance of object
				return @object;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				throw new RepositoryOperationException($"Error occurred while fetching object [{typeof(T).ToString()}]", ex);
			}
		}

		/// <summary>
		/// Gets the instance of a object from repository
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="id">The string that present identity (primary-key)</param>
		/// <param name="callHandlers">true to call event-handlers before processing</param>
		/// <param name="processCache">true to process cache (first check existed object, then update cache)</param>
		/// <returns></returns>
		public static T Get<T>(RepositoryContext context, string aliasTypeName, string id, bool callHandlers = true, bool processCache = true) where T : class
		{
			context.AliasTypeName = aliasTypeName;
			return RepositoryMediator.Get<T>(context, context.GetPrimaryDataSource(), id, callHandlers, processCache);
		}

		/// <summary>
		/// Gets the instance of a object from repository
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="id">The string that present identity (primary-key)</param>
		/// <returns></returns>
		public static T Get<T>(string aliasTypeName, string id) where T : class
		{
			using (var context = new RepositoryContext(false))
			{
				return RepositoryMediator.Get<T>(context, aliasTypeName, id);
			}
		}

		/// <summary>
		/// Gets the instance of a object from repository
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="id">The string that present identity (primary-key)</param>
		/// <param name="callHandlers">true to call event-handlers before processing</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <param name="processCache">true to process cache (first check existed object, then update cache)</param>
		/// <returns></returns>
		public static async Task<T> GetAsync<T>(RepositoryContext context, DataSource dataSource, string id, bool callHandlers = true, CancellationToken cancellationToken = default(CancellationToken), bool processCache = true) where T : class
		{
			try
			{
				// prepare
				if (callHandlers)
				{
					context.Operation = RepositoryOperation.Get;
					context.EntityDefinition = RepositoryMediator.GetEntityDefinition<T>();
				}

				// call pre-handlers
				if (callHandlers && await context.CallPreGetHandlersAsync<T>(id, cancellationToken).ConfigureAwait(false))
					return null;

				// get cached object
				var @object = processCache && context.EntityDefinition.Cache != null
					? await context.EntityDefinition.Cache.FetchAsync<T>(id).ConfigureAwait(false)
					: null;

#if DEBUG || PROCESSLOGS
				if (@object != null)
					RepositoryMediator.WriteLogs("GET: The cached object is found [" + @object.GetCacheKey() + "]");
#endif

				// load from data store if got no cached
				if (@object == null)
				{
					// load from primary data source
					dataSource = dataSource ?? context.GetPrimaryDataSource();
					@object = dataSource.Mode.Equals(RepositoryMode.NoSQL)
						? await context.GetAsync<T>(dataSource, id, null, cancellationToken).ConfigureAwait(false)
						: dataSource.Mode.Equals(RepositoryMode.SQL)
							? await context.GetAsync<T>(dataSource, id, cancellationToken).ConfigureAwait(false)
							: null;

					// when the object is not found, then get instance from secondary source
					if (@object == null)
					{
						var secondaryDataSource = context.GetSecondaryDataSource();
						if (secondaryDataSource != null && !secondaryDataSource.Name.IsEquals(dataSource.Name))
						{
							@object = secondaryDataSource.Mode.Equals(RepositoryMode.NoSQL)
								? await context.GetAsync<T>(secondaryDataSource, id, null, cancellationToken).ConfigureAwait(false)
								: secondaryDataSource.Mode.Equals(RepositoryMode.SQL)
									? await context.GetAsync<T>(secondaryDataSource, id, cancellationToken).ConfigureAwait(false)
									: null;

							// re-create object at primary data source
							if (@object == null)
							{
								if (dataSource.Mode.Equals(RepositoryMode.NoSQL))
									await context.CreateAsync<T>(dataSource, @object, cancellationToken).ConfigureAwait(false);
								else if (dataSource.Mode.Equals(RepositoryMode.SQL))
									await context.CreateAsync<T>(dataSource, @object, cancellationToken).ConfigureAwait(false);
							}
						}
					}

					// update into cache storage
					if (@object != null && processCache && context.EntityDefinition.Cache != null)
#if DEBUG || PROCESSLOGS
						if (await context.EntityDefinition.CacheStorage.SetAsync(@object).ConfigureAwait(false))
							RepositoryMediator.WriteLogs("GET: Add the object into the cache storage successful [" + @object.GetCacheKey(false) + "]");
#else
						await context.EntityDefinition.Cache.SetAsync(@object).ConfigureAwait(false);
#endif
				}

				// update state & call post-handlers
				if (callHandlers && @object != null)
				{
					context.SetCurrentState(@object);
					await context.CallPostGetHandlersAsync(@object, cancellationToken).ConfigureAwait(false);
				}

				// return the instance of object
				return @object;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				throw new RepositoryOperationException($"Error occurred while fetching object [{typeof(T).ToString()}]", ex);
			}
		}

		/// <summary>
		/// Gets the instance of a object from repository
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="id">The string that present identity (primary-key)</param>
		/// <param name="callHandlers">true to call event-handlers before processing</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <param name="processCache">true to process cache (first check existed object, then update cache)</param>
		/// <returns></returns>
		public static Task<T> GetAsync<T>(RepositoryContext context, string aliasTypeName, string id, bool callHandlers = true, CancellationToken cancellationToken = default(CancellationToken), bool processCache = true) where T : class
		{
			context.AliasTypeName = aliasTypeName;
			return RepositoryMediator.GetAsync<T>(context, context.GetPrimaryDataSource(), id, callHandlers, cancellationToken, processCache);
		}

		/// <summary>
		/// Gets the instance of a object from repository
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="id">The string that present identity (primary-key)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<T> GetAsync<T>(string aliasTypeName, string id, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			using (var context = new RepositoryContext(false))
			{
				return await RepositoryMediator.GetAsync<T>(context, aliasTypeName, id, true, cancellationToken).ConfigureAwait(false);
			}
		}
		#endregion

		#region Get (first match)
		/// <summary>
		/// Finds the first instance of object that matched with the filter
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="filter">Filter expression</param>
		/// <param name="sort">Sort expression</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns></returns>
		public static T Get<T>(RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, SortBy<T> sort = null, string businessEntityID = null) where T : class
		{
			try
			{
				// prepare
				context.Operation = RepositoryOperation.Get;
				context.EntityDefinition = RepositoryMediator.GetEntityDefinition<T>();
				dataSource = dataSource ?? context.GetPrimaryDataSource();

				// find
				return dataSource.Mode.Equals(RepositoryMode.NoSQL)
					? context.Get<T>(dataSource, filter, sort, businessEntityID, null)
					: dataSource.Mode.Equals(RepositoryMode.SQL)
						? context.Get<T>(dataSource, filter, sort, businessEntityID)
						: null;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				throw new RepositoryOperationException($"Error occurred while fetching first-matched object [{typeof(T).ToString()}]", ex);
			}
		}

		/// <summary>
		/// Finds the first instance of object that matched with the filter
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">Filter expression</param>
		/// <param name="sort">Sort expression</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns></returns>
		public static T Get<T>(RepositoryContext context, string aliasTypeName, IFilterBy<T> filter, SortBy<T> sort = null, string businessEntityID = null) where T : class
		{
			context.AliasTypeName = aliasTypeName;
			return RepositoryMediator.Get<T>(context, context.GetPrimaryDataSource(), filter, sort, businessEntityID);
		}

		/// <summary>
		/// Finds the first instance of object that matched with the filter
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">Filter expression</param>
		/// <param name="sort">Sort expression</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns></returns>
		public static T Get<T>(string aliasTypeName, IFilterBy<T> filter, SortBy<T> sort = null, string businessEntityID = null) where T : class
		{
			using (var context = new RepositoryContext(false))
			{
				return RepositoryMediator.Get<T>(context, aliasTypeName, filter, sort);
			}
		}

		/// <summary>
		/// Finds the first instance of object that matched with the filter
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="filter">Filter expression</param>
		/// <param name="sort">Sort expression</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<T> GetAsync<T>(RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, SortBy<T> sort = null, string businessEntityID = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			try
			{
				// prepare
				context.Operation = RepositoryOperation.Get;
				context.EntityDefinition = RepositoryMediator.GetEntityDefinition<T>();
				dataSource = dataSource ?? context.GetPrimaryDataSource();

				// find
				return dataSource.Mode.Equals(RepositoryMode.NoSQL)
					? await context.GetAsync<T>(dataSource, filter, sort, businessEntityID, null, cancellationToken).ConfigureAwait(false)
					: dataSource.Mode.Equals(RepositoryMode.SQL)
						? await context.GetAsync<T>(dataSource, filter, sort, businessEntityID, cancellationToken).ConfigureAwait(false)
						: null;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				throw new RepositoryOperationException($"Error occurred while fetching first-matched object [{typeof(T).ToString()}]", ex);
			}
		}

		/// <summary>
		/// Finds the first instance of object that matched with the filter
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">Filter expression</param>
		/// <param name="sort">Sort expression</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<T> GetAsync<T>(RepositoryContext context, string aliasTypeName, IFilterBy<T> filter, SortBy<T> sort = null, string businessEntityID = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			context.AliasTypeName = aliasTypeName;
			return RepositoryMediator.GetAsync<T>(context, context.GetPrimaryDataSource(), filter, sort, businessEntityID, cancellationToken);
		}

		/// <summary>
		/// Finds the first instance of object that matched with the filter
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">Filter expression</param>
		/// <param name="sort">Sort expression</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<T> GetAsync<T>(string aliasTypeName, IFilterBy<T> filter, SortBy<T> sort = null, string businessEntityID = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			using (var context = new RepositoryContext(false))
			{
				return await RepositoryMediator.GetAsync<T>(context, aliasTypeName, filter, sort, businessEntityID, cancellationToken).ConfigureAwait(false);
			}
		}
		#endregion

		#region Get (by definition and identity)
		/// <summary>
		/// Gets an object by definition and identity
		/// </summary>
		/// <param name="definition">The definition</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="id">The identity</param>
		/// <returns></returns>
		public static object Get(EntityDefinition definition, DataSource dataSource, string id)
		{
			// check
			if (definition == null || string.IsNullOrWhiteSpace(id) || !id.IsValidUUID())
				return null;

			// get cached object
			var @object = definition.Cache != null
				? definition.Cache.Get(definition.Type.GetTypeName(true) + "#" + id.Trim().ToLower())
				: null;

#if DEBUG || PROCESSLOGS
			if (@object != null)
				RepositoryMediator.WriteLogs("GET: The cached object is found [" + @object.GetCacheKey(false) + "]");
#endif

			// load from data store if got no cached
			if (@object == null)
			{
				dataSource = dataSource ?? definition.GetPrimaryDataSource();
				@object = dataSource.Mode.Equals(RepositoryMode.NoSQL)
					? NoSqlHelper.Get(definition, id)
					: dataSource.Mode.Equals(RepositoryMode.SQL)
						? SqlHelper.Get(definition, id)
						: null;

				// TO DO: check to get instance from secondary source if primary source is not available

				// update into cache storage
				if (@object != null && definition.Cache != null)
#if DEBUG || PROCESSLOGS
					if (definition.CacheStorage.Set(@object))
						RepositoryMediator.WriteLogs("GET: Add the object into the cache storage successful [" + @object.GetCacheKey(false) + "]");
#else
					definition.Cache.Set(@object);
#endif
			}

			// return the instance of object
			return @object;
		}

		/// <summary>
		/// Gets an object by definition and identity
		/// </summary>
		/// <param name="definition">The definition</param>
		/// <param name="id">The identity</param>
		/// <returns></returns>
		public static object Get(EntityDefinition definition, string id)
		{
			return RepositoryMediator.Get(definition, null, id);
		}

		/// <summary>
		/// Gets an object by definition and identity
		/// </summary>
		/// <param name="definitionID">The identity of the entity definition</param>
		/// <param name="objectID">The identity of the object</param>
		/// <returns></returns>
		public static object Get(string definitionID, string objectID)
		{
			var entity = !string.IsNullOrWhiteSpace(definitionID)
				? RepositoryMediator.GetRuntimeRepositoryEntity(definitionID)
				: null;
			return entity != null
				? RepositoryMediator.Get(entity.Definition, objectID)
				: null;
		}

		/// <summary>
		/// Gets an object by definition and identity
		/// </summary>
		/// <param name="definition">The definition</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="id">The identity</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<object> GetAsync(EntityDefinition definition, DataSource dataSource, string id, CancellationToken cancellationToken = default(CancellationToken))
		{
			// check
			if (definition == null || string.IsNullOrWhiteSpace(id) || !id.IsValidUUID())
				return null;

			// get cached object
			var @object = definition.Cache != null
				? await definition.Cache.GetAsync(definition.Type.GetTypeName(true) + "#" + id.Trim().ToLower()).ConfigureAwait(false)
				: null;

#if DEBUG || PROCESSLOGS
			if (@object != null)
				RepositoryMediator.WriteLogs("GET: The cached object is found [" + @object.GetCacheKey(false) + "]");
#endif

			// load from data store if got no cached
			if (@object == null)
			{
				dataSource = dataSource ?? definition.GetPrimaryDataSource();
				@object = dataSource.Mode.Equals(RepositoryMode.NoSQL)
					? await NoSqlHelper.GetAsync(definition, id, null, cancellationToken).ConfigureAwait(false)
					: dataSource.Mode.Equals(RepositoryMode.SQL)
						? await SqlHelper.GetAsync(definition, id, cancellationToken).ConfigureAwait(false)
						: null;

				// TO DO: check to get instance from secondary source if primary source is not available

				// update into cache storage
				if (@object != null && definition.Cache != null)
#if DEBUG || PROCESSLOGS
					if (await definition.CacheStorage.SetAsync(@object).ConfigureAwait(false))
						RepositoryMediator.WriteLogs("GET: Add the object into the cache storage successful [" + @object.GetCacheKey(false) + "]");
#else
					await definition.Cache.SetAsync(@object).ConfigureAwait(false);
#endif
			}

			// return the instance of object
			return @object;
		}

		/// <summary>
		/// Gets an object by definition and identity
		/// </summary>
		/// <param name="definition">The definition</param>
		/// <param name="id">The identity</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<object> GetAsync(EntityDefinition definition, string id, CancellationToken cancellationToken = default(CancellationToken))
		{
			return RepositoryMediator.GetAsync(definition, null, id, cancellationToken);
		}

		/// <summary>
		/// Gets an object by definition and identity
		/// </summary>
		/// <param name="definitionID">The identity of the entity definition</param>
		/// <param name="objectID">The identity of the object</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<object> GetAsync(string definitionID, string objectID, CancellationToken cancellationToken = default(CancellationToken))
		{
			var entity = !string.IsNullOrWhiteSpace(definitionID)
				? RepositoryMediator.GetRuntimeRepositoryEntity(definitionID)
				: null;
			return entity != null
				? RepositoryMediator.GetAsync(entity.Definition, objectID, cancellationToken)
				: null;
		}
		#endregion

		#region Replace
		/// <summary>
		/// Updates instance of object into repository (using replace method)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="object">The object that presents the instance in repository need to be updated</param>
		/// <param name="dontCreateNewVersion">Force to not create new version when update the object</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		public static bool Replace<T>(RepositoryContext context, DataSource dataSource, T @object, bool dontCreateNewVersion = false, string userID = null) where T : class
		{
			try
			{
				// prepare
				context.Operation = RepositoryOperation.Update;
				context.EntityDefinition = RepositoryMediator.GetEntityDefinition<T>();

				// check state
				var previousInstance = @object != null
					? RepositoryMediator.Get<T>(context, dataSource, @object.GetEntityID(), false)
					: null;

				var previousState = previousInstance != null
					? context.SetPreviousState(previousInstance)
					: null;

				var currentState = context.SetCurrentState(@object);
				var dirtyAttributes = context.FindDirty(previousState, currentState);
				if (dirtyAttributes.Count < 1)
					return false;

				// validate & re-update object
				if (RepositoryMediator.Validate(context.EntityDefinition, currentState))
				{
					// re-update object
					currentState.ForEach(data =>
					{
						if (data.Key.StartsWith("ExtendedProperties."))
							(@object as IBusinessEntity).ExtendedProperties[data.Key.Replace("ExtendedProperties.", "")] = data.Value;
						else
							@object.SetAttributeValue(data.Key, data.Value);
					});

					// update state
					context.SetCurrentState(@object, currentState);
				}

				// call pre-handlers
				if (context.CallPreUpdateHandlers(@object, dirtyAttributes, false))
					return false;

				// reset search score
				if (@object is RepositoryBase)
					(@object as RepositoryBase).SearchScore = null;

				// create new version
				if (!dontCreateNewVersion && previousInstance != null)
				{
					userID = userID ?? (@object is IBusinessEntity ? (@object as IBusinessEntity).LastModifiedID : @object.GetAttributeValue("LastModifiedID") as string);
					var createNewVersion = context.EntityDefinition.CreateNewVersionWhenUpdated;
					if (@object is IBusinessEntity)
					{
						var runtimeEntity = RepositoryMediator.GetRuntimeRepositoryEntity((@object as IBusinessEntity).EntityID);
						createNewVersion = runtimeEntity != null
							? runtimeEntity.CreateNewVersionWhenUpdated
							: context.EntityDefinition.CreateNewVersionWhenUpdated;
					}

					if (createNewVersion)
						RepositoryMediator.CreateVersion<T>(context, previousInstance, userID);
				}

				// update
				dataSource = dataSource ?? context.GetPrimaryDataSource();
				if (dataSource.Mode.Equals(RepositoryMode.NoSQL))
					context.Replace<T>(dataSource, @object, null);
				else if (dataSource.Mode.Equals(RepositoryMode.SQL))
					context.Replace<T>(dataSource, @object);

				// update into cache storage
				if (context.EntityDefinition.Cache != null)
#if DEBUG || PROCESSLOGS
					if (context.EntityDefinition.CacheStorage.Set(@object))
						RepositoryMediator.WriteLogs("REPLACE: Add the object into the cache storage successful [" + @object.GetCacheKey(false) + "]");
#else
					context.EntityDefinition.Cache.Set(@object);
#endif

				// call post-handlers
				context.CallPostUpdateHandlers(@object, dirtyAttributes, false);
				return true;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				throw new RepositoryOperationException($"Error occurred while replacing object [{typeof(T).ToString()}]", ex);
			}
		}

		/// <summary>
		/// Updates instance of object into repository (using replace method)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object that presents the instance in repository need to be updated</param>
		/// <param name="dontCreateNewVersion">Force to not create new version when update the object</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		public static void Replace<T>(RepositoryContext context, string aliasTypeName, T @object, bool dontCreateNewVersion = false, string userID = null) where T : class
		{
			context.AliasTypeName = aliasTypeName;
			if (RepositoryMediator.Replace<T>(context, context.GetPrimaryDataSource(), @object, dontCreateNewVersion, userID))
			{
				// secondary data source
				var secondaryDataSource = context.GetSecondaryDataSource();
				if (secondaryDataSource != null)
					Task.Run(() =>
					{
						try
						{
							if (secondaryDataSource.Mode.Equals(RepositoryMode.NoSQL))
								context.Replace<T>(secondaryDataSource, @object);
							else if (secondaryDataSource.Mode.Equals(RepositoryMode.SQL))
								context.Replace<T>(secondaryDataSource, @object);
						}
						catch { }
					}).ConfigureAwait(false);

				// sync data sources
				context.GetSyncDataSources()
					.ForEach(syncDataSource =>
					{
						Task.Run(() =>
						{
							try
							{
								if (syncDataSource.Mode.Equals(RepositoryMode.NoSQL))
									context.Replace<T>(syncDataSource, @object);
								else if (syncDataSource.Mode.Equals(RepositoryMode.SQL))
									context.Replace<T>(syncDataSource, @object);
							}
							catch { }
						}).ConfigureAwait(false);
					});
			}
		}

		/// <summary>
		/// Updates instance of object into repository (using replace method)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object that presents the instance in repository need to be updated</param>
		/// <param name="dontCreateNewVersion">Force to not create new version when update the object</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		public static void Replace<T>(string aliasTypeName, T @object, bool dontCreateNewVersion = false, string userID = null) where T : class
		{
			using (var context = new RepositoryContext())
			{
				RepositoryMediator.Replace<T>(context, aliasTypeName, @object, dontCreateNewVersion, userID);
			}
		}

		/// <summary>
		/// Updates instance of object into repository (using replace method)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="object">The object that presents the instance in repository need to be updated</param>
		/// <param name="dontCreateNewVersion">Force to not create new version when update the object</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		public static async Task<bool> ReplaceAsync<T>(RepositoryContext context, DataSource dataSource, T @object, bool dontCreateNewVersion = false, string userID = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			try
			{
				// prepare
				context.Operation = RepositoryOperation.Update;
				context.EntityDefinition = RepositoryMediator.GetEntityDefinition<T>();

				// check state
				var previousInstance = @object != null
					? await RepositoryMediator.GetAsync<T>(context, dataSource, @object.GetEntityID(), false, cancellationToken).ConfigureAwait(false)
					: null;

				var previousState = previousInstance != null
					? context.SetPreviousState(previousInstance)
					: null;

				var currentState = context.SetCurrentState(@object);
				var dirtyAttributes = context.FindDirty(previousState, currentState);
				if (dirtyAttributes.Count < 1)
					return false;

				// validate & re-update object
				if (RepositoryMediator.Validate(context.EntityDefinition, currentState))
				{
					// re-update object
					currentState.ForEach(data =>
					{
						if (data.Key.StartsWith("ExtendedProperties."))
							(@object as IBusinessEntity).ExtendedProperties[data.Key.Replace("ExtendedProperties.", "")] = data.Value;
						else
							@object.SetAttributeValue(data.Key, data.Value);
					});

					// update state
					context.SetCurrentState(@object, currentState);
				}

				// call pre-handlers
				if (await context.CallPreUpdateHandlersAsync(@object, dirtyAttributes, false, cancellationToken).ConfigureAwait(false))
					return false;

				// reset search score
				if (@object is RepositoryBase)
					(@object as RepositoryBase).SearchScore = null;

				// create new version
				if (!dontCreateNewVersion && previousInstance != null)
				{
					userID = userID ?? (@object is IBusinessEntity ? (@object as IBusinessEntity).LastModifiedID : @object.GetAttributeValue("LastModifiedID") as string);
					var createNewVersion = context.EntityDefinition.CreateNewVersionWhenUpdated;
					if (@object is IBusinessEntity)
					{
						var runtimeEntity = RepositoryMediator.GetRuntimeRepositoryEntity((@object as IBusinessEntity).EntityID);
						createNewVersion = runtimeEntity != null
							? runtimeEntity.CreateNewVersionWhenUpdated
							: context.EntityDefinition.CreateNewVersionWhenUpdated;
					}

					if (createNewVersion)
						await RepositoryMediator.CreateVersionAsync<T>(context, previousInstance, userID, cancellationToken).ConfigureAwait(false);
				}

				// update
				dataSource = dataSource ?? context.GetPrimaryDataSource();
				if (dataSource.Mode.Equals(RepositoryMode.NoSQL))
					await context.ReplaceAsync<T>(dataSource, @object, null, cancellationToken).ConfigureAwait(false);
				else if (dataSource.Mode.Equals(RepositoryMode.SQL))
					await context.ReplaceAsync<T>(dataSource, @object, cancellationToken).ConfigureAwait(false);

				// update into cache storage
				if (context.EntityDefinition.Cache != null)
#if DEBUG || PROCESSLOGS
					if (await context.EntityDefinition.CacheStorage.SetAsync(@object).ConfigureAwait(false))
						RepositoryMediator.WriteLogs("REPLACE: Add the object into the cache storage successful [" + @object.GetCacheKey(false) + "]");
#else
					await context.EntityDefinition.Cache.SetAsync(@object).ConfigureAwait(false);
#endif

				// call post-handlers
				await context.CallPostUpdateHandlersAsync(@object, dirtyAttributes, false, cancellationToken).ConfigureAwait(false);
				return true;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				throw new RepositoryOperationException($"Error occurred while replacing object [{typeof(T).ToString()}]", ex);
			}
		}

		/// <summary>
		/// Updates instance of object into repository (using replace method)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object that presents the instance in repository need to be updated</param>
		/// <param name="dontCreateNewVersion">Force to not create new version when update the object</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		public static async Task ReplaceAsync<T>(RepositoryContext context, string aliasTypeName, T @object, bool dontCreateNewVersion = false, string userID = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			context.AliasTypeName = aliasTypeName;
			if (await RepositoryMediator.ReplaceAsync<T>(context, context.GetPrimaryDataSource(), @object, dontCreateNewVersion, userID, cancellationToken).ConfigureAwait(false))
			{
				// secondary data source
				var tasks = new List<Task>();
				var secondaryDataSource = context.GetSecondaryDataSource();
				if (secondaryDataSource != null)
					tasks.Add(Task.Run(async () =>
					{
						try
						{
							if (secondaryDataSource.Mode.Equals(RepositoryMode.NoSQL))
								await context.ReplaceAsync<T>(secondaryDataSource, @object, cancellationToken).ConfigureAwait(false);
							else if (secondaryDataSource.Mode.Equals(RepositoryMode.SQL))
								await context.ReplaceAsync<T>(secondaryDataSource, @object, cancellationToken).ConfigureAwait(false);
						}
						catch { }
					}));

				// sync data sources
				context.GetSyncDataSources()
					.ForEach(syncDataSource =>
					{
						tasks.Add(Task.Run(async () =>
						{
							try
							{
								if (syncDataSource.Mode.Equals(RepositoryMode.NoSQL))
									await context.ReplaceAsync<T>(syncDataSource, @object, cancellationToken).ConfigureAwait(false);
								else if (syncDataSource.Mode.Equals(RepositoryMode.SQL))
									await context.ReplaceAsync<T>(syncDataSource, @object, cancellationToken).ConfigureAwait(false);
							}
							catch { }
						}));
					});

				// force al tasks to run
				var task = Task.Run(async () => await Task.WhenAll(tasks).ConfigureAwait(false));
			}
		}

		/// <summary>
		/// Updates instance of object into repository (using replace method)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object that presents the instance in repository need to be updated</param>
		/// <param name="dontCreateNewVersion">Force to not create new version when update the object</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		public static async Task ReplaceAsync<T>(string aliasTypeName, T @object, bool dontCreateNewVersion = false, string userID = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			using (var context = new RepositoryContext())
			{
				await RepositoryMediator.ReplaceAsync<T>(context, aliasTypeName, @object, dontCreateNewVersion, userID, cancellationToken).ConfigureAwait(false);
			}
		}
		#endregion

		#region Update
		/// <summary>
		/// Updates instance of object into repository (only update changed attributes)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="object">The object that presents the instance in repository need to be updated</param>
		/// <param name="dontCreateNewVersion">Force to not create new version when update the object</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		public static Tuple<bool, List<string>> Update<T>(RepositoryContext context, DataSource dataSource, T @object, bool dontCreateNewVersion = false, string userID = null) where T : class
		{
			try
			{
				// prepare
				context.Operation = RepositoryOperation.Update;
				context.EntityDefinition = RepositoryMediator.GetEntityDefinition<T>();

				// check state
				var previousInstance = @object != null
					? RepositoryMediator.Get<T>(context, dataSource, @object.GetEntityID(), false)
					: null;

				var previousState = previousInstance != null
					? context.SetPreviousState(previousInstance)
					: null;

				var currentState = context.SetCurrentState(@object);
				var dirtyAttributes = context.FindDirty(previousState, currentState);
				if (dirtyAttributes.Count < 1)
					return new Tuple<bool, List<string>>(false, null);

				// validate & re-update object
				if (RepositoryMediator.Validate(context.EntityDefinition, currentState))
				{
					// re-update object
					currentState.ForEach(data =>
					{
						if (data.Key.StartsWith("ExtendedProperties."))
							(@object as IBusinessEntity).ExtendedProperties[data.Key.Replace("ExtendedProperties.", "")] = data.Value;
						else
							@object.SetAttributeValue(data.Key, data.Value);
					});

					// update state
					context.SetCurrentState(@object, currentState);
				}

				// call pre-handlers
				if (context.CallPreUpdateHandlers(@object, dirtyAttributes, false))
					return new Tuple<bool, List<string>>(false, null);

				// create new version
				if (!dontCreateNewVersion && previousInstance != null)
				{
					userID = userID ?? (@object is IBusinessEntity ? (@object as IBusinessEntity).LastModifiedID : @object.GetAttributeValue("LastModifiedID") as string);
					var createNewVersion = context.EntityDefinition.CreateNewVersionWhenUpdated;
					if (@object is IBusinessEntity)
					{
						var runtimeEntity = RepositoryMediator.GetRuntimeRepositoryEntity((@object as IBusinessEntity).EntityID);
						createNewVersion = runtimeEntity != null
							? runtimeEntity.CreateNewVersionWhenUpdated
							: context.EntityDefinition.CreateNewVersionWhenUpdated;
					}

					if (createNewVersion)
						RepositoryMediator.CreateVersion<T>(context, previousInstance, userID);
				}

				// update
				var updatedAttributes = dirtyAttributes.Select(item => item.StartsWith("ExtendedProperties.") ? item.Replace("ExtendedProperties.", "") : item).ToList();
				dataSource = dataSource ?? context.GetPrimaryDataSource();

				if (dataSource.Mode.Equals(RepositoryMode.NoSQL))
					context.Update<T>(dataSource, @object, updatedAttributes, null);
				else if (dataSource.Mode.Equals(RepositoryMode.SQL))
					context.Update<T>(dataSource, @object, updatedAttributes);

				// update into cache storage
				if (context.EntityDefinition.Cache != null)
#if DEBUG || PROCESSLOGS
					if (context.EntityDefinition.CacheStorage.Set(@object))
						RepositoryMediator.WriteLogs("UPDATE: Add the object into the cache storage successful [" + @object.GetCacheKey(false) + "]");
#else
					context.EntityDefinition.Cache.Set(@object);
#endif

				// call post-handlers
				context.CallPostUpdateHandlers(@object, dirtyAttributes, false);
				return new Tuple<bool, List<string>>(true, updatedAttributes);
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				throw new RepositoryOperationException($"Error occurred while updating object [{typeof(T).ToString()}]", ex);
			}
		}

		/// <summary>
		/// Updates instance of object into repository (only update changed attributes)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object that presents the instance in repository need to be updated</param>
		/// <param name="dontCreateNewVersion">Force to not create new version when update the object</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		public static void Update<T>(RepositoryContext context, string aliasTypeName, T @object, bool dontCreateNewVersion = false, string userID = null) where T : class
		{
			context.AliasTypeName = aliasTypeName;
			var result = RepositoryMediator.Update<T>(context, context.GetPrimaryDataSource(), @object, dontCreateNewVersion, userID);
			if (result.Item1)
			{
				// secondary data source
				var secondaryDataSource = context.GetSecondaryDataSource();
				if (secondaryDataSource != null)
					Task.Run(() =>
					{
						try
						{
							if (secondaryDataSource.Mode.Equals(RepositoryMode.NoSQL))
								context.Update<T>(secondaryDataSource, @object, result.Item2);
							else if (secondaryDataSource.Mode.Equals(RepositoryMode.SQL))
								context.Update<T>(secondaryDataSource, @object, result.Item2);
						}
						catch { }
					}).ConfigureAwait(false);

				// sync data sources
				context.GetSyncDataSources()
					.ForEach(syncDataSource =>
					{
						Task.Run(() =>
						{
							try
							{
								if (syncDataSource.Mode.Equals(RepositoryMode.NoSQL))
									context.Update<T>(syncDataSource, @object, result.Item2);
								else if (syncDataSource.Mode.Equals(RepositoryMode.SQL))
									context.Update<T>(syncDataSource, @object, result.Item2);
							}
							catch { }
						}).ConfigureAwait(false);
					});
			}
		}

		/// <summary>
		/// Updates instance of object into repository (only update changed attributes)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object that presents the instance in repository need to be updated</param>
		/// <param name="dontCreateNewVersion">Force to not create new version when update the object</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		public static void Update<T>(string aliasTypeName, T @object, bool dontCreateNewVersion = false, string userID = null) where T : class
		{
			using (var context = new RepositoryContext())
			{
				RepositoryMediator.Update<T>(context, aliasTypeName, @object, dontCreateNewVersion, userID);
			}
		}

		/// <summary>
		/// Updates instance of object into repository (only update changed attributes)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="object">The object that presents the instance in repository need to be updated</param>
		/// <param name="dontCreateNewVersion">Force to not create new version when update the object</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		public static async Task<Tuple<bool, List<string>>> UpdateAsync<T>(RepositoryContext context, DataSource dataSource, T @object, bool dontCreateNewVersion = false, string userID = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			try
			{
				// prepare
				context.Operation = RepositoryOperation.Update;
				context.EntityDefinition = RepositoryMediator.GetEntityDefinition<T>();

				// check state
				var previousInstance = @object != null
					? await RepositoryMediator.GetAsync<T>(context, dataSource, @object.GetEntityID(), false).ConfigureAwait(false)
					: null;

				var previousState = previousInstance != null
					? context.SetPreviousState(previousInstance)
					: null;

				var currentState = context.SetCurrentState(@object);
				var dirtyAttributes = context.FindDirty(previousState, currentState);
				if (dirtyAttributes.Count < 1)
					return new Tuple<bool, List<string>>(false, null);

				// validate & re-update object
				if (RepositoryMediator.Validate(context.EntityDefinition, currentState))
				{
					// re-update object
					currentState.ForEach(data =>
					{
						if (data.Key.StartsWith("ExtendedProperties."))
							(@object as IBusinessEntity).ExtendedProperties[data.Key.Replace("ExtendedProperties.", "")] = data.Value;
						else
							@object.SetAttributeValue(data.Key, data.Value);
					});

					// update state
					context.SetCurrentState(@object, currentState);
				}

				// call pre-handlers
				if (await context.CallPreUpdateHandlersAsync(@object, dirtyAttributes, false, cancellationToken).ConfigureAwait(false))
					return new Tuple<bool, List<string>>(false, null);

				// create new version
				if (!dontCreateNewVersion && previousInstance != null)
				{
					userID = userID ?? (@object is IBusinessEntity ? (@object as IBusinessEntity).LastModifiedID : @object.GetAttributeValue("LastModifiedID") as string);
					var createNewVersion = context.EntityDefinition.CreateNewVersionWhenUpdated;
					if (@object is IBusinessEntity)
					{
						var runtimeEntity = RepositoryMediator.GetRuntimeRepositoryEntity((@object as IBusinessEntity).EntityID);
						createNewVersion = runtimeEntity != null
							? runtimeEntity.CreateNewVersionWhenUpdated
							: context.EntityDefinition.CreateNewVersionWhenUpdated;
					}

					if (createNewVersion)
						await RepositoryMediator.CreateVersionAsync<T>(context, previousInstance, userID, cancellationToken).ConfigureAwait(false);
				}

				// update
				var updatedAttributes = dirtyAttributes.Select(item => item.StartsWith("ExtendedProperties.") ? item.Replace("ExtendedProperties.", "") : item).ToList();
				dataSource = dataSource ?? context.GetPrimaryDataSource();

				if (dataSource.Mode.Equals(RepositoryMode.NoSQL))
					await context.UpdateAsync<T>(dataSource, @object, updatedAttributes, null, cancellationToken).ConfigureAwait(false);
				else if (dataSource.Mode.Equals(RepositoryMode.SQL))
					await context.UpdateAsync<T>(dataSource, @object, updatedAttributes, cancellationToken).ConfigureAwait(false);

				// update into cache storage
				if (context.EntityDefinition.Cache != null)
#if DEBUG || PROCESSLOGS
					if (await context.EntityDefinition.CacheStorage.SetAsync(@object).ConfigureAwait(false))
						RepositoryMediator.WriteLogs("UPDATE: Add the object into the cache storage successful [" + @object.GetCacheKey(false) + "]");
#else
					await context.EntityDefinition.Cache.SetAsync(@object).ConfigureAwait(false);
#endif

				// call post-handlers
				await context.CallPostUpdateHandlersAsync(@object, dirtyAttributes, false, cancellationToken).ConfigureAwait(false);
				return new Tuple<bool, List<string>>(true, updatedAttributes);
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				throw new RepositoryOperationException($"Error occurred while updating object [{typeof(T).ToString()}]", ex);
			}
		}

		/// <summary>
		/// Updates instance of object into repository (only update changed attributes)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object that presents the instance in repository need to be updated</param>
		/// <param name="dontCreateNewVersion">Force to not create new version when update the object</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		public static async Task UpdateAsync<T>(RepositoryContext context, string aliasTypeName, T @object, bool dontCreateNewVersion = false, string userID = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			context.AliasTypeName = aliasTypeName;
			var result = await RepositoryMediator.UpdateAsync<T>(context, context.GetPrimaryDataSource(), @object, dontCreateNewVersion, userID, cancellationToken).ConfigureAwait(false);
			if (result.Item1)
			{
				// secondary data source
				var tasks = new List<Task>();
				var secondaryDataSource = context.GetSecondaryDataSource();
				if (secondaryDataSource != null)
					tasks.Add(Task.Run(async () =>
					{
						try
						{
							if (secondaryDataSource.Mode.Equals(RepositoryMode.NoSQL))
								await context.UpdateAsync<T>(secondaryDataSource, @object, result.Item2, cancellationToken).ConfigureAwait(false);
							else if (secondaryDataSource.Mode.Equals(RepositoryMode.SQL))
								await context.UpdateAsync<T>(secondaryDataSource, @object, result.Item2, cancellationToken).ConfigureAwait(false);
						}
						catch { }
					}));

				// sync data sources
				context.GetSyncDataSources()
					.ForEach(syncDataSource =>
					{
						tasks.Add(Task.Run(async () =>
						{
							try
							{
								if (syncDataSource.Mode.Equals(RepositoryMode.NoSQL))
									await context.UpdateAsync<T>(syncDataSource, @object, result.Item2, cancellationToken).ConfigureAwait(false);
								else if (syncDataSource.Mode.Equals(RepositoryMode.SQL))
									await context.UpdateAsync<T>(syncDataSource, @object, result.Item2, cancellationToken).ConfigureAwait(false);
							}
							catch { }
						}));
					});

				// force al tasks to run
				var task = Task.Run(async () => await Task.WhenAll(tasks).ConfigureAwait(false));
			}
		}

		/// <summary>
		/// Updates instance of object into repository (only update changed attributes)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object that presents the instance in repository need to be updated</param>
		/// <param name="dontCreateNewVersion">Force to not create new version when update the object</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		public static async Task UpdateAsync<T>(string aliasTypeName, T @object, bool dontCreateNewVersion = false, string userID = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			using (var context = new RepositoryContext())
			{
				await RepositoryMediator.UpdateAsync<T>(context, aliasTypeName, @object, dontCreateNewVersion, userID, cancellationToken).ConfigureAwait(false);
			}
		}
		#endregion

		#region Delete
		/// <summary>
		/// Deletes instance of object from repository
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="id">The string that presents object identity that want to delete instance from repository</param>
		/// <param name="userID">The identity of user who deletes the object (for creating new trash content)</param>
		public static T Delete<T>(RepositoryContext context, DataSource dataSource, string id, string userID = null) where T : class
		{
			try
			{
				// prepare
				context.Operation = RepositoryOperation.Delete;
				context.EntityDefinition = RepositoryMediator.GetEntityDefinition<T>();

				// check existing
				var @object = RepositoryMediator.Get<T>(context, dataSource, id, false);
				if (@object == null)
					return null;

				// call pre-handlers
				context.SetCurrentState(@object);
				if (context.CallPreDeleteHandlers(@object))
					return null;

				// create trash content
				RepositoryMediator.CreateTrashContent<T>(context, @object, userID);

				// delete
				dataSource = dataSource ?? context.GetPrimaryDataSource();
				if (dataSource.Mode.Equals(RepositoryMode.NoSQL))
					context.Delete<T>(dataSource, @object, null);
				else if (dataSource.Mode.Equals(RepositoryMode.SQL))
					context.Delete<T>(dataSource, @object);

				// remove from cache storage
				if (context.EntityDefinition.Cache != null)
#if DEBUG || PROCESSLOGS
					if (context.EntityDefinition.CacheStorage.Remove(@object))
						RepositoryMediator.WriteLogs("DELETE: Remove the cached object from the cache storage successful [" + @object.GetCacheKey(false) + "]");
#else
					context.EntityDefinition.Cache.Remove(@object);
#endif

				// call post-handlers
				context.CallPostDeleteHandlers(@object);
				return @object;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				throw new RepositoryOperationException($"Error occurred while deleting object [{typeof(T).ToString()}]", ex);
			}
		}

		/// <summary>
		/// Deletes instance of object from repository
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="id">The string that presents object identity that want to delete instance from repository</param>
		/// <param name="userID">The identity of user who deletes the object (for creating new trash content)</param>
		public static void Delete<T>(RepositoryContext context, string aliasTypeName, string id, string userID = null) where T : class
		{
			context.AliasTypeName = aliasTypeName;
			var @object = RepositoryMediator.Delete<T>(context, context.GetPrimaryDataSource(), id, userID);
			if (@object != null)
			{
				// secondary data source
				var secondaryDataSource = context.GetSecondaryDataSource();
				if (secondaryDataSource != null)
					Task.Run(() =>
					{
						try
						{
							if (secondaryDataSource.Mode.Equals(RepositoryMode.NoSQL))
								context.Delete<T>(secondaryDataSource, @object);
							else if (secondaryDataSource.Mode.Equals(RepositoryMode.SQL))
								context.Create<T>(secondaryDataSource, @object);
						}
						catch { }
					}).ConfigureAwait(false);

				// sync data sources
				context.GetSyncDataSources()
					.ForEach(syncDataSource =>
					{
						Task.Run(() =>
						{
							try
							{
								if (syncDataSource.Mode.Equals(RepositoryMode.NoSQL))
									context.Create<T>(syncDataSource, @object);
								else if (syncDataSource.Mode.Equals(RepositoryMode.SQL))
									context.Create<T>(syncDataSource, @object);
							}
							catch { }
						}).ConfigureAwait(false);
					});
			}
		}

		/// <summary>
		/// Deletes instance of object from repository
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="id">The string that presents object identity that want to delete instance from repository</param>
		/// <param name="userID">The identity of user who deletes the object (for creating new trash content)</param>
		public static void Delete<T>(string aliasTypeName, string id, string userID = null) where T : class
		{
			using (var context = new RepositoryContext())
			{
				RepositoryMediator.Delete<T>(context, aliasTypeName, id, userID);
			}
		}

		/// <summary>
		/// Deletes instance of object from repository
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="id">The string that presents object identity that want to delete instance from repository</param>
		/// <param name="userID">The identity of user who deletes the object (for creating new trash content)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		public static async Task<T> DeleteAsync<T>(RepositoryContext context, DataSource dataSource, string id, string userID = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			try
			{
				// prepare
				context.Operation = RepositoryOperation.Delete;
				context.EntityDefinition = RepositoryMediator.GetEntityDefinition<T>();

				// check existing
				var @object = await RepositoryMediator.GetAsync<T>(context, dataSource, id, false, cancellationToken).ConfigureAwait(false);
				if (@object == null)
					return null;

				// call pre-handlers
				context.SetCurrentState(@object);
				if (await context.CallPreDeleteHandlersAsync(@object, cancellationToken).ConfigureAwait(false))
					return null;

				// create trash content
				await RepositoryMediator.CreateTrashContentAsync<T>(context, @object, userID, cancellationToken).ConfigureAwait(false);

				// delete
				dataSource = dataSource ?? context.GetPrimaryDataSource();
				if (dataSource.Mode.Equals(RepositoryMode.NoSQL))
					await context.DeleteAsync<T>(dataSource, @object, null, cancellationToken).ConfigureAwait(false);
				else if (dataSource.Mode.Equals(RepositoryMode.SQL))
					await context.DeleteAsync<T>(dataSource, @object, cancellationToken).ConfigureAwait(false);

				// remove from cache storage
				if (context.EntityDefinition.Cache != null)
#if DEBUG || PROCESSLOGS
					if (await context.EntityDefinition.CacheStorage.RemoveAsync(@object).ConfigureAwait(false))
						RepositoryMediator.WriteLogs("DELETE: Remove the cached object from the cache storage successful [" + @object.GetCacheKey(false) + "]");
#else
					await context.EntityDefinition.Cache.RemoveAsync(@object).ConfigureAwait(false);
#endif

				// call post-handlers
				await context.CallPostDeleteHandlersAsync(@object, cancellationToken).ConfigureAwait(false);
				return @object;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				throw new RepositoryOperationException($"Error occurred while deleting object [{typeof(T).ToString()}]", ex);
			}
		}

		/// <summary>
		/// Deletes instance of object from repository
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="id">The string that presents object identity that want to delete instance from repository</param>
		/// <param name="userID">The identity of user who deletes the object (for creating new trash content)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		public static async Task DeleteAsync<T>(RepositoryContext context, string aliasTypeName, string id, string userID = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			context.AliasTypeName = aliasTypeName;
			var @object = await RepositoryMediator.DeleteAsync<T>(context, context.GetPrimaryDataSource(), id, userID, cancellationToken).ConfigureAwait(false);
			if (@object != null)
			{
				// secondary data source
				var tasks = new List<Task>();
				var secondaryDataSource = context.GetSecondaryDataSource();
				if (secondaryDataSource != null)
					tasks.Add(Task.Run(async () =>
					{
						try
						{
							if (secondaryDataSource.Mode.Equals(RepositoryMode.NoSQL))
								await context.DeleteAsync<T>(secondaryDataSource, @object, cancellationToken).ConfigureAwait(false);
							else if (secondaryDataSource.Mode.Equals(RepositoryMode.SQL))
								await context.DeleteAsync<T>(secondaryDataSource, @object, cancellationToken).ConfigureAwait(false);
						}
						catch { }
					}));

				// sync data sources
				context.GetSyncDataSources()
					.ForEach(syncDataSource =>
					{
						tasks.Add(Task.Run(async () =>
						{
							try
							{
								if (syncDataSource.Mode.Equals(RepositoryMode.NoSQL))
									await context.DeleteAsync<T>(syncDataSource, @object, cancellationToken).ConfigureAwait(false);
								else if (syncDataSource.Mode.Equals(RepositoryMode.SQL))
									await context.DeleteAsync<T>(syncDataSource, @object, cancellationToken).ConfigureAwait(false);
							}
							catch { }
						}));
					});

				// force al tasks to run
				var task = Task.Run(async () => await Task.WhenAll(tasks).ConfigureAwait(false));
			}
		}

		/// <summary>
		/// Deletes instance of object from repository
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="id">The string that presents object identity that want to delete instance from repository</param>
		/// <param name="userID">The identity of user who deletes the object (for creating new trash content)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		public static async Task DeleteAsync<T>(string aliasTypeName, string id, string userID = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			using (var context = new RepositoryContext())
			{
				await RepositoryMediator.DeleteAsync<T>(context, aliasTypeName, id, userID, cancellationToken).ConfigureAwait(false);
			}
		}
		#endregion

		#region Delete (many)
		/// <summary>
		/// Deletes many instances of objects from repository
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="filter">Filter expression to filter instances to delete</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		public static void DeleteMany<T>(RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, string businessEntityID = null) where T : class
		{
			try
			{
				// prepare
				context.Operation = RepositoryOperation.Delete;
				context.EntityDefinition = RepositoryMediator.GetEntityDefinition<T>();
				dataSource = dataSource ?? context.GetPrimaryDataSource();

				// delete
				if (dataSource.Mode.Equals(RepositoryMode.NoSQL))
					context.DeleteMany<T>(dataSource, filter, businessEntityID, null);
				else if (dataSource.Mode.Equals(RepositoryMode.SQL))
					context.DeleteMany<T>(dataSource, filter, businessEntityID);

				// delete from secondary data source
				var secondaryDataSource = context.GetSecondaryDataSource();
				if (secondaryDataSource != null && !secondaryDataSource.Name.IsEquals(dataSource.Name))
					Task.Run(() =>
					{
						try
						{
							if (secondaryDataSource.Mode.Equals(RepositoryMode.NoSQL))
								context.DeleteMany<T>(secondaryDataSource, filter, businessEntityID, null);
							else if (secondaryDataSource.Mode.Equals(RepositoryMode.SQL))
								context.DeleteMany<T>(secondaryDataSource, filter, businessEntityID);
						}
						catch { }
					}).ConfigureAwait(false);

				// delete from sync data sources
				context.GetSyncDataSources()
					.Where(syncDataSource => !syncDataSource.Name.IsEquals(dataSource.Name))
					.ForEach(syncDataSource =>
					{
						Task.Run(() =>
						{
							try
							{
								if (syncDataSource.Mode.Equals(RepositoryMode.NoSQL))
									context.DeleteMany<T>(syncDataSource, filter, businessEntityID, null);
								else if (syncDataSource.Mode.Equals(RepositoryMode.SQL))
									context.DeleteMany<T>(syncDataSource, filter, businessEntityID);
							}
							catch { }
						}).ConfigureAwait(false);
					});
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				throw new RepositoryOperationException($"Error occurred while deleting multiple objects [{typeof(T).ToString()}]", ex);
			}
		}

		/// <summary>
		/// Deletes many instances of objects from repository
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">Filter expression to filter instances to delete</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		public static void DeleteMany<T>(RepositoryContext context, string aliasTypeName, IFilterBy<T> filter, string businessEntityID = null) where T : class
		{
			context.AliasTypeName = aliasTypeName;
			RepositoryMediator.DeleteMany<T>(context, context.GetPrimaryDataSource(), filter, businessEntityID);
		}

		/// <summary>
		/// Deletes many instances of objects from repository
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">Filter expression to filter instances to delete</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		public static void DeleteMany<T>(string aliasTypeName, IFilterBy<T> filter, string businessEntityID = null) where T : class
		{
			using (var context = new RepositoryContext())
			{
				RepositoryMediator.DeleteMany<T>(context, aliasTypeName, filter, businessEntityID);
			}
		}

		/// <summary>
		/// Deletes many instances of objects from repository
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="filter">Filter expression to filter instances to delete</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		public static async Task DeleteManyAsync<T>(RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, string businessEntityID = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			try
			{
				// prepare
				context.Operation = RepositoryOperation.Delete;
				context.EntityDefinition = RepositoryMediator.GetEntityDefinition<T>();
				dataSource = dataSource ?? context.GetPrimaryDataSource();

				// delete
				if (dataSource.Mode.Equals(RepositoryMode.NoSQL))
					await context.DeleteManyAsync<T>(dataSource, filter, businessEntityID, null, cancellationToken).ConfigureAwait(false);
				else if (dataSource.Mode.Equals(RepositoryMode.SQL))
					await context.DeleteManyAsync<T>(dataSource, filter, businessEntityID, cancellationToken).ConfigureAwait(false);

				// delete from secondary data source
				var tasks = new List<Task>();
				var secondaryDataSource = context.GetSecondaryDataSource();
				if (secondaryDataSource != null && !secondaryDataSource.Name.IsEquals(dataSource.Name))
					tasks.Add(Task.Run(async () =>
					{
						try
						{
							if (secondaryDataSource.Mode.Equals(RepositoryMode.NoSQL))
								await context.DeleteManyAsync<T>(secondaryDataSource, filter, businessEntityID, null, cancellationToken).ConfigureAwait(false);
							else if (secondaryDataSource.Mode.Equals(RepositoryMode.SQL))
								await context.DeleteManyAsync<T>(secondaryDataSource, filter, businessEntityID, cancellationToken).ConfigureAwait(false);
						}
						catch { }
					}));

				// delete from sync data sources
				context.GetSyncDataSources()
					.Where(syncDataSource => !syncDataSource.Name.IsEquals(dataSource.Name))
					.ForEach(syncDataSource =>
					{
						tasks.Add(Task.Run(async () =>
						{
							try
							{
								if (syncDataSource.Mode.Equals(RepositoryMode.NoSQL))
									await context.DeleteManyAsync<T>(syncDataSource, filter, businessEntityID, null, cancellationToken).ConfigureAwait(false);
								else if (syncDataSource.Mode.Equals(RepositoryMode.SQL))
									await context.DeleteManyAsync<T>(syncDataSource, filter, businessEntityID, cancellationToken).ConfigureAwait(false);
							}
							catch { }
						}));
					});

				// force al tasks to run
				var task = Task.Run(async () => await Task.WhenAll(tasks).ConfigureAwait(false));
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				throw new RepositoryOperationException($"Error occurred while deleting multiple objects [{typeof(T).ToString()}]", ex);
			}
		}

		/// <summary>
		/// Deletes many instances of objects from repository
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">Filter expression to filter instances to delete</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		public static async Task DeleteManyAsync<T>(RepositoryContext context, string aliasTypeName, IFilterBy<T> filter, string businessEntityID = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			// prepare
			context.AliasTypeName = aliasTypeName;
			await RepositoryMediator.DeleteManyAsync<T>(context, context.GetPrimaryDataSource(), filter, businessEntityID, cancellationToken).ConfigureAwait(false);

			// TO DO: sync to other data sources
			// ...
		}

		/// <summary>
		/// Deletes many instances of objects from repository
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">Filter expression to filter instances to delete</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		public static async Task DeleteManyAsync<T>(string aliasTypeName, IFilterBy<T> filter, string businessEntityID = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			using (var context = new RepositoryContext())
			{
				await RepositoryMediator.DeleteManyAsync<T>(context, aliasTypeName, filter, businessEntityID, cancellationToken).ConfigureAwait(false);
			}
		}
		#endregion

		#region Find
		/// <summary>
		/// Finds the identity of objects from repository
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="filter">Filter expression</param>
		/// <param name="sort">Sort expression</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <returns></returns>
		public static List<string> FindIdentities<T>(RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessEntityID = null, bool autoAssociateWithMultipleParents = true, string cacheKey = null, int cacheTime = 0) where T : class
		{
			try
			{
				// prepare
				context.EntityDefinition = RepositoryMediator.GetEntityDefinition<T>();
				dataSource = dataSource ?? context.GetPrimaryDataSource();

				// find identities
				return context.EntityDefinition.Cache != null && !string.IsNullOrWhiteSpace(cacheKey)
					? context.EntityDefinition.Cache.Get<List<string>>(cacheKey)
					: dataSource.Mode.Equals(RepositoryMode.NoSQL)
						? context.SelectIdentities<T>(dataSource, filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents, null)
						: dataSource.Mode.Equals(RepositoryMode.SQL)
							? context.SelectIdentities<T>(dataSource, filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents)
							: new List<string>();
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				throw new RepositoryOperationException("Error occurred while finding identities of objects", ex);
			}
		}

		/// <summary>
		/// Finds the identity of objects from repository
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">Filter expression</param>
		/// <param name="sort">Sort expression</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <returns></returns>
		public static List<string> FindIdentities<T>(RepositoryContext context, string aliasTypeName, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessEntityID = null, bool autoAssociateWithMultipleParents = true, string cacheKey = null, int cacheTime = 0) where T : class
		{
			context.AliasTypeName = aliasTypeName;
			return RepositoryMediator.FindIdentities<T>(context, context.GetPrimaryDataSource(), filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents, cacheKey, cacheTime);
		}

		/// <summary>
		/// Finds the identity of objects from repository
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">Filter expression</param>
		/// <param name="sort">Sort expression</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <returns></returns>
		public static List<string> FindIdentities<T>(string aliasTypeName, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessEntityID = null, bool autoAssociateWithMultipleParents = true, string cacheKey = null, int cacheTime = 0) where T : class
		{
			using (var context = new RepositoryContext(false))
			{
				return RepositoryMediator.FindIdentities<T>(context, aliasTypeName, filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents, cacheKey, cacheTime);
			}
		}

		/// <summary>
		/// Finds the identity of objects from repository
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="filter">Filter expression</param>
		/// <param name="sort">Sort expression</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <returns></returns>
		public static List<string> FindIdentities<T>(IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessEntityID = null, bool autoAssociateWithMultipleParents = true, string cacheKey = null, int cacheTime = 0) where T : class
		{
			return RepositoryMediator.FindIdentities<T>(null, filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents, cacheKey, cacheTime);
		}

		/// <summary>
		/// Finds the intance of objects from repository
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="filter">Filter expression</param>
		/// <param name="sort">Sort expression</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <returns></returns>
		public static List<T> Find<T>(RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessEntityID = null, bool autoAssociateWithMultipleParents = true, string cacheKey = null, int cacheTime = 0) where T : class
		{
			try
			{
				// prepare
				context.EntityDefinition = RepositoryMediator.GetEntityDefinition<T>();
				dataSource = dataSource ?? context.GetPrimaryDataSource();
				List<T> objects = null;

				// find identities
				var identities = context.EntityDefinition.Cache == null
					? null
					: RepositoryMediator.FindIdentities<T>(context, dataSource, filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents, cacheKey, cacheTime);

#if DEBUG || PROCESSLOGS
				RepositoryMediator.WriteLogs(new List<string>()
				{
					"FIND: Find objects of [" + context.EntityDefinition.Type.GetTypeName() + "]",
					"- Total: " + (identities != null ? identities.Count.ToString() : "0"),
					"- Mode: " + primaryDataSource.Mode.ToString(),
					"- Page Size: " + pageSize.ToString(),
					"- Page Number: " + pageNumber.ToString(),
					"- Filter By: " + (filter != null ? "\r\n" + filter.ToString() : "None"),
					"- Sort By: " + (sort != null ? "\r\n" + sort.ToString() : "None"),
				}, null);
#endif

				// process cache
				if (identities != null && identities.Count > 0)
				{
#if DEBUG || PROCESSLOGS
					RepositoryMediator.WriteLogs("FIND: Total " + identities.Count + " identities are fetched [" + identities.ToString(" - ") + "]");
#endif
					// get cached objects
					var cached = context.EntityDefinition.Cache.Get<T>(identities.Select(id => id.GetCacheKey<T>()));
					if (cached != null)
					{
#if DEBUG || PROCESSLOGS
						RepositoryMediator.WriteLogs("FIND: Total " + cached.Count + " cached object(s) are found [" + cached.Select(item => item.Key).ToString(" - ") + "]");
#endif
						// prepare
						var results = identities.ToDictionary<string, string, T>(id => id, id => default(T));

						// add cached objects
						var ids = new List<string>();
						cached.ForEach(item =>
						{
							var id = item.Value.GetEntityID();
							ids.Add(id);
							results[id] = item.Value;
						});

						// find missing objects
						identities = identities.Except(ids).ToList();
						if (identities.Count > 0)
						{
							var missing = dataSource.Mode.Equals(RepositoryMode.NoSQL)
								? context.Find<T>(dataSource, identities, sort, businessEntityID, null)
								: dataSource.Mode.Equals(RepositoryMode.SQL)
									? context.Find<T>(dataSource, identities, sort, businessEntityID)
									: new List<T>();

							// update results & cache
							missing.Where(obj => obj != null).ForEach(obj => results[obj.GetEntityID()] = obj);
							context.EntityDefinition.Cache.Set(missing);
#if DEBUG || PROCESSLOGS
							RepositoryMediator.WriteLogs("FIND: Add " + missing.Count + " missing object(s) into cache storage successful [" + missing.Select(o => o.GetCacheKey()).ToString(" - ") + "]");
#endif
						}

						// update the collection of objects
						objects = results.Select(item => item.Value).ToList();
					}
				}

				// fetch objects if has no cache
				if (objects == null)
				{
					objects = identities == null || identities.Count > 0
						? dataSource.Mode.Equals(RepositoryMode.NoSQL)
							? context.Find<T>(dataSource, filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents, null)
							: dataSource.Mode.Equals(RepositoryMode.SQL)
								? context.Find<T>(dataSource, filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents)
								: new List<T>()
						: new List<T>();

					if (context.EntityDefinition.Cache != null && objects.Count > 0)
					{
						if (!string.IsNullOrWhiteSpace(cacheKey))
							context.EntityDefinition.Cache.Set(cacheKey, objects.Select(o => o.GetEntityID()).ToList(), cacheTime < 1 ? context.EntityDefinition.Cache.ExpirationTime / 2 : cacheTime);
						context.EntityDefinition.Cache.Set(objects);
#if DEBUG || PROCESSLOGS
						RepositoryMediator.WriteLogs("FIND: Add " + objects.Count + " raw object(s) into cache storage successful [" + objects.Select(o => o.GetCacheKey()).ToString(" - ") + "]");
#endif
					}
				}
				return objects;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				throw new RepositoryOperationException($"Error occurred while finding objects [{typeof(T).ToString()}]", ex);
			}
		}

		/// <summary>
		/// Finds the intance of objects from repository
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">Filter expression</param>
		/// <param name="sort">Sort expression</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <returns></returns>
		public static List<T> Find<T>(RepositoryContext context, string aliasTypeName, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessEntityID = null, bool autoAssociateWithMultipleParents = true, string cacheKey = null, int cacheTime = 0) where T : class
		{
			context.AliasTypeName = aliasTypeName;
			return RepositoryMediator.Find<T>(context, context.GetPrimaryDataSource(), filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents, cacheKey, cacheTime);
		}

		/// <summary>
		/// Finds intances of objects from repository
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">Filter expression</param>
		/// <param name="sort">Sort expression</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <returns></returns>
		public static List<T> Find<T>(string aliasTypeName, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessEntityID = null, bool autoAssociateWithMultipleParents = true, string cacheKey = null, int cacheTime = 0) where T : class
		{
			using (var context = new RepositoryContext(false))
			{
				return RepositoryMediator.Find<T>(context, aliasTypeName, filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents, cacheKey, cacheTime);
			}
		}

		/// <summary>
		/// Finds the identity of objects from repository
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="filter">Filter expression</param>
		/// <param name="sort">Sort expression</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<List<string>> FindIdentitiesAsync<T>(RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessEntityID = null, bool autoAssociateWithMultipleParents = true, string cacheKey = null, int cacheTime = 0, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			try
			{
				// prepare
				context.EntityDefinition = RepositoryMediator.GetEntityDefinition<T>();
				dataSource = dataSource ?? context.GetPrimaryDataSource();

				// find identities
				return context.EntityDefinition.Cache != null && !string.IsNullOrWhiteSpace(cacheKey)
					? await context.EntityDefinition.Cache.GetAsync<List<string>>(cacheKey)
					: dataSource.Mode.Equals(RepositoryMode.NoSQL)
						? await context.SelectIdentitiesAsync<T>(dataSource, filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents, null, cancellationToken)
						: dataSource.Mode.Equals(RepositoryMode.SQL)
							? await context.SelectIdentitiesAsync<T>(dataSource, filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents, cancellationToken)
							: new List<string>();
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				throw new RepositoryOperationException("Error occurred while finding identities of objects", ex);
			}
		}

		/// <summary>
		/// Finds the identity of objects from repository
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">Filter expression</param>
		/// <param name="sort">Sort expression</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<List<string>> FindIdentitiesAsync<T>(RepositoryContext context, string aliasTypeName, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessEntityID = null, bool autoAssociateWithMultipleParents = true, string cacheKey = null, int cacheTime = 0, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			context.AliasTypeName = aliasTypeName;
			return await RepositoryMediator.FindIdentitiesAsync<T>(context, context.GetPrimaryDataSource(), filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents, cacheKey, cacheTime, cancellationToken).ConfigureAwait(false);
		}

		/// <summary>
		/// Finds the identity of objects from repository
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">Filter expression</param>
		/// <param name="sort">Sort expression</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<List<string>> FindIdentitiesAsync<T>(string aliasTypeName, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessEntityID = null, bool autoAssociateWithMultipleParents = true, string cacheKey = null, int cacheTime = 0, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			using (var context = new RepositoryContext(false))
			{
				return await RepositoryMediator.FindIdentitiesAsync<T>(context, aliasTypeName, filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents, cacheKey, cacheTime, cancellationToken).ConfigureAwait(false);
			}
		}

		/// <summary>
		/// Finds the identity of objects from repository
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="filter">Filter expression</param>
		/// <param name="sort">Sort expression</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<List<string>> FindIdentitiesAsync<T>(IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessEntityID = null, bool autoAssociateWithMultipleParents = true, string cacheKey = null, int cacheTime = 0, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			return RepositoryMediator.FindIdentitiesAsync<T>(null, filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents, cacheKey, cacheTime, cancellationToken);
		}

		/// <summary>
		/// Finds the intance of objects from repository
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="filter">Filter expression</param>
		/// <param name="sort">Sort expression</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<List<T>> FindAsync<T>(RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessEntityID = null, bool autoAssociateWithMultipleParents = true, string cacheKey = null, int cacheTime = 0, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			try
			{
				// prepare
				context.EntityDefinition = RepositoryMediator.GetEntityDefinition<T>();
				dataSource = dataSource ?? context.GetPrimaryDataSource();
				List<T> objects = null;

				// find identities
				var identities = context.EntityDefinition.Cache == null
					? null
					: await RepositoryMediator.FindIdentitiesAsync<T>(context, dataSource, filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents, cacheKey, cacheTime, cancellationToken).ConfigureAwait(false);

#if DEBUG || PROCESSLOGS
				RepositoryMediator.WriteLogs(new List<string>()
				{
					"FIND: Find objects of [" + context.EntityDefinition.Type.GetTypeName() + "]",
					"- Total: " + (identities != null ? identities.Count.ToString() : "0"),
					"- Mode: " + primaryDataSource.Mode.ToString(),
					"- Page Size: " + pageSize.ToString(),
					"- Page Number: " + pageNumber.ToString(),
					"- Filter By: " + (filter != null ? "\r\n" + filter.ToString() : "None"),
					"- Sort By: " + (sort != null ? "\r\n" + sort.ToString() : "None"),
				}, null);
#endif

				// process
				if (identities != null && identities.Count > 0)
				{
#if DEBUG || PROCESSLOGS
					RepositoryMediator.WriteLogs("FIND: Total " + identities.Count + " identities are fetched [" + identities.ToString(" - ") + "]");
#endif
					// get cached objects
					var cached = await context.EntityDefinition.Cache.GetAsync<T>(identities.Select(id => id.GetCacheKey<T>())).ConfigureAwait(false);
					if (cached != null)
					{
#if DEBUG || PROCESSLOGS
						RepositoryMediator.WriteLogs("FIND: Total " + cached.Count + " cached object(s) are found [" + cached.Select(item => item.Key).ToString(" - ") + "]");
#endif
						// prepare
						var results = identities.ToDictionary<string, string, T>(id => id, id => default(T));

						// add cached objects
						var ids = new List<string>();
						cached.ForEach(item =>
						{
							var id = item.Value.GetEntityID();
							ids.Add(id);
							results[id] = item.Value;
						});

						// find missing objects
						identities = identities.Except(ids).ToList();
						if (identities.Count > 0)
						{
							var missing = dataSource.Mode.Equals(RepositoryMode.NoSQL)
								? await context.FindAsync<T>(dataSource, identities, sort, businessEntityID, null, cancellationToken).ConfigureAwait(false)
								: dataSource.Mode.Equals(RepositoryMode.SQL)
									? await context.FindAsync<T>(dataSource, identities, sort, businessEntityID, cancellationToken).ConfigureAwait(false)
									: new List<T>();

							// update results & cache
							missing.Where(obj => obj != null).ForEach(obj => results[obj.GetEntityID()] = obj);
							await context.EntityDefinition.Cache.SetAsync(missing).ConfigureAwait(false);
#if DEBUG || PROCESSLOGS
							RepositoryMediator.WriteLogs("FIND: Add " + missing.Count + " missing object(s) into cache storage successful [" + missing.Select(o => o.GetCacheKey()).ToString(" - ") + "]");
#endif
						}

						// update the collection of objects
						objects = results.Select(item => item.Value).ToList();
					}
				}

				// fetch objects if has no cache
				if (objects == null)
				{
					objects = identities == null || identities.Count > 0
						? dataSource.Mode.Equals(RepositoryMode.NoSQL)
							? await context.FindAsync<T>(dataSource, filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents, null, cancellationToken).ConfigureAwait(false)
							: dataSource.Mode.Equals(RepositoryMode.SQL)
								? await context.FindAsync<T>(dataSource, filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents, cancellationToken).ConfigureAwait(false)
								: new List<T>()
						: new List<T>();

					if (context.EntityDefinition.Cache != null && objects.Count > 0)
					{
						if (!string.IsNullOrWhiteSpace(cacheKey))
							await context.EntityDefinition.Cache.SetAsync(cacheKey, objects.Select(o => o.GetEntityID()).ToList(), cacheTime < 1 ? context.EntityDefinition.Cache.ExpirationTime / 2 : cacheTime).ConfigureAwait(false);
						await context.EntityDefinition.Cache.SetAsync(objects).ConfigureAwait(false);
#if DEBUG || PROCESSLOGS
						RepositoryMediator.WriteLogs("FIND: Add " + objects.Count + " raw object(s) into cache storage successful [" + objects.Select(o => o.GetCacheKey()).ToString(" - ") + "]");
#endif
					}
				}
				return objects;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				throw new RepositoryOperationException($"Error occurred while finding objects [{typeof(T).ToString()}]", ex);
			}
		}

		/// <summary>
		/// Finds the intance of objects from repository
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">Filter expression</param>
		/// <param name="sort">Sort expression</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<List<T>> FindAsync<T>(RepositoryContext context, string aliasTypeName, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessEntityID = null, bool autoAssociateWithMultipleParents = true, string cacheKey = null, int cacheTime = 0, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			context.AliasTypeName = aliasTypeName;
			return RepositoryMediator.FindAsync<T>(context, context.GetPrimaryDataSource(), filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents, cacheKey, cacheTime, cancellationToken);
		}

		/// <summary>
		/// Finds intances of objects from repository
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">Filter expression</param>
		/// <param name="sort">Sort expression</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<List<T>> FindAsync<T>(string aliasTypeName, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessEntityID = null, bool autoAssociateWithMultipleParents = true, string cacheKey = null, int cacheTime = 0, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			using (var context = new RepositoryContext(false))
			{
				return await RepositoryMediator.FindAsync<T>(context, aliasTypeName, filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents, cacheKey, cacheTime, cancellationToken).ConfigureAwait(false);
			}
		}
		#endregion

		#region Count
		/// <summary>
		/// Counts the number of intances of objects
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="filter">Filter expression</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of total number of objects</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <returns>The integer number that presents total of objects that matched with the filter expression</returns>
		public static long Count<T>(RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, string businessEntityID = null, bool autoAssociateWithMultipleParents = true, string cacheKey = null, int cacheTime = 0) where T : class
		{
			try
			{
				// prepare
				context.EntityDefinition = RepositoryMediator.GetEntityDefinition<T>();

				// check cache
				var total = !string.IsNullOrWhiteSpace(cacheKey) && context.EntityDefinition.Cache != null && context.EntityDefinition.Cache.Exists(cacheKey)
					? context.EntityDefinition.Cache.Get<long>(cacheKey)
					: -1;
				if (total > -1)
					return total;

				// count
				dataSource = dataSource ?? context.GetPrimaryDataSource();
				total = dataSource.Mode.Equals(RepositoryMode.NoSQL)
					? context.Count<T>(dataSource, filter, businessEntityID, autoAssociateWithMultipleParents, null)
					: dataSource.Mode.Equals(RepositoryMode.SQL)
						? context.Count<T>(dataSource, filter, businessEntityID, autoAssociateWithMultipleParents)
						: 0;

#if DEBUG || PROCESSLOGS
				RepositoryMediator.WriteLogs(new List<string>()
				{
					"COUNT: Count objects of [" + context.EntityDefinition.Type.GetTypeName() + "]",
					"- Total: " + total.ToString(),
					"- Mode: " + primaryDataSource.Mode.ToString(),
					"- Filter By: " + (filter != null ? "\r\n" + filter.ToString() : "None")
				}, null);
#endif

				// update cache and return
				if (!string.IsNullOrWhiteSpace(cacheKey) && context.EntityDefinition.Cache != null)
					context.EntityDefinition.Cache.Set(cacheKey, total, cacheTime < 1 ? context.EntityDefinition.Cache.ExpirationTime / 2 : cacheTime);

				return total;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				throw new RepositoryOperationException($"Error occurred while counting objects [{typeof(T).ToString()}]", ex);
			}
		}

		/// <summary>
		/// Counts the number of intances of objects
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">Filter expression</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of total number of objects</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <returns>The integer number that presents total of objects that matched with the filter expression</returns>
		public static long Count<T>(RepositoryContext context, string aliasTypeName, IFilterBy<T> filter, string businessEntityID = null, bool autoAssociateWithMultipleParents = true, string cacheKey = null, int cacheTime = 0) where T : class
		{
			context.AliasTypeName = aliasTypeName;
			return RepositoryMediator.Count<T>(context, context.GetPrimaryDataSource(), filter, businessEntityID, autoAssociateWithMultipleParents, cacheKey, cacheTime);
		}

		/// <summary>
		/// Counts the number of intances of objects
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">Filter expression</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of total number of objects</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <returns>The integer number that presents total of objects that matched with the filter expression</returns>
		public static long Count<T>(string aliasTypeName, IFilterBy<T> filter, string businessEntityID = null, bool autoAssociateWithMultipleParents = true, string cacheKey = null, int cacheTime = 0) where T : class
		{
			using (var context = new RepositoryContext(false))
			{
				return RepositoryMediator.Count<T>(context, aliasTypeName, filter, businessEntityID, autoAssociateWithMultipleParents, cacheKey, cacheTime);
			}
		}

		/// <summary>
		/// Counts the number of intances of objects
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="filter">Filter expression</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of total number of objects</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The integer number that presents total of objects that matched with the filter expression</returns>
		public static async Task<long> CountAsync<T>(RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, string businessEntityID = null, bool autoAssociateWithMultipleParents = true, string cacheKey = null, int cacheTime = 0, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			try
			{
				// prepare
				context.EntityDefinition = RepositoryMediator.GetEntityDefinition<T>();

				// check cache
				var total = !string.IsNullOrWhiteSpace(cacheKey) && context.EntityDefinition.Cache != null && await context.EntityDefinition.Cache.ExistsAsync(cacheKey).ConfigureAwait(false)
					? await context.EntityDefinition.Cache.GetAsync<long>(cacheKey).ConfigureAwait(false)
					: -1;
				if (total > -1)
					return total;

				// count
				dataSource = dataSource ?? context.GetPrimaryDataSource();
				total = dataSource.Mode.Equals(RepositoryMode.NoSQL)
					? await context.CountAsync<T>(dataSource, filter, businessEntityID, autoAssociateWithMultipleParents, null, cancellationToken).ConfigureAwait(false)
					: dataSource.Mode.Equals(RepositoryMode.SQL)
						? await context.CountAsync<T>(dataSource, filter, businessEntityID, autoAssociateWithMultipleParents, cancellationToken).ConfigureAwait(false)
						: 0;

#if DEBUG || PROCESSLOGS
				RepositoryMediator.WriteLogs(new List<string>()
				{
					"COUNT: Count objects of [" + context.EntityDefinition.Type.GetTypeName() + "]",
					"- Total: " + total.ToString(),
					"- Mode: " + primaryDataSource.Mode.ToString(),
					"- Filter By: " + (filter != null ? "\r\n" + filter.ToString() : "None")
				}, null);
#endif

				// update cache and return
				if (!string.IsNullOrWhiteSpace(cacheKey) && context.EntityDefinition.Cache != null)
					await context.EntityDefinition.Cache.SetAsync(cacheKey, total, cacheTime < 1 ? context.EntityDefinition.Cache.ExpirationTime / 2 : cacheTime).ConfigureAwait(false);

				return total;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				throw new RepositoryOperationException($"Error occurred while counting objects [{typeof(T).ToString()}]", ex);
			}
		}

		/// <summary>
		/// Counts the number of intances of objects
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">Filter expression</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of total number of objects</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The integer number that presents total of objects that matched with the filter expression</returns>
		public static Task<long> CountAsync<T>(RepositoryContext context, string aliasTypeName, IFilterBy<T> filter, string businessEntityID = null, bool autoAssociateWithMultipleParents = true, string cacheKey = null, int cacheTime = 0, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			context.AliasTypeName = aliasTypeName;
			return RepositoryMediator.CountAsync<T>(context, context.GetPrimaryDataSource(), filter, businessEntityID, autoAssociateWithMultipleParents, cacheKey, cacheTime, cancellationToken);
		}

		/// <summary>
		/// Counts the number of intances of objects
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">Filter expression</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of total number of objects</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The integer number that presents total of objects that matched with the filter expression</returns>
		public static async Task<long> CountAsync<T>(string aliasTypeName, IFilterBy<T> filter, string businessEntityID = null, bool autoAssociateWithMultipleParents = true, string cacheKey = null, int cacheTime = 0, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			using (var context = new RepositoryContext(false))
			{
				return await RepositoryMediator.CountAsync<T>(context, aliasTypeName, filter, businessEntityID, autoAssociateWithMultipleParents, cacheKey, cacheTime, cancellationToken).ConfigureAwait(false);
			}
		}
		#endregion

		#region Search (by query)
		/// <summary>
		/// Searchs intances of objects from repository (using full-text search)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="query">The searching query (like Google searching query)</param>
		/// <param name="filter">The object that presents other filter expression to combine with seaching query</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns></returns>
		public static List<T> Search<T>(RepositoryContext context, DataSource dataSource, string query, IFilterBy<T> filter, int pageSize, int pageNumber, string businessEntityID = null) where T : class
		{
			try
			{
				// prepare
				context.EntityDefinition = RepositoryMediator.GetEntityDefinition<T>();
				dataSource = dataSource ?? context.GetPrimaryDataSource();
				List<T> objects = null;

				// search identities
				var identities = context.EntityDefinition.Cache == null
					? null
					: dataSource.Mode.Equals(RepositoryMode.NoSQL)
						? context.SearchIdentities<T>(dataSource, query, filter, pageSize, pageNumber, businessEntityID, null)
						: dataSource.Mode.Equals(RepositoryMode.SQL)
							? context.SearchIdentities<T>(dataSource, query, filter, pageSize, pageNumber, businessEntityID)
							: new List<string>();

#if DEBUG || PROCESSLOGS
				RepositoryMediator.WriteLogs(new List<string>()
				{
					"SEARCH: Search objects of [" + context.EntityDefinition.Type.GetTypeName() + "]",
					"- Total: " + (identities != null ? identities.Count.ToString() : "0"),
					"- Mode: " + primaryDataSource.Mode.ToString(),
					"- Page Size: " + pageSize.ToString(),
					"- Page Number: " + pageNumber.ToString(),
					"- Query: " + (!string.IsNullOrWhiteSpace(query) ? query : "None"),
					"- Filter By (Additional): " + (filter != null ? "\r\n" + filter.ToString() : "None")
				}, null);
#endif

				// process
				if (identities != null && identities.Count > 0)
				{
#if DEBUG || PROCESSLOGS
					RepositoryMediator.WriteLogs("SEARCH: Total " + identities.Count + " identities are searched [" + identities.ToString(" - ") + "]");
#endif
					// get cached objects
					var cached = context.EntityDefinition.Cache.Get(identities.Select(id => id.GetCacheKey<T>()));
					if (cached != null)
					{
#if DEBUG || PROCESSLOGS
						RepositoryMediator.WriteLogs("SEARCH: Total " + cached.Count + " cached object(s) are found [" + cached.Select(item => item.Key).ToString(" - ") + "]");
#endif
						// prepare
						var results = identities.ToDictionary<string, string, T>(id => id, id => default(T));

						// add cached objects
						var ids = new List<string>();
						cached.ForEach(item =>
						{
							var id = (item.Value as T).GetEntityID();
							ids.Add(id);
							results[id] = item.Value as T;
						});

						// find missing objects
						identities = identities.Except(ids).ToList();
						if (identities.Count > 0)
						{
							var missing = dataSource.Mode.Equals(RepositoryMode.NoSQL)
								? context.Find<T>(dataSource, identities, null, businessEntityID, null)
								: dataSource.Mode.Equals(RepositoryMode.SQL)
									? context.Find<T>(dataSource, identities, null, businessEntityID)
									: new List<T>();

							// update results & cache
							missing.Where(obj => obj != null).ForEach(obj => results[obj.GetEntityID()] = obj);
							context.EntityDefinition.Cache.Set(missing);
#if DEBUG || PROCESSLOGS
							RepositoryMediator.WriteLogs("SEARCH: Add " + missing.Count + " missing object(s) into cache storage successful [" + missing.Select(o => o.GetCacheKey()).ToString(" - ") + "]");
#endif
						}

						// update the collection of objects
						objects = results.Select(item => item.Value as T).ToList();
					}
				}

				// search raw objects if has no cache
				if (objects == null)
				{
					objects = identities == null || identities.Count > 0
						? dataSource.Mode.Equals(RepositoryMode.NoSQL)
							? context.Search<T>(dataSource, query, filter, pageSize, pageNumber, businessEntityID, null)
							: dataSource.Mode.Equals(RepositoryMode.SQL)
								? context.Search<T>(dataSource, query, filter, pageSize, pageNumber, businessEntityID)
								: new List<T>()
						: new List<T>();

					if (context.EntityDefinition.Cache != null && objects.Count > 0)
					{
						context.EntityDefinition.Cache.Set(objects);
#if DEBUG || PROCESSLOGS
						RepositoryMediator.WriteLogs("SEARCH: Add " + objects.Count + " raw object(s) into cache storage successful [" + objects.Select(o => o.GetCacheKey()).ToString(" - ") + "]");
#endif
					}
				}
				return objects;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				throw new RepositoryOperationException($"Error occurred while searching objects [{typeof(T).ToString()}]", ex);
			}
		}

		/// <summary>
		/// Searchs intances of objects from repository (using full-text search)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="query">The searching query (like Google searching query)</param>
		/// <param name="filter">The object that presents other filter expression to combine with seaching query</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns></returns>
		public static List<T> Search<T>(RepositoryContext context, string aliasTypeName, string query, IFilterBy<T> filter, int pageSize, int pageNumber, string businessEntityID = null) where T : class
		{
			context.AliasTypeName = aliasTypeName;
			return RepositoryMediator.Search<T>(context, context.GetPrimaryDataSource(), query, filter, pageSize, pageNumber, businessEntityID);
		}

		/// <summary>
		/// Searchs intances of objects from repository (using full-text search)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="query">The searching query (like Google searching query)</param>
		/// <param name="filter">The object that presents other filter expression to combine with seaching query</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns></returns>
		public static List<T> Search<T>(string aliasTypeName, string query, IFilterBy<T> filter, int pageSize, int pageNumber, string businessEntityID = null) where T : class
		{
			using (var context = new RepositoryContext(false))
			{
				return RepositoryMediator.Search<T>(context, aliasTypeName, query, filter, pageSize, pageNumber, businessEntityID);
			}
		}

		/// <summary>
		/// Searchs intances of objects from repository (using full-text search)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="query">The searching query (like Google searching query)</param>
		/// <param name="filter">The object that presents other filter expression to combine with seaching query</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<List<T>> SearchAsync<T>(RepositoryContext context, DataSource dataSource, string query, IFilterBy<T> filter, int pageSize, int pageNumber, string businessEntityID = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			try
			{
				// prepare
				context.EntityDefinition = RepositoryMediator.GetEntityDefinition<T>();
				dataSource = dataSource ?? context.GetPrimaryDataSource();
				List<T> objects = null;

				// search identities
				var identities = context.EntityDefinition.Cache == null
					? null
					: dataSource.Mode.Equals(RepositoryMode.NoSQL)
						? await context.SearchIdentitiesAsync<T>(dataSource, query, filter, pageSize, pageNumber, businessEntityID, null, cancellationToken).ConfigureAwait(false)
						: dataSource.Mode.Equals(RepositoryMode.SQL)
							? await context.SearchIdentitiesAsync<T>(dataSource, query, filter, pageSize, pageNumber, businessEntityID, cancellationToken).ConfigureAwait(false)
							: new List<string>();

#if DEBUG || PROCESSLOGS
				RepositoryMediator.WriteLogs(new List<string>()
				{
					"SEARCH: Search objects of [" + context.EntityDefinition.Type.GetTypeName() + "]",
					"- Total: " + (identities != null ? identities.Count.ToString() : "0"),
					"- Mode: " + primaryDataSource.Mode.ToString(),
					"- Page Size: " + pageSize.ToString(),
					"- Page Number: " + pageNumber.ToString(),
					"- Query: " + (!string.IsNullOrWhiteSpace(query) ? query : "None"),
					"- Filter By (Additional): " + (filter != null ? "\r\n" + filter.ToString() : "None")
				}, null);
#endif

				// process
				if (identities != null && identities.Count > 0)
				{
#if DEBUG || PROCESSLOGS
					RepositoryMediator.WriteLogs("SEARCH: Total " + identities.Count + " identities are searched [" + identities.ToString(" - ") + "]");
#endif
					// get cached objects
					var cached = await context.EntityDefinition.Cache.GetAsync(identities.Select(id => id.GetCacheKey<T>())).ConfigureAwait(false);
					if (cached != null)
					{
#if DEBUG || PROCESSLOGS
						RepositoryMediator.WriteLogs("SEARCH: Total " + cached.Count + " cached object(s) are found [" + cached.Select(item => item.Key).ToString(" - ") + "]");
#endif
						// prepare
						var results = identities.ToDictionary<string, string, T>(id => id, id => default(T));

						// add cached objects
						var ids = new List<string>();
						cached.ForEach(item =>
						{
							var id = (item.Value as T).GetEntityID();
							ids.Add(id);
							results[id] = item.Value as T;
						});

						// find missing objects
						identities = identities.Except(ids).ToList();
						if (identities.Count > 0)
						{
							var missing = dataSource.Mode.Equals(RepositoryMode.NoSQL)
								? await context.FindAsync<T>(dataSource, identities, null, businessEntityID, null, cancellationToken).ConfigureAwait(false)
								: dataSource.Mode.Equals(RepositoryMode.SQL)
									? await context.FindAsync<T>(dataSource, identities, null, businessEntityID, cancellationToken).ConfigureAwait(false)
									: new List<T>();

							// update results & cache
							missing.Where(obj => obj != null).ForEach(obj => results[obj.GetEntityID()] = obj);
							await context.EntityDefinition.Cache.SetAsync(missing).ConfigureAwait(false);
#if DEBUG || PROCESSLOGS
							RepositoryMediator.WriteLogs("SEARCH: Add " + missing.Count + " missing object(s) into cache storage successful [" + missing.Select(o => o.GetCacheKey()).ToString(" - ") + "]");
#endif
						}

						// update the collection of objects
						objects = results.Select(item => item.Value as T).ToList();
					}
				}

				// search raw objects if has no cache
				if (objects == null)
				{
					objects = identities == null || identities.Count > 0
						? dataSource.Mode.Equals(RepositoryMode.NoSQL)
							? await context.SearchAsync<T>(dataSource, query, filter, pageSize, pageNumber, businessEntityID, null, cancellationToken).ConfigureAwait(false)
							: dataSource.Mode.Equals(RepositoryMode.SQL)
								? await context.SearchAsync<T>(dataSource, query, filter, pageSize, pageNumber, businessEntityID, cancellationToken).ConfigureAwait(false)
								: new List<T>()
						: new List<T>();

					if (context.EntityDefinition.Cache != null && objects.Count > 0)
					{
						await context.EntityDefinition.Cache.SetAsync(objects).ConfigureAwait(false);
#if DEBUG || PROCESSLOGS
						RepositoryMediator.WriteLogs("SEARCH: Add " + objects.Count + " raw object(s) into cache storage successful [" + objects.Select(o => o.GetCacheKey()).ToString(" - ") + "]");
#endif
					}
				}
				return objects;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				throw new RepositoryOperationException($"Error occurred while searching objects [{typeof(T).ToString()}]", ex);
			}
		}

		/// <summary>
		/// Searchs intances of objects from repository (using full-text search)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="query">The searching query (like Google searching query)</param>
		/// <param name="filter">The object that presents other filter expression to combine with seaching query</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<List<T>> SearchAsync<T>(RepositoryContext context, string aliasTypeName, string query, IFilterBy<T> filter, int pageSize, int pageNumber, string businessEntityID = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			context.AliasTypeName = aliasTypeName;
			return await RepositoryMediator.SearchAsync<T>(context, context.GetPrimaryDataSource(), query, filter, pageSize, pageNumber, businessEntityID, cancellationToken).ConfigureAwait(false);
		}

		/// <summary>
		/// Searchs intances of objects from repository (using full-text search)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="query">The searching query (like Google searching query)</param>
		/// <param name="filter">The object that presents other filter expression to combine with seaching query</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<List<T>> SearchAsync<T>(string aliasTypeName, string query, IFilterBy<T> filter, int pageSize, int pageNumber, string businessEntityID = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			using (var context = new RepositoryContext(false))
			{
				return await RepositoryMediator.SearchAsync<T>(context, aliasTypeName, query, filter, pageSize, pageNumber, businessEntityID, cancellationToken).ConfigureAwait(false);
			}
		}
		#endregion

		#region Count (by query)
		/// <summary>
		/// Counts the number of intances of objects (using full-text search)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="query">The searching query (like Google searching query)</param>
		/// <param name="filter">The object that presents other filter expression to combine with seaching query</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns>The integer number that presents total of objects that matched with searching query (and filter expression)</returns>
		public static long Count<T>(RepositoryContext context, DataSource dataSource, string query, IFilterBy<T> filter, string businessEntityID = null) where T : class
		{
			try
			{
				// prepare
				context.EntityDefinition = RepositoryMediator.GetEntityDefinition<T>();
				dataSource = dataSource ?? context.GetPrimaryDataSource();

				// count
				var total = dataSource.Mode.Equals(RepositoryMode.NoSQL)
					? context.Count<T>(dataSource, query, filter, businessEntityID, null)
					: dataSource.Mode.Equals(RepositoryMode.SQL)
						? context.Count<T>(dataSource, query, filter, businessEntityID)
						: 0;

#if DEBUG || PROCESSLOGS
				RepositoryMediator.WriteLogs(new List<string>()
				{
					"COUNT: Count objects of [" + context.EntityDefinition.Type.GetTypeName() + "]",
					"- Total: " + total.ToString(),
					"- Mode: " + primaryDataSource.Mode.ToString(),
					"- Query: " + (!string.IsNullOrWhiteSpace(query) ? query : "None"),
					"- Filter By (Additional): " + (filter != null ? "\r\n" + filter.ToString() : "None")
				}, null);
#endif
				return total;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				throw new RepositoryOperationException($"Error occurred while counting objects by query [{typeof(T).ToString()}]", ex);
			}
		}

		/// <summary>
		/// Counts the number of intances of objects (using full-text search)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="query">The searching query (like Google searching query)</param>
		/// <param name="filter">The object that presents other filter expression to combine with seaching query</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns>The integer number that presents total of objects that matched with searching query (and filter expression)</returns>
		public static long Count<T>(RepositoryContext context, string aliasTypeName, string query, IFilterBy<T> filter, string businessEntityID = null) where T : class
		{
			context.AliasTypeName = aliasTypeName;
			return RepositoryMediator.Count<T>(context, context.GetPrimaryDataSource(), query, filter, businessEntityID);
		}

		/// <summary>
		/// Counts the number of intances of objects (using full-text search)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="query">The searching query (like Google searching query)</param>
		/// <param name="filter">The object that presents other filter expression to combine with seaching query</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns>The integer number that presents total of objects that matched with searching query (and filter expression)</returns>
		public static long Count<T>(string aliasTypeName, string query, IFilterBy<T> filter, string businessEntityID = null) where T : class
		{
			using (var context = new RepositoryContext(false))
			{
				return RepositoryMediator.Count<T>(context, aliasTypeName, query, filter, businessEntityID);
			}
		}

		/// <summary>
		/// Counts the number of intances of objects (using full-text search)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="query">The searching query (like Google searching query)</param>
		/// <param name="filter">The object that presents other filter expression to combine with seaching query</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The integer number that presents total of objects that matched with searching query (and filter expression)</returns>
		public static async Task<long> CountAsync<T>(RepositoryContext context, DataSource dataSource, string query, IFilterBy<T> filter, string businessEntityID = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			try
			{
				// prepare
				context.EntityDefinition = RepositoryMediator.GetEntityDefinition<T>();
				dataSource = dataSource ?? context.GetPrimaryDataSource();

				// count
				var total = dataSource.Mode.Equals(RepositoryMode.NoSQL)
					? await context.CountAsync<T>(dataSource, query, filter, businessEntityID, null, cancellationToken).ConfigureAwait(false)
					: dataSource.Mode.Equals(RepositoryMode.SQL)
						? await context.CountAsync<T>(dataSource, query, filter, businessEntityID, cancellationToken).ConfigureAwait(false)
						: 0;

#if DEBUG || PROCESSLOGS
				RepositoryMediator.WriteLogs(new List<string>()
				{
					"COUNT: Count objects of [" + context.EntityDefinition.Type.GetTypeName() + "]",
					"- Total: " + total.ToString(),
					"- Mode: " + primaryDataSource.Mode.ToString(),
					"- Query: " + (!string.IsNullOrWhiteSpace(query) ? query : "None"),
					"- Filter By (Additional): " + (filter != null ? "\r\n" + filter.ToString() : "None")
				}, null);
#endif
				return total;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				throw new RepositoryOperationException($"Error occurred while counting objects by query [{typeof(T).ToString()}]", ex);
			}
		}

		/// <summary>
		/// Counts the number of intances of objects (using full-text search)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="query">The searching query (like Google searching query)</param>
		/// <param name="filter">The object that presents other filter expression to combine with seaching query</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The integer number that presents total of objects that matched with searching query (and filter expression)</returns>
		public static Task<long> CountAsync<T>(RepositoryContext context, string aliasTypeName, string query, IFilterBy<T> filter, string businessEntityID = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			context.AliasTypeName = aliasTypeName;
			return RepositoryMediator.CountAsync<T>(context, context.GetPrimaryDataSource(), query, filter, businessEntityID, cancellationToken);
		}

		/// <summary>
		/// Counts the number of intances of objects (using full-text search)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="query">The searching query (like Google searching query)</param>
		/// <param name="filter">The object that presents other filter expression to combine with seaching query</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The integer number that presents total of objects that matched with searching query (and filter expression)</returns>
		public static async Task<long> CountAsync<T>(string aliasTypeName, string query, IFilterBy<T> filter, string businessEntityID = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			using (var context = new RepositoryContext(false))
			{
				return await RepositoryMediator.CountAsync<T>(context, aliasTypeName, query, filter, businessEntityID, cancellationToken).ConfigureAwait(false);
			}
		}
		#endregion

		#region Create version
		/// <summary>
		/// Creates new version of object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="object">The object to create new instance in repository</param>
		/// <param name="userID">The identity of user who created this verion of the object</param>
		public static VersionContent CreateVersion<T>(RepositoryContext context, DataSource dataSource, T @object, string userID = null) where T : class
		{
			try
			{
				// check
				if (@object == null)
					throw new ArgumentNullException(nameof(@object), "The object is null");

				if (context.EntityDefinition == null)
					context.EntityDefinition = RepositoryMediator.GetEntityDefinition<T>();

				dataSource = dataSource ?? context.GetVersionDataSource();
				if (dataSource == null)
					return null;

				// prepare
				var version = VersionContent.Prepare(@object);
				var filter = !string.IsNullOrWhiteSpace(version.ServiceName) || !string.IsNullOrWhiteSpace(version.EntityID)
					? Filters<VersionContent>.And(
						Filters<VersionContent>.Equals("ObjectID", version.ObjectID),
						!string.IsNullOrWhiteSpace(version.EntityID) ? Filters<VersionContent>.Equals("EntityID", version.EntityID) : Filters<VersionContent>.Equals("ServiceName", version.ServiceName)
					) as IFilterBy<VersionContent>
					: Filters<VersionContent>.Equals("ObjectID", version.ObjectID) as IFilterBy<VersionContent>;
				var latest = VersionContent.Find<VersionContent>(dataSource, "Versions", filter, Sorts<VersionContent>.Descending("VersionNumber"), 1, 1);
				version.VersionNumber = latest != null && latest.Count > 0 ? latest[0].VersionNumber + 1 : 1;
				version.CreatedID = userID ?? "";

				// create new
				return VersionContent.Create(dataSource, "Versions", version);
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				throw new RepositoryOperationException("Error occurred while creating new version of an object", ex);
			}
		}

		/// <summary>
		/// Creates new version of object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="object">The object to create new instance in repository</param>
		/// <param name="userID">The identity of user who created this verion of the object</param>
		public static VersionContent CreateVersion<T>(RepositoryContext context, T @object, string userID = null) where T : class
		{
			return RepositoryMediator.CreateVersion<T>(context, null, @object, userID);
		}

		/// <summary>
		/// Creates new version of object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="object">The object to create new instance in repository</param>
		/// <param name="userID">The identity of user who created this verion of the object</param>
		public static VersionContent CreateVersion<T>(T @object, string userID = null) where T : class
		{
			using (var context = new RepositoryContext())
			{
				return RepositoryMediator.CreateVersion<T>(context, @object, userID);
			}
		}

		/// <summary>
		/// Creates new version of object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="object">The object to create new instance in repository</param>
		/// <param name="userID">The identity of user who created this verion of the object</param>
		/// <param name="cancellationToken">The cancellation token</param>
		public static async Task<VersionContent> CreateVersionAsync<T>(RepositoryContext context, DataSource dataSource, T @object, string userID = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			try
			{
				// check
				if (@object == null)
					throw new ArgumentNullException(nameof(@object), "The object is null");

				if (context.EntityDefinition == null)
					context.EntityDefinition = RepositoryMediator.GetEntityDefinition<T>();

				dataSource = dataSource ?? context.GetVersionDataSource();
				if (dataSource == null)
					return null;

				// prepare
				var version = VersionContent.Prepare(@object);
				var filter = !string.IsNullOrWhiteSpace(version.ServiceName) || !string.IsNullOrWhiteSpace(version.EntityID)
					? Filters<VersionContent>.And(
						Filters<VersionContent>.Equals("ObjectID", version.ObjectID),
						!string.IsNullOrWhiteSpace(version.EntityID) ? Filters<VersionContent>.Equals("EntityID", version.EntityID) : Filters<VersionContent>.Equals("ServiceName", version.ServiceName)
					) as IFilterBy<VersionContent>
					: Filters<VersionContent>.Equals("ObjectID", version.ObjectID) as IFilterBy<VersionContent>;
				var latest = await VersionContent.FindAsync<VersionContent>(dataSource, "Versions", filter, Sorts<VersionContent>.Descending("VersionNumber"), 1, 1, cancellationToken).ConfigureAwait(false);
				version.VersionNumber = latest != null && latest.Count > 0 ? latest[0].VersionNumber + 1 : 1;
				version.CreatedID = userID ?? "";

				// create new
				return await VersionContent.CreateAsync(dataSource, "Versions", version, cancellationToken).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				throw new RepositoryOperationException("Error occurred while creating new version of an object", ex);
			}
		}

		/// <summary>
		/// Creates new version of object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="object">The object to create new instance in repository</param>
		/// <param name="userID">The identity of user who created this verion of the object</param>
		/// <param name="cancellationToken">The cancellation token</param>
		public static Task<VersionContent> CreateVersionAsync<T>(RepositoryContext context, T @object, string userID = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			return RepositoryMediator.CreateVersionAsync<T>(context, null, @object, userID, cancellationToken);
		}

		/// <summary>
		/// Creates new version of object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="object">The object to create new instance in repository</param>
		/// <param name="userID">The identity of user who created this verion of the object</param>
		/// <param name="cancellationToken">The cancellation token</param>
		public static async Task<VersionContent> CreateVersionAsync<T>(T @object, string userID = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			using (var context = new RepositoryContext())
			{
				return await RepositoryMediator.CreateVersionAsync<T>(context, @object, userID, cancellationToken).ConfigureAwait(false);
			}
		}
		#endregion

		#region Rollback version
		/// <summary>
		/// Rollbacks an object from a version content
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="version">The object that presents information of a version content that use to rollback</param>
		/// <param name="userID">The identity of user who performs the rollback action</param>
		/// <returns></returns>
		public static T Rollback<T>(RepositoryContext context, VersionContent version, string userID) where T : class
		{
			// prepare
			if (version == null)
				throw new ArgumentNullException(nameof(version), "Version content is invalid");
			else if (!(version.Object is T))
				throw new InformationInvalidException($"Original object of the version content is not matched with type [{typeof(T).GetTypeName()}]");

			// process
			try
			{
				// get current object
				context.Operation = RepositoryOperation.Update;
				if (context.EntityDefinition == null)
					context.EntityDefinition = RepositoryMediator.GetEntityDefinition<T>();
				var dataSource = context.GetPrimaryDataSource();
				var @object = RepositoryMediator.Get<T>(context, dataSource, version.ObjectID, false);

				// call pre-handlers
				var changed = context.EntityDefinition.Attributes.Select(attribute => attribute.Name).ToHashSet();
				if (context.CallPreUpdateHandlers(@object, changed, true))
					return null;

				// create new version of current object
				if (@object is RepositoryBase)
					(@object as RepositoryBase).SearchScore = null;
				RepositoryMediator.CreateVersion(context, @object, userID);

				// rollback (update) with original object
				if (dataSource.Mode.Equals(RepositoryMode.NoSQL))
					context.Replace<T>(dataSource, version.Object as T, null);
				else if (dataSource.Mode.Equals(RepositoryMode.SQL))
					context.Replace<T>(dataSource, version.Object as T);

				// update into cache storage
				if (context.EntityDefinition.Cache != null)
#if DEBUG || PROCESSLOGS
					if (context.EntityDefinition.CacheStorage.Set(version.Object as T))
						RepositoryMediator.WriteLogs("ROLLBACK: Add the object into the cache storage successful [" + (version.Object as T).GetCacheKey(false) + "]");
#else
					context.EntityDefinition.Cache.Set(version.Object as T);
#endif

				// call post-handlers
				context.CallPostUpdateHandlers(version.Object as T, changed, true);

				// TO DO: sync to other data sources
				// ...

				// return the original object
				return version.Object as T;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				throw new RepositoryOperationException("Error occurred while performing rollback an object", ex);
			}
		}

		/// <summary>
		/// Rollbacks an object from a version content
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="version">The object that presents information of a version content that use to rollback</param>
		/// <param name="userID">The identity of user who performs the rollback action</param>
		/// <returns></returns>
		public static T Rollback<T>(VersionContent version, string userID) where T : class
		{
			using (var context = new RepositoryContext())
			{
				return RepositoryMediator.Rollback<T>(context, version, userID);
			}
		}

		/// <summary>
		/// Rollbacks an object from a version content
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="versionID">The identity of a version content that use to rollback</param>
		/// <param name="userID">The identity of user who performs the rollback action</param>
		/// <returns></returns>
		public static T Rollback<T>(RepositoryContext context, string versionID, string userID) where T : class
		{
			// prepare
			if (string.IsNullOrWhiteSpace(versionID))
				throw new ArgumentNullException(nameof(versionID), "The identity of version content is invalid");

			if (context.EntityDefinition == null)
				context.EntityDefinition = RepositoryMediator.GetEntityDefinition<T>();
			var dataSource = context.GetVersionDataSource();

			var versions = VersionContent.Find(dataSource, "Versions", Filters<VersionContent>.Equals("ID", versionID), null, 0, 1);
			if (versions == null || versions.Count < 1)
				throw new InformationInvalidException("The identity of version content is invalid");

			// rollback
			return RepositoryMediator.Rollback<T>(context, versions[0], userID);
		}

		/// <summary>
		/// Rollbacks an object from a version content
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="versionID">The identity of a version content that use to rollback</param>
		/// <param name="userID">The identity of user who performs the rollback action</param>
		/// <returns></returns>
		public static T Rollback<T>(string versionID, string userID) where T : class
		{
			using (var context = new RepositoryContext())
			{
				return RepositoryMediator.Rollback<T>(context, versionID, userID);
			}
		}

		/// <summary>
		/// Rollbacks an object from a version content
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="version">The object that presents information of a version content that use to rollback</param>
		/// <param name="userID">The identity of user who performs the rollback action</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<T> RollbackAsync<T>(RepositoryContext context, VersionContent version, string userID, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			// prepare
			if (version == null)
				throw new ArgumentNullException(nameof(version), "Version content is invalid");
			else if (!(version.Object is T))
				throw new InformationInvalidException($"Original object of the version content is not matched with type [{typeof(T).GetTypeName()}]");

			// process
			try
			{
				// get current object
				context.Operation = RepositoryOperation.Update;
				if (context.EntityDefinition == null)
					context.EntityDefinition = RepositoryMediator.GetEntityDefinition<T>();
				var dataSource = context.GetPrimaryDataSource();
				var @object = await RepositoryMediator.GetAsync<T>(context, dataSource, version.ObjectID, false, cancellationToken).ConfigureAwait(false);

				// call pre-handlers
				var changed = context.EntityDefinition.Attributes.Select(attribute => attribute.Name).ToHashSet();
				if (await context.CallPreUpdateHandlersAsync(@object, changed, true, cancellationToken).ConfigureAwait(false))
					return null;

				// create new version of current object
				if (@object is RepositoryBase)
					(@object as RepositoryBase).SearchScore = null;
				await RepositoryMediator.CreateVersionAsync(context, @object, userID, cancellationToken).ConfigureAwait(false);

				// rollback (update) with original object
				if (dataSource.Mode.Equals(RepositoryMode.NoSQL))
					await context.ReplaceAsync<T>(dataSource, version.Object as T, null, cancellationToken).ConfigureAwait(false);
				else if (dataSource.Mode.Equals(RepositoryMode.SQL))
					await context.ReplaceAsync<T>(dataSource, version.Object as T, cancellationToken).ConfigureAwait(false);

				// update into cache storage
				if (context.EntityDefinition.Cache != null)
#if DEBUG || PROCESSLOGS
					if (await context.EntityDefinition.CacheStorage.SetAsync(version.Object as T))
						RepositoryMediator.WriteLogs("ROLLBACK: Add the object into the cache storage successful [" + (version.Object as T).GetCacheKey(false) + "]");
#else
					await context.EntityDefinition.Cache.SetAsync(version.Object as T);
#endif

				// call post-handlers
				await context.CallPostUpdateHandlersAsync(version.Object as T, changed, true, cancellationToken).ConfigureAwait(false);

				// TO DO: sync to other data sources
				// ...

				// return the original object
				return version.Object as T;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				throw new RepositoryOperationException("Error occurred while performing rollback an object", ex);
			}
		}

		/// <summary>
		/// Rollbacks an object from a version content
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="version">The object that presents information of a version content that use to rollback</param>
		/// <param name="userID">The identity of user who performs the rollback action</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<T> RollbackAsync<T>(VersionContent version, string userID, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			using (var context = new RepositoryContext())
			{
				return await RepositoryMediator.RollbackAsync<T>(context, version, userID, cancellationToken).ConfigureAwait(false);
			}
		}

		/// <summary>
		/// Rollbacks an object from a version content
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="versionID">The identity of a version content that use to rollback</param>
		/// <param name="userID">The identity of user who performs the rollback action</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<T> RollbackAsync<T>(RepositoryContext context, string versionID, string userID, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			// prepare
			if (string.IsNullOrWhiteSpace(versionID))
				throw new ArgumentNullException(nameof(versionID), "The identity of version content is invalid");

			if (context.EntityDefinition == null)
				context.EntityDefinition = RepositoryMediator.GetEntityDefinition<T>();
			var dataSource = context.GetVersionDataSource();

			var versions = await VersionContent.FindAsync(dataSource, "Versions", Filters<VersionContent>.Equals("ID", versionID), null, 0, 1, cancellationToken).ConfigureAwait(false);
			if (versions == null || versions.Count < 1)
				throw new InformationInvalidException("The identity of version content is invalid");

			// rollback
			return await RepositoryMediator.RollbackAsync<T>(context, versions[0], userID, cancellationToken).ConfigureAwait(false);
		}

		/// <summary>
		/// Rollbacks an object from a version content
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="versionID">The identity of a version content that use to rollback</param>
		/// <param name="userID">The identity of user who performs the rollback action</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<T> RollbackAsync<T>(string versionID, string userID, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			using (var context = new RepositoryContext())
			{
				return await RepositoryMediator.RollbackAsync<T>(context, versionID, userID, cancellationToken).ConfigureAwait(false);
			}
		}
		#endregion

		#region Count version contents
		static IFilterBy<VersionContent> PrepareVersionFilter(string objectID, string serviceName, string systemID, string repositoryID, string entityID, string userID)
		{
			if (!string.IsNullOrWhiteSpace(objectID))
				return Filters<VersionContent>.Equals("ObjectID", objectID);
			else
			{
				var filter = Filters<VersionContent>.And();
				if (!string.IsNullOrWhiteSpace(serviceName))
					filter.Add(Filters<VersionContent>.Equals("ServiceName", serviceName));
				if (!string.IsNullOrWhiteSpace(systemID))
					filter.Add(Filters<VersionContent>.Equals("SystemID", systemID));
				if (!string.IsNullOrWhiteSpace(repositoryID))
					filter.Add(Filters<VersionContent>.Equals("RepositoryID", repositoryID));
				if (!string.IsNullOrWhiteSpace(entityID))
					filter.Add(Filters<VersionContent>.Equals("EntityID", entityID));
				if (!string.IsNullOrWhiteSpace(userID))
					filter.Add(Filters<VersionContent>.Equals("CreatedID", userID));
				return filter;
			}
		}

		/// <summary>
		/// Counts the number of version contents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="objectID">The identity of object that associates with</param>
		/// <param name="serviceName">The name of service that associates with</param>
		/// <param name="systemID">The identity of system that associates with</param>
		/// <param name="repositoryID">The identity of repository that associates with</param>
		/// <param name="entityID">The identity of business repository entity that associates with</param>
		/// <param name="userID">The identity of user who created this verion of the object</param>
		/// <returns></returns>
		public static long CountVersionContents<T>(RepositoryContext context, DataSource dataSource, string objectID, string serviceName = null, string systemID = null, string repositoryID = null, string entityID = null, string userID = null) where T : class
		{
			try
			{
				// prepare
				if (dataSource == null)
				{
					if (context.EntityDefinition == null)
						context.EntityDefinition = RepositoryMediator.GetEntityDefinition(typeof(T));
					dataSource = context.GetVersionDataSource();
				}

				// check
				if (dataSource == null)
					return 0;

				// count
				var filter = RepositoryMediator.PrepareVersionFilter(objectID, serviceName, systemID, repositoryID, entityID, userID);
				return VersionContent.Count<VersionContent>(dataSource, "Versions", filter);
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				throw new RepositoryOperationException("Error occurred while counting version contents", ex);
			}
		}

		/// <summary>
		/// Counts the number of version contents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="objectID">The identity of object that associates with</param>
		/// <param name="serviceName">The name of service that associates with</param>
		/// <param name="systemID">The identity of system that associates with</param>
		/// <param name="repositoryID">The identity of repository that associates with</param>
		/// <param name="entityID">The identity of business repository entity that associates with</param>
		/// <param name="userID">The identity of user who created this verion of the object</param>
		/// <returns></returns>
		public static long CountVersionContents<T>(RepositoryContext context, string objectID, string serviceName = null, string systemID = null, string repositoryID = null, string entityID = null, string userID = null) where T : class
		{
			return RepositoryMediator.CountVersionContents<T>(context, null, objectID, serviceName, systemID, repositoryID, entityID, userID);
		}

		/// <summary>
		/// Counts the number of version contents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="objectID">The identity of object that associates with</param>
		/// <param name="serviceName">The name of service that associates with</param>
		/// <param name="systemID">The identity of system that associates with</param>
		/// <param name="repositoryID">The identity of repository that associates with</param>
		/// <param name="entityID">The identity of business repository entity that associates with</param>
		/// <param name="userID">The identity of user who created this verion of the object</param>
		/// <returns></returns>
		public static long CountVersionContents<T>(string objectID, string serviceName = null, string systemID = null, string repositoryID = null, string entityID = null, string userID = null) where T : class
		{
			using (var context = new RepositoryContext(false))
			{
				return RepositoryMediator.CountVersionContents<T>(context, objectID, serviceName, systemID, repositoryID, entityID, userID);
			}
		}

		/// <summary>
		/// Counts the number of version contents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="objectID">The identity of object that associates with</param>
		/// <param name="serviceName">The name of service that associates with</param>
		/// <param name="systemID">The identity of system that associates with</param>
		/// <param name="repositoryID">The identity of repository that associates with</param>
		/// <param name="entityID">The identity of business repository entity that associates with</param>
		/// <param name="userID">The identity of user who created this verion of the object</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<long> CountVersionContentsAsync<T>(RepositoryContext context, DataSource dataSource, string objectID, string serviceName = null, string systemID = null, string repositoryID = null, string entityID = null, string userID = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			try
			{
				// prepare
				if (dataSource == null)
				{
					if (context.EntityDefinition == null)
						context.EntityDefinition = RepositoryMediator.GetEntityDefinition(typeof(T));
					dataSource = context.GetVersionDataSource();
				}

				// check
				if (dataSource == null)
					return 0;

				// count
				var filter = RepositoryMediator.PrepareVersionFilter(objectID, serviceName, systemID, repositoryID, entityID, userID);
				return await VersionContent.CountAsync<VersionContent>(dataSource, "Versions", filter, cancellationToken).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				throw new RepositoryOperationException("Error occurred while counting version contents", ex);
			}
		}

		/// <summary>
		/// Counts the number of version contents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="objectID">The identity of object that associates with</param>
		/// <param name="serviceName">The name of service that associates with</param>
		/// <param name="systemID">The identity of system that associates with</param>
		/// <param name="repositoryID">The identity of repository that associates with</param>
		/// <param name="entityID">The identity of business repository entity that associates with</param>
		/// <param name="userID">The identity of user who created this verion of the object</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<long> CountVersionContentsAsync<T>(RepositoryContext context, string objectID, string serviceName = null, string systemID = null, string repositoryID = null, string entityID = null, string userID = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			return RepositoryMediator.CountVersionContentsAsync<T>(context, null, objectID, serviceName, systemID, repositoryID, entityID, userID, cancellationToken);
		}

		/// <summary>
		/// Counts the number of version contents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="objectID">The identity of object that associates with</param>
		/// <param name="serviceName">The name of service that associates with</param>
		/// <param name="systemID">The identity of system that associates with</param>
		/// <param name="repositoryID">The identity of repository that associates with</param>
		/// <param name="entityID">The identity of business repository entity that associates with</param>
		/// <param name="userID">The identity of user who created this verion of the object</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<long> CountVersionContentsAsync<T>(string objectID, string serviceName = null, string systemID = null, string repositoryID = null, string entityID = null, string userID = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			using (var context = new RepositoryContext(false))
			{
				return await RepositoryMediator.CountVersionContentsAsync<T>(context, objectID, serviceName, systemID, repositoryID, entityID, userID, cancellationToken).ConfigureAwait(false);
			}
		}

		/// <summary>
		/// Counts the number of version contents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="objectID">The identity of object that associates with</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<long> CountVersionContentsAsync<T>(RepositoryContext context, DataSource dataSource, string objectID, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			return RepositoryMediator.CountVersionContentsAsync<T>(context, dataSource, objectID, null, null, null, null, null, cancellationToken);
		}

		/// <summary>
		/// Counts the number of version contents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="objectID">The identity of object that associates with</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<long> CountVersionContentsAsync<T>(RepositoryContext context, string objectID, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			return RepositoryMediator.CountVersionContentsAsync<T>(context, null, objectID, cancellationToken);
		}

		/// <summary>
		/// Counts the number of version contents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="objectID">The identity of object that associates with</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<long> CountVersionContentsAsync<T>(string objectID, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			using (var context = new RepositoryContext(false))
			{
				return await RepositoryMediator.CountVersionContentsAsync<T>(context, objectID, cancellationToken).ConfigureAwait(false);
			}
		}
		#endregion

		#region Find version contents
		/// <summary>
		/// Gets the collection of version contents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="objectID">The identity of object that associates with</param>
		/// <param name="serviceName">The name of service that associates with</param>
		/// <param name="systemID">The identity of system that associates with</param>
		/// <param name="repositoryID">The identity of repository that associates with</param>
		/// <param name="entityID">The identity of business repository entity that associates with</param>
		/// <param name="userID">The identity of user who created this verion of the object</param>
		/// <param name="pageSize">The identity of business repository entity that associates with</param>
		/// <param name="pageNumber">The identity of business repository entity that associates with</param>
		/// <returns></returns>
		public static List<VersionContent> FindVersionContents<T>(RepositoryContext context, DataSource dataSource, string objectID, string serviceName = null, string systemID = null, string repositoryID = null, string entityID = null, string userID = null, int pageSize = 0, int pageNumber = 1) where T : class
		{
			try
			{
				// prepare
				if (dataSource == null)
				{
					if (context.EntityDefinition == null)
						context.EntityDefinition = RepositoryMediator.GetEntityDefinition(typeof(T));
					dataSource = context.GetVersionDataSource();
				}

				// check
				if (dataSource == null)
					return null;

				// find
				var filter = RepositoryMediator.PrepareVersionFilter(objectID, serviceName, systemID, repositoryID, entityID, userID);
				var sort = Sorts<VersionContent>.Descending(string.IsNullOrWhiteSpace(objectID) ? "Created" : "VersionNumber");
				return VersionContent.Find<VersionContent>(dataSource, "Versions", filter, sort, pageSize, pageNumber);
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				throw new RepositoryOperationException("Error occurred while fetching version contents", ex);
			}
		}

		/// <summary>
		/// Gets the collection of version contents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="objectID">The identity of object that associates with</param>
		/// <param name="serviceName">The name of service that associates with</param>
		/// <param name="systemID">The identity of system that associates with</param>
		/// <param name="repositoryID">The identity of repository that associates with</param>
		/// <param name="entityID">The identity of business repository entity that associates with</param>
		/// <param name="userID">The identity of user who created this verion of the object</param>
		/// <param name="pageSize">The identity of business repository entity that associates with</param>
		/// <param name="pageNumber">The identity of business repository entity that associates with</param>
		/// <returns></returns>
		public static List<VersionContent> FindVersionContents<T>(RepositoryContext context, string objectID, string serviceName = null, string systemID = null, string repositoryID = null, string entityID = null, string userID = null, int pageSize = 0, int pageNumber = 1) where T : class
		{
			return RepositoryMediator.FindVersionContents<T>(context, null, objectID, serviceName, systemID, repositoryID, entityID, userID, pageSize, pageNumber);
		}

		/// <summary>
		/// Gets the collection of version contents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="objectID">The identity of object that associates with</param>
		/// <param name="serviceName">The name of service that associates with</param>
		/// <param name="systemID">The identity of system that associates with</param>
		/// <param name="repositoryID">The identity of repository that associates with</param>
		/// <param name="entityID">The identity of business repository entity that associates with</param>
		/// <param name="userID">The identity of user who created this verion of the object</param>
		/// <param name="pageSize">The identity of business repository entity that associates with</param>
		/// <param name="pageNumber">The identity of business repository entity that associates with</param>
		/// <returns></returns>
		public static List<VersionContent> FindVersionContents<T>(string objectID, string serviceName = null, string systemID = null, string repositoryID = null, string entityID = null, string userID = null, int pageSize = 0, int pageNumber = 1) where T : class
		{
			using (var context = new RepositoryContext(false))
			{
				return RepositoryMediator.FindVersionContents<T>(context, objectID, serviceName, systemID, repositoryID, entityID, userID, pageSize, pageNumber);
			}
		}

		/// <summary>
		/// Gets the collection of version contents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="objectID">The identity of object that associates with</param>
		/// <returns></returns>
		public static List<VersionContent> FindVersionContents<T>(RepositoryContext context, DataSource dataSource, string objectID) where T : class
		{
			return string.IsNullOrWhiteSpace(objectID) || !objectID.IsValidUUID()
				? throw new ArgumentNullException(nameof(objectID), "Object identity is invalid")
				: RepositoryMediator.FindVersionContents<T>(context, dataSource, objectID, null, null, null, null, null, 0, 1);
		}

		/// <summary>
		/// Gets the collection of version contents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="objectID">The identity of object that associates with</param>
		/// <returns></returns>
		public static List<VersionContent> FindVersionContents<T>(RepositoryContext context, string objectID) where T : class
		{
			return RepositoryMediator.FindVersionContents<T>(context, null, objectID);
		}

		/// <summary>
		/// Gets the collection of version contents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="objectID">The identity of object that associates with</param>
		/// <returns></returns>
		public static List<VersionContent> FindVersionContents<T>(string objectID) where T : class
		{
			using (var context = new RepositoryContext(false))
			{
				return RepositoryMediator.FindVersionContents<T>(context, objectID);
			}
		}

		/// <summary>
		/// Gets the collection of version contents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="objectID">The identity of object that associates with</param>
		/// <param name="serviceName">The name of service that associates with</param>
		/// <param name="systemID">The identity of system that associates with</param>
		/// <param name="repositoryID">The identity of repository that associates with</param>
		/// <param name="entityID">The identity of business repository entity that associates with</param>
		/// <param name="userID">The identity of user who created this verion of the object</param>
		/// <param name="pageSize">The identity of business repository entity that associates with</param>
		/// <param name="pageNumber">The identity of business repository entity that associates with</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<List<VersionContent>> FindVersionContentsAsync<T>(RepositoryContext context, DataSource dataSource, string objectID, string serviceName = null, string systemID = null, string repositoryID = null, string entityID = null, string userID = null, int pageSize = 0, int pageNumber = 1, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			try
			{
				// prepare
				if (dataSource == null)
				{
					if (context.EntityDefinition == null)
						context.EntityDefinition = RepositoryMediator.GetEntityDefinition(typeof(T));
					dataSource = context.GetVersionDataSource();
				}

				// check
				if (dataSource == null)
					return null;

				// find
				var filter = RepositoryMediator.PrepareVersionFilter(objectID, serviceName, systemID, repositoryID, entityID, userID);
				var sort = Sorts<VersionContent>.Descending(string.IsNullOrWhiteSpace(objectID) ? "Created" : "VersionNumber");
				return await VersionContent.FindAsync<VersionContent>(dataSource, "Versions", filter, sort, pageSize, pageNumber, cancellationToken).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				throw new RepositoryOperationException("Error occurred while fetching version contents", ex);
			}
		}

		/// <summary>
		/// Gets the collection of version contents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="objectID">The identity of object that associates with</param>
		/// <param name="serviceName">The name of service that associates with</param>
		/// <param name="systemID">The identity of system that associates with</param>
		/// <param name="repositoryID">The identity of repository that associates with</param>
		/// <param name="entityID">The identity of business repository entity that associates with</param>
		/// <param name="userID">The identity of user who created this verion of the object</param>
		/// <param name="pageSize">The identity of business repository entity that associates with</param>
		/// <param name="pageNumber">The identity of business repository entity that associates with</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<List<VersionContent>> FindVersionContentsAsync<T>(RepositoryContext context, string objectID, string serviceName = null, string systemID = null, string repositoryID = null, string entityID = null, string userID = null, int pageSize = 0, int pageNumber = 1, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			return RepositoryMediator.FindVersionContentsAsync<T>(context, null, objectID, serviceName, systemID, repositoryID, entityID, userID, pageSize, pageNumber, cancellationToken);
		}

		/// <summary>
		/// Gets the collection of version contents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="objectID">The identity of object that associates with</param>
		/// <param name="serviceName">The name of service that associates with</param>
		/// <param name="systemID">The identity of system that associates with</param>
		/// <param name="repositoryID">The identity of repository that associates with</param>
		/// <param name="entityID">The identity of business repository entity that associates with</param>
		/// <param name="userID">The identity of user who created this verion of the object</param>
		/// <param name="pageSize">The identity of business repository entity that associates with</param>
		/// <param name="pageNumber">The identity of business repository entity that associates with</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<List<VersionContent>> FindVersionContentsAsync<T>(string objectID, string serviceName = null, string systemID = null, string repositoryID = null, string entityID = null, string userID = null, int pageSize = 0, int pageNumber = 1, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			using (var context = new RepositoryContext(false))
			{
				return await RepositoryMediator.FindVersionContentsAsync<T>(context, objectID, serviceName, systemID, repositoryID, entityID, userID, pageSize, pageNumber, cancellationToken);
			}
		}

		/// <summary>
		/// Gets the collection of version contents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="objectID">The identity of object that associates with</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<List<VersionContent>> FindVersionContentsAsync<T>(RepositoryContext context, DataSource dataSource, string objectID, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			return string.IsNullOrWhiteSpace(objectID) || !objectID.IsValidUUID()
				? Task.FromException<List<VersionContent>>(new ArgumentNullException(nameof(objectID), "Object identity is invalid"))
				: RepositoryMediator.FindVersionContentsAsync<T>(context, dataSource, objectID, null, null, null, null, null, 0, 1, cancellationToken);
		}

		/// <summary>
		/// Gets the collection of version contents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="objectID">The identity of object that associates with</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<List<VersionContent>> FindVersionContentsAsync<T>(RepositoryContext context, string objectID, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			return RepositoryMediator.FindVersionContentsAsync<T>(context, null, objectID, cancellationToken);
		}

		/// <summary>
		/// Gets the collection of version contents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="objectID">The identity of object that associates with</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<List<VersionContent>> FindVersionContentsAsync<T>(string objectID, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			using (var context = new RepositoryContext(false))
			{
				return await RepositoryMediator.FindVersionContentsAsync<T>(context, objectID, cancellationToken);
			}
		}
		#endregion

		#region Clean version contents
		/// <summary>
		/// Cleans old version contents
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="days">The remain days</param>
		public static void CleanVersionContents(RepositoryContext context, DataSource dataSource, int days = 30)
		{
			try
			{
				context.Operation = RepositoryOperation.Delete;
				VersionContent.Delete(dataSource ?? context.GetVersionDataSource(), "Versions", Filters<VersionContent>.LessThanOrEquals("Created", DateTime.Now.AddDays(0 - (days > 0 ? days : 30))));
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				throw new RepositoryOperationException("Error occurred while cleaning old version contents", ex);
			}
		}

		/// <summary>
		/// Cleans old version contents
		/// </summary>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="days">The remain days</param>
		public static void CleanVersionContents(DataSource dataSource, int days = 30)
		{
			using (var context = new RepositoryContext())
			{
				RepositoryMediator.CleanVersionContents(context, dataSource, days);
			}
		}

		/// <summary>
		/// Cleans old version contents
		/// </summary>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="days">The remain days</param>
		public static void CleanVersionContents(string dataSource, int days = 30)
		{
			RepositoryMediator.CleanVersionContents(!string.IsNullOrWhiteSpace(dataSource) && RepositoryMediator.DataSources.ContainsKey(dataSource) ? RepositoryMediator.DataSources[dataSource] : null, days);
		}

		/// <summary>
		/// Cleans old version contents
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="days">The remain days</param>
		public static async Task CleanVersionContentsAsync(RepositoryContext context, DataSource dataSource, int days = 30, CancellationToken cancellationToken = default(CancellationToken))
		{
			try
			{
				context.Operation = RepositoryOperation.Delete;
				await VersionContent.DeleteAsync(dataSource ?? context.GetVersionDataSource(), "Versions", Filters<VersionContent>.LessThanOrEquals("Created", DateTime.Now.AddDays(0 - (days > 0 ? days : 30))), cancellationToken).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				throw new RepositoryOperationException("Error occurred while cleaning old version contents", ex);
			}
		}

		/// <summary>
		/// Cleans old version contents
		/// </summary>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="days">The remain days</param>
		public static async Task CleanVersionContentsAsync(DataSource dataSource, int days = 30, CancellationToken cancellationToken = default(CancellationToken))
		{
			using (var context = new RepositoryContext())
			{
				await RepositoryMediator.CleanVersionContentsAsync(context, dataSource, days, cancellationToken).ConfigureAwait(false);
			}
		}

		/// <summary>
		/// Cleans old version contents
		/// </summary>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="days">The remain days</param>
		public static Task CleanVersionContentsAsync(string dataSource, int days = 30, CancellationToken cancellationToken = default(CancellationToken))
		{
			return RepositoryMediator.CleanVersionContentsAsync(!string.IsNullOrWhiteSpace(dataSource) && RepositoryMediator.DataSources.ContainsKey(dataSource) ? RepositoryMediator.DataSources[dataSource] : null, days, cancellationToken);
		}
		#endregion

		#region Create trash content
		/// <summary>
		/// Creates new trash content of object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="object">The object to create new instance in repository</param>
		/// <param name="userID">The identity of user who created this trash content of the object (means who deletes the object)</param>
		public static TrashContent CreateTrashContent<T>(RepositoryContext context, DataSource dataSource, T @object, string userID = null) where T : class
		{
			try
			{
				// prepare
				if (@object == null)
					throw new ArgumentNullException(nameof(@object), "The object is null");

				if (context.EntityDefinition == null)
					context.EntityDefinition = RepositoryMediator.GetEntityDefinition<T>();

				dataSource = dataSource ?? RepositoryMediator.GetTrashDataSource(context);
				if (dataSource == null)
					return null;

				// create new
				var trash = TrashContent.Prepare(@object, (content) => content.CreatedID = userID ?? "");
				try
				{
					TrashContent.Create(dataSource, "Trashs", trash);
				}
				catch
				{
					TrashContent.Delete(dataSource, "Trashs", Filters<TrashContent>.Equals("ID", trash.ID));
					TrashContent.Create(dataSource, "Trashs", trash);
				}
				return trash;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				throw new RepositoryOperationException("Error occurred while creating new trash content of an object", ex);
			}
		}

		/// <summary>
		/// Creates new trash content of object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="object">The object to create new instance in repository</param>
		/// <param name="userID">The identity of user who created this trash content of the object (means who deletes the object)</param>
		public static TrashContent CreateTrashContent<T>(RepositoryContext context, T @object, string userID = null) where T : class
		{
			return RepositoryMediator.CreateTrashContent<T>(context, null, @object, userID);
		}

		/// <summary>
		/// Creates new trash content of object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="object">The object to create new instance in repository</param>
		/// <param name="userID">The identity of user who created this trash content of the object (means who deletes the object)</param>
		public static TrashContent CreateTrashContent<T>(T @object, string userID = null) where T : class
		{
			using (var context = new RepositoryContext())
			{
				return RepositoryMediator.CreateTrashContent<T>(context, @object, userID);
			}
		}

		/// <summary>
		/// Creates new trash content of object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="object">The object to create new instance in repository</param>
		/// <param name="userID">The identity of user who created this trash content of the object (means who deletes the object)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		public static async Task<TrashContent> CreateTrashContentAsync<T>(RepositoryContext context, DataSource dataSource, T @object, string userID = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			try
			{
				// prepare
				if (@object == null)
					throw new ArgumentNullException(nameof(@object), "The object is null");

				if (context.EntityDefinition == null)
					context.EntityDefinition = RepositoryMediator.GetEntityDefinition<T>();

				dataSource = dataSource ?? RepositoryMediator.GetTrashDataSource(context);
				if (dataSource == null)
					return null;

				// create new
				var trash = TrashContent.Prepare(@object, (content) => content.CreatedID = userID ?? "");
				try
				{
					await TrashContent.CreateAsync(dataSource, "Trashs", trash, cancellationToken).ConfigureAwait(false);
				}
				catch
				{
					await TrashContent.DeleteAsync(dataSource, "Trashs", Filters<TrashContent>.Equals("ID", trash.ID), cancellationToken).ConfigureAwait(false);
					await TrashContent.CreateAsync(dataSource, "Trashs", trash, cancellationToken).ConfigureAwait(false);
				}
				return trash;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				throw new RepositoryOperationException("Error occurred while creating new trash content of an object", ex);
			}
		}

		/// <summary>
		/// Creates new trash content of object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="object">The object to create new instance in repository</param>
		/// <param name="userID">The identity of user who created this trash content of the object (means who deletes the object)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		public static Task<TrashContent> CreateTrashContentAsync<T>(RepositoryContext context, T @object, string userID = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			return RepositoryMediator.CreateTrashContentAsync<T>(context, null, @object, userID, cancellationToken);
		}

		/// <summary>
		/// Creates new trash content of object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="object">The object to create new instance in repository</param>
		/// <param name="userID">The identity of user who created this trash content of the object (means who deletes the object)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		public static async Task<TrashContent> CreateTrashContentAsync<T>(T @object, string userID = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			using (var context = new RepositoryContext())
			{
				return await RepositoryMediator.CreateTrashContentAsync<T>(context, @object, userID, cancellationToken).ConfigureAwait(false);
			}
		}
		#endregion

		#region Restore trash
		/// <summary>
		/// Restores an object from a trash content
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="trashContent">The object that presents information of a version content that use to rollback</param>
		/// <param name="userID">The identity of user who performs the restore action</param>
		/// <returns></returns>
		public static T Restore<T>(RepositoryContext context, TrashContent trashContent, string userID) where T : class
		{
			// prepare
			if (trashContent == null)
				throw new ArgumentNullException(nameof(trashContent), "Trash content is invalid");
			else if (!(trashContent.Object is T))
				throw new InformationInvalidException($"Original object of the trash content is not matched with type [{typeof(T).GetTypeName()}]");

			// process
			try
			{
				// prepare
				context.Operation = RepositoryOperation.Create;
				if (context.EntityDefinition == null)
					context.EntityDefinition = RepositoryMediator.GetEntityDefinition<T>();
				var dataSource = context.GetPrimaryDataSource();
				if (dataSource == null)
					return null;

				// call pre-handlers
				if (context.CallPreCreateHandlers(trashContent.Object, true))
					return null;

				// restore (create) with original object
				if (dataSource.Mode.Equals(RepositoryMode.NoSQL))
					context.Create<T>(dataSource, trashContent.Object as T, null);
				else if (dataSource.Mode.Equals(RepositoryMode.SQL))
					context.Create<T>(dataSource, trashContent.Object as T);

				// update into cache storage
				if (context.EntityDefinition.Cache != null)
#if DEBUG || PROCESSLOGS
					if (context.EntityDefinition.CacheStorage.Set(trash.Object as T))
						RepositoryMediator.WriteLogs("RESTORE: Add the object into the cache storage successful [" + (trash.Object as T).GetCacheKey(false) + "]");
#else
					context.EntityDefinition.Cache.Set(trashContent.Object as T);
#endif

				// call post-handlers
				context.CallPostCreateHandlers(trashContent.Object as T, true);

				// TO DO: sync to other data sources
				// ...

				// delete trash content
				TrashContent.Delete(RepositoryMediator.GetTrashDataSource(context), "Trashs", Filters<TrashContent>.Equals("ID", trashContent.ID));

				// return the original object
				return trashContent.Object as T;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				throw new RepositoryOperationException("Error occurred while performing restore an object", ex);
			}
		}

		/// <summary>
		/// Restores an object from a trash content
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="trashContent">The object that presents information of a version content that use to rollback</param>
		/// <param name="userID">The identity of user who performs the restore action</param>
		/// <returns></returns>
		public static T Restore<T>(TrashContent trashContent, string userID) where T : class
		{
			using (var context = new RepositoryContext())
			{
				return RepositoryMediator.Restore<T>(context, trashContent, userID);
			}
		}

		/// <summary>
		/// Restores an object from a trash content
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="trashContentID">The identity of a trash content that use to restore</param>
		/// <param name="userID">The identity of user who performs the restore action</param>
		/// <returns></returns>
		public static T Restore<T>(RepositoryContext context, string trashContentID, string userID) where T : class
		{
			// prepare
			if (string.IsNullOrWhiteSpace(trashContentID))
				throw new ArgumentNullException(nameof(trashContentID), "The identity of trash content is invalid");

			if (context.EntityDefinition == null)
				context.EntityDefinition = RepositoryMediator.GetEntityDefinition<T>();
			var dataSource = RepositoryMediator.GetTrashDataSource(context);

			var trashs = TrashContent.Find(dataSource, "Trashs", Filters<VersionContent>.Equals("ID", trashContentID), null, 0, 1);
			if (trashs == null || trashs.Count < 1)
				throw new InformationInvalidException("The identity of trash content is invalid");

			// rollback
			return RepositoryMediator.Restore<T>(context, trashs[0], userID);
		}

		/// <summary>
		/// Restores an object from a trash content
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="trashContentID">The identity of a trash content that use to restore</param>
		/// <param name="userID">The identity of user who performs the restore action</param>
		/// <returns></returns>
		public static T Restore<T>(string trashContentID, string userID) where T : class
		{
			using (var context = new RepositoryContext())
			{
				return RepositoryMediator.Restore<T>(context, trashContentID, userID);
			}
		}

		/// <summary>
		/// Restores an object from a trash content
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="trashContent">The object that presents information of a version content that use to rollback</param>
		/// <param name="userID">The identity of user who performs the restore action</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<T> RestoreAsync<T>(RepositoryContext context, TrashContent trashContent, string userID, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			// prepare
			if (trashContent == null)
				throw new ArgumentNullException(nameof(trashContent), "Trash content is invalid");
			else if (!(trashContent.Object is T))
				throw new InformationInvalidException($"Original object of the trash content is not matched with type [{typeof(T).GetTypeName()}]");

			// process
			try
			{
				// prepare
				context.Operation = RepositoryOperation.Create;
				if (context.EntityDefinition == null)
					context.EntityDefinition = RepositoryMediator.GetEntityDefinition<T>();
				var dataSource = context.GetPrimaryDataSource();
				if (dataSource == null)
					return null;

				// call pre-handlers
				if (await context.CallPreCreateHandlersAsync(trashContent.Object, true, cancellationToken).ConfigureAwait(false))
					return null;

				// restore (create) with original object
				if (dataSource.Mode.Equals(RepositoryMode.NoSQL))
					await context.CreateAsync<T>(dataSource, trashContent.Object as T, null, cancellationToken).ConfigureAwait(false);
				else if (dataSource.Mode.Equals(RepositoryMode.SQL))
					await context.CreateAsync<T>(dataSource, trashContent.Object as T, cancellationToken).ConfigureAwait(false);

				// update into cache storage
				if (context.EntityDefinition.Cache != null)
#if DEBUG || PROCESSLOGS
					if (await context.EntityDefinition.CacheStorage.SetAsync(trash.Object as T).ConfigureAwait(false))
						RepositoryMediator.WriteLogs("RESTORE: Add the object into the cache storage successful [" + (trash.Object as T).GetCacheKey(false) + "]");
#else
					await context.EntityDefinition.Cache.SetAsync(trashContent.Object as T).ConfigureAwait(false);
#endif

				// call post-handlers
				await context.CallPostCreateHandlersAsync(trashContent.Object as T, true, cancellationToken).ConfigureAwait(false);

				// TO DO: sync to other data sources
				// ...

				// delete trash content
				await TrashContent.DeleteAsync(RepositoryMediator.GetTrashDataSource(context), "Trashs", Filters<TrashContent>.Equals("ID", trashContent.ID), cancellationToken).ConfigureAwait(false);

				// return the original object
				return trashContent.Object as T;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				throw new RepositoryOperationException("Error occurred while performing restore an object", ex);
			}
		}

		/// <summary>
		/// Restores an object from a trash content
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="trashContent">The object that presents information of a version content that use to rollback</param>
		/// <param name="userID">The identity of user who performs the restore action</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<T> RestoreAsync<T>(TrashContent trashContent, string userID, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			using (var context = new RepositoryContext())
			{
				return await RepositoryMediator.RestoreAsync<T>(context, trashContent, userID, cancellationToken).ConfigureAwait(false);
			}
		}

		/// <summary>
		/// Restores an object from a trash content
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="trashContentID">The identity of a trash content that use to restore</param>
		/// <param name="userID">The identity of user who performs the restore action</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<T> RestoreAsync<T>(RepositoryContext context, string trashContentID, string userID, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			// prepare
			if (string.IsNullOrWhiteSpace(trashContentID))
				throw new ArgumentNullException(nameof(trashContentID), "The identity of trash content is invalid");

			if (context.EntityDefinition == null)
				context.EntityDefinition = RepositoryMediator.GetEntityDefinition<T>();
			var dataSource = RepositoryMediator.GetTrashDataSource(context);

			var trashs = await TrashContent.FindAsync(dataSource, "Trashs", Filters<VersionContent>.Equals("ID", trashContentID), null, 0, 1, cancellationToken).ConfigureAwait(false);
			if (trashs == null || trashs.Count < 1)
				throw new InformationInvalidException("The identity of trash content is invalid");

			// rollback
			return await RepositoryMediator.RestoreAsync<T>(context, trashs[0], userID, cancellationToken).ConfigureAwait(false);
		}

		/// <summary>
		/// Restores an object from a trash content
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="trashContentID">The identity of a trash content that use to restore</param>
		/// <param name="userID">The identity of user who performs the restore action</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<T> RestoreAsync<T>(string trashContentID, string userID, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			using (var context = new RepositoryContext())
			{
				return await RepositoryMediator.RestoreAsync<T>(context, trashContentID, userID, cancellationToken).ConfigureAwait(false);
			}
		}
		#endregion

		#region Count trash contents
		static IFilterBy<TrashContent> PrepareTrashFilter(string serviceName, string systemID, string repositoryID, string entityID, string userID)
		{
			var filter = Filters<TrashContent>.And();
			if (!string.IsNullOrWhiteSpace(serviceName))
				filter.Add(Filters<TrashContent>.Equals("ServiceName", serviceName));
			if (!string.IsNullOrWhiteSpace(systemID))
				filter.Add(Filters<TrashContent>.Equals("SystemID", systemID));
			if (!string.IsNullOrWhiteSpace(repositoryID))
				filter.Add(Filters<TrashContent>.Equals("RepositoryID", repositoryID));
			if (!string.IsNullOrWhiteSpace(entityID))
				filter.Add(Filters<TrashContent>.Equals("EntityID", entityID));
			if (!string.IsNullOrWhiteSpace(userID))
				filter.Add(Filters<TrashContent>.Equals("CreatedID", userID));
			return filter;
		}

		/// <summary>
		/// Counts the number of trash contents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="serviceName">The name of service that associates with</param>
		/// <param name="systemID">The identity of system that associates with</param>
		/// <param name="repositoryID">The identity of repository that associates with</param>
		/// <param name="entityID">The identity of business repository entity that associates with</param>
		/// <param name="userID">The identity of user who created this verion of the object</param>
		/// <returns></returns>
		public static long CountTrashContents<T>(RepositoryContext context, DataSource dataSource, string serviceName = null, string systemID = null, string repositoryID = null, string entityID = null, string userID = null) where T : class
		{
			try
			{
				// prepare
				if (dataSource == null)
				{
					if (context.EntityDefinition == null)
						context.EntityDefinition = RepositoryMediator.GetEntityDefinition(typeof(T));
					dataSource = RepositoryMediator.GetTrashDataSource(context);
				}

				// check
				if (dataSource == null)
					return 0;

				// count
				var filter = RepositoryMediator.PrepareTrashFilter(serviceName, systemID, repositoryID, entityID, userID);
				return TrashContent.Count<TrashContent>(dataSource, "Trashs", filter);
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				throw new RepositoryOperationException("Error occurred while counting trash contents", ex);
			}
		}

		/// <summary>
		/// Counts the number of trash contents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="serviceName">The name of service that associates with</param>
		/// <param name="systemID">The identity of system that associates with</param>
		/// <param name="repositoryID">The identity of repository that associates with</param>
		/// <param name="entityID">The identity of business repository entity that associates with</param>
		/// <param name="userID">The identity of user who created this verion of the object</param>
		/// <returns></returns>
		public static long CountTrashContents<T>(RepositoryContext context, string serviceName = null, string systemID = null, string repositoryID = null, string entityID = null, string userID = null) where T : class
		{
			return RepositoryMediator.CountTrashContents<T>(context, null, serviceName, systemID, repositoryID, entityID, userID);
		}

		/// <summary>
		/// Counts the number of trash contents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="serviceName">The name of service that associates with</param>
		/// <param name="systemID">The identity of system that associates with</param>
		/// <param name="repositoryID">The identity of repository that associates with</param>
		/// <param name="entityID">The identity of business repository entity that associates with</param>
		/// <param name="userID">The identity of user who created this verion of the object</param>
		/// <returns></returns>
		public static long CountTrashContents<T>(string serviceName = null, string systemID = null, string repositoryID = null, string entityID = null, string userID = null) where T : class
		{
			using (var context = new RepositoryContext(false))
			{
				return RepositoryMediator.CountTrashContents<T>(context, serviceName, systemID, repositoryID, entityID, userID);
			}
		}

		/// <summary>
		/// Counts the number of trash contents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="serviceName">The name of service that associates with</param>
		/// <param name="systemID">The identity of system that associates with</param>
		/// <param name="repositoryID">The identity of repository that associates with</param>
		/// <param name="entityID">The identity of business repository entity that associates with</param>
		/// <param name="userID">The identity of user who created this verion of the object</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<long> CountTrashContentsAsync<T>(RepositoryContext context, DataSource dataSource, string serviceName = null, string systemID = null, string repositoryID = null, string entityID = null, string userID = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			try
			{
				// prepare
				if (dataSource == null)
				{
					if (context.EntityDefinition == null)
						context.EntityDefinition = RepositoryMediator.GetEntityDefinition(typeof(T));
					dataSource = RepositoryMediator.GetTrashDataSource(context);
				}

				// check
				if (dataSource == null)
					return 0;

				// count
				var filter = RepositoryMediator.PrepareTrashFilter(serviceName, systemID, repositoryID, entityID, userID);
				return await TrashContent.CountAsync<TrashContent>(dataSource, "Trashs", filter, cancellationToken).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				throw new RepositoryOperationException("Error occurred while counting trash contents", ex);
			}
		}

		/// <summary>
		/// Counts the number of trash contents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="serviceName">The name of service that associates with</param>
		/// <param name="systemID">The identity of system that associates with</param>
		/// <param name="repositoryID">The identity of repository that associates with</param>
		/// <param name="entityID">The identity of business repository entity that associates with</param>
		/// <param name="userID">The identity of user who created this verion of the object</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<long> CountTrashContentsAsync<T>(RepositoryContext context, string serviceName = null, string systemID = null, string repositoryID = null, string entityID = null, string userID = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			return RepositoryMediator.CountTrashContentsAsync<T>(context, null, serviceName, systemID, repositoryID, entityID, userID, cancellationToken);
		}

		/// <summary>
		/// Counts the number of trash contents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="serviceName">The name of service that associates with</param>
		/// <param name="systemID">The identity of system that associates with</param>
		/// <param name="repositoryID">The identity of repository that associates with</param>
		/// <param name="entityID">The identity of business repository entity that associates with</param>
		/// <param name="userID">The identity of user who created this verion of the object</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<long> CountTrashContentsAsync<T>(string serviceName = null, string systemID = null, string repositoryID = null, string entityID = null, string userID = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			using (var context = new RepositoryContext(false))
			{
				return await RepositoryMediator.CountTrashContentsAsync<T>(context, serviceName, systemID, repositoryID, entityID, userID, cancellationToken).ConfigureAwait(false);
			}
		}

		/// <summary>
		/// Counts the number of version contents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="serviceName">The name of service that associates with</param>
		/// <param name="systemID">The identity of system that associates with</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<long> CountTrashContentsAsync<T>(RepositoryContext context, DataSource dataSource, string serviceName, string systemID, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			return RepositoryMediator.CountTrashContentsAsync<T>(context, dataSource, serviceName, systemID, null, null, null, cancellationToken);
		}

		/// <summary>
		/// Counts the number of version contents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="serviceName">The name of service that associates with</param>
		/// <param name="systemID">The identity of system that associates with</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<long> CountTrashContentsAsync<T>(RepositoryContext context, string serviceName, string systemID, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			return RepositoryMediator.CountTrashContentsAsync<T>(context, null, serviceName, systemID, cancellationToken);
		}

		/// <summary>
		/// Counts the number of version contents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="serviceName">The name of service that associates with</param>
		/// <param name="systemID">The identity of system that associates with</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<long> CountTrashContentsAsync<T>(string serviceName, string systemID, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			using (var context = new RepositoryContext(false))
			{
				return await RepositoryMediator.CountTrashContentsAsync<T>(context, serviceName, systemID, cancellationToken).ConfigureAwait(false);
			}
		}
		#endregion

		#region Find trash contents
		/// <summary>
		/// Gets the collection of trash contents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="serviceName">The name of service that associates with</param>
		/// <param name="systemID">The identity of system that associates with</param>
		/// <param name="repositoryID">The identity of repository that associates with</param>
		/// <param name="entityID">The identity of business repository entity that associates with</param>
		/// <param name="userID">The identity of user who created this verion of the object</param>
		/// <param name="pageSize">The identity of business repository entity that associates with</param>
		/// <param name="pageNumber">The identity of business repository entity that associates with</param>
		/// <returns></returns>
		public static List<TrashContent> FindTrashContents<T>(RepositoryContext context, DataSource dataSource, string serviceName = null, string systemID = null, string repositoryID = null, string entityID = null, string userID = null, int pageSize = 0, int pageNumber = 1) where T : class
		{
			try
			{
				// prepare
				if (dataSource == null)
				{
					if (context.EntityDefinition == null)
						context.EntityDefinition = RepositoryMediator.GetEntityDefinition(typeof(T));
					dataSource = RepositoryMediator.GetTrashDataSource(context);
				}

				// check
				if (dataSource == null)
					return null;

				// find
				var filter = RepositoryMediator.PrepareTrashFilter(serviceName, systemID, repositoryID, entityID, userID);
				var sort = Sorts<TrashContent>.Descending("Created");
				return TrashContent.Find<TrashContent>(dataSource, "Trashs", filter, sort, pageSize, pageNumber);
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				throw new RepositoryOperationException("Error occurred while fetching trash contents", ex);
			}
		}

		/// <summary>
		/// Gets the collection of trash contents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="serviceName">The name of service that associates with</param>
		/// <param name="systemID">The identity of system that associates with</param>
		/// <param name="repositoryID">The identity of repository that associates with</param>
		/// <param name="entityID">The identity of business repository entity that associates with</param>
		/// <param name="userID">The identity of user who created this verion of the object</param>
		/// <param name="pageSize">The identity of business repository entity that associates with</param>
		/// <param name="pageNumber">The identity of business repository entity that associates with</param>
		/// <returns></returns>
		public static List<TrashContent> FindTrashContents<T>(RepositoryContext context, string serviceName = null, string systemID = null, string repositoryID = null, string entityID = null, string userID = null, int pageSize = 0, int pageNumber = 1) where T : class
		{
			return RepositoryMediator.FindTrashContents<T>(context, null, serviceName, systemID, repositoryID, entityID, userID, pageSize, pageNumber);
		}

		/// <summary>
		/// Gets the collection of trash contents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="serviceName">The name of service that associates with</param>
		/// <param name="systemID">The identity of system that associates with</param>
		/// <param name="repositoryID">The identity of repository that associates with</param>
		/// <param name="entityID">The identity of business repository entity that associates with</param>
		/// <param name="userID">The identity of user who created this verion of the object</param>
		/// <param name="pageSize">The identity of business repository entity that associates with</param>
		/// <param name="pageNumber">The identity of business repository entity that associates with</param>
		/// <returns></returns>
		public static List<TrashContent> FindTrashContents<T>(string serviceName = null, string systemID = null, string repositoryID = null, string entityID = null, string userID = null, int pageSize = 0, int pageNumber = 1) where T : class
		{
			using (var context = new RepositoryContext(false))
			{
				return RepositoryMediator.FindTrashContents<T>(context, serviceName, systemID, repositoryID, entityID, userID, pageSize, pageNumber);
			}
		}

		/// <summary>
		/// Gets the collection of trash contents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="serviceName">The name of service that associates with</param>
		/// <param name="systemID">The identity of system that associates with</param>
		/// <param name="pageSize">The identity of business repository entity that associates with</param>
		/// <param name="pageNumber">The identity of business repository entity that associates with</param>
		/// <returns></returns>
		public static List<TrashContent> FindTrashContents<T>(RepositoryContext context, DataSource dataSource, string serviceName, string systemID, int pageSize = 20, int pageNumber = 1) where T : class
		{
			return string.IsNullOrWhiteSpace(serviceName) && string.IsNullOrWhiteSpace(systemID)
				? throw new InformationRequiredException("Service name or system identity is required")
				: RepositoryMediator.FindTrashContents<T>(context, dataSource, serviceName, systemID, null, null, null, pageSize, pageNumber);
		}

		/// <summary>
		/// Gets the collection of trash contents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="serviceName">The name of service that associates with</param>
		/// <param name="systemID">The identity of system that associates with</param>
		/// <param name="pageSize">The identity of business repository entity that associates with</param>
		/// <param name="pageNumber">The identity of business repository entity that associates with</param>
		/// <returns></returns>
		public static List<TrashContent> FindTrashContents<T>(RepositoryContext context, string serviceName, string systemID, int pageSize = 20, int pageNumber = 1) where T : class
		{
			return RepositoryMediator.FindTrashContents<T>(context, null, serviceName, systemID, pageSize, pageNumber);
		}

		/// <summary>
		/// Gets the collection of trash contents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="serviceName">The name of service that associates with</param>
		/// <param name="systemID">The identity of system that associates with</param>
		/// <param name="pageSize">The identity of business repository entity that associates with</param>
		/// <param name="pageNumber">The identity of business repository entity that associates with</param>
		/// <returns></returns>
		public static List<TrashContent> FindTrashContents<T>(string serviceName, string systemID, int pageSize = 20, int pageNumber = 1) where T : class
		{
			using (var context = new RepositoryContext(false))
			{
				return RepositoryMediator.FindTrashContents<T>(context, serviceName, systemID, pageSize, pageNumber);
			}
		}

		/// <summary>
		/// Gets the collection of trash contents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="serviceName">The name of service that associates with</param>
		/// <param name="systemID">The identity of system that associates with</param>
		/// <param name="repositoryID">The identity of repository that associates with</param>
		/// <param name="entityID">The identity of business repository entity that associates with</param>
		/// <param name="userID">The identity of user who created this verion of the object</param>
		/// <param name="pageSize">The identity of business repository entity that associates with</param>
		/// <param name="pageNumber">The identity of business repository entity that associates with</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<List<TrashContent>> FindTrashContentsAsync<T>(RepositoryContext context, DataSource dataSource, string serviceName = null, string systemID = null, string repositoryID = null, string entityID = null, string userID = null, int pageSize = 0, int pageNumber = 1, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			try
			{
				// prepare
				if (dataSource == null)
				{
					if (context.EntityDefinition == null)
						context.EntityDefinition = RepositoryMediator.GetEntityDefinition(typeof(T));
					dataSource = RepositoryMediator.GetTrashDataSource(context);
				}

				// check
				if (dataSource == null)
					return null;

				// find
				var filter = RepositoryMediator.PrepareTrashFilter(serviceName, systemID, repositoryID, entityID, userID);
				var sort = Sorts<TrashContent>.Descending("Created");
				return await TrashContent.FindAsync<TrashContent>(dataSource, "Trashs", filter, sort, pageSize, pageNumber, cancellationToken).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				throw new RepositoryOperationException("Error occurred while fetching trash contents of an object", ex);
			}
		}

		/// <summary>
		/// Gets the collection of trash contents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="serviceName">The name of service that associates with</param>
		/// <param name="systemID">The identity of system that associates with</param>
		/// <param name="repositoryID">The identity of repository that associates with</param>
		/// <param name="entityID">The identity of business repository entity that associates with</param>
		/// <param name="userID">The identity of user who created this verion of the object</param>
		/// <param name="pageSize">The identity of business repository entity that associates with</param>
		/// <param name="pageNumber">The identity of business repository entity that associates with</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<List<TrashContent>> FindTrashContentsAsync<T>(RepositoryContext context, string serviceName = null, string systemID = null, string repositoryID = null, string entityID = null, string userID = null, int pageSize = 0, int pageNumber = 1, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			return RepositoryMediator.FindTrashContentsAsync<T>(context, null, serviceName, systemID, repositoryID, entityID, userID, pageSize, pageNumber, cancellationToken);
		}

		/// <summary>
		/// Gets the collection of trash contents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="serviceName">The name of service that associates with</param>
		/// <param name="systemID">The identity of system that associates with</param>
		/// <param name="repositoryID">The identity of repository that associates with</param>
		/// <param name="entityID">The identity of business repository entity that associates with</param>
		/// <param name="userID">The identity of user who created this verion of the object</param>
		/// <param name="pageSize">The identity of business repository entity that associates with</param>
		/// <param name="pageNumber">The identity of business repository entity that associates with</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<List<TrashContent>> FindTrashContentsAsync<T>(string serviceName = null, string systemID = null, string repositoryID = null, string entityID = null, string userID = null, int pageSize = 0, int pageNumber = 1, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			using (var context = new RepositoryContext(false))
			{
				return await RepositoryMediator.FindTrashContentsAsync<T>(context, serviceName, systemID, repositoryID, entityID, userID, pageSize, pageNumber, cancellationToken);
			}
		}

		/// <summary>
		/// Gets the collection of trash contents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="serviceName">The name of service that associates with</param>
		/// <param name="systemID">The identity of system that associates with</param>
		/// <param name="pageSize">The identity of business repository entity that associates with</param>
		/// <param name="pageNumber">The identity of business repository entity that associates with</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<List<TrashContent>> FindTrashContentsAsync<T>(RepositoryContext context, DataSource dataSource, string serviceName, string systemID, int pageSize = 20, int pageNumber = 1, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			return string.IsNullOrWhiteSpace(serviceName) && string.IsNullOrWhiteSpace(systemID)
				? Task.FromException<List<TrashContent>>(new InformationRequiredException("Service name or system identity is required"))
				: RepositoryMediator.FindTrashContentsAsync<T>(context, dataSource, serviceName, systemID, null, null, null, pageSize, pageNumber, cancellationToken);
		}

		/// <summary>
		/// Gets the collection of trash contents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="serviceName">The name of service that associates with</param>
		/// <param name="systemID">The identity of system that associates with</param>
		/// <param name="pageSize">The identity of business repository entity that associates with</param>
		/// <param name="pageNumber">The identity of business repository entity that associates with</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<List<TrashContent>> FindTrashContentsAsync<T>(RepositoryContext context, string serviceName, string systemID, int pageSize = 20, int pageNumber = 1, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			return RepositoryMediator.FindTrashContentsAsync<T>(context, null, serviceName, systemID, pageSize, pageNumber, cancellationToken);
		}

		/// <summary>
		/// Gets the collection of trash contents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="systemID">The identity of system that associates with</param>
		/// <param name="pageSize">The identity of business repository entity that associates with</param>
		/// <param name="pageNumber">The identity of business repository entity that associates with</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<List<TrashContent>> FindTrashContentsAsync<T>(string serviceName, string systemID, int pageSize = 20, int pageNumber = 1, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			using (var context = new RepositoryContext(false))
			{
				return await RepositoryMediator.FindTrashContentsAsync<T>(context, serviceName, systemID, pageSize, pageNumber, cancellationToken);
			}
		}
		#endregion

		#region Clean trash contents
		/// <summary>
		/// Cleans old trash contents
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="days">The remain days</param>
		public static void CleanTrashContents(RepositoryContext context, DataSource dataSource, int days = 30)
		{
			try
			{
				context.Operation = RepositoryOperation.Delete;
				TrashContent.Delete(dataSource ?? context.GetTrashDataSource(), "Trashs", Filters<TrashContent>.LessThanOrEquals("Created", DateTime.Now.AddDays(0 - (days > 0 ? days : 30))));
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				throw new RepositoryOperationException("Error occurred while cleaning old trash contents", ex);
			}
		}

		/// <summary>
		/// Cleans old trash contents
		/// </summary>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="days">The remain days</param>
		public static void CleanTrashContents(DataSource dataSource, int days = 30)
		{
			using (var context = new RepositoryContext())
			{
				RepositoryMediator.CleanTrashContents(context, dataSource, days);
			}
		}

		/// <summary>
		/// Cleans old trash contents
		/// </summary>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="days">The remain days</param>
		public static void CleanTrashContents(string dataSource, int days = 30)
		{
			RepositoryMediator.CleanTrashContents(!string.IsNullOrWhiteSpace(dataSource) && RepositoryMediator.DataSources.ContainsKey(dataSource) ? RepositoryMediator.DataSources[dataSource] : null, days);
		}

		/// <summary>
		/// Cleans old trash contents
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="days">The remain days</param>
		public static async Task CleanTrashContentsAsync(RepositoryContext context, DataSource dataSource, int days = 30, CancellationToken cancellationToken = default(CancellationToken))
		{
			try
			{
				context.Operation = RepositoryOperation.Delete;
				await TrashContent.DeleteAsync(dataSource ?? context.GetTrashDataSource(), "Trashs", Filters<TrashContent>.LessThanOrEquals("Created", DateTime.Now.AddDays(0 - (days > 0 ? days : 30))), cancellationToken).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				throw new RepositoryOperationException("Error occurred while cleaning old trash contents", ex);
			}
		}

		/// <summary>
		/// Cleans old trash contents
		/// </summary>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="days">The remain days</param>
		public static async Task CleanTrashContentsAsync(DataSource dataSource, int days = 30, CancellationToken cancellationToken = default(CancellationToken))
		{
			using (var context = new RepositoryContext())
			{
				await RepositoryMediator.CleanTrashContentsAsync(context, dataSource, days, cancellationToken).ConfigureAwait(false);
			}
		}

		/// <summary>
		/// Cleans old trash contents
		/// </summary>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="days">The remain days</param>
		public static Task CleanTrashContentsAsync(string dataSource, int days = 30, CancellationToken cancellationToken = default(CancellationToken))
		{
			return RepositoryMediator.CleanTrashContentsAsync(!string.IsNullOrWhiteSpace(dataSource) && RepositoryMediator.DataSources.ContainsKey(dataSource) ? RepositoryMediator.DataSources[dataSource] : null, days, cancellationToken);
		}
		#endregion

		#region Call handlers of Create event
		static List<Type> GetHandlers(Func<Type, bool> predicate)
		{
			return RepositoryMediator.EventHandlers.Count < 1
				? new List<Type>()
				: RepositoryMediator.EventHandlers.Where(type => predicate(type)).ToList();
		}

		static bool CallPreCreateHandlers<T>(this RepositoryContext context, T @object, bool isRestore = false) where T : class
		{
			var handlers = RepositoryMediator.GetHandlers(type => typeof(IPreCreateHandler).IsAssignableFrom(type));
			for (var index = 0; index < handlers.Count; index++)
				try
				{
					var handler = ObjectService.CreateInstance(handlers[index]) as IPreCreateHandler;
					if (handler.OnPreCreate(context, @object, isRestore))
						return true;
				}
				catch { }
			return false;
		}

		static async Task<bool> CallPreCreateHandlersAsync<T>(this RepositoryContext context, T @object, bool isRestore = false, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			var handlers = RepositoryMediator.GetHandlers(type => typeof(IPreCreateHandler).IsAssignableFrom(type));
			for (var index = 0; index < handlers.Count; index++)
				try
				{
					var handler = ObjectService.CreateInstance(handlers[index]) as IPreCreateHandler;
					if (await handler.OnPreCreateAsync(context, @object, isRestore, cancellationToken).ConfigureAwait(false))
						return true;
				}
				catch { }
			return false;
		}

		static void CallPostCreateHandlers<T>(this RepositoryContext context, T @object, bool isRestore = false) where T : class
		{
			RepositoryMediator.GetHandlers(type => typeof(IPostCreateHandler).IsAssignableFrom(type))
				.Select(type => Task.Run(() =>
				{
					try
					{
						var handler = ObjectService.CreateInstance(type) as IPostCreateHandler;
						handler.OnPostCreate(context, @object, isRestore);
					}
					catch { }
				}))
				.ToList();
		}

		static async Task CallPostCreateHandlersAsync<T>(this RepositoryContext context, T @object, bool isRestore = false, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			await RepositoryMediator.GetHandlers(type => typeof(IPostCreateHandler).IsAssignableFrom(type))
				.ForEachAsync(async (type, token) =>
				{
					try
					{
						var handler = ObjectService.CreateInstance(type) as IPostCreateHandler;
						await handler.OnPostCreateAsync(context, @object, isRestore, token).ConfigureAwait(false);
					}
					catch { }
				}, cancellationToken, false).ConfigureAwait(false);
		}
		#endregion

		#region Call handlers of Get event
		static bool CallPreGetHandlers<T>(this RepositoryContext context, string id) where T : class
		{
			var handlers = RepositoryMediator.GetHandlers(type => typeof(IPreGetHandler).IsAssignableFrom(type));
			for (var index = 0; index < handlers.Count; index++)
				try
				{
					var handler = ObjectService.CreateInstance(handlers[index]) as IPreGetHandler;
					if (handler.OnPreGet<T>(context, id))
						return true;
				}
				catch { }
			return false;
		}

		static async Task<bool> CallPreGetHandlersAsync<T>(this RepositoryContext context, string id, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			var handlers = RepositoryMediator.GetHandlers(type => typeof(IPreGetHandler).IsAssignableFrom(type));
			for (var index = 0; index < handlers.Count; index++)
				try
				{
					var handler = ObjectService.CreateInstance(handlers[index]) as IPreGetHandler;
					if (await handler.OnPreGetAsync<T>(context, id, cancellationToken).ConfigureAwait(false))
						return true;
				}
				catch { }
			return false;
		}

		static void CallPostGetHandlers<T>(this RepositoryContext context, T @object) where T : class
		{
			RepositoryMediator.GetHandlers(type => typeof(IPostGetHandler).IsAssignableFrom(type))
				.Select(type => Task.Run(() =>
				{
					try
					{
						var handler = ObjectService.CreateInstance(type) as IPostGetHandler;
						handler.OnPostGet(context, @object);
					}
					catch { }
				}))
				.ToList();
		}

		static async Task CallPostGetHandlersAsync<T>(this RepositoryContext context, T @object, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			await RepositoryMediator.GetHandlers(type => typeof(IPostGetHandler).IsAssignableFrom(type))
				.ForEachAsync(async (type, token) =>
				{
					try
					{
						var handler = ObjectService.CreateInstance(type) as IPostGetHandler;
						await handler.OnPostGetAsync(context, @object, token).ConfigureAwait(false);
					}
					catch { }
				}, cancellationToken, false).ConfigureAwait(false);
		}
		#endregion

		#region Call handlers of Update event
		static bool CallPreUpdateHandlers<T>(this RepositoryContext context, T @object, HashSet<string> changed, bool isRollback = false) where T : class
		{
			var handlers = RepositoryMediator.GetHandlers(type => typeof(IPreUpdateHandler).IsAssignableFrom(type));
			for (var index = 0; index < handlers.Count; index++)
				try
				{
					var handler = ObjectService.CreateInstance(handlers[index]) as IPreUpdateHandler;
					if (handler.OnPreUpdate(context, @object, changed, isRollback))
						return true;
				}
				catch { }
			return false;
		}

		static async Task<bool> CallPreUpdateHandlersAsync<T>(this RepositoryContext context, T @object, HashSet<string> changed, bool isRestore = false, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			var handlers = RepositoryMediator.GetHandlers(type => typeof(IPreUpdateHandler).IsAssignableFrom(type));
			for (var index = 0; index < handlers.Count; index++)
				try
				{
					var handler = ObjectService.CreateInstance(handlers[index]) as IPreUpdateHandler;
					if (await handler.OnPreUpdateAsync(context, @object, changed, isRestore, cancellationToken).ConfigureAwait(false))
						return true;
				}
				catch { }
			return false;
		}

		static void CallPostUpdateHandlers<T>(this RepositoryContext context, T @object, HashSet<string> changed, bool isRestore = false) where T : class
		{
			RepositoryMediator.GetHandlers(type => typeof(IPostUpdateHandler).IsAssignableFrom(type))
				.Select(type => Task.Run(() =>
				{
					try
					{
						var handler = ObjectService.CreateInstance(type) as IPostUpdateHandler;
						handler.OnPostUpdate(context, @object, changed, isRestore);
					}
					catch { }
				}))
				.ToList();
		}

		static async Task CallPostUpdateHandlersAsync<T>(this RepositoryContext context, T @object, HashSet<string> changed, bool isRestore = false, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			await RepositoryMediator.GetHandlers(type => typeof(IPostUpdateHandler).IsAssignableFrom(type))
				.ForEachAsync(async (type, token) =>
				{
					try
					{
						var handler = ObjectService.CreateInstance(type) as IPostUpdateHandler;
						await handler.OnPostUpdateAsync(context, @object, changed, isRestore, token).ConfigureAwait(false);
					}
					catch { }
				}, cancellationToken, false).ConfigureAwait(false);
		}
		#endregion

		#region Call handlers of Delete event
		static bool CallPreDeleteHandlers<T>(this RepositoryContext context, T @object) where T : class
		{
			var handlers = RepositoryMediator.GetHandlers(type => typeof(IPreDeleteHandler).IsAssignableFrom(type));
			for (var index = 0; index < handlers.Count; index++)
				try
				{
					var handler = ObjectService.CreateInstance(handlers[index]) as IPreDeleteHandler;
					if (handler.OnPreDelete(context, @object))
						return true;
				}
				catch { }
			return false;
		}

		static async Task<bool> CallPreDeleteHandlersAsync<T>(this RepositoryContext context, T @object, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			var handlers = RepositoryMediator.GetHandlers(type => typeof(IPreDeleteHandler).IsAssignableFrom(type));
			for (var index = 0; index < handlers.Count; index++)
				try
				{
					var handler = ObjectService.CreateInstance(handlers[index]) as IPreDeleteHandler;
					if (await handler.OnPreDeleteAsync(context, @object, cancellationToken).ConfigureAwait(false))
						return true;
				}
				catch { }
			return false;
		}

		static void CallPostDeleteHandlers<T>(this RepositoryContext context, T @object) where T : class
		{
			RepositoryMediator.GetHandlers(type => typeof(IPostDeleteHandler).IsAssignableFrom(type))
				.Select(type => Task.Run(() =>
				{
					try
					{
						var handler = ObjectService.CreateInstance(type) as IPostDeleteHandler;
						handler.OnPostDelete(context, @object);
					}
					catch { }
				}))
				.ToList();
		}

		static async Task CallPostDeleteHandlersAsync<T>(this RepositoryContext context, T @object, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			await RepositoryMediator.GetHandlers(type => typeof(IPostDeleteHandler).IsAssignableFrom(type))
				.ForEachAsync(async (type, token) =>
				{
					try
					{
						var handler = ObjectService.CreateInstance(type) as IPostDeleteHandler;
						await handler.OnPostDeleteAsync(context, @object, token).ConfigureAwait(false);
					}
					catch { }
				}, cancellationToken, false).ConfigureAwait(false);
		}
		#endregion

		#region JSON/XML conversions
		/// <summary>
		/// Serializes the collection of objects to an array of JSON objects
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="objects">The object to serialize</param>
		/// <param name="addTypeOfExtendedProperties">true to add type of all extended properties when generate elements</param>
		/// <param name="onItemPreCompleted">The action to run on item pre-completed</param>
		/// <returns></returns>
		public static JArray ToJsonArray<T>(this List<T> objects, bool addTypeOfExtendedProperties = false, Action<JObject> onItemPreCompleted = null) where T : class
		{
			return objects == null || objects.Count < 1
				? new JArray()
				: objects.ToJArray(@object => @object is RepositoryBase ? (@object as RepositoryBase).ToJson(addTypeOfExtendedProperties, onItemPreCompleted) : @object.ToJson());
		}

		/// <summary>
		/// Serializes the collection of objects to a JSON object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="objects">The object to serialize</param>
		/// <param name="addTypeOfExtendedProperties">true to add type of all extended properties when generate elements</param>
		/// <param name="onItemPreCompleted">The action to run on item pre-completed</param>
		/// <returns></returns>
		public static JObject ToJsonObject<T>(this List<T> objects, bool addTypeOfExtendedProperties = false, Action<JObject> onItemPreCompleted = null) where T : class
		{
			var json = new JObject();
			objects.ForEach(@object => json.Add(new JProperty(@object.GetEntityID(), @object is RepositoryBase ? (@object as RepositoryBase).ToJson(addTypeOfExtendedProperties, onItemPreCompleted) : @object.ToJson())));
			return json;
		}

		/// <summary>
		/// Serializes the collection of objects to XML
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="objects">The object to serialize</param>
		/// <param name="name">The string that presents name of root tag, null to use default</param>
		/// <param name="addTypeOfExtendedProperties">true to add type of all extended properties when generate elements</param>
		/// <param name="onItemPreCompleted">The action to run on item pre-completed</param>
		/// <returns></returns>
		public static XElement ToXml<T>(this List<T> objects, string name = null, bool addTypeOfExtendedProperties = false, Action<XElement> onItemPreCompleted = null) where T : class
		{
			var xml = new XElement(XName.Get(string.IsNullOrWhiteSpace(name) ? typeof(T).GetTypeName(true) : name));
			if (objects != null)
				objects.ForEach(@object => xml.Add(@object is RepositoryBase ? (@object as RepositoryBase).ToXml(addTypeOfExtendedProperties, onItemPreCompleted) : @object.ToXml()));
			return xml;
		}
		#endregion

		#region Caching extension methods
		/// <summary>
		/// Gets the identity/primary-key of the entity object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="entity">The entity object (usually is object of sub-class of <see cref="RepositoryBase">RepositoryBase</see>)</param>
		/// <param name="keyAttribute">The string that presents the name of identity attribute)</param>
		/// <returns></returns>
		public static string GetEntityID<T>(this T entity, string keyAttribute = null) where T : class
		{
			var identity = entity is RepositoryBase
				? (entity as RepositoryBase).ID
				: entity.GetAttributeValue(string.IsNullOrWhiteSpace(keyAttribute) ? "ID" : keyAttribute) as string;

			return !string.IsNullOrWhiteSpace(identity)
				? identity
				: entity.GetAttributeValue("Id") as string;
		}

		/// <summary>
		/// Gets a key for working with caching storage
		/// The key is combination of objects' type name (just name only), hashtag, and object identity (ID/Id - if object has no attribute named 'ID/Id', the hash code will be used)
		/// Ex: the object type is net.vieapps.data.Article, got attribute named ID with value '12345', then the cached-key is 'Article#12345'
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="object">The object for generating key from</param>
		/// <param name="useFullTypeName">true to use full type name, false to use just name (last element) only</param>
		/// <returns>The string that present a key</returns>
		public static string GetCacheKey<T>(this T @object, bool useFullTypeName = false) where T : class
		{
			var key = @object.GetEntityID();
			if (string.IsNullOrWhiteSpace(key))
				key = @object.GetHashCode().ToString();
			return @object.GetType().GetTypeName(!useFullTypeName) + "#" + key;
		}

		/// <summary>
		/// Gets a key for working with caching storage
		/// The key is combination of objects' type name (just name only), hashtag, and object identity (ID/Id - if object has no attribute named 'ID/Id', the hash code will be used)
		/// Ex: the object type is net.vieapps.data.Article, the identity value value is '12345', then the cached-key is 'Article#12345'
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="identity">The string that presents identity of an object</param>
		/// <param name="useFullTypeName">true to use full type name, false to use just name (last element) only</param>
		/// <returns>The string that present a key</returns>
		public static string GetCacheKey<T>(this string identity, bool useFullTypeName = false) where T : class
		{
			return typeof(T).GetTypeName(!useFullTypeName) + "#" + identity.Trim().ToLower();
		}

		/// <summary>
		/// Adds an object into cache storage
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="cache">The cache storage</param>
		/// <param name="object">The object to update into cache storage</param>
		/// <param name="expirationTime">The number that presents time for caching (in minutes)</param>
		public static bool Set<T>(this Cache cache, T @object, int expirationTime = 0) where T : class
		{
			return @object != null
				? cache.Set(@object.GetCacheKey(), @object, expirationTime)
				: false;
		}

		/// <summary>
		/// Adds the collection of objects into cache storage
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="cache">The cache storage</param>
		/// <param name="objects">The collection of objects</param>
		public static void Set<T>(this Cache cache, List<T> objects) where T : class
		{
			if (objects != null)
				cache.Set(objects.Where(obj => obj != null).ToDictionary(obj => obj.GetCacheKey()));
		}

		/// <summary>
		/// Adds an object into cache storage
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="cache">The cache storage</param>
		/// <param name="object">The object to update into cache storage</param>
		/// <param name="expirationTime">The number that presents time for caching (in minutes)</param>
		public static Task<bool> SetAsync<T>(this Cache cache, T @object, int expirationTime = 0) where T : class
		{
			return @object != null
				? cache.SetAsync(@object.GetCacheKey(), @object, expirationTime)
				: Task.FromResult<bool>(false);
		}

		/// <summary>
		/// Adds the collection of objects into cache storage
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="cache">The cache storage</param>
		/// <param name="objects">The collection of objects</param>
		/// <param name="expirationTime">The number that presents time for caching (in minutes)</param>
		public static void Set<T>(this Cache cache, IEnumerable<T> objects, int expirationTime = 0) where T : class
		{
			if (objects != null)
				cache.Set(objects.Where(obj => obj != null).ToDictionary(obj => obj.GetCacheKey()), null, expirationTime);
		}

		/// <summary>
		/// Adds the collection of objects into cache storage
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="cache">The cache storage</param>
		/// <param name="objects">The collection of objects</param>
		/// <param name="expirationTime">The number that presents time for caching (in minutes)</param>
		public static Task SetAsync<T>(this Cache cache, IEnumerable<T> objects, int expirationTime = 0) where T : class
		{
			return objects != null
				? cache.SetAsync(objects.Where(obj => obj != null).ToDictionary(obj => obj.GetCacheKey()), null, expirationTime)
				: Task.CompletedTask;
		}

		/// <summary>
		/// Adds an object into cache storage (when its no cached)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="cache">The cache storage</param>
		/// <param name="object">The object to update into cache storage</param>
		/// <param name="expirationTime">The number that presents time for caching (in minutes)</param>
		public static bool Add<T>(this Cache cache, T @object, int expirationTime = 0) where T : class
		{
			return @object != null
				? cache.Add(@object.GetCacheKey(), @object, expirationTime)
				: false;
		}

		/// <summary>
		/// Adds an object into cache storage (when its no cached)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="cache">The cache storage</param>
		/// <param name="object">The object to update into cache storage</param>
		/// <param name="expirationTime">The number that presents time for caching (in minutes)</param>
		public static Task<bool> AddAsync<T>(this Cache cache, T @object, int expirationTime = 0) where T : class
		{
			return @object != null
				? cache.AddAsync(@object.GetCacheKey(), @object, expirationTime)
				: Task.FromResult<bool>(false);
		}

		/// <summary>
		/// Replaces an object in the cache storage
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="cache">The cache storage</param>
		/// <param name="object">The object to update into cache storage</param>
		/// <param name="expirationTime">The number that presents time for caching (in minutes)</param>
		public static bool Replace<T>(this Cache cache, T @object, int expirationTime = 0) where T : class
		{
			return @object != null
				? cache.Replace(@object.GetCacheKey(), @object, expirationTime)
				: false;
		}

		/// <summary>
		/// Replaces an object in the cache storage
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="cache">The cache storage</param>
		/// <param name="object">The object to update into cache storage</param>
		/// <param name="expirationTime">The number that presents time for caching (in minutes)</param>
		public static Task<bool> ReplaceAsync<T>(this Cache cache, T @object, int expirationTime = 0) where T : class
		{
			return @object != null
				? cache.ReplaceAsync(@object.GetCacheKey(), @object, expirationTime)
				: Task.FromResult<bool>(false);
		}

		/// <summary>
		/// Fetchs an object from cache storage
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="cache">The cache storage</param>
		/// <param name="identity">The string that presents identity of object need to get</param>
		/// <returns></returns>
		public static T Fetch<T>(this Cache cache, string identity) where T : class
		{
			return !string.IsNullOrWhiteSpace(identity)
				? cache.Get<T>(identity.GetCacheKey<T>())
				: null;
		}

		/// <summary>
		/// Fetchs an object from cache storage
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="cache">The cache storage</param>
		/// <param name="identity">The string that presents identity of object need to get</param>
		/// <returns></returns>
		public static Task<T> FetchAsync<T>(this Cache cache, string identity) where T : class
		{
			return !string.IsNullOrWhiteSpace(identity)
				? cache.GetAsync<T>(identity.GetCacheKey<T>())
				: Task.FromResult<T>(null);
		}

		/// <summary>
		/// Removes a cached object from cache storage
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="cache">The cache storage</param>
		/// <param name="object">The object need to delete from cache storage</param>
		public static bool Remove<T>(this Cache cache, T @object) where T : class
		{
			return @object != null
				? cache.Remove(@object.GetCacheKey())
				: false;
		}

		/// <summary>
		/// Removes a cached object from cache storage
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="cache">The cache storage</param>
		/// <param name="identity">The string that presents identity of object need to delete</param>
		public static bool Remove<T>(this Cache cache, string identity) where T : class
		{
			return !string.IsNullOrWhiteSpace(identity)
				? cache.Remove(identity.GetCacheKey<T>())
				: false;
		}

		/// <summary>
		/// Remove a cached object from cache storage
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="cache">The cache storage</param>
		/// <param name="object">The object need to delete from cache storage</param>
		public static Task<bool> RemoveAsync<T>(this Cache cache, T @object) where T : class
		{
			return @object != null
				? cache.RemoveAsync(@object.GetCacheKey())
				: Task.FromResult<bool>(false);
		}

		/// <summary>
		/// Removes a cached object from cache storage
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="cache">The cache storage</param>
		/// <param name="identity">The string that presents identity of object need to delete</param>
		public static Task<bool> RemoveAsync<T>(this Cache cache, string identity) where T : class
		{
			return !string.IsNullOrWhiteSpace(identity)
				? cache.RemoveAsync(identity.GetCacheKey<T>())
				: Task.FromResult<bool>(false);
		}

		/// <summary>
		/// Checks existing of a cached object in cache storage
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="cache">The cache storage</param>
		/// <param name="identity">The string that presents identity of object</param>
		public static bool Exists<T>(this Cache cache, string identity) where T : class
		{
			return !string.IsNullOrWhiteSpace(identity)
				? cache.Exists(identity.GetCacheKey<T>())
				: false;
		}

		/// <summary>
		/// Checks existing of a cached object in cache storage
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="cache">The cache storage</param>
		/// <param name="identity">The string that presents identity of object</param>
		public static Task<bool> ExistsAsync<T>(this Cache cache, string identity) where T : class
		{
			return !string.IsNullOrWhiteSpace(identity)
				? cache.ExistsAsync(identity.GetCacheKey<T>())
				: Task.FromResult<bool>(false);
		}
		#endregion

		#region Property/Attribute extension methods
		internal static Tuple<Dictionary<string, AttributeInfo>, Dictionary<string, ExtendedPropertyDefinition>> GetProperties<T>(string businessEntityID, EntityDefinition definition = null, bool lowerCaseKeys = false) where T : class
		{
			definition = definition != null
				? definition
				: RepositoryMediator.GetEntityDefinition<T>();

			var standardProperties = definition != null
				? definition.Attributes.ToDictionary(attribute => lowerCaseKeys ? attribute.Name.ToLower() : attribute.Name)
				: ObjectService.GetProperties(typeof(T)).ToDictionary(attribute => lowerCaseKeys ? attribute.Name.ToLower() : attribute.Name, attribute => new AttributeInfo(attribute));

			var extendedProperties = definition != null && definition.Type.CreateInstance().IsGotExtendedProperties(businessEntityID, definition)
				? definition.RuntimeEntities[businessEntityID].ExtendedPropertyDefinitions.ToDictionary(attribute => lowerCaseKeys ? attribute.Name.ToLower() : attribute.Name)
				: null;

			return new Tuple<Dictionary<string, AttributeInfo>, Dictionary<string, ExtendedPropertyDefinition>>(standardProperties, extendedProperties);
		}

		internal static List<string> GetAssociatedParentIDs<T>(this IFilterBy<T> filter, EntityDefinition definition = null) where T : class
		{
			definition = definition != null
				? definition
				: RepositoryMediator.GetEntityDefinition<T>();

			if (definition == null || definition.ParentType == null || string.IsNullOrWhiteSpace(definition.ParentAssociatedProperty))
				return null;

			if (filter is FilterBy<T>)
				return (filter as FilterBy<T>).Attribute.Equals(definition.ParentAssociatedProperty)
					? new List<string>() { (filter as FilterBy<T>).Value as string }
					: null;

			if ((filter as FilterBys<T>).Children == null || (filter as FilterBys<T>).Children.Count < 1)
				return null;

			var parentIDs = new List<string>();

			(filter as FilterBys<T>).Children.ForEach(info =>
			{
				if (info is FilterBy<T> && (info as FilterBy<T>).Attribute.Equals(definition.ParentAssociatedProperty))
					parentIDs.Add((info as FilterBy<T>).Value as string);
				else if (info is FilterBys<T> && (info as FilterBys<T>).Children != null && (info as FilterBys<T>).Children.Count > 0)
				{
					var ids = info.GetAssociatedParentIDs(definition);
					if (ids != null)
						parentIDs.Append(ids);
				}
			});

			return parentIDs.Count > 0
				? parentIDs.Distinct().ToList()
				: null;
		}

		internal static bool IsGotExtendedProperties<T>(this T @object, string businessEntityID = null, EntityDefinition definition = null) where T : class
		{
			if (!(@object is IBusinessEntity))
				return false;

			businessEntityID = string.IsNullOrWhiteSpace(businessEntityID)
				? (@object as IBusinessEntity).EntityID
				: businessEntityID;

			if (string.IsNullOrWhiteSpace(businessEntityID))
				return false;

			definition = definition != null
				? definition
				: RepositoryMediator.GetEntityDefinition<T>();

			if (definition == null || definition.RuntimeEntities == null)
				return false;

			var attributes = definition.RuntimeEntities.ContainsKey(businessEntityID)
				? definition.RuntimeEntities[businessEntityID].ExtendedPropertyDefinitions
				: null;
			return attributes != null && attributes.Count > 0;
		}

		internal static bool IsIgnored(this ObjectService.AttributeInfo attribute)
		{
			return attribute.Info.GetCustomAttributes(typeof(IgnoreAttribute), true).Length > 0
				? true
				: attribute.Info.GetCustomAttributes(typeof(MongoDB.Bson.Serialization.Attributes.BsonIgnoreAttribute), true).Length > 0;
		}

		internal static bool IsIgnoredIfNull(this ObjectService.AttributeInfo attribute)
		{
			return attribute.Info.GetCustomAttributes(typeof(IgnoreIfNullAttribute), true).Length > 0;
		}

		internal static bool IsStoredAsJson(this ObjectService.AttributeInfo attribute)
		{
			return attribute.Type.IsClassType() && attribute.Info.GetCustomAttributes(typeof(AsJsonAttribute), true).Length > 0;
		}

		internal static bool IsStoredAsString(this ObjectService.AttributeInfo attribute)
		{
			return attribute.Type.IsDateTimeType() && attribute.Info.GetCustomAttributes(typeof(AsStringAttribute), true).Length > 0;
		}

		internal static bool IsEnumString(this ObjectService.AttributeInfo attribute)
		{
			var attributes = attribute.Type.IsEnum
				? attribute.Info.GetCustomAttributes(typeof(JsonConverterAttribute), true)
				: new object[] { };
			return attributes.Length > 0 && (attributes[0] as JsonConverterAttribute).ConverterType.Equals(typeof(Newtonsoft.Json.Converters.StringEnumConverter));
		}

		internal static bool IsSortable(this ObjectService.AttributeInfo attribute)
		{
			return attribute.Info.GetCustomAttributes(typeof(SortableAttribute), true).Length > 0;
		}

		internal static bool IsSearchable(this ObjectService.AttributeInfo attribute)
		{
			return attribute.Type.IsStringType() && attribute.Info.GetCustomAttributes(typeof(SearchableAttribute), true).Length > 0;
		}
		#endregion

		#region [Logs]
		static string LogsPath = null;

		internal static async Task WriteLogsAsync(string filePath, List<string> logs, Exception ex)
		{
			// prepare
			var info = DateTime.Now.ToString("HH:mm:ss.fff") + "\t" + "[" + Process.GetCurrentProcess().Id.ToString()
				+ " : " + AppDomain.CurrentDomain.Id.ToString() + " : " + Thread.CurrentThread.ManagedThreadId.ToString() + "]" + "\t";

			var content = "";
			logs?.ForEach(log => content += !string.IsNullOrWhiteSpace(log) ? info + log + "\r\n" : "");

			if (ex != null)
			{
				content += info + "- " + (ex.Message != null ? ex.Message : "No error message")
					+ " [" + ex.GetType().ToString() + "]" + "\r\n"
					+ info + "- " + (ex.StackTrace != null ? ex.StackTrace : "No stack trace");

				var inner = ex.InnerException;
				var counter = 1;
				while (inner != null)
				{
					content += info + $"- Inner ({counter}): ----------------------------------" + "\r\n"
						+ info + "- " + (inner.Message != null ? inner.Message : "No error message") + " [" + inner.GetType().ToString() + "]" + "\r\n"
						+ info + "- " + (inner.StackTrace != null ? inner.StackTrace : "No stack trace");
					counter++;
					inner = inner.InnerException;
				}
			}

			// write logs into file
			await UtilityService.WriteTextFileAsync(filePath, content, true).ConfigureAwait(false);
		}

		internal static void WriteLogs(List<string> logs, Exception ex)
		{
			// prepare path of all log files
			if (string.IsNullOrWhiteSpace(RepositoryMediator.LogsPath))
				try
				{
					RepositoryMediator.LogsPath = UtilityService.GetAppSetting("Path:Logs");
					if (!RepositoryMediator.LogsPath.EndsWith(Path.DirectorySeparatorChar.ToString()))
						RepositoryMediator.LogsPath += Path.DirectorySeparatorChar.ToString();
				}
				catch { }

			if (string.IsNullOrWhiteSpace(RepositoryMediator.LogsPath))
				try
				{
					RepositoryMediator.LogsPath = Path.Combine(Directory.GetCurrentDirectory(), "Logs") + Path.DirectorySeparatorChar.ToString();
				}
				catch { }

			// write logs via other thread
			if (!string.IsNullOrWhiteSpace(RepositoryMediator.LogsPath))
				Task.Run(async () =>
				{
					try
					{
						await RepositoryMediator.WriteLogsAsync(RepositoryMediator.LogsPath + DateTime.Now.ToString("yyyy-MM-dd-HH") + ".repository.txt", logs, ex).ConfigureAwait(false);
					}
					catch { }
				}).ConfigureAwait(false);
		}

		internal static void WriteLogs(string log, Exception ex = null)
		{
			RepositoryMediator.WriteLogs(string.IsNullOrWhiteSpace(log) ? null : new List<string>() { log }, ex);
		}
		#endregion

	}
}