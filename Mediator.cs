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

using Microsoft.Extensions.Logging;

using net.vieapps.Components.Caching;
using net.vieapps.Components.Utility;
using net.vieapps.Components.Security;
#endregion

#if !SIGN
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("VIEApps.Components.XUnitTests")]
#endif

namespace net.vieapps.Components.Repository
{
	/// <summary>
	/// Allow to use the Repository functionality without direct reference to <see cref="RepositoryBase"/> class
	/// </summary>
	public static class RepositoryMediator
	{
		internal static Dictionary<Type, RepositoryDefinition> RepositoryDefinitions { get; } = new Dictionary<Type, RepositoryDefinition>();

		internal static Dictionary<Type, EntityDefinition> EntityDefinitions { get; } = new Dictionary<Type, EntityDefinition>();

		internal static Dictionary<string, DataSource> DataSources { get; } = new Dictionary<string, DataSource>(StringComparer.OrdinalIgnoreCase);

		internal static List<Type> EventHandlers { get; } = new List<Type>();

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
		/// <param name="type">The type of the definition</param>
		/// <param name="verify">true to verify</param>
		/// <returns></returns>
		public static RepositoryDefinition GetRepositoryDefinition(Type type, bool verify = false)
		{
			if (type != null && RepositoryMediator.RepositoryDefinitions.TryGetValue(type, out var definition))
				return definition;
			if (verify)
				throw new InformationNotFoundException($"The repository definition [{type?.GetTypeName()}] is not found");
			return null;
		}

		/// <summary>
		/// Gets the repository definition that matched with the type name
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="verify">true to verify</param>
		/// <returns></returns>
		public static RepositoryDefinition GetRepositoryDefinition<T>(bool verify = true) where T : class
			=> RepositoryMediator.GetRepositoryDefinition(typeof(T), verify);

		/// <summary>
		/// Gets the repository entity definition that matched with the type
		/// </summary>
		/// <param name="type">The type of the definition</param>
		/// <param name="verify">true to verify</param>
		/// <returns></returns>
		public static EntityDefinition GetEntityDefinition(Type type, bool verify = false)
		{
			if (type != null && RepositoryMediator.EntityDefinitions.TryGetValue(type, out var definition))
				return definition;
			if (verify)
				throw new InformationNotFoundException($"The repository entity definition [{type?.GetTypeName()}] is not found");
			return null;
		}

		/// <summary>
		/// Gets the repository entity definition that matched with the type of a class
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="verify">true to verify</param>
		/// <returns></returns>
		public static EntityDefinition GetEntityDefinition<T>(bool verify = true) where T : class
			=> RepositoryMediator.GetEntityDefinition(typeof(T), verify);
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
				.Where(kvp => kvp.Value.RuntimeRepositories != null && kvp.Value.RuntimeRepositories.Count > 0)
				.ForEach(kvp => repositories = repositories.Concat(kvp.Value.RuntimeRepositories.Where(data => data.Value.SystemID.IsEquals(systemID)).Select(data => data.Value)).ToList());

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
				.Where(kvp => kvp.Value.RuntimeRepositories.ContainsKey(repositoryID))
				.Select(kvp => kvp.Value.RuntimeRepositories)
				.FirstOrDefault();

			return repositories != null && repositories.TryGetValue(repositoryID, out var repository)
				? repository
				: null;
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

			return entities != null && entities.TryGetValue(entityID, out var entity)
				? entity
				: null;
		}
		#endregion

		#region Data Source
		internal static void ConstructDataSources(List<XmlNode> nodes, Action<string, Exception> tracker = null)
		{
			nodes.ForEach(node =>
			{
				var dataSource = DataSource.FromJson(node.ToJson());
				if (!RepositoryMediator.DataSources.ContainsKey(dataSource.Name))
					RepositoryMediator.DataSources.Add(dataSource.Name, dataSource);
			});
			tracker?.Invoke($"Construct {RepositoryMediator.DataSources.Count} data sources [{RepositoryMediator.DataSources.Select(kvp => kvp.Key).ToString(", ")}]", null);
			if (RepositoryMediator.IsDebugEnabled)
				RepositoryMediator.WriteLogs($"Construct {RepositoryMediator.DataSources.Count} data sources [{RepositoryMediator.DataSources.Select(kvp => kvp.Key).ToString(", ")}]", null);
		}

		/// <summary>
		/// Gets the primary data source
		/// </summary>
		/// <param name="name">The string that presents name of a data source</param>
		/// <returns></returns>
		public static DataSource GetDataSource(string name)
			=> !string.IsNullOrWhiteSpace(name) && RepositoryMediator.DataSources.TryGetValue(name, out var dataSource)
				? dataSource
				: null;

		/// <summary>
		/// Gets the primary data source
		/// </summary>
		/// <param name="aliasTypeName">The string that presents alias of a type name</param>
		/// <param name="definition">The definition of a repository entity</param>
		/// <returns></returns>
		public static DataSource GetPrimaryDataSource(string aliasTypeName, EntityDefinition definition)
		{
			var dataSource = definition?.PrimaryDataSource;
			if (dataSource == null && definition != null)
			{
				var parent = !string.IsNullOrWhiteSpace(aliasTypeName)
					? RepositoryMediator.GetRepositoryDefinition(Type.GetType(aliasTypeName))
					: definition.RepositoryDefinition;
				if (parent != null)
					dataSource = parent.PrimaryDataSource;
			}
			return dataSource;
		}

		/// <summary>
		/// Gets the primary data source
		/// </summary>
		/// <param name="context">The working context of a repository entity</param>
		/// <returns></returns>
		public static DataSource GetPrimaryDataSource(this RepositoryContext context)
			=> RepositoryMediator.GetPrimaryDataSource(context.AliasTypeName, context.EntityDefinition);

		/// <summary>
		/// Gets the primary data source
		/// </summary>
		/// <param name="definition">The definition of a repository entity</param>
		/// <returns></returns>
		public static DataSource GetPrimaryDataSource(this EntityDefinition definition)
			=> RepositoryMediator.GetPrimaryDataSource(null, definition);

		/// <summary>
		/// Gets the primary data source
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="aliasTypeName">The string that presents alias of a type name</param>
		/// <returns></returns>
		public static DataSource GetPrimaryDataSource<T>(string aliasTypeName = null)
			=> RepositoryMediator.GetPrimaryDataSource(aliasTypeName, RepositoryMediator.GetEntityDefinition(typeof(T)));

		/// <summary>
		/// Gets the secondary data source
		/// </summary>
		/// <param name="aliasTypeName">The string that presents alias of a type name</param>
		/// <param name="definition">The definition of a repository entity</param>
		/// <returns></returns>
		public static DataSource GetSecondaryDataSource(string aliasTypeName, EntityDefinition definition)
		{
			var dataSource = definition?.SecondaryDataSource;
			if (dataSource == null && definition != null)
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
			=> RepositoryMediator.GetSecondaryDataSource(context.AliasTypeName, context.EntityDefinition);

		/// <summary>
		/// Gets the secondary data source
		/// </summary>
		/// <param name="definition">The definition of a repository entity</param>
		/// <returns></returns>
		public static DataSource GetSecondaryDataSource(this EntityDefinition definition)
			=> RepositoryMediator.GetSecondaryDataSource(null, definition);

		/// <summary>
		/// Gets the secondary data source
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="aliasTypeName">The string that presents alias of a type name</param>
		/// <returns></returns>
		public static DataSource GetSecondaryDataSource<T>(string aliasTypeName = null)
			=> RepositoryMediator.GetSecondaryDataSource(aliasTypeName, RepositoryMediator.GetEntityDefinition(typeof(T)));

		/// <summary>
		/// Gets the sync data sources
		/// </summary>
		/// <param name="aliasTypeName">The string that presents alias of a type name</param>
		/// <param name="definition">The definition of a repository entity</param>
		/// <returns></returns>
		public static List<DataSource> GetSyncDataSources(string aliasTypeName, EntityDefinition definition)
			=> definition?.SyncDataSources ?? (!string.IsNullOrWhiteSpace(aliasTypeName)
				? RepositoryMediator.GetRepositoryDefinition(Type.GetType(aliasTypeName))
				: definition?.RepositoryDefinition)?.SyncDataSources ?? new List<DataSource>();

		/// <summary>
		/// Gets the sync data sources
		/// </summary>
		/// <param name="context">The working context of a repository entity</param>
		/// <returns></returns>
		public static List<DataSource> GetSyncDataSources(this RepositoryContext context)
			=> RepositoryMediator.GetSyncDataSources(context.AliasTypeName, context.EntityDefinition);

		/// <summary>
		/// Gets the sync data sources
		/// </summary>
		/// <param name="definition">The definition of a repository entity</param>
		/// <returns></returns>
		public static List<DataSource> GetSyncDataSources(this EntityDefinition definition)
			=> RepositoryMediator.GetSyncDataSources(null, definition);

		/// <summary>
		/// Gets the sync data sources
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="aliasTypeName">The string that presents alias of a type name</param>
		/// <returns></returns>
		public static List<DataSource> GetSyncDataSources<T>(string aliasTypeName = null)
			=> RepositoryMediator.GetSyncDataSources(aliasTypeName, RepositoryMediator.GetEntityDefinition(typeof(T)));

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
			=> RepositoryMediator.GetVersionDataSource(context.AliasTypeName, context.EntityDefinition);

		/// <summary>
		/// Gets the data source that use to store versioning contents
		/// </summary>
		/// <param name="definition">The definition of a repository entity</param>
		/// <returns></returns>
		public static DataSource GetVersionDataSource(this EntityDefinition definition)
			=> RepositoryMediator.GetVersionDataSource(null, definition);

		/// <summary>
		/// Gets the data source that use to store versioning contents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="aliasTypeName">The string that presents alias of a type name</param>
		/// <returns></returns>
		public static DataSource GetVersionDataSource<T>(string aliasTypeName = null)
			=> RepositoryMediator.GetVersionDataSource(aliasTypeName, RepositoryMediator.GetEntityDefinition(typeof(T)));

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
			=> RepositoryMediator.GetTrashDataSource(context.AliasTypeName, context.EntityDefinition);

		/// <summary>
		/// Gets the data source that use to store trash contents
		/// </summary>
		/// <param name="definition">The definition of a repository entity</param>
		/// <returns></returns>
		public static DataSource GetTrashDataSource(this EntityDefinition definition)
			=> RepositoryMediator.GetTrashDataSource(null, definition);

		/// <summary>
		/// Gets the data source that use to store trash contents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="aliasTypeName">The string that presents alias of a type name</param>
		/// <returns></returns>
		public static DataSource GetTrashDataSource<T>(string aliasTypeName = null)
			=> RepositoryMediator.GetTrashDataSource(aliasTypeName, RepositoryMediator.GetEntityDefinition(typeof(T)));
		#endregion

		#region Session [NoSQL]
		/// <summary>
		/// Starts a client session that available for working with NoSQL database transaction of this data-source
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="dataSource"></param>
		/// <param name="options"></param>
		/// <returns></returns>
		public static IClientSessionHandle StartSession<T>(this DataSource dataSource, ClientSessionOptions options = null) where T : class
		{
			if (dataSource == null || !dataSource.Mode.Equals(RepositoryMode.NoSQL))
				return null;
			var collection = dataSource.GetCollection<T>();
			return collection == null || !collection.IsReplicaSet()
				? null
				: collection.StartSession(options);
		}

		/// <summary>
		/// Starts a client session that available for working with NoSQL database transaction of this data-source
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="dataSource"></param>
		/// <param name="options"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public static async Task<IClientSessionHandle> StartSessionAsync<T>(this DataSource dataSource, ClientSessionOptions options = null, CancellationToken cancellationToken = default) where T : class
		{
			if (dataSource == null || !dataSource.Mode.Equals(RepositoryMode.NoSQL))
				return null;
			var collection = dataSource.GetCollection<T>();
			return collection != null && await collection.IsReplicaSetAsync(cancellationToken).ConfigureAwait(false)
				? await collection.StartSessionAsync(options, cancellationToken).ConfigureAwait(false)
				: null;
		}

		/// <summary>
		/// Starts a client session that available for working with NoSQL database transaction of this data-source
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="dataSource"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public static Task<IClientSessionHandle> StartSessionAsync<T>(this DataSource dataSource, CancellationToken cancellationToken) where T : class
			=> dataSource.StartSessionAsync<T>(null, cancellationToken);
		#endregion

		#region Connection String
		/// <summary>
		/// Gets the settings of the connection string of the data source (for working with SQL/NoSQL database)
		/// </summary>
		/// <param name="name">The string that presents name of a connection string</param>
		/// <returns></returns>
		public static ConnectionStringSettings GetConnectionStringSettings(string name)
			=> !string.IsNullOrWhiteSpace(name)
				? ConfigurationManager.ConnectionStrings[name]
				: null;

		/// <summary>
		/// Gets the settings of the connection string of the data source (for working with SQL/NoSQL database)
		/// </summary>
		/// <param name="dataSource">The data source</param>
		/// <returns></returns>
		public static ConnectionStringSettings GetConnectionStringSettings(this DataSource dataSource)
			=> RepositoryMediator.GetConnectionStringSettings(dataSource?.ConnectionStringName);

		/// <summary>
		/// Gets the connection string of the data source (for working with SQL/NoSQL database)
		/// </summary>
		/// <param name="dataSource">The data source</param>
		/// <returns></returns>
		public static string GetConnectionString(this DataSource dataSource)
			=> dataSource.ConnectionString ?? RepositoryMediator.GetConnectionStringSettings(dataSource)?.ConnectionString;
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

				stateData.TryGetValue(attribute.Name, out object value);

				if (value == null)
				{
					if (attribute.Name.Equals(definition.PrimaryKey))
						throw new InformationRequiredException("The value of the primary-key is required");
					else if (attribute.NotNull)
						throw new InformationRequiredException($"The value of the {(attribute.IsPublic ? "property" : "attribute")} named '{attribute.Name}' is required (doesn't allow null)");
				}

				else if (attribute.Type.IsStringType())
				{
					var isCLOB = attribute.IsCLOB != null ? attribute.IsCLOB.Value : false;
					if (!isCLOB)
					{
						var isNotEmpty = attribute.NotEmpty != null ? attribute.NotEmpty.Value : false;
						if (isNotEmpty && string.IsNullOrWhiteSpace(value as string))
							throw new InformationRequiredException($"The value of the {(attribute.IsPublic ? "property" : "attribute")} named '{attribute.Name}' is required (doesn't allow empty or null)");

						var maxLength = attribute.MaxLength != null ? attribute.MaxLength.Value : 4000;
						if ((value as string).Length > maxLength)
						{
							changed = true;
							stateData[attribute.Name] = (value as string).Left(maxLength);
						}
					}
				}
			}

			// extended properties
			stateData.TryGetValue("EntityID", out object entityID);
			if (entityID != null && entityID is string && !string.IsNullOrWhiteSpace(entityID as string))
				if (definition.RuntimeEntities.TryGetValue(entityID as string, out var repositoryEntity))
					foreach (var attribute in (repositoryEntity?.ExtendedPropertyDefinitions ?? new List<ExtendedPropertyDefinition>()))
					{
						if (attribute.Mode.Equals(ExtendedPropertyMode.YesNo) || attribute.Mode.Equals(ExtendedPropertyMode.Number)
						|| attribute.Mode.Equals(ExtendedPropertyMode.Decimal) || attribute.Mode.Equals(ExtendedPropertyMode.DateTime)
						|| attribute.Mode.Equals(ExtendedPropertyMode.LargeText))
							continue;

						if (!stateData.TryGetValue($"ExtendedProperties.{attribute.Name}", out object value))
							continue;

						if (value == null || string.IsNullOrWhiteSpace(value as string))
							continue;

						var maxLength = attribute.Mode.Equals(ExtendedPropertyMode.SmallText) || attribute.Mode.Equals(ExtendedPropertyMode.Select)
						|| attribute.Mode.Equals(ExtendedPropertyMode.Lookup) || attribute.Mode.Equals(ExtendedPropertyMode.User)
							? 250
							: 4000;

						if ((value as string).Length > maxLength)
						{
							changed = true;
							stateData[$"ExtendedProperties.{attribute.Name}"] = (value as string).Left(maxLength);
						}
					}

			return changed;
		}
		#endregion

		#region Create
		/// <summary>
		/// Creates new instance of object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="object">The object to create new instance in repository</param>
		public static bool Create<T>(RepositoryContext context, DataSource dataSource, T @object) where T : class
		{
			context.Prepare<T>(RepositoryOperation.Create, (dataSource ?? context.GetPrimaryDataSource())?.StartSession<T>());
			try
			{
				// validate & re-update object
				var currentState = context.SetCurrentState(@object);
				if (RepositoryMediator.Validate(context.EntityDefinition, currentState))
				{
					// re-update object
					currentState.ForEach(kvp =>
					{
						if (kvp.Key.StartsWith("ExtendedProperties."))
							(@object as IBusinessEntity).ExtendedProperties[kvp.Key.Replace("ExtendedProperties.", "")] = kvp.Value;
						else
							@object.SetAttributeValue(kvp.Key, kvp.Value);
					});

					// update state
					context.SetCurrentState(@object, currentState);
				}

				// call pre-create handlers
				if (context.CallPreCreateHandlers(@object, false))
					return false;

				// create
				dataSource = dataSource ?? context.GetPrimaryDataSource();
				if (dataSource == null)
					throw new InformationInvalidException("Data source is invalid, please check the configuration");

				if (dataSource.Mode.Equals(RepositoryMode.NoSQL))
					context.Create(dataSource, @object, null);
				else if (dataSource.Mode.Equals(RepositoryMode.SQL))
					context.Create(dataSource, @object);

				// update in cache storage
				if (context.EntityDefinition.Cache != null && context.EntityDefinition.Cache.Set(@object) && RepositoryMediator.IsDebugEnabled)
					RepositoryMediator.WriteLogs($"CREATE: Add the object into the cache storage successful [{@object.GetCacheKey(false)}]");

				// call post-handlers
				context.CallPostCreateHandlers(@object, false);
				return true;
			}
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw ex;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw new RepositoryOperationException($"Error occurred while creating new [{typeof(T)}#{@object?.GetEntityID()}]", ex);
			}
		}

		/// <summary>
		/// Creates new instance of object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object to create new instance in repository</param>
		public static void Create<T>(RepositoryContext context, string aliasTypeName, T @object) where T : class
		{
			context.AliasTypeName = aliasTypeName;
			if (RepositoryMediator.Create<T>(context, context.GetPrimaryDataSource(), @object))
				Task.Run(() => RepositoryMediator.SyncAsync(@object, context.AliasTypeName, false)).ConfigureAwait(false);
		}

		/// <summary>
		/// Creates new instance of object
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
		/// Creates new instance of object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="object">The object to create instance in repository</param>
		/// <param name="cancellationToken">The cancellation token</param>
		public static async Task<bool> CreateAsync<T>(RepositoryContext context, DataSource dataSource, T @object, CancellationToken cancellationToken = default) where T : class
		{
			context.Prepare<T>(RepositoryOperation.Create, (dataSource ?? context.GetPrimaryDataSource())?.StartSession<T>());
			try
			{
				// validate & re-update object
				var currentState = context.SetCurrentState(@object);
				if (RepositoryMediator.Validate(context.EntityDefinition, currentState))
				{
					// re-update object
					currentState.ForEach(kvp =>
					{
						if (kvp.Key.StartsWith("ExtendedProperties."))
							(@object as IBusinessEntity).ExtendedProperties[kvp.Key.Replace("ExtendedProperties.", "")] = kvp.Value;
						else
							@object.SetAttributeValue(kvp.Key, kvp.Value);
					});

					// update state
					context.SetCurrentState(@object, currentState);
				}

				// call pre-handlers
				if (await context.CallPreCreateHandlersAsync(@object, false, cancellationToken).ConfigureAwait(false))
					return false;

				// create
				dataSource = dataSource ?? context.GetPrimaryDataSource();
				if (dataSource == null)
					throw new InformationInvalidException("Data source is invalid, please check the configuration");

				if (dataSource.Mode.Equals(RepositoryMode.NoSQL))
					await context.CreateAsync(dataSource, @object, null, cancellationToken).ConfigureAwait(false);
				else if (dataSource.Mode.Equals(RepositoryMode.SQL))
					await context.CreateAsync(dataSource, @object, cancellationToken).ConfigureAwait(false);

				// update in cache storage
				if (context.EntityDefinition.Cache != null && await context.EntityDefinition.Cache.SetAsync(@object, 0, cancellationToken).ConfigureAwait(false) && RepositoryMediator.IsDebugEnabled)
					RepositoryMediator.WriteLogs($"CREATE: Add the object into the cache storage successful [{@object.GetCacheKey(false)}]");

				// call post-handlers
				await context.CallPostCreateHandlersAsync(@object, false, cancellationToken).ConfigureAwait(false);
				return true;
			}
			catch (OperationCanceledException ex)
			{
				context.Exception = ex;
				throw ex;
			}
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw ex;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw new RepositoryOperationException($"Error occurred while creating new [{typeof(T)}#{@object?.GetEntityID()}]", ex);
			}
		}

		/// <summary>
		/// Creates new instance of object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object to create instance in repository</param>
		/// <param name="cancellationToken">The cancellation token</param>
		public static async Task CreateAsync<T>(RepositoryContext context, string aliasTypeName, T @object, CancellationToken cancellationToken = default) where T : class
		{
			context.AliasTypeName = aliasTypeName;
			if (await RepositoryMediator.CreateAsync<T>(context, context.GetPrimaryDataSource(), @object, cancellationToken).ConfigureAwait(false))
			{
				var sync = Task.Run(() => RepositoryMediator.SyncAsync(@object, context.AliasTypeName, false, false, cancellationToken)).ConfigureAwait(false);
			}
		}

		/// <summary>
		/// Creates new instance of object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object to create instance in repository</param>
		public static async Task CreateAsync<T>(string aliasTypeName, T @object, CancellationToken cancellationToken = default) where T : class
		{
			using (var context = new RepositoryContext())
			{
				await RepositoryMediator.CreateAsync(context, aliasTypeName, @object, cancellationToken).ConfigureAwait(false);
			}
		}
		#endregion

		#region Get
		/// <summary>
		/// Gets the instance of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="id">The string that present identity (primary-key)</param>
		/// <param name="callHandlers">true to call event-handlers before processing</param>
		/// <param name="processCache">true to process cache (first check existed object, then update cache)</param>
		/// <param name="processSecondaryWhenNotFound">true to process with secondary data source when object is not found</param>
		/// <returns></returns>
		public static T Get<T>(RepositoryContext context, DataSource dataSource, string id, bool callHandlers = true, bool processCache = true, bool processSecondaryWhenNotFound = true) where T : class
		{
			try
			{
				// pre-process
				if (callHandlers)
				{
					context.Prepare<T>(RepositoryOperation.Get);
					if (context.CallPreGetHandlers<T>(id))
						return null;
				}

				// get cached object
				var @object = processCache && context.EntityDefinition.Cache != null
					? context.EntityDefinition.Cache.Fetch<T>(id)
					: null;

				// auto sync
				if (@object != null)
				{
					if (RepositoryMediator.IsDebugEnabled)
						RepositoryMediator.WriteLogs($"GET: The cached object is found [{@object.GetCacheKey(false)}]");
					if (context.EntityDefinition.AutoSync)
						Task.Run(() => RepositoryMediator.SyncAsync(@object, context.AliasTypeName)).ConfigureAwait(false);
				}

				// load from data store if got no cached
				else
				{
					dataSource = dataSource ?? context.GetPrimaryDataSource();
					if (dataSource == null)
						throw new InformationInvalidException("Data source is invalid, please check the configuration");

					@object = dataSource.Mode.Equals(RepositoryMode.NoSQL)
						? context.Get<T>(dataSource, id, null)
						: dataSource.Mode.Equals(RepositoryMode.SQL)
							? context.Get<T>(dataSource, id)
							: null;

					// auto sync
					if (@object != null)
					{
						if (context.EntityDefinition.AutoSync)
							Task.Run(() => RepositoryMediator.SyncAsync(@object, context.AliasTypeName)).ConfigureAwait(false);
					}

					// when not found in primary, then get instance from secondary source
					else if (processSecondaryWhenNotFound)
						try
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
								if (@object != null)
									Task.Run(() => RepositoryMediator.SyncAsync(@object, context.AliasTypeName, context.EntityDefinition)).ConfigureAwait(false);
							}
						}
						catch (Exception ex)
						{
							RepositoryMediator.WriteLogs($"GET: Error occurred while fetching object from secondary data source [{typeof(T)}#{id}]", ex);
						}

					// update into cache storage
					if (@object != null && processCache && context.EntityDefinition.Cache != null && context.EntityDefinition.Cache.Set(@object) && RepositoryMediator.IsDebugEnabled)
						RepositoryMediator.WriteLogs($"GET: Add the object into the cache storage successful [{@object.GetCacheKey(false)}]");
				}

				// update state & call post-handlers
				if (callHandlers && @object != null)
				{
					context.SetCurrentState(@object);
					context.CallPostGetHandlers(@object);
				}

				// return
				return @object;
			}
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw ex;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw new RepositoryOperationException($"Error occurred while fetching object [{typeof(T)}#{id}]", ex);
			}
		}

		/// <summary>
		/// Gets the instance of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="id">The string that present identity (primary-key)</param>
		/// <param name="callHandlers">true to call event-handlers before processing</param>
		/// <param name="processCache">true to process cache (first check existed object, then update cache)</param>
		/// <param name="processSecondaryWhenNotFound">true to process with secondary data source when object is not found</param>
		/// <returns></returns>
		public static T Get<T>(RepositoryContext context, string aliasTypeName, string id, bool callHandlers = true, bool processCache = true, bool processSecondaryWhenNotFound = true) where T : class
		{
			context.AliasTypeName = aliasTypeName;
			return RepositoryMediator.Get<T>(context, context.GetPrimaryDataSource(), id, callHandlers, processCache, processSecondaryWhenNotFound);
		}

		/// <summary>
		/// Gets the instance of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="id">The string that present identity (primary-key)</param>
		/// <param name="processSecondaryWhenNotFound">true to process with secondary data source when object is not found</param>
		/// <returns></returns>
		public static T Get<T>(string aliasTypeName, string id, bool processSecondaryWhenNotFound = true) where T : class
		{
			using (var context = new RepositoryContext(false))
			{
				return RepositoryMediator.Get<T>(context, aliasTypeName, id, true, processSecondaryWhenNotFound);
			}
		}

		/// <summary>
		/// Gets the instance of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="id">The string that present identity (primary-key)</param>
		/// <param name="callHandlers">true to call event-handlers before processing</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <param name="processCache">true to process cache (first check existed object, then update cache)</param>
		/// <param name="processSecondaryWhenNotFound">true to process with secondary data source when object is not found</param>
		/// <returns></returns>
		public static async Task<T> GetAsync<T>(RepositoryContext context, DataSource dataSource, string id, bool callHandlers = true, CancellationToken cancellationToken = default, bool processCache = true, bool processSecondaryWhenNotFound = true) where T : class
		{
			try
			{
				// pre-process
				if (callHandlers)
				{
					context.Prepare<T>(RepositoryOperation.Get);
					if (await context.CallPreGetHandlersAsync<T>(id, cancellationToken).ConfigureAwait(false))
						return null;
				}

				// get cached object
				var @object = processCache && context.EntityDefinition.Cache != null
					? await context.EntityDefinition.Cache.FetchAsync<T>(id, cancellationToken).ConfigureAwait(false)
					: null;

				// auto sync
				if (@object != null)
				{
					if (RepositoryMediator.IsDebugEnabled)
						RepositoryMediator.WriteLogs($"GET: The cached object is found [{@object.GetCacheKey()}]");
					if (context.EntityDefinition.AutoSync)
					{
						var sync = Task.Run(() => RepositoryMediator.SyncAsync(@object, context.AliasTypeName, true, false, cancellationToken)).ConfigureAwait(false);
					}
				}

				// load from data store if got no cached
				else
				{
					// load from primary data source
					dataSource = dataSource ?? context.GetPrimaryDataSource();
					if (dataSource == null)
						throw new InformationInvalidException("Data source is invalid, please check the configuration");

					@object = dataSource.Mode.Equals(RepositoryMode.NoSQL)
						? await context.GetAsync<T>(dataSource, id, null, cancellationToken).ConfigureAwait(false)
						: dataSource.Mode.Equals(RepositoryMode.SQL)
							? await context.GetAsync<T>(dataSource, id, cancellationToken).ConfigureAwait(false)
							: null;

					// auto sync
					if (@object != null)
					{
						if (context.EntityDefinition.AutoSync)
						{
							var sync = Task.Run(() => RepositoryMediator.SyncAsync(@object, context.AliasTypeName, true, false, cancellationToken)).ConfigureAwait(false);
						}
					}

					// when not found in primary, then get instance from secondary source
					else if (processSecondaryWhenNotFound)
						try
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
								if (@object != null)
								{
									var sync = Task.Run(() => RepositoryMediator.SyncAsync(@object, context.AliasTypeName, context.EntityDefinition, cancellationToken)).ConfigureAwait(false);
								}
							}
						}
						catch (Exception ex)
						{
							RepositoryMediator.WriteLogs($"GET: Error occurred while fetching object from secondary data source [{typeof(T)}#{id}]", ex);
						}

					// update into cache storage
					if (@object != null && processCache && context.EntityDefinition.Cache != null && await context.EntityDefinition.Cache.SetAsync(@object, 0, cancellationToken).ConfigureAwait(false) && RepositoryMediator.IsDebugEnabled)
						RepositoryMediator.WriteLogs($"GET: Add the object into the cache storage successful [{@object.GetCacheKey(false)}]");
				}

				// update state & call post-handlers
				if (callHandlers && @object != null)
				{
					context.SetCurrentState(@object);
					await context.CallPostGetHandlersAsync(@object, cancellationToken).ConfigureAwait(false);
				}

				// return
				return @object;
			}
			catch (OperationCanceledException ex)
			{
				context.Exception = ex;
				throw ex;
			}
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw ex;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw new RepositoryOperationException($"Error occurred while fetching object [{typeof(T)}#{id}]", ex);
			}
		}

		/// <summary>
		/// Gets the instance of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="id">The string that present identity (primary-key)</param>
		/// <param name="callHandlers">true to call event-handlers before processing</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <param name="processCache">true to process cache (first check existed object, then update cache)</param>
		/// <param name="processSecondaryWhenNotFound">true to process with secondary data source when object is not found</param>
		/// <returns></returns>
		public static Task<T> GetAsync<T>(RepositoryContext context, string aliasTypeName, string id, bool callHandlers = true, CancellationToken cancellationToken = default, bool processCache = true, bool processSecondaryWhenNotFound = true) where T : class
		{
			context.AliasTypeName = aliasTypeName;
			return RepositoryMediator.GetAsync<T>(context, context.GetPrimaryDataSource(), id, callHandlers, cancellationToken, processCache, processSecondaryWhenNotFound);
		}

		/// <summary>
		/// Gets the instance of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="id">The string that present identity (primary-key)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <param name="processSecondaryWhenNotFound">true to process with secondary data source when object is not found</param>
		/// <returns></returns>
		public static async Task<T> GetAsync<T>(string aliasTypeName, string id, CancellationToken cancellationToken = default, bool processSecondaryWhenNotFound = true) where T : class
		{
			using (var context = new RepositoryContext(false))
			{
				return await RepositoryMediator.GetAsync<T>(context, aliasTypeName, id, true, cancellationToken, true, processSecondaryWhenNotFound).ConfigureAwait(false);
			}
		}
		#endregion

		#region Get (first match)
		/// <summary>
		/// Finds the first instance of an object that matched with the filter expression
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
			context.Prepare<T>(RepositoryOperation.Get, (dataSource ?? context.GetPrimaryDataSource())?.StartSession<T>());
			try
			{
				// prepare
				dataSource = dataSource ?? context.GetPrimaryDataSource();
				if (dataSource == null)
					throw new InformationInvalidException("Data source is invalid, please check the configuration");

				// find
				var @object = dataSource.Mode.Equals(RepositoryMode.NoSQL)
					? context.Get(dataSource, filter, sort, businessEntityID, null)
					: dataSource.Mode.Equals(RepositoryMode.SQL)
						? context.Get(dataSource, filter, sort, businessEntityID)
						: null;

				// auto sync
				if (@object != null && context.EntityDefinition.AutoSync)
					Task.Run(() => RepositoryMediator.SyncAsync(@object, context.AliasTypeName)).ConfigureAwait(false);

				// return
				return @object;
			}
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw ex;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw new RepositoryOperationException($"Error occurred while fetching first-matched object [{typeof(T)}]", ex);
			}
		}

		/// <summary>
		/// Finds the first instance of an object that matched with the filter expression
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
		/// Finds the first instance of an object that matched with the filter expression
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
				return RepositoryMediator.Get<T>(context, aliasTypeName, filter, sort, businessEntityID);
			}
		}

		/// <summary>
		/// Finds the first instance of an object that matched with the filter expression
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="filter">Filter expression</param>
		/// <param name="sort">Sort expression</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<T> GetAsync<T>(RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, SortBy<T> sort = null, string businessEntityID = null, CancellationToken cancellationToken = default) where T : class
		{
			context.Prepare<T>(RepositoryOperation.Get, (dataSource ?? context.GetPrimaryDataSource())?.StartSession<T>());
			try
			{
				// prepare
				dataSource = dataSource ?? context.GetPrimaryDataSource();
				if (dataSource == null)
					throw new InformationInvalidException("Data source is invalid, please check the configuration");

				// find
				var @object = dataSource.Mode.Equals(RepositoryMode.NoSQL)
					? await context.GetAsync(dataSource, filter, sort, businessEntityID, null, cancellationToken).ConfigureAwait(false)
					: dataSource.Mode.Equals(RepositoryMode.SQL)
						? await context.GetAsync(dataSource, filter, sort, businessEntityID, cancellationToken).ConfigureAwait(false)
						: null;

				// auto sync
				if (@object != null && context.EntityDefinition.AutoSync)
				{
					var sync = Task.Run(() => RepositoryMediator.SyncAsync(@object, context.AliasTypeName, true, false, cancellationToken)).ConfigureAwait(false);
				}

				// return
				return @object;
			}
			catch (OperationCanceledException ex)
			{
				context.Exception = ex;
				throw ex;
			}
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw ex;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw new RepositoryOperationException($"Error occurred while fetching first-matched object [{typeof(T)}]", ex);
			}
		}

		/// <summary>
		/// Finds the first instance of an object that matched with the filter expression
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">Filter expression</param>
		/// <param name="sort">Sort expression</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<T> GetAsync<T>(RepositoryContext context, string aliasTypeName, IFilterBy<T> filter, SortBy<T> sort = null, string businessEntityID = null, CancellationToken cancellationToken = default) where T : class
		{
			context.AliasTypeName = aliasTypeName;
			return RepositoryMediator.GetAsync<T>(context, context.GetPrimaryDataSource(), filter, sort, businessEntityID, cancellationToken);
		}

		/// <summary>
		/// Finds the first instance of an object that matched with the filter expression
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">Filter expression</param>
		/// <param name="sort">Sort expression</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<T> GetAsync<T>(string aliasTypeName, IFilterBy<T> filter, SortBy<T> sort = null, string businessEntityID = null, CancellationToken cancellationToken = default) where T : class
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
		/// <param name="processSecondaryWhenNotFound">true to process with secondary data source when object is not found</param>
		/// <returns></returns>
		public static object Get(EntityDefinition definition, DataSource dataSource, string id, bool processSecondaryWhenNotFound = true)
		{
			// check
			if (definition == null || string.IsNullOrWhiteSpace(id) || !id.IsValidUUID())
				return null;

			// get cached object
			var @object = definition.Cache?.Get(definition.Type.GetTypeName(true) + "#" + id.Trim().ToLower());

			// auto sync
			if (@object != null)
			{
				if (RepositoryMediator.IsDebugEnabled)
					RepositoryMediator.WriteLogs($"GET (by definition): The cached object is found [{@object.GetCacheKey(false)}]");

				if (definition.AutoSync)
					Task.Run(() => RepositoryMediator.SyncAsync(@object, definition.RepositoryDefinition.IsAlias ? definition.RepositoryDefinition.Type.GetTypeName() : null)).ConfigureAwait(false);
			}

			// load from data store if got no cached
			else
			{
				dataSource = dataSource ?? definition.GetPrimaryDataSource();
				if (dataSource == null)
					throw new InformationInvalidException("Data source is invalid, please check the configuration");

				@object = dataSource.Mode.Equals(RepositoryMode.NoSQL)
					? NoSqlHelper.Get(dataSource, definition, id)
					: dataSource.Mode.Equals(RepositoryMode.SQL)
						? SqlHelper.Get(dataSource, definition, id)
						: null;

				// auto sync
				if (@object != null)
				{
					if (definition.AutoSync)
						Task.Run(() => RepositoryMediator.SyncAsync(@object, definition.RepositoryDefinition.IsAlias ? definition.RepositoryDefinition.Type.GetTypeName() : null)).ConfigureAwait(false);
				}

				// when not found in primary, then get instance from secondary source
				else if (processSecondaryWhenNotFound)
					try
					{
						var secondaryDataSource = definition.GetSecondaryDataSource();
						if (secondaryDataSource != null && !secondaryDataSource.Name.IsEquals(dataSource.Name))
						{
							@object = secondaryDataSource.Mode.Equals(RepositoryMode.NoSQL)
								? NoSqlHelper.Get(secondaryDataSource, definition, id)
								: dataSource.Mode.Equals(RepositoryMode.SQL)
									? SqlHelper.Get(secondaryDataSource, definition, id)
									: null;

							// re-create object at primary data source
							if (@object != null)
								Task.Run(() => RepositoryMediator.SyncAsync(@object, definition.RepositoryDefinition.IsAlias ? definition.RepositoryDefinition.Type.GetTypeName() : null, definition)).ConfigureAwait(false);
						}
					}
					catch (Exception ex)
					{
						RepositoryMediator.WriteLogs($"GET (by definition): Error occurred while fetching object from secondary data source [{definition.Type}#{id}]", ex);
					}

				// update into cache storage
				if (@object != null && definition.Cache != null && definition.Cache.Set(@object) && RepositoryMediator.IsDebugEnabled)
					RepositoryMediator.WriteLogs($"GET (by definition): Add the object into the cache storage successful [{@object.GetCacheKey(false)}]");
			}

			// return
			return @object;
		}

		/// <summary>
		/// Gets an object by definition and identity
		/// </summary>
		/// <param name="definition">The definition</param>
		/// <param name="id">The identity</param>
		/// <param name="processSecondaryWhenNotFound">true to process with secondary data source when object is not found</param>
		/// <returns></returns>
		public static object Get(EntityDefinition definition, string id, bool processSecondaryWhenNotFound = true)
			=> RepositoryMediator.Get(definition, null, id, processSecondaryWhenNotFound);

		/// <summary>
		/// Gets an object by definition and identity
		/// </summary>
		/// <param name="definitionID">The identity of the entity definition</param>
		/// <param name="objectID">The identity of the object</param>
		/// <param name="processSecondaryWhenNotFound">true to process with secondary data source when object is not found</param>
		/// <returns></returns>
		public static object Get(string definitionID, string objectID, bool processSecondaryWhenNotFound = true)
		{
			var entity = !string.IsNullOrWhiteSpace(definitionID)
				? RepositoryMediator.GetRuntimeRepositoryEntity(definitionID)
				: null;
			return entity != null
				? RepositoryMediator.Get(entity.Definition, objectID, processSecondaryWhenNotFound)
				: null;
		}

		/// <summary>
		/// Gets an object by definition and identity
		/// </summary>
		/// <param name="definition">The definition</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="id">The identity</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <param name="processSecondaryWhenNotFound">true to process with secondary data source when object is not found</param>
		/// <returns></returns>
		public static async Task<object> GetAsync(EntityDefinition definition, DataSource dataSource, string id, CancellationToken cancellationToken = default, bool processSecondaryWhenNotFound = true)
		{
			// check
			if (definition == null || string.IsNullOrWhiteSpace(id) || !id.IsValidUUID())
				return null;

			// get cached object
			var @object = definition.Cache != null
				? await definition.Cache.GetAsync(definition.Type.GetTypeName(true) + "#" + id.Trim().ToLower(), cancellationToken).ConfigureAwait(false)
				: null;

			// auto sync
			if (@object != null)
			{
				if (RepositoryMediator.IsDebugEnabled)
					RepositoryMediator.WriteLogs($"GET (by definition): The cached object is found [{@object.GetCacheKey(false)}]");

				if (definition.AutoSync)
				{
					var sync = Task.Run(() => RepositoryMediator.SyncAsync(@object, definition.RepositoryDefinition.IsAlias ? definition.RepositoryDefinition.Type.GetTypeName() : null, true, false, cancellationToken)).ConfigureAwait(false);
				}
			}

			// load from data store if got no cached
			else
			{
				dataSource = dataSource ?? definition.GetPrimaryDataSource();
				if (dataSource == null)
					throw new InformationInvalidException("Data source is invalid, please check the configuration");

				@object = dataSource.Mode.Equals(RepositoryMode.NoSQL)
					? await NoSqlHelper.GetAsync(definition, id, null, cancellationToken).ConfigureAwait(false)
					: dataSource.Mode.Equals(RepositoryMode.SQL)
						? await SqlHelper.GetAsync(definition, id, cancellationToken).ConfigureAwait(false)
						: null;

				// auto sync
				if (@object != null)
				{
					if (definition.AutoSync)
					{
						var sync = Task.Run(() => RepositoryMediator.SyncAsync(@object, definition.RepositoryDefinition.IsAlias ? definition.RepositoryDefinition.Type.GetTypeName() : null, true, false, cancellationToken)).ConfigureAwait(false);
					}
				}

				// when not found in primary, then get instance from secondary source
				else if (processSecondaryWhenNotFound)
					try
					{
						var secondaryDataSource = definition.GetSecondaryDataSource();
						if (secondaryDataSource != null && !secondaryDataSource.Name.IsEquals(dataSource.Name))
						{
							@object = secondaryDataSource.Mode.Equals(RepositoryMode.NoSQL)
								? await NoSqlHelper.GetAsync(secondaryDataSource, definition, id, null, cancellationToken).ConfigureAwait(false)
								: dataSource.Mode.Equals(RepositoryMode.SQL)
									? await SqlHelper.GetAsync(secondaryDataSource, definition, id, cancellationToken).ConfigureAwait(false)
									: null;

							// re-create object at primary data source
							if (@object != null)
							{
								var sync = Task.Run(() => RepositoryMediator.SyncAsync(@object, definition.RepositoryDefinition.IsAlias ? definition.RepositoryDefinition.Type.GetTypeName() : null, definition, cancellationToken)).ConfigureAwait(false);
							}
						}
					}
					catch (OperationCanceledException ex)
					{
						throw ex;
					}
					catch (Exception ex)
					{
						RepositoryMediator.WriteLogs($"GET (by definition): Error occurred while fetching object from secondary data source [{definition.Type}#{id}]", ex);
					}

				// update into cache storage
				if (@object != null && definition.Cache != null && await definition.Cache.SetAsync(@object, 0, cancellationToken).ConfigureAwait(false) && RepositoryMediator.IsDebugEnabled)
					RepositoryMediator.WriteLogs($"GET (by definition): Add the object into the cache storage successful [{@object.GetCacheKey(false)}]");
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
		/// <param name="processSecondaryWhenNotFound">true to process with secondary data source when object is not found</param>
		/// <returns></returns>
		public static Task<object> GetAsync(EntityDefinition definition, string id, CancellationToken cancellationToken = default, bool processSecondaryWhenNotFound = true)
			=> RepositoryMediator.GetAsync(definition, null, id, cancellationToken, processSecondaryWhenNotFound);

		/// <summary>
		/// Gets an object by definition and identity
		/// </summary>
		/// <param name="definitionID">The identity of the entity definition</param>
		/// <param name="objectID">The identity of the object</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <param name="processSecondaryWhenNotFound">true to process with secondary data source when object is not found</param>
		/// <returns></returns>
		public static Task<object> GetAsync(string definitionID, string objectID, CancellationToken cancellationToken = default, bool processSecondaryWhenNotFound = true)
		{
			var entity = !string.IsNullOrWhiteSpace(definitionID)
				? RepositoryMediator.GetRuntimeRepositoryEntity(definitionID)
				: null;
			return entity != null
				? RepositoryMediator.GetAsync(entity.Definition, objectID, cancellationToken, processSecondaryWhenNotFound)
				: null;
		}
		#endregion

		#region Replace
		/// <summary>
		/// Updates instance of an object (using replace method)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="object">The object that presents the instance in repository need to be updated</param>
		/// <param name="dontCreateNewVersion">Force to not create new version when update the object</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		public static bool Replace<T>(RepositoryContext context, DataSource dataSource, T @object, bool dontCreateNewVersion = false, string userID = null) where T : class
		{
			context.Prepare<T>(RepositoryOperation.Update, (dataSource ?? context.GetPrimaryDataSource())?.StartSession<T>());
			try
			{
				// check state
				var previousInstance = @object != null
					? RepositoryMediator.Get<T>(context, dataSource, @object?.GetEntityID(), false)
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
					currentState.ForEach(kvp =>
					{
						if (kvp.Key.StartsWith("ExtendedProperties."))
							(@object as IBusinessEntity).ExtendedProperties[kvp.Key.Replace("ExtendedProperties.", "")] = kvp.Value;
						else
							@object.SetAttributeValue(kvp.Key, kvp.Value);
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
				if (dataSource == null)
					throw new InformationInvalidException("Data source is invalid, please check the configuration");

				if (dataSource.Mode.Equals(RepositoryMode.NoSQL))
					context.Replace(dataSource, @object, null);
				else if (dataSource.Mode.Equals(RepositoryMode.SQL))
					context.Replace(dataSource, @object);

				// update into cache storage
				if (context.EntityDefinition.Cache != null && context.EntityDefinition.Cache.Set(@object) && RepositoryMediator.IsDebugEnabled)
					RepositoryMediator.WriteLogs($"REPLACE: Add the object into the cache storage successful [{@object.GetCacheKey(false)}]");

				// call post-handlers
				context.CallPostUpdateHandlers(@object, dirtyAttributes, false);
				return true;
			}
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw ex;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw new RepositoryOperationException($"Error occurred while replacing object [{typeof(T)}#{@object?.GetEntityID()}]", ex);
			}
		}

		/// <summary>
		/// Updates instance of an object (using replace method)
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
				Task.Run(() => RepositoryMediator.SyncAsync(@object, context.AliasTypeName)).ConfigureAwait(false);
		}

		/// <summary>
		/// Updates instance of an object (using replace method)
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
		/// Updates instance of an object (using replace method)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="object">The object that presents the instance in repository need to be updated</param>
		/// <param name="dontCreateNewVersion">Force to not create new version when update the object</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		public static async Task<bool> ReplaceAsync<T>(RepositoryContext context, DataSource dataSource, T @object, bool dontCreateNewVersion = false, string userID = null, CancellationToken cancellationToken = default) where T : class
		{
			context.Prepare<T>(RepositoryOperation.Update, (dataSource ?? context.GetPrimaryDataSource())?.StartSession<T>());
			try
			{
				// check state
				var previousInstance = @object != null
					? await RepositoryMediator.GetAsync<T>(context, dataSource, @object?.GetEntityID(), false, cancellationToken).ConfigureAwait(false)
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
					currentState.ForEach(kvp =>
					{
						if (kvp.Key.StartsWith("ExtendedProperties."))
							(@object as IBusinessEntity).ExtendedProperties[kvp.Key.Replace("ExtendedProperties.", "")] = kvp.Value;
						else
							@object.SetAttributeValue(kvp.Key, kvp.Value);
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
				if (dataSource == null)
					throw new InformationInvalidException("Data source is invalid, please check the configuration");

				if (dataSource.Mode.Equals(RepositoryMode.NoSQL))
					await context.ReplaceAsync(dataSource, @object, null, cancellationToken).ConfigureAwait(false);
				else if (dataSource.Mode.Equals(RepositoryMode.SQL))
					await context.ReplaceAsync(dataSource, @object, cancellationToken).ConfigureAwait(false);

				// update into cache storage
				if (context.EntityDefinition.Cache != null && await context.EntityDefinition.Cache.SetAsync(@object, 0, cancellationToken).ConfigureAwait(false) && RepositoryMediator.IsDebugEnabled)
					RepositoryMediator.WriteLogs($"REPLACE: Add the object into the cache storage successful [{@object.GetCacheKey(false)}]");

				// call post-handlers
				await context.CallPostUpdateHandlersAsync(@object, dirtyAttributes, false, cancellationToken).ConfigureAwait(false);
				return true;
			}
			catch (OperationCanceledException ex)
			{
				context.Exception = ex;
				throw ex;
			}
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw ex;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw new RepositoryOperationException($"Error occurred while replacing object [{typeof(T)}#{@object?.GetEntityID()}]", ex);
			}
		}

		/// <summary>
		/// Updates instance of an object (using replace method)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object that presents the instance in repository need to be updated</param>
		/// <param name="dontCreateNewVersion">Force to not create new version when update the object</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		public static async Task ReplaceAsync<T>(RepositoryContext context, string aliasTypeName, T @object, bool dontCreateNewVersion = false, string userID = null, CancellationToken cancellationToken = default) where T : class
		{
			context.AliasTypeName = aliasTypeName;
			if (await RepositoryMediator.ReplaceAsync<T>(context, context.GetPrimaryDataSource(), @object, dontCreateNewVersion, userID, cancellationToken).ConfigureAwait(false))
			{
				var sync = Task.Run(() => RepositoryMediator.SyncAsync(@object, context.AliasTypeName, true, false, cancellationToken)).ConfigureAwait(false);
			}
		}

		/// <summary>
		/// Updates instance of an object (using replace method)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object that presents the instance in repository need to be updated</param>
		/// <param name="dontCreateNewVersion">Force to not create new version when update the object</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		public static async Task ReplaceAsync<T>(string aliasTypeName, T @object, bool dontCreateNewVersion = false, string userID = null, CancellationToken cancellationToken = default) where T : class
		{
			using (var context = new RepositoryContext())
			{
				await RepositoryMediator.ReplaceAsync<T>(context, aliasTypeName, @object, dontCreateNewVersion, userID, cancellationToken).ConfigureAwait(false);
			}
		}
		#endregion

		#region Update
		/// <summary>
		/// Updates instance of an object (only update changed attributes) 
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="object">The object that presents the instance in repository need to be updated</param>
		/// <param name="dontCreateNewVersion">Force to not create new version when update the object</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		public static bool Update<T>(RepositoryContext context, DataSource dataSource, T @object, bool dontCreateNewVersion = false, string userID = null) where T : class
		{
			context.Prepare<T>(RepositoryOperation.Update, (dataSource ?? context.GetPrimaryDataSource())?.StartSession<T>());
			try
			{
				// check state
				var previousInstance = @object != null
					? RepositoryMediator.Get<T>(context, dataSource, @object?.GetEntityID(), false)
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
					currentState.ForEach(kvp =>
					{
						if (kvp.Key.StartsWith("ExtendedProperties."))
							(@object as IBusinessEntity).ExtendedProperties[kvp.Key.Replace("ExtendedProperties.", "")] = kvp.Value;
						else
							@object.SetAttributeValue(kvp.Key, kvp.Value);
					});

					// update state
					context.SetCurrentState(@object, currentState);
				}

				// call pre-handlers
				if (context.CallPreUpdateHandlers(@object, dirtyAttributes, false))
					return false;

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
				if (dataSource == null)
					throw new InformationInvalidException("Data source is invalid, please check the configuration");

				if (dataSource.Mode.Equals(RepositoryMode.NoSQL))
					context.Update(dataSource, @object, updatedAttributes, null);
				else if (dataSource.Mode.Equals(RepositoryMode.SQL))
					context.Update(dataSource, @object, updatedAttributes);

				// update into cache storage
				if (context.EntityDefinition.Cache != null && context.EntityDefinition.Cache.Set(@object) && RepositoryMediator.IsDebugEnabled)
					RepositoryMediator.WriteLogs($"UPDATE: Add the object into the cache storage successful [{@object.GetCacheKey(false)}]");

				// call post-handlers
				context.CallPostUpdateHandlers(@object, dirtyAttributes, false);
				return true;
			}
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw ex;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw new RepositoryOperationException($"Error occurred while updating object [{typeof(T)}#{@object?.GetEntityID()}]", ex);
			}
		}

		/// <summary>
		/// Updates instance of an object (only update changed attributes) 
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
			if (RepositoryMediator.Update<T>(context, context.GetPrimaryDataSource(), @object, dontCreateNewVersion, userID))
				Task.Run(() => RepositoryMediator.SyncAsync(@object, context.AliasTypeName)).ConfigureAwait(false);
		}

		/// <summary>
		/// Updates instance of an object (only update changed attributes) 
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
		/// Updates instance of an object (only update changed attributes) 
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="object">The object that presents the instance in repository need to be updated</param>
		/// <param name="dontCreateNewVersion">Force to not create new version when update the object</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		public static async Task<bool> UpdateAsync<T>(RepositoryContext context, DataSource dataSource, T @object, bool dontCreateNewVersion = false, string userID = null, CancellationToken cancellationToken = default) where T : class
		{
			context.Prepare<T>(RepositoryOperation.Update, (dataSource ?? context.GetPrimaryDataSource())?.StartSession<T>());
			try
			{
				// check state
				var previousInstance = @object != null
					? await RepositoryMediator.GetAsync<T>(context, dataSource, @object?.GetEntityID(), false, cancellationToken).ConfigureAwait(false)
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
					currentState.ForEach(kvp =>
					{
						if (kvp.Key.StartsWith("ExtendedProperties."))
							(@object as IBusinessEntity).ExtendedProperties[kvp.Key.Replace("ExtendedProperties.", "")] = kvp.Value;
						else
							@object.SetAttributeValue(kvp.Key, kvp.Value);
					});

					// update state
					context.SetCurrentState(@object, currentState);
				}

				// call pre-handlers
				if (await context.CallPreUpdateHandlersAsync(@object, dirtyAttributes, false, cancellationToken).ConfigureAwait(false))
					return false;

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
				if (dataSource == null)
					throw new InformationInvalidException("Data source is invalid, please check the configuration");

				if (dataSource.Mode.Equals(RepositoryMode.NoSQL))
					await context.UpdateAsync(dataSource, @object, updatedAttributes, null, cancellationToken).ConfigureAwait(false);
				else if (dataSource.Mode.Equals(RepositoryMode.SQL))
					await context.UpdateAsync(dataSource, @object, updatedAttributes, cancellationToken).ConfigureAwait(false);

				// update into cache storage
				if (context.EntityDefinition.Cache != null && await context.EntityDefinition.Cache.SetAsync(@object, 0, cancellationToken).ConfigureAwait(false) && RepositoryMediator.IsDebugEnabled)
					RepositoryMediator.WriteLogs($"UPDATE: Add the object into the cache storage successful [{@object.GetCacheKey(false)}]");

				// call post-handlers
				await context.CallPostUpdateHandlersAsync(@object, dirtyAttributes, false, cancellationToken).ConfigureAwait(false);
				return true;
			}
			catch (OperationCanceledException ex)
			{
				context.Exception = ex;
				throw ex;
			}
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw ex;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw new RepositoryOperationException($"Error occurred while updating object [{typeof(T)}#{@object?.GetEntityID()}]", ex);
			}
		}

		/// <summary>
		/// Updates instance of an object (only update changed attributes) 
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object that presents the instance in repository need to be updated</param>
		/// <param name="dontCreateNewVersion">Force to not create new version when update the object</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		public static async Task UpdateAsync<T>(RepositoryContext context, string aliasTypeName, T @object, bool dontCreateNewVersion = false, string userID = null, CancellationToken cancellationToken = default) where T : class
		{
			context.AliasTypeName = aliasTypeName;
			if (await RepositoryMediator.UpdateAsync<T>(context, context.GetPrimaryDataSource(), @object, dontCreateNewVersion, userID, cancellationToken).ConfigureAwait(false))
			{
				var sync = Task.Run(() => RepositoryMediator.SyncAsync(@object, context.AliasTypeName, true, false, cancellationToken)).ConfigureAwait(false);
			}
		}

		/// <summary>
		/// Updates instance of an object (only update changed attributes) 
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object that presents the instance in repository need to be updated</param>
		/// <param name="dontCreateNewVersion">Force to not create new version when update the object</param>
		/// <param name="userID">The identity of user who updates the object (for creating new version)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		public static async Task UpdateAsync<T>(string aliasTypeName, T @object, bool dontCreateNewVersion = false, string userID = null, CancellationToken cancellationToken = default) where T : class
		{
			using (var context = new RepositoryContext())
			{
				await RepositoryMediator.UpdateAsync<T>(context, aliasTypeName, @object, dontCreateNewVersion, userID, cancellationToken).ConfigureAwait(false);
			}
		}
		#endregion

		#region Delete
		/// <summary>
		/// Deletes instance of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="id">The string that presents object identity that want to delete instance from repository</param>
		/// <param name="userID">The identity of user who deletes the object (for creating new trash content)</param>
		public static T Delete<T>(RepositoryContext context, DataSource dataSource, string id, string userID = null) where T : class
		{
			context.Prepare<T>(RepositoryOperation.Delete, (dataSource ?? context.GetPrimaryDataSource())?.StartSession<T>());
			try
			{
				// check existing
				var @object = RepositoryMediator.Get<T>(context, dataSource, id, false);
				if (@object == null)
					return null;

				// call pre-handlers
				context.SetCurrentState(@object);
				if (context.CallPreDeleteHandlers(@object))
					return null;

				// create trash content
				RepositoryMediator.CreateTrashContent(context, @object, userID);

				// delete
				dataSource = dataSource ?? context.GetPrimaryDataSource();
				if (dataSource == null)
					throw new InformationInvalidException("Data source is invalid, please check the configuration");

				if (dataSource.Mode.Equals(RepositoryMode.NoSQL))
					context.Delete(dataSource, @object, null);
				else if (dataSource.Mode.Equals(RepositoryMode.SQL))
					context.Delete(dataSource, @object);

				// remove from cache storage
				if (context.EntityDefinition.Cache != null && context.EntityDefinition.Cache.Remove(@object) && RepositoryMediator.IsDebugEnabled)
					RepositoryMediator.WriteLogs($"DELETE: Remove the cached object from the cache storage successful [{@object.GetCacheKey(false)}]");

				// call post-handlers
				context.CallPostDeleteHandlers(@object);
				return @object;
			}
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw ex;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw new RepositoryOperationException($"Error occurred while deleting object [{typeof(T)}#{id}]", ex);
			}
		}

		/// <summary>
		/// Deletes instance of an object
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
				Task.Run(() => RepositoryMediator.SyncAsync(@object, context.AliasTypeName, false, true)).ConfigureAwait(false);
		}

		/// <summary>
		/// Deletes instance of an object
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
		/// Deletes instance of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="id">The string that presents object identity that want to delete instance from repository</param>
		/// <param name="userID">The identity of user who deletes the object (for creating new trash content)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		public static async Task<T> DeleteAsync<T>(RepositoryContext context, DataSource dataSource, string id, string userID = null, CancellationToken cancellationToken = default) where T : class
		{
			context.Prepare<T>(RepositoryOperation.Delete, (dataSource ?? context.GetPrimaryDataSource())?.StartSession<T>());
			try
			{
				// check existing
				var @object = await RepositoryMediator.GetAsync<T>(context, dataSource, id, false, cancellationToken).ConfigureAwait(false);
				if (@object == null)
					return null;

				// call pre-handlers
				context.SetCurrentState(@object);
				if (await context.CallPreDeleteHandlersAsync(@object, cancellationToken).ConfigureAwait(false))
					return null;

				// create trash content
				await RepositoryMediator.CreateTrashContentAsync(context, @object, userID, cancellationToken).ConfigureAwait(false);

				// delete
				dataSource = dataSource ?? context.GetPrimaryDataSource();
				if (dataSource == null)
					throw new InformationInvalidException("Data source is invalid, please check the configuration");

				if (dataSource.Mode.Equals(RepositoryMode.NoSQL))
					await context.DeleteAsync(dataSource, @object, null, cancellationToken).ConfigureAwait(false);
				else if (dataSource.Mode.Equals(RepositoryMode.SQL))
					await context.DeleteAsync(dataSource, @object, cancellationToken).ConfigureAwait(false);

				// remove from cache storage
				if (context.EntityDefinition.Cache != null && await context.EntityDefinition.Cache.RemoveAsync(@object, cancellationToken).ConfigureAwait(false) && RepositoryMediator.IsDebugEnabled)
					RepositoryMediator.WriteLogs($"DELETE: Remove the cached object from the cache storage successful [{@object.GetCacheKey(false)}]");

				// call post-handlers
				await context.CallPostDeleteHandlersAsync(@object, cancellationToken).ConfigureAwait(false);
				return @object;
			}
			catch (OperationCanceledException ex)
			{
				context.Exception = ex;
				throw ex;
			}
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw ex;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw new RepositoryOperationException($"Error occurred while deleting object [{typeof(T)}#{id}]", ex);
			}
		}

		/// <summary>
		/// Deletes instance of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="id">The string that presents object identity that want to delete instance from repository</param>
		/// <param name="userID">The identity of user who deletes the object (for creating new trash content)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		public static async Task DeleteAsync<T>(RepositoryContext context, string aliasTypeName, string id, string userID = null, CancellationToken cancellationToken = default) where T : class
		{
			context.AliasTypeName = aliasTypeName;
			var @object = await RepositoryMediator.DeleteAsync<T>(context, context.GetPrimaryDataSource(), id, userID, cancellationToken).ConfigureAwait(false);
			if (@object != null)
			{
				var sync = Task.Run(() => RepositoryMediator.SyncAsync(@object, context.AliasTypeName, false, true, cancellationToken)).ConfigureAwait(false);
			}
		}

		/// <summary>
		/// Deletes instance of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="id">The string that presents object identity that want to delete instance from repository</param>
		/// <param name="userID">The identity of user who deletes the object (for creating new trash content)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		public static async Task DeleteAsync<T>(string aliasTypeName, string id, string userID = null, CancellationToken cancellationToken = default) where T : class
		{
			using (var context = new RepositoryContext())
			{
				await RepositoryMediator.DeleteAsync<T>(context, aliasTypeName, id, userID, cancellationToken).ConfigureAwait(false);
			}
		}
		#endregion

		#region Delete (many)
		/// <summary>
		/// Deletes many instances of objects
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="filter">Filter expression to filter instances to delete</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		public static void DeleteMany<T>(RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, string businessEntityID = null) where T : class
		{
			context.Prepare<T>(RepositoryOperation.Delete, (dataSource ?? context.GetPrimaryDataSource())?.StartSession<T>());
			try
			{
				// prepare
				dataSource = dataSource ?? context.GetPrimaryDataSource();
				if (dataSource == null)
					throw new InformationInvalidException("Data source is invalid, please check the configuration");

				// delete
				if (dataSource.Mode.Equals(RepositoryMode.NoSQL))
					context.DeleteMany(dataSource, filter, businessEntityID, null);
				else if (dataSource.Mode.Equals(RepositoryMode.SQL))
					context.DeleteMany(dataSource, filter, businessEntityID);

				// delete other data sources
				Task.Run(() => RepositoryMediator.SyncAsync(filter, context.AliasTypeName, businessEntityID)).ConfigureAwait(false);
			}
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw ex;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw new RepositoryOperationException($"Error occurred while deleting multiple objects [{typeof(T)}]", ex);
			}
		}

		/// <summary>
		/// Deletes many instances of objects
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
		/// Deletes many instances of objects
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
		/// Deletes many instances of objects
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="filter">Filter expression to filter instances to delete</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		public static async Task DeleteManyAsync<T>(RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, string businessEntityID = null, CancellationToken cancellationToken = default) where T : class
		{
			context.Prepare<T>(RepositoryOperation.Delete, (dataSource ?? context.GetPrimaryDataSource())?.StartSession<T>());
			try
			{
				// prepare
				dataSource = dataSource ?? context.GetPrimaryDataSource();
				if (dataSource == null)
					throw new InformationInvalidException("Data source is invalid, please check the configuration");

				// delete
				if (dataSource.Mode.Equals(RepositoryMode.NoSQL))
					await context.DeleteManyAsync(dataSource, filter, businessEntityID, null, cancellationToken).ConfigureAwait(false);
				else if (dataSource.Mode.Equals(RepositoryMode.SQL))
					await context.DeleteManyAsync(dataSource, filter, businessEntityID, cancellationToken).ConfigureAwait(false);

				// delete other data sources
				var sync = Task.Run(() => RepositoryMediator.SyncAsync(filter, context.AliasTypeName, businessEntityID, cancellationToken)).ConfigureAwait(false);
			}
			catch (OperationCanceledException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw ex;
			}
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw ex;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				throw new RepositoryOperationException($"Error occurred while deleting multiple objects [{typeof(T)}]", ex);
			}
		}

		/// <summary>
		/// Deletes many instances of objects
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">Filter expression to filter instances to delete</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		public static async Task DeleteManyAsync<T>(RepositoryContext context, string aliasTypeName, IFilterBy<T> filter, string businessEntityID = null, CancellationToken cancellationToken = default) where T : class
		{
			context.AliasTypeName = aliasTypeName;
			await RepositoryMediator.DeleteManyAsync<T>(context, context.GetPrimaryDataSource(), filter, businessEntityID, cancellationToken).ConfigureAwait(false);
		}

		/// <summary>
		/// Deletes many instances of objects
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">Filter expression to filter instances to delete</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		public static async Task DeleteManyAsync<T>(string aliasTypeName, IFilterBy<T> filter, string businessEntityID = null, CancellationToken cancellationToken = default) where T : class
		{
			using (var context = new RepositoryContext())
			{
				await RepositoryMediator.DeleteManyAsync<T>(context, aliasTypeName, filter, businessEntityID, cancellationToken).ConfigureAwait(false);
			}
		}
		#endregion

		#region Find
		/// <summary>
		/// Finds the identity of objects
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
			context.Prepare<T>(RepositoryOperation.Query, (dataSource ?? context.GetPrimaryDataSource())?.StartSession<T>());
			try
			{
				// prepare
				dataSource = dataSource ?? context.GetPrimaryDataSource();
				if (dataSource == null)
					throw new InformationInvalidException("Data source is invalid, please check the configuration");

				// find identities
				return context.EntityDefinition.Cache != null && !string.IsNullOrWhiteSpace(cacheKey)
					? context.EntityDefinition.Cache.Get<List<string>>(cacheKey)
					: dataSource.Mode.Equals(RepositoryMode.NoSQL)
						? context.SelectIdentities(dataSource, filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents, null)
						: dataSource.Mode.Equals(RepositoryMode.SQL)
							? context.SelectIdentities(dataSource, filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents)
							: new List<string>();
			}
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw ex;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw new RepositoryOperationException("Error occurred while finding identities of objects", ex);
			}
		}

		/// <summary>
		/// Finds the identity of objects
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
		/// Finds the identity of objects
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
		/// Finds the identity of objects
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
			=> RepositoryMediator.FindIdentities<T>(null, filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents, cacheKey, cacheTime);

		/// <summary>
		/// Finds objects
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
			context.Prepare<T>(RepositoryOperation.Query, (dataSource ?? context.GetPrimaryDataSource())?.StartSession<T>());
			try
			{
				// prepare
				dataSource = dataSource ?? context.GetPrimaryDataSource();
				if (dataSource == null)
					throw new InformationInvalidException("Data source is invalid, please check the configuration");

				List<T> objects = null;

				// find identities
				var identities = context.EntityDefinition.Cache == null
					? null
					: RepositoryMediator.FindIdentities<T>(context, dataSource, filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents, cacheKey, cacheTime);

				if (RepositoryMediator.IsDebugEnabled)
					RepositoryMediator.WriteLogs(
						$"FIND: Find objects [{context.EntityDefinition.Type.GetTypeName()}] - Caching storage: {context.EntityDefinition.Cache?.Name ?? "None"}" + "\r\n" +
						$"- Total identities are found: {(identities != null ? identities.Count.ToString() : "0")}" + "\r\n" +
						$"- Mode: {dataSource.Mode}" + "\r\n" +
						$"- Page Size: {pageSize}" + "\r\n" +
						$"- Page Number: {pageNumber}" + "\r\n" +
						$"- Filter By: {filter?.ToString() ?? "None"}" + "\r\n" +
						$"- Sort By: {sort?.ToString() ?? "None"}"
					);

				// process cache
				if (identities != null && identities.Count > 0)
				{
					if (RepositoryMediator.IsDebugEnabled)
						RepositoryMediator.WriteLogs($"FIND: Total {identities.Count} identities are fetched [{identities.ToString(" - ")}]");

					// get cached objects
					var cached = context.EntityDefinition.Cache?.Get<T>(identities.Select(id => id.GetCacheKey<T>()));
					if (cached != null)
					{
						if (RepositoryMediator.IsDebugEnabled)
							RepositoryMediator.WriteLogs($"FIND: Total {cached.Count} cached object(s) are found [{cached.Select(item => item.Key).ToString(" - ")}]");

						// prepare
						var results = identities.Where(id => !string.IsNullOrWhiteSpace(id)).ToDictionary(id => id, id => default(T));

						// add cached objects
						var ids = new List<string>();
						cached.ForEach(item =>
						{
							var id = item.Value.GetEntityID();
							if (!string.IsNullOrWhiteSpace(id))
							{
								ids.Add(id);
								results[id] = item.Value;
							}
						});

						// find missing objects
						identities = identities.Where(id => !string.IsNullOrWhiteSpace(id)).Except(ids, StringComparer.OrdinalIgnoreCase).ToList();
						if (identities.Count > 0)
						{
							var missing = dataSource.Mode.Equals(RepositoryMode.NoSQL)
								? context.Find(dataSource, identities, sort, businessEntityID, null)
								: dataSource.Mode.Equals(RepositoryMode.SQL)
									? context.Find(dataSource, identities, sort, businessEntityID)
									: new List<T>();

							// update results & cache
							missing.Where(@object => @object != null).ForEach(@object => results[@object.GetEntityID()] = @object);
							context.EntityDefinition.Cache?.Set(missing);

							if (context.EntityDefinition.Cache != null && RepositoryMediator.IsDebugEnabled)
								RepositoryMediator.WriteLogs($"FIND: Add {missing.Count} missing object(s) into cache storage successful [{missing.Select(o => o.GetCacheKey()).ToString(" - ")}]");
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
							? context.Find(dataSource, filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents, null)
							: dataSource.Mode.Equals(RepositoryMode.SQL)
								? context.Find(dataSource, filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents)
								: new List<T>()
						: new List<T>();

					if (context.EntityDefinition.Cache != null && objects.Count > 0)
					{
						if (!string.IsNullOrWhiteSpace(cacheKey))
							context.EntityDefinition.Cache.Set(cacheKey, objects.Select(@object => @object.GetEntityID()).ToList(), cacheTime < 1 ? context.EntityDefinition.Cache.ExpirationTime / 2 : cacheTime);
						context.EntityDefinition.Cache.Set(objects);
						if (RepositoryMediator.IsDebugEnabled)
							RepositoryMediator.WriteLogs($"FIND: Add {objects.Count} raw object(s) into cache storage successful [{objects.Select(o => o.GetCacheKey()).ToString(" - ")}]");
					}
				}

				return objects;
			}
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw ex;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw new RepositoryOperationException($"Error occurred while finding objects [{typeof(T)}]", ex);
			}
		}

		/// <summary>
		/// Finds objects
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
		/// Finds objects
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
		/// Finds the identity of objects
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
		public static async Task<List<string>> FindIdentitiesAsync<T>(RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessEntityID = null, bool autoAssociateWithMultipleParents = true, string cacheKey = null, int cacheTime = 0, CancellationToken cancellationToken = default) where T : class
		{
			context.Prepare<T>(RepositoryOperation.Query, (dataSource ?? context.GetPrimaryDataSource())?.StartSession<T>());
			try
			{
				// prepare
				dataSource = dataSource ?? context.GetPrimaryDataSource();
				if (dataSource == null)
					throw new InformationInvalidException("Data source is invalid, please check the configuration");

				// find identities
				return context.EntityDefinition.Cache != null && !string.IsNullOrWhiteSpace(cacheKey)
					? await context.EntityDefinition.Cache.GetAsync<List<string>>(cacheKey)
					: dataSource.Mode.Equals(RepositoryMode.NoSQL)
						? await context.SelectIdentitiesAsync(dataSource, filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents, null, cancellationToken)
						: dataSource.Mode.Equals(RepositoryMode.SQL)
							? await context.SelectIdentitiesAsync(dataSource, filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents, cancellationToken)
							: new List<string>();
			}
			catch (OperationCanceledException ex)
			{
				context.Exception = ex;
				throw ex;
			}
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw ex;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw new RepositoryOperationException("Error occurred while finding identities of objects", ex);
			}
		}

		/// <summary>
		/// Finds the identity of objects
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
		public static async Task<List<string>> FindIdentitiesAsync<T>(RepositoryContext context, string aliasTypeName, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessEntityID = null, bool autoAssociateWithMultipleParents = true, string cacheKey = null, int cacheTime = 0, CancellationToken cancellationToken = default) where T : class
		{
			context.AliasTypeName = aliasTypeName;
			return await RepositoryMediator.FindIdentitiesAsync<T>(context, context.GetPrimaryDataSource(), filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents, cacheKey, cacheTime, cancellationToken).ConfigureAwait(false);
		}

		/// <summary>
		/// Finds the identity of objects
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
		public static async Task<List<string>> FindIdentitiesAsync<T>(string aliasTypeName, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessEntityID = null, bool autoAssociateWithMultipleParents = true, string cacheKey = null, int cacheTime = 0, CancellationToken cancellationToken = default) where T : class
		{
			using (var context = new RepositoryContext(false))
			{
				return await RepositoryMediator.FindIdentitiesAsync<T>(context, aliasTypeName, filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents, cacheKey, cacheTime, cancellationToken).ConfigureAwait(false);
			}
		}

		/// <summary>
		/// Finds the identity of objects
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
		public static Task<List<string>> FindIdentitiesAsync<T>(IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessEntityID = null, bool autoAssociateWithMultipleParents = true, string cacheKey = null, int cacheTime = 0, CancellationToken cancellationToken = default) where T : class
			=> RepositoryMediator.FindIdentitiesAsync<T>(null, filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents, cacheKey, cacheTime, cancellationToken);

		/// <summary>
		/// Finds objects
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
		public static async Task<List<T>> FindAsync<T>(RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessEntityID = null, bool autoAssociateWithMultipleParents = true, string cacheKey = null, int cacheTime = 0, CancellationToken cancellationToken = default) where T : class
		{
			context.Prepare<T>(RepositoryOperation.Query, (dataSource ?? context.GetPrimaryDataSource())?.StartSession<T>());
			try
			{
				// prepare
				dataSource = dataSource ?? context.GetPrimaryDataSource();
				if (dataSource == null)
					throw new InformationInvalidException("Data source is invalid, please check the configuration");

				List<T> objects = null;

				// find identities
				var identities = context.EntityDefinition.Cache == null
					? null
					: await RepositoryMediator.FindIdentitiesAsync<T>(context, dataSource, filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents, cacheKey, cacheTime, cancellationToken).ConfigureAwait(false);

				if (RepositoryMediator.IsDebugEnabled)
					RepositoryMediator.WriteLogs(
						$"FIND: Find objects [{context.EntityDefinition.Type.GetTypeName()}] - Caching storage: {context.EntityDefinition.Cache?.Name ?? "None"}" + "\r\n" +
						$"- Total identities are found: {(identities != null ? identities.Count.ToString() : "0")}" + "\r\n" +
						$"- Mode: {dataSource.Mode}" + "\r\n" +
						$"- Page Size: {pageSize}" + "\r\n" +
						$"- Page Number: {pageNumber}" + "\r\n" +
						$"- Filter By: {filter?.ToString() ?? "None"}" + "\r\n" +
						$"- Sort By: {sort?.ToString() ?? "None"}"
					);

				// process
				if (identities != null && identities.Count > 0)
				{
					if (RepositoryMediator.IsDebugEnabled)
						RepositoryMediator.WriteLogs($"FIND: Total {identities.Count} identities are fetched [{identities.ToString(" - ")}]");

					// get cached objects
					var cached = context.EntityDefinition.Cache != null
						? await context.EntityDefinition.Cache.GetAsync<T>(identities.Select(id => id.GetCacheKey<T>()), cancellationToken).ConfigureAwait(false)
						: null;
					if (cached != null)
					{
						if (RepositoryMediator.IsDebugEnabled)
							RepositoryMediator.WriteLogs($"FIND: Total {cached.Count} cached object(s) are found [{cached.Select(item => item.Key).ToString(" - ")}]");

						// prepare
						var results = identities.Where(id => !string.IsNullOrWhiteSpace(id)).ToDictionary(id => id, id => default(T));

						// add cached objects
						var ids = new List<string>();
						cached.ForEach(item =>
						{
							var id = item.Value.GetEntityID();
							if (!string.IsNullOrWhiteSpace(id))
							{
								ids.Add(id);
								results[id] = item.Value;
							}
						});

						// find missing objects
						identities = identities.Where(id => !string.IsNullOrWhiteSpace(id)).Except(ids, StringComparer.OrdinalIgnoreCase).ToList();
						if (identities.Count > 0)
						{
							var missing = dataSource.Mode.Equals(RepositoryMode.NoSQL)
								? await context.FindAsync(dataSource, identities, sort, businessEntityID, null, cancellationToken).ConfigureAwait(false)
								: dataSource.Mode.Equals(RepositoryMode.SQL)
									? await context.FindAsync(dataSource, identities, sort, businessEntityID, cancellationToken).ConfigureAwait(false)
									: new List<T>();

							// update results & cache
							missing.Where(@object => @object != null).ForEach(@object => results[@object.GetEntityID()] = @object);
							if (context.EntityDefinition.Cache != null)
							{
								await context.EntityDefinition.Cache.SetAsync(missing, cancellationToken).ConfigureAwait(false);
								if (RepositoryMediator.IsDebugEnabled)
									RepositoryMediator.WriteLogs($"FIND: Add {missing.Count} missing object(s) into cache storage successful [{missing.Select(o => o.GetCacheKey()).ToString(" - ")}]");
							}
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
							? await context.FindAsync(dataSource, filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents, null, cancellationToken).ConfigureAwait(false)
							: dataSource.Mode.Equals(RepositoryMode.SQL)
								? await context.FindAsync(dataSource, filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents, cancellationToken).ConfigureAwait(false)
								: new List<T>()
						: new List<T>();

					if (context.EntityDefinition.Cache != null && objects.Count > 0)
					{
						if (!string.IsNullOrWhiteSpace(cacheKey))
							await context.EntityDefinition.Cache.SetAsync(cacheKey, objects.Select(o => o.GetEntityID()).ToList(), cacheTime < 1 ? context.EntityDefinition.Cache.ExpirationTime / 2 : cacheTime, cancellationToken).ConfigureAwait(false);
						await context.EntityDefinition.Cache.SetAsync(objects, cancellationToken).ConfigureAwait(false);

						if (RepositoryMediator.IsDebugEnabled)
							RepositoryMediator.WriteLogs($"FIND: Add {objects.Count} raw object(s) into cache storage successful [{objects.Select(o => o.GetCacheKey()).ToString(" - ")}]");
					}
				}

				return objects;
			}
			catch (OperationCanceledException ex)
			{
				context.Exception = ex;
				throw ex;
			}
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw ex;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw new RepositoryOperationException($"Error occurred while finding objects [{typeof(T)}]", ex);
			}
		}

		/// <summary>
		/// Finds objects
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
		public static Task<List<T>> FindAsync<T>(RepositoryContext context, string aliasTypeName, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessEntityID = null, bool autoAssociateWithMultipleParents = true, string cacheKey = null, int cacheTime = 0, CancellationToken cancellationToken = default) where T : class
		{
			context.AliasTypeName = aliasTypeName;
			return RepositoryMediator.FindAsync<T>(context, context.GetPrimaryDataSource(), filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents, cacheKey, cacheTime, cancellationToken);
		}

		/// <summary>
		/// Finds objects
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
		public static async Task<List<T>> FindAsync<T>(string aliasTypeName, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessEntityID = null, bool autoAssociateWithMultipleParents = true, string cacheKey = null, int cacheTime = 0, CancellationToken cancellationToken = default) where T : class
		{
			using (var context = new RepositoryContext(false))
			{
				return await RepositoryMediator.FindAsync<T>(context, aliasTypeName, filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents, cacheKey, cacheTime, cancellationToken).ConfigureAwait(false);
			}
		}
		#endregion

		#region Count
		/// <summary>
		/// Counts objects
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
			context.Prepare<T>(RepositoryOperation.Query, (dataSource ?? context.GetPrimaryDataSource())?.StartSession<T>());
			try
			{
				// check cache
				var total = !string.IsNullOrWhiteSpace(cacheKey) && context.EntityDefinition.Cache != null && context.EntityDefinition.Cache.Exists(cacheKey)
					? context.EntityDefinition.Cache.Get<long>(cacheKey)
					: -1;
				if (total > -1)
					return total;

				// count
				dataSource = dataSource ?? context.GetPrimaryDataSource();
				if (dataSource == null)
					throw new InformationInvalidException("Data source is invalid, please check the configuration");

				total = dataSource.Mode.Equals(RepositoryMode.NoSQL)
					? context.Count(dataSource, filter, businessEntityID, autoAssociateWithMultipleParents, null)
					: dataSource.Mode.Equals(RepositoryMode.SQL)
						? context.Count(dataSource, filter, businessEntityID, autoAssociateWithMultipleParents)
						: 0;

				if (RepositoryMediator.IsDebugEnabled)
					RepositoryMediator.WriteLogs(
						$"COUNT: Count objects [{context.EntityDefinition.Type.GetTypeName()}]" + "\r\n" +
						$"- Total: {total}" + "\r\n" +
						$"- Mode: {dataSource.Mode}" + "\r\n" +
						$"- Filter By: {filter?.ToString() ?? "None"}"
					);

				// update cache and return
				if (!string.IsNullOrWhiteSpace(cacheKey) && context.EntityDefinition.Cache != null)
					context.EntityDefinition.Cache.Set(cacheKey, total, cacheTime < 1 ? context.EntityDefinition.Cache.ExpirationTime / 2 : cacheTime);

				return total;
			}
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw ex;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw new RepositoryOperationException($"Error occurred while counting objects [{typeof(T)}]", ex);
			}
		}

		/// <summary>
		/// Counts objects
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
		/// Counts objects
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
		/// Counts objects
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
		public static async Task<long> CountAsync<T>(RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, string businessEntityID = null, bool autoAssociateWithMultipleParents = true, string cacheKey = null, int cacheTime = 0, CancellationToken cancellationToken = default) where T : class
		{
			context.Prepare<T>(RepositoryOperation.Query, (dataSource ?? context.GetPrimaryDataSource())?.StartSession<T>());
			try
			{
				// check cache
				var total = !string.IsNullOrWhiteSpace(cacheKey) && context.EntityDefinition.Cache != null && await context.EntityDefinition.Cache.ExistsAsync(cacheKey).ConfigureAwait(false)
					? await context.EntityDefinition.Cache.GetAsync<long>(cacheKey, cancellationToken).ConfigureAwait(false)
					: -1;
				if (total > -1)
					return total;

				// count
				dataSource = dataSource ?? context.GetPrimaryDataSource();
				if (dataSource == null)
					throw new InformationInvalidException("Data source is invalid, please check the configuration");

				total = dataSource.Mode.Equals(RepositoryMode.NoSQL)
					? await context.CountAsync(dataSource, filter, businessEntityID, autoAssociateWithMultipleParents, null, cancellationToken).ConfigureAwait(false)
					: dataSource.Mode.Equals(RepositoryMode.SQL)
						? await context.CountAsync(dataSource, filter, businessEntityID, autoAssociateWithMultipleParents, cancellationToken).ConfigureAwait(false)
						: 0;

				if (RepositoryMediator.IsDebugEnabled)
					RepositoryMediator.WriteLogs(
						$"COUNT: Count objects [{context.EntityDefinition.Type.GetTypeName()}]" + "\r\n" +
						$"- Total: {total}" + "\r\n" +
						$"- Mode: {dataSource.Mode}" + "\r\n" +
						$"- Filter By: {filter?.ToString() ?? "None"}"
					);

				// update cache and return
				if (!string.IsNullOrWhiteSpace(cacheKey) && context.EntityDefinition.Cache != null)
					await context.EntityDefinition.Cache.SetAsync(cacheKey, total, cacheTime < 1 ? context.EntityDefinition.Cache.ExpirationTime / 2 : cacheTime, cancellationToken).ConfigureAwait(false);

				return total;
			}
			catch (OperationCanceledException ex)
			{
				context.Exception = ex;
				throw ex;
			}
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw ex;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw new RepositoryOperationException($"Error occurred while counting objects [{typeof(T)}]", ex);
			}
		}

		/// <summary>
		/// Counts objects
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
		public static Task<long> CountAsync<T>(RepositoryContext context, string aliasTypeName, IFilterBy<T> filter, string businessEntityID = null, bool autoAssociateWithMultipleParents = true, string cacheKey = null, int cacheTime = 0, CancellationToken cancellationToken = default) where T : class
		{
			context.AliasTypeName = aliasTypeName;
			return RepositoryMediator.CountAsync<T>(context, context.GetPrimaryDataSource(), filter, businessEntityID, autoAssociateWithMultipleParents, cacheKey, cacheTime, cancellationToken);
		}

		/// <summary>
		/// Counts objects
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
		public static async Task<long> CountAsync<T>(string aliasTypeName, IFilterBy<T> filter, string businessEntityID = null, bool autoAssociateWithMultipleParents = true, string cacheKey = null, int cacheTime = 0, CancellationToken cancellationToken = default) where T : class
		{
			using (var context = new RepositoryContext(false))
			{
				return await RepositoryMediator.CountAsync<T>(context, aliasTypeName, filter, businessEntityID, autoAssociateWithMultipleParents, cacheKey, cacheTime, cancellationToken).ConfigureAwait(false);
			}
		}
		#endregion

		#region Search
		/// <summary>
		/// Searchs objects (using full-text search)
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
			context.Prepare<T>(RepositoryOperation.Query, (dataSource ?? context.GetPrimaryDataSource())?.StartSession<T>());
			try
			{
				// prepare
				dataSource = dataSource ?? context.GetPrimaryDataSource();
				if (dataSource == null)
					throw new InformationInvalidException("Data source is invalid, please check the configuration");

				if (RepositoryMediator.IsDebugEnabled)
					RepositoryMediator.WriteLogs(
						$"SEARCH: Search objects [{context.EntityDefinition.Type.GetTypeName()}] - Caching storage: {context.EntityDefinition.Cache?.Name ?? "None"}" + "\r\n" +
						$"- Mode: {dataSource.Mode}" + "\r\n" +
						$"- Page Size: {pageSize}" + "\r\n" +
						$"- Page Number: {pageNumber}" + "\r\n" +
						$"- Query: {(!string.IsNullOrWhiteSpace(query) ? query : "None")}" + "\r\n" +
						$"- Filter By (Additional): {filter?.ToString() ?? "None"}"
					);

				// no caching storage => direct search
				if (context.EntityDefinition.Cache == null)
					return dataSource.Mode.Equals(RepositoryMode.NoSQL)
						? context.Search(dataSource, query, filter, pageSize, pageNumber, businessEntityID, null)
						: dataSource.Mode.Equals(RepositoryMode.SQL)
							? context.Search(dataSource, query, filter, pageSize, pageNumber, businessEntityID)
							: new List<T>();

				// search identities
				var identities = dataSource.Mode.Equals(RepositoryMode.NoSQL)
					? context.SearchIdentities(dataSource, query, filter, pageSize, pageNumber, businessEntityID, null)
					: dataSource.Mode.Equals(RepositoryMode.SQL)
						? context.SearchIdentities(dataSource, query, filter, pageSize, pageNumber, businessEntityID)
						: new List<string>();

				if (RepositoryMediator.IsDebugEnabled)
					RepositoryMediator.WriteLogs($"SEARCH: Total {identities.Count} identities are found [{identities.ToString(" - ")}]");

				// no identity is found, then return empty collection
				if (identities.Count < 1)
					return new List<T>();

				// get cached objects
				var cached = context.EntityDefinition.Cache.Get(identities.Select(id => id.GetCacheKey<T>()));
				if (cached != null && cached.Count > 0)
				{
					if (RepositoryMediator.IsDebugEnabled)
						RepositoryMediator.WriteLogs($"SEARCH: Total {cached.Count} cached object(s) are found [{cached.Select(item => item.Key).ToString(" - ")}]");

					// prepare
					var results = identities.Where(id => !string.IsNullOrWhiteSpace(id)).ToDictionary(id => id, id => default(T));

					// add cached objects
					var ids = new List<string>();
					cached.Where(item => item.Value != null).ForEach(item =>
					{
						var id = item.Value.GetEntityID();
						if (!string.IsNullOrWhiteSpace(id))
						{
							ids.Add(id);
							results[id] = item.Value as T;
						}
					});

					// find missing objects
					identities = identities.Where(id => !string.IsNullOrWhiteSpace(id)).Except(ids, StringComparer.OrdinalIgnoreCase).ToList();
					if (identities.Count > 0)
					{
						var missing = dataSource.Mode.Equals(RepositoryMode.NoSQL)
							? context.Find<T>(dataSource, identities, null, businessEntityID, null)
							: dataSource.Mode.Equals(RepositoryMode.SQL)
								? context.Find<T>(dataSource, identities, null, businessEntityID)
								: new List<T>();

						// update results & cache
						missing.Where(@object => @object != null).ForEach(@object => results[@object.GetEntityID()] = @object);
						context.EntityDefinition.Cache.Set(missing);
						if (RepositoryMediator.IsDebugEnabled)
							RepositoryMediator.WriteLogs($"SEARCH: Add {missing.Count} missing object(s) into cache storage successful [{missing.Select(o => o.GetCacheKey()).ToString(" - ")}]");
					}

					// return the collection of objects
					return results.Where(kvp => kvp.Value != null).Select(kvp => kvp.Value as T).ToList();
				}
				else if (RepositoryMediator.IsDebugEnabled)
					RepositoryMediator.WriteLogs($"SEARCH: No cached object is found => search raw objects");

				// search raw objects if has no cache
				var objects = dataSource.Mode.Equals(RepositoryMode.NoSQL)
					? context.Search(dataSource, query, filter, pageSize, pageNumber, businessEntityID, null)
					: dataSource.Mode.Equals(RepositoryMode.SQL)
						? context.Search(dataSource, query, filter, pageSize, pageNumber, businessEntityID)
						: new List<T>();

				if (objects.Count > 0)
				{
					context.EntityDefinition.Cache.Set(objects);
					if (RepositoryMediator.IsDebugEnabled)
						RepositoryMediator.WriteLogs($"SEARCH: Add {objects.Count} raw object(s) into cache storage successful [{objects.Select(o => o.GetCacheKey()).ToString(" - ")}]");
				}
				else if (RepositoryMediator.IsDebugEnabled)
					RepositoryMediator.WriteLogs($"SEARCH: No matched object is found");

				return objects;
			}
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw ex;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw new RepositoryOperationException($"Error occurred while searching objects [{typeof(T)}]", ex);
			}
		}

		/// <summary>
		/// Searchs objects (using full-text search)
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
		/// Searchs objects (using full-text search)
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
		/// Searchs objects (using full-text search)
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
		public static async Task<List<T>> SearchAsync<T>(RepositoryContext context, DataSource dataSource, string query, IFilterBy<T> filter, int pageSize, int pageNumber, string businessEntityID = null, CancellationToken cancellationToken = default) where T : class
		{
			context.Prepare<T>(RepositoryOperation.Query, (dataSource ?? context.GetPrimaryDataSource())?.StartSession<T>());
			try
			{
				// prepare
				dataSource = dataSource ?? context.GetPrimaryDataSource();
				if (dataSource == null)
					throw new InformationInvalidException("Data source is invalid, please check the configuration");

				if (RepositoryMediator.IsDebugEnabled)
					RepositoryMediator.WriteLogs(
						$"SEARCH: Search objects [{context.EntityDefinition.Type.GetTypeName()}] - Caching storage: {context.EntityDefinition.Cache?.Name ?? "None"}" + "\r\n" +
						$"- Mode: {dataSource.Mode}" + "\r\n" +
						$"- Page Size: {pageSize}" + "\r\n" +
						$"- Page Number: {pageNumber}" + "\r\n" +
						$"- Query: {(!string.IsNullOrWhiteSpace(query) ? query : "None")}" + "\r\n" +
						$"- Filter By (Additional): {filter?.ToString() ?? "None"}"
					);

				// no caching storage => direct search
				if (context.EntityDefinition.Cache == null)
					return dataSource.Mode.Equals(RepositoryMode.NoSQL)
						? await context.SearchAsync(dataSource, query, filter, pageSize, pageNumber, businessEntityID, null, cancellationToken).ConfigureAwait(false)
						: dataSource.Mode.Equals(RepositoryMode.SQL)
							? await context.SearchAsync(dataSource, query, filter, pageSize, pageNumber, businessEntityID, cancellationToken).ConfigureAwait(false)
							: new List<T>();

				// search identities
				var identities = dataSource.Mode.Equals(RepositoryMode.NoSQL)
					? await context.SearchIdentitiesAsync(dataSource, query, filter, pageSize, pageNumber, businessEntityID, null, cancellationToken).ConfigureAwait(false)
					: dataSource.Mode.Equals(RepositoryMode.SQL)
						? await context.SearchIdentitiesAsync(dataSource, query, filter, pageSize, pageNumber, businessEntityID, cancellationToken).ConfigureAwait(false)
						: new List<string>();

				if (RepositoryMediator.IsDebugEnabled)
					RepositoryMediator.WriteLogs($"SEARCH: Total {identities.Count} identities are found [{identities.ToString(" - ")}]");

				// no identity is found, then return empty collection
				if (identities.Count < 1)
					return new List<T>();

				// get cached objects
				var cached = await context.EntityDefinition.Cache.GetAsync(identities.Select(id => id.GetCacheKey<T>()), cancellationToken).ConfigureAwait(false);
				if (cached != null && cached.Count > 0)
				{
					if (RepositoryMediator.IsDebugEnabled)
						RepositoryMediator.WriteLogs($"SEARCH: Total {cached.Count} cached object(s) are found [{cached.Select(item => item.Key).ToString(" - ")}]");

					// prepare
					var results = identities.Where(id => !string.IsNullOrWhiteSpace(id)).ToDictionary(id => id, id => default(T));

					// add cached objects
					var ids = new List<string>();
					cached.Where(item => item.Value != null).ForEach(item =>
					{
						var id = item.Value.GetEntityID();
						if (!string.IsNullOrWhiteSpace(id))
						{
							ids.Add(id);
							results[id] = item.Value as T;
						}
					});

					// find missing objects
					identities = identities.Where(id => !string.IsNullOrWhiteSpace(id)).Except(ids, StringComparer.OrdinalIgnoreCase).ToList();
					if (identities.Count > 0)
					{
						var missing = dataSource.Mode.Equals(RepositoryMode.NoSQL)
							? await context.FindAsync<T>(dataSource, identities, null, businessEntityID, null, cancellationToken).ConfigureAwait(false)
							: dataSource.Mode.Equals(RepositoryMode.SQL)
								? await context.FindAsync<T>(dataSource, identities, null, businessEntityID, cancellationToken).ConfigureAwait(false)
								: new List<T>();

						// update results & cache
						missing.Where(@object => @object != null).ForEach(@object => results[@object.GetEntityID()] = @object);
						await context.EntityDefinition.Cache.SetAsync(missing, cancellationToken).ConfigureAwait(false);
						if (RepositoryMediator.IsDebugEnabled)
							RepositoryMediator.WriteLogs($"SEARCH: Add {missing.Count} missing object(s) into cache storage successful [{missing.Select(o => o.GetCacheKey()).ToString(" - ")}]");
					}

					// return the collection of objects
					return results.Where(kvp => kvp.Value != null).Select(kvp => kvp.Value as T).ToList();
				}
				else if (RepositoryMediator.IsDebugEnabled)
					RepositoryMediator.WriteLogs($"SEARCH: No cached object is found => search raw objects");

				// search raw objects if has no cache
				var objects = dataSource.Mode.Equals(RepositoryMode.NoSQL)
					? await context.SearchAsync(dataSource, query, filter, pageSize, pageNumber, businessEntityID, null, cancellationToken).ConfigureAwait(false)
					: dataSource.Mode.Equals(RepositoryMode.SQL)
						? await context.SearchAsync(dataSource, query, filter, pageSize, pageNumber, businessEntityID, cancellationToken).ConfigureAwait(false)
						: new List<T>();

				if (objects.Count > 0)
				{
					await context.EntityDefinition.Cache.SetAsync(objects, cancellationToken).ConfigureAwait(false);
					if (RepositoryMediator.IsDebugEnabled)
						RepositoryMediator.WriteLogs($"SEARCH: Add {objects.Count} raw object(s) into cache storage successful [{objects.Select(o => o.GetCacheKey()).ToString(" - ")}]");
				}
				else if (RepositoryMediator.IsDebugEnabled)
					RepositoryMediator.WriteLogs($"SEARCH: No matched object is found");

				return objects;
			}
			catch (OperationCanceledException ex)
			{
				context.Exception = ex;
				throw ex;
			}
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw ex;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw new RepositoryOperationException($"Error occurred while searching objects [{typeof(T)}]", ex);
			}
		}

		/// <summary>
		/// Searchs objects (using full-text search)
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
		public static async Task<List<T>> SearchAsync<T>(RepositoryContext context, string aliasTypeName, string query, IFilterBy<T> filter, int pageSize, int pageNumber, string businessEntityID = null, CancellationToken cancellationToken = default) where T : class
		{
			context.AliasTypeName = aliasTypeName;
			return await RepositoryMediator.SearchAsync<T>(context, context.GetPrimaryDataSource(), query, filter, pageSize, pageNumber, businessEntityID, cancellationToken).ConfigureAwait(false);
		}

		/// <summary>
		/// Searchs objects (using full-text search)
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
		public static async Task<List<T>> SearchAsync<T>(string aliasTypeName, string query, IFilterBy<T> filter, int pageSize, int pageNumber, string businessEntityID = null, CancellationToken cancellationToken = default) where T : class
		{
			using (var context = new RepositoryContext(false))
			{
				return await RepositoryMediator.SearchAsync<T>(context, aliasTypeName, query, filter, pageSize, pageNumber, businessEntityID, cancellationToken).ConfigureAwait(false);
			}
		}
		#endregion

		#region Count (searching)
		/// <summary>
		/// Counts objects (using full-text search)
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
			context.Prepare<T>(RepositoryOperation.Query, (dataSource ?? context.GetPrimaryDataSource())?.StartSession<T>());
			try
			{
				// prepare
				dataSource = dataSource ?? context.GetPrimaryDataSource();
				if (dataSource == null)
					throw new InformationInvalidException("Data source is invalid, please check the configuration");

				// count
				var total = dataSource.Mode.Equals(RepositoryMode.NoSQL)
					? context.Count(dataSource, query, filter, businessEntityID, null)
					: dataSource.Mode.Equals(RepositoryMode.SQL)
						? context.Count(dataSource, query, filter, businessEntityID)
						: 0;
				if (RepositoryMediator.IsDebugEnabled)
					RepositoryMediator.WriteLogs(
						$"COUNT: Count objects [{context.EntityDefinition.Type.GetTypeName()}]" + "\r\n" +
						$"- Total: {total}" + "\r\n" +
						$"- Mode: {dataSource.Mode}" + "\r\n" +
						$"- Query: {(!string.IsNullOrWhiteSpace(query) ? query : "None")}" + "\r\n" +
						$"- Filter By (Additional): {filter?.ToString() ?? "None"}"
					);

				return total;
			}
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw ex;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw new RepositoryOperationException($"Error occurred while counting objects by query [{typeof(T)}]", ex);
			}
		}

		/// <summary>
		/// Counts objects (using full-text search)
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
		/// Counts objects (using full-text search)
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
		/// Counts objects (using full-text search)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="query">The searching query (like Google searching query)</param>
		/// <param name="filter">The object that presents other filter expression to combine with seaching query</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The integer number that presents total of objects that matched with searching query (and filter expression)</returns>
		public static async Task<long> CountAsync<T>(RepositoryContext context, DataSource dataSource, string query, IFilterBy<T> filter, string businessEntityID = null, CancellationToken cancellationToken = default) where T : class
		{
			context.Prepare<T>(RepositoryOperation.Query, (dataSource ?? context.GetPrimaryDataSource())?.StartSession<T>());
			try
			{
				// prepare
				dataSource = dataSource ?? context.GetPrimaryDataSource();
				if (dataSource == null)
					throw new InformationInvalidException("Data source is invalid, please check the configuration");

				// count
				var total = dataSource.Mode.Equals(RepositoryMode.NoSQL)
					? await context.CountAsync(dataSource, query, filter, businessEntityID, null, cancellationToken).ConfigureAwait(false)
					: dataSource.Mode.Equals(RepositoryMode.SQL)
						? await context.CountAsync(dataSource, query, filter, businessEntityID, cancellationToken).ConfigureAwait(false)
						: 0;

				if (RepositoryMediator.IsDebugEnabled)
					RepositoryMediator.WriteLogs(
						$"COUNT: Count objects [{context.EntityDefinition.Type.GetTypeName()}]" + "\r\n" +
						$"- Total: {total}" + "\r\n" +
						$"- Mode: {dataSource.Mode}" + "\r\n" +
						$"- Query: {(!string.IsNullOrWhiteSpace(query) ? query : "None")}" + "\r\n" +
						$"- Filter By (Additional): {filter?.ToString() ?? "None"}"
					);

				return total;
			}
			catch (OperationCanceledException ex)
			{
				context.Exception = ex;
				throw ex;
			}
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw ex;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw new RepositoryOperationException($"Error occurred while counting objects by query [{typeof(T)}]", ex);
			}
		}

		/// <summary>
		/// Counts objects (using full-text search)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="query">The searching query (like Google searching query)</param>
		/// <param name="filter">The object that presents other filter expression to combine with seaching query</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The integer number that presents total of objects that matched with searching query (and filter expression)</returns>
		public static Task<long> CountAsync<T>(RepositoryContext context, string aliasTypeName, string query, IFilterBy<T> filter, string businessEntityID = null, CancellationToken cancellationToken = default) where T : class
		{
			context.AliasTypeName = aliasTypeName;
			return RepositoryMediator.CountAsync<T>(context, context.GetPrimaryDataSource(), query, filter, businessEntityID, cancellationToken);
		}

		/// <summary>
		/// Counts objects (using full-text search)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="query">The searching query (like Google searching query)</param>
		/// <param name="filter">The object that presents other filter expression to combine with seaching query</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The integer number that presents total of objects that matched with searching query (and filter expression)</returns>
		public static async Task<long> CountAsync<T>(string aliasTypeName, string query, IFilterBy<T> filter, string businessEntityID = null, CancellationToken cancellationToken = default) where T : class
		{
			using (var context = new RepositoryContext(false))
			{
				return await RepositoryMediator.CountAsync<T>(context, aliasTypeName, query, filter, businessEntityID, cancellationToken).ConfigureAwait(false);
			}
		}
		#endregion

		#region Create version
		/// <summary>
		/// Creates a new version of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="object">The object to create new instance in repository</param>
		/// <param name="userID">The identity of user who created this verion of the object</param>
		public static VersionContent CreateVersion<T>(RepositoryContext context, DataSource dataSource, T @object, string userID = null) where T : class
		{
			context.Prepare<T>(RepositoryOperation.Create, (dataSource ?? context.GetVersionDataSource())?.StartSession<T>());
			try
			{
				// check
				if (@object == null)
					throw new ArgumentNullException(nameof(@object), "The object is null");

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
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw ex;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw new RepositoryOperationException("Error occurred while creating new version of an object", ex);
			}
		}

		/// <summary>
		/// Creates a new version of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="object">The object to create new instance in repository</param>
		/// <param name="userID">The identity of user who created this verion of the object</param>
		public static VersionContent CreateVersion<T>(RepositoryContext context, T @object, string userID = null) where T : class
			=> RepositoryMediator.CreateVersion<T>(context, null, @object, userID);

		/// <summary>
		/// Creates a new version of an object
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
		/// Creates a new version of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="object">The object to create new instance in repository</param>
		/// <param name="userID">The identity of user who created this verion of the object</param>
		/// <param name="cancellationToken">The cancellation token</param>
		public static async Task<VersionContent> CreateVersionAsync<T>(RepositoryContext context, DataSource dataSource, T @object, string userID = null, CancellationToken cancellationToken = default) where T : class
		{
			context.Prepare<T>(RepositoryOperation.Create, (dataSource ?? context.GetVersionDataSource())?.StartSession<T>());
			try
			{
				// check
				if (@object == null)
					throw new ArgumentNullException(nameof(@object), "The object is null");

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
			catch (OperationCanceledException ex)
			{
				context.Exception = ex;
				throw ex;
			}
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw ex;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw new RepositoryOperationException("Error occurred while creating new version of an object", ex);
			}
		}

		/// <summary>
		/// Creates a new version of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="object">The object to create new instance in repository</param>
		/// <param name="userID">The identity of user who created this verion of the object</param>
		/// <param name="cancellationToken">The cancellation token</param>
		public static Task<VersionContent> CreateVersionAsync<T>(RepositoryContext context, T @object, string userID = null, CancellationToken cancellationToken = default) where T : class
			=> RepositoryMediator.CreateVersionAsync<T>(context, null, @object, userID, cancellationToken);

		/// <summary>
		/// Creates a new version of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="object">The object to create new instance in repository</param>
		/// <param name="userID">The identity of user who created this verion of the object</param>
		/// <param name="cancellationToken">The cancellation token</param>
		public static async Task<VersionContent> CreateVersionAsync<T>(T @object, string userID = null, CancellationToken cancellationToken = default) where T : class
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
				throw new InformationInvalidException($"Original object of the version content is not matched with type [{typeof(T)}]");

			// process
			context.Prepare<T>(RepositoryOperation.Update, context.GetPrimaryDataSource()?.StartSession<T>());
			try
			{
				// get current object
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
					context.Replace(dataSource, version.Object as T, null);
				else if (dataSource.Mode.Equals(RepositoryMode.SQL))
					context.Replace(dataSource, version.Object as T);

				// update into cache storage
				if (context.EntityDefinition.Cache != null)
					if (context.EntityDefinition.Cache.Set(version.Object as T))
						RepositoryMediator.WriteLogs($"ROLLBACK: Add the object into the cache storage successful [{(version.Object as T).GetCacheKey(false)}]");

				// call post-handlers
				context.CallPostUpdateHandlers(version.Object as T, changed, true);

				// update other data sources
				Task.Run(() => RepositoryMediator.SyncAsync(version.Object as T, context.AliasTypeName)).ConfigureAwait(false);

				// return the original object
				return version.Object as T;
			}
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw ex;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
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
		public static async Task<T> RollbackAsync<T>(RepositoryContext context, VersionContent version, string userID, CancellationToken cancellationToken = default) where T : class
		{
			// prepare
			if (version == null)
				throw new ArgumentNullException(nameof(version), "Version content is invalid");
			else if (!(version.Object is T))
				throw new InformationInvalidException($"Original object of the version content is not matched with type [{typeof(T)}]");

			// process
			context.Prepare<T>(RepositoryOperation.Update, context.GetPrimaryDataSource()?.StartSession<T>());
			try
			{
				// get current object
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
					await context.ReplaceAsync(dataSource, version.Object as T, null, cancellationToken).ConfigureAwait(false);
				else if (dataSource.Mode.Equals(RepositoryMode.SQL))
					await context.ReplaceAsync(dataSource, version.Object as T, cancellationToken).ConfigureAwait(false);

				// update into cache storage
				if (context.EntityDefinition.Cache != null)
					if (await context.EntityDefinition.Cache.SetAsync(version.Object as T, 0, cancellationToken).ConfigureAwait(false) && RepositoryMediator.IsDebugEnabled)
						RepositoryMediator.WriteLogs($"ROLLBACK: Add the object into the cache storage successful [{(version.Object as T).GetCacheKey(false)}]");

				// call post-handlers
				await context.CallPostUpdateHandlersAsync(version.Object as T, changed, true, cancellationToken).ConfigureAwait(false);

				// update other data sources
				var sync = Task.Run(() => RepositoryMediator.SyncAsync(version.Object as T, context.AliasTypeName, true, false, cancellationToken)).ConfigureAwait(false);

				// return the original object
				return version.Object as T;
			}
			catch (OperationCanceledException ex)
			{
				context.Exception = ex;
				throw ex;
			}
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw ex;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
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
		public static async Task<T> RollbackAsync<T>(VersionContent version, string userID, CancellationToken cancellationToken = default) where T : class
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
		public static async Task<T> RollbackAsync<T>(RepositoryContext context, string versionID, string userID, CancellationToken cancellationToken = default) where T : class
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
		public static async Task<T> RollbackAsync<T>(string versionID, string userID, CancellationToken cancellationToken = default) where T : class
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
				return VersionContent.Count(dataSource, "Versions", filter);
			}
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw ex;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
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
			=> RepositoryMediator.CountVersionContents<T>(context, null, objectID, serviceName, systemID, repositoryID, entityID, userID);

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
		public static async Task<long> CountVersionContentsAsync<T>(RepositoryContext context, DataSource dataSource, string objectID, string serviceName = null, string systemID = null, string repositoryID = null, string entityID = null, string userID = null, CancellationToken cancellationToken = default) where T : class
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
				return await VersionContent.CountAsync(dataSource, "Versions", filter, cancellationToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException ex)
			{
				context.Exception = ex;
				throw ex;
			}
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw ex;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
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
		public static Task<long> CountVersionContentsAsync<T>(RepositoryContext context, string objectID, string serviceName = null, string systemID = null, string repositoryID = null, string entityID = null, string userID = null, CancellationToken cancellationToken = default) where T : class
			=> RepositoryMediator.CountVersionContentsAsync<T>(context, null, objectID, serviceName, systemID, repositoryID, entityID, userID, cancellationToken);

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
		public static async Task<long> CountVersionContentsAsync<T>(string objectID, string serviceName = null, string systemID = null, string repositoryID = null, string entityID = null, string userID = null, CancellationToken cancellationToken = default) where T : class
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
		public static Task<long> CountVersionContentsAsync<T>(RepositoryContext context, DataSource dataSource, string objectID, CancellationToken cancellationToken = default) where T : class
			=> RepositoryMediator.CountVersionContentsAsync<T>(context, dataSource, objectID, null, null, null, null, null, cancellationToken);

		/// <summary>
		/// Counts the number of version contents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="objectID">The identity of object that associates with</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<long> CountVersionContentsAsync<T>(RepositoryContext context, string objectID, CancellationToken cancellationToken = default) where T : class
			=> RepositoryMediator.CountVersionContentsAsync<T>(context, null, objectID, cancellationToken);

		/// <summary>
		/// Counts the number of version contents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="objectID">The identity of object that associates with</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<long> CountVersionContentsAsync<T>(string objectID, CancellationToken cancellationToken = default) where T : class
		{
			using (var context = new RepositoryContext(false))
			{
				return await RepositoryMediator.CountVersionContentsAsync<T>(context, objectID, cancellationToken).ConfigureAwait(false);
			}
		}
		#endregion

		#region Find version contents
		/// <summary>
		/// Finds version contents of an object
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
				return VersionContent.Find(dataSource, "Versions", filter, sort, pageSize, pageNumber);
			}
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw ex;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw new RepositoryOperationException("Error occurred while fetching version contents", ex);
			}
		}

		/// <summary>
		/// Finds version contents of an object
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
			=> RepositoryMediator.FindVersionContents<T>(context, null, objectID, serviceName, systemID, repositoryID, entityID, userID, pageSize, pageNumber);

		/// <summary>
		/// Finds version contents of an object
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
		/// Finds version contents of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="objectID">The identity of object that associates with</param>
		/// <returns></returns>
		public static List<VersionContent> FindVersionContents<T>(RepositoryContext context, DataSource dataSource, string objectID) where T : class
			=> string.IsNullOrWhiteSpace(objectID) || !objectID.IsValidUUID()
				? throw new ArgumentNullException(nameof(objectID), "Object identity is invalid")
				: RepositoryMediator.FindVersionContents<T>(context, dataSource, objectID, null, null, null, null, null, 0, 1);

		/// <summary>
		/// Finds version contents of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="objectID">The identity of object that associates with</param>
		/// <returns></returns>
		public static List<VersionContent> FindVersionContents<T>(RepositoryContext context, string objectID) where T : class
			=> RepositoryMediator.FindVersionContents<T>(context, null, objectID);

		/// <summary>
		/// Finds version contents of an object
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
		/// Finds version contents of an object
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
		public static async Task<List<VersionContent>> FindVersionContentsAsync<T>(RepositoryContext context, DataSource dataSource, string objectID, string serviceName = null, string systemID = null, string repositoryID = null, string entityID = null, string userID = null, int pageSize = 0, int pageNumber = 1, CancellationToken cancellationToken = default) where T : class
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
			catch (OperationCanceledException ex)
			{
				context.Exception = ex;
				throw ex;
			}
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw ex;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw new RepositoryOperationException("Error occurred while fetching version contents", ex);
			}
		}

		/// <summary>
		/// Finds version contents of an object
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
		public static Task<List<VersionContent>> FindVersionContentsAsync<T>(RepositoryContext context, string objectID, string serviceName = null, string systemID = null, string repositoryID = null, string entityID = null, string userID = null, int pageSize = 0, int pageNumber = 1, CancellationToken cancellationToken = default) where T : class
			=> RepositoryMediator.FindVersionContentsAsync<T>(context, null, objectID, serviceName, systemID, repositoryID, entityID, userID, pageSize, pageNumber, cancellationToken);

		/// <summary>
		/// Finds version contents of an object
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
		public static async Task<List<VersionContent>> FindVersionContentsAsync<T>(string objectID, string serviceName = null, string systemID = null, string repositoryID = null, string entityID = null, string userID = null, int pageSize = 0, int pageNumber = 1, CancellationToken cancellationToken = default) where T : class
		{
			using (var context = new RepositoryContext(false))
			{
				return await RepositoryMediator.FindVersionContentsAsync<T>(context, objectID, serviceName, systemID, repositoryID, entityID, userID, pageSize, pageNumber, cancellationToken);
			}
		}

		/// <summary>
		/// Finds version contents of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="objectID">The identity of object that associates with</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<List<VersionContent>> FindVersionContentsAsync<T>(RepositoryContext context, DataSource dataSource, string objectID, CancellationToken cancellationToken = default) where T : class
			=> string.IsNullOrWhiteSpace(objectID) || !objectID.IsValidUUID()
				? Task.FromException<List<VersionContent>>(new ArgumentNullException(nameof(objectID), "Object identity is invalid"))
				: RepositoryMediator.FindVersionContentsAsync<T>(context, dataSource, objectID, null, null, null, null, null, 0, 1, cancellationToken);

		/// <summary>
		/// Finds version contents of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="objectID">The identity of object that associates with</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<List<VersionContent>> FindVersionContentsAsync<T>(RepositoryContext context, string objectID, CancellationToken cancellationToken = default) where T : class
			=> RepositoryMediator.FindVersionContentsAsync<T>(context, null, objectID, cancellationToken);

		/// <summary>
		/// Finds version contents of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="objectID">The identity of object that associates with</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<List<VersionContent>> FindVersionContentsAsync<T>(string objectID, CancellationToken cancellationToken = default) where T : class
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
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw ex;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
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
			=> RepositoryMediator.CleanVersionContents(!string.IsNullOrWhiteSpace(dataSource) && RepositoryMediator.DataSources.ContainsKey(dataSource) ? RepositoryMediator.DataSources[dataSource] : null, days);

		/// <summary>
		/// Cleans old version contents
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="days">The remain days</param>
		public static async Task CleanVersionContentsAsync(RepositoryContext context, DataSource dataSource, int days = 30, CancellationToken cancellationToken = default)
		{
			try
			{
				context.Operation = RepositoryOperation.Delete;
				await VersionContent.DeleteAsync(dataSource ?? context.GetVersionDataSource(), "Versions", Filters<VersionContent>.LessThanOrEquals("Created", DateTime.Now.AddDays(0 - (days > 0 ? days : 30))), cancellationToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException ex)
			{
				context.Exception = ex;
				throw ex;
			}
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw ex;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw new RepositoryOperationException("Error occurred while cleaning old version contents", ex);
			}
		}

		/// <summary>
		/// Cleans old version contents
		/// </summary>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="days">The remain days</param>
		public static async Task CleanVersionContentsAsync(DataSource dataSource, int days = 30, CancellationToken cancellationToken = default)
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
		public static Task CleanVersionContentsAsync(string dataSource, int days = 30, CancellationToken cancellationToken = default)
			=> RepositoryMediator.CleanVersionContentsAsync(!string.IsNullOrWhiteSpace(dataSource) && RepositoryMediator.DataSources.ContainsKey(dataSource) ? RepositoryMediator.DataSources[dataSource] : null, days, cancellationToken);
		#endregion

		#region Create trash content
		/// <summary>
		/// Creates a new trash content of an object
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
				var trash = TrashContent.Prepare(@object, content => content.CreatedID = userID ?? "");
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
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw ex;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw new RepositoryOperationException("Error occurred while creating new trash content of an object", ex);
			}
		}

		/// <summary>
		/// Creates a new trash content of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="object">The object to create new instance in repository</param>
		/// <param name="userID">The identity of user who created this trash content of the object (means who deletes the object)</param>
		public static TrashContent CreateTrashContent<T>(RepositoryContext context, T @object, string userID = null) where T : class
			=> RepositoryMediator.CreateTrashContent(context, null, @object, userID);

		/// <summary>
		/// Creates a new trash content of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="object">The object to create new instance in repository</param>
		/// <param name="userID">The identity of user who created this trash content of the object (means who deletes the object)</param>
		public static TrashContent CreateTrashContent<T>(T @object, string userID = null) where T : class
		{
			using (var context = new RepositoryContext())
			{
				return RepositoryMediator.CreateTrashContent(context, @object, userID);
			}
		}

		/// <summary>
		/// Creates a new trash content of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="object">The object to create new instance in repository</param>
		/// <param name="userID">The identity of user who created this trash content of the object (means who deletes the object)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		public static async Task<TrashContent> CreateTrashContentAsync<T>(RepositoryContext context, DataSource dataSource, T @object, string userID = null, CancellationToken cancellationToken = default) where T : class
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
			catch (OperationCanceledException ex)
			{
				context.Exception = ex;
				throw ex;
			}
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw ex;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw new RepositoryOperationException("Error occurred while creating new trash content of an object", ex);
			}
		}

		/// <summary>
		/// Creates a new trash content of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="object">The object to create new instance in repository</param>
		/// <param name="userID">The identity of user who created this trash content of the object (means who deletes the object)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		public static Task<TrashContent> CreateTrashContentAsync<T>(RepositoryContext context, T @object, string userID = null, CancellationToken cancellationToken = default) where T : class
			=> RepositoryMediator.CreateTrashContentAsync(context, null, @object, userID, cancellationToken);

		/// <summary>
		/// Creates a new trash content of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="object">The object to create new instance in repository</param>
		/// <param name="userID">The identity of user who created this trash content of the object (means who deletes the object)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		public static async Task<TrashContent> CreateTrashContentAsync<T>(T @object, string userID = null, CancellationToken cancellationToken = default) where T : class
		{
			using (var context = new RepositoryContext())
			{
				return await RepositoryMediator.CreateTrashContentAsync(context, @object, userID, cancellationToken).ConfigureAwait(false);
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
				throw new InformationInvalidException($"Original object of the trash content is not matched with type [{typeof(T)}]");

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
					context.Create(dataSource, trashContent.Object as T, null);
				else if (dataSource.Mode.Equals(RepositoryMode.SQL))
					context.Create(dataSource, trashContent.Object as T);

				// update into cache storage
				if (context.EntityDefinition.Cache != null)
					if (context.EntityDefinition.Cache.Set(trashContent.Object as T) && RepositoryMediator.IsDebugEnabled)
						RepositoryMediator.WriteLogs($"RESTORE: Add the object into the cache storage successful [{(trashContent.Object as T).GetCacheKey(false)}]");

				// call post-handlers
				context.CallPostCreateHandlers(trashContent.Object as T, true);

				// delete trash content
				TrashContent.Delete(RepositoryMediator.GetTrashDataSource(context), "Trashs", Filters<TrashContent>.Equals("ID", trashContent.ID));

				// update other data sources
				Task.Run(() => RepositoryMediator.SyncAsync(trashContent.Object as T, context.AliasTypeName, false)).ConfigureAwait(false);

				// return the original object
				return trashContent.Object as T;
			}
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw ex;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
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
		public static async Task<T> RestoreAsync<T>(RepositoryContext context, TrashContent trashContent, string userID, CancellationToken cancellationToken = default) where T : class
		{
			// prepare
			if (trashContent == null)
				throw new ArgumentNullException(nameof(trashContent), "Trash content is invalid");
			else if (!(trashContent.Object is T))
				throw new InformationInvalidException($"Original object of the trash content is not matched with type [{typeof(T)}]");

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
					await context.CreateAsync(dataSource, trashContent.Object as T, null, cancellationToken).ConfigureAwait(false);
				else if (dataSource.Mode.Equals(RepositoryMode.SQL))
					await context.CreateAsync(dataSource, trashContent.Object as T, cancellationToken).ConfigureAwait(false);

				// update into cache storage
				if (context.EntityDefinition.Cache != null)
					if (await context.EntityDefinition.Cache.SetAsync(trashContent.Object as T, 0, cancellationToken).ConfigureAwait(false) && RepositoryMediator.IsDebugEnabled)
						RepositoryMediator.WriteLogs($"RESTORE: Add the object into the cache storage successful [{(trashContent.Object as T).GetCacheKey(false)}]");

				// call post-handlers
				await context.CallPostCreateHandlersAsync(trashContent.Object as T, true, cancellationToken).ConfigureAwait(false);

				// delete trash content
				await TrashContent.DeleteAsync(RepositoryMediator.GetTrashDataSource(context), "Trashs", Filters<TrashContent>.Equals("ID", trashContent.ID), cancellationToken).ConfigureAwait(false);

				// update other data sources
				var sync = Task.Run(() => RepositoryMediator.SyncAsync(trashContent.Object as T, context.AliasTypeName, false, false, cancellationToken)).ConfigureAwait(false);

				// return the original object
				return trashContent.Object as T;
			}
			catch (OperationCanceledException ex)
			{
				context.Exception = ex;
				throw ex;
			}
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw ex;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
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
		public static async Task<T> RestoreAsync<T>(TrashContent trashContent, string userID, CancellationToken cancellationToken = default) where T : class
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
		public static async Task<T> RestoreAsync<T>(RepositoryContext context, string trashContentID, string userID, CancellationToken cancellationToken = default) where T : class
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
		public static async Task<T> RestoreAsync<T>(string trashContentID, string userID, CancellationToken cancellationToken = default) where T : class
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
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw ex;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
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
			=> RepositoryMediator.CountTrashContents<T>(context, null, serviceName, systemID, repositoryID, entityID, userID);

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
		public static async Task<long> CountTrashContentsAsync<T>(RepositoryContext context, DataSource dataSource, string serviceName = null, string systemID = null, string repositoryID = null, string entityID = null, string userID = null, CancellationToken cancellationToken = default) where T : class
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
			catch (OperationCanceledException ex)
			{
				context.Exception = ex;
				throw ex;
			}
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw ex;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
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
		public static Task<long> CountTrashContentsAsync<T>(RepositoryContext context, string serviceName = null, string systemID = null, string repositoryID = null, string entityID = null, string userID = null, CancellationToken cancellationToken = default) where T : class
			=> RepositoryMediator.CountTrashContentsAsync<T>(context, null, serviceName, systemID, repositoryID, entityID, userID, cancellationToken);

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
		public static async Task<long> CountTrashContentsAsync<T>(string serviceName = null, string systemID = null, string repositoryID = null, string entityID = null, string userID = null, CancellationToken cancellationToken = default) where T : class
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
		public static Task<long> CountTrashContentsAsync<T>(RepositoryContext context, DataSource dataSource, string serviceName, string systemID, CancellationToken cancellationToken = default) where T : class
			=> RepositoryMediator.CountTrashContentsAsync<T>(context, dataSource, serviceName, systemID, null, null, null, cancellationToken);

		/// <summary>
		/// Counts the number of version contents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="serviceName">The name of service that associates with</param>
		/// <param name="systemID">The identity of system that associates with</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<long> CountTrashContentsAsync<T>(RepositoryContext context, string serviceName, string systemID, CancellationToken cancellationToken = default) where T : class
			=> RepositoryMediator.CountTrashContentsAsync<T>(context, null, serviceName, systemID, cancellationToken);

		/// <summary>
		/// Counts the number of version contents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="serviceName">The name of service that associates with</param>
		/// <param name="systemID">The identity of system that associates with</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<long> CountTrashContentsAsync<T>(string serviceName, string systemID, CancellationToken cancellationToken = default) where T : class
		{
			using (var context = new RepositoryContext(false))
			{
				return await RepositoryMediator.CountTrashContentsAsync<T>(context, serviceName, systemID, cancellationToken).ConfigureAwait(false);
			}
		}
		#endregion

		#region Find trash contents
		/// <summary>
		/// Finds trash contents
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
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw ex;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw new RepositoryOperationException("Error occurred while fetching trash contents", ex);
			}
		}

		/// <summary>
		/// Finds trash contents
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
			=> RepositoryMediator.FindTrashContents<T>(context, null, serviceName, systemID, repositoryID, entityID, userID, pageSize, pageNumber);

		/// <summary>
		/// Finds trash contents
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
		/// Finds trash contents
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
			=> string.IsNullOrWhiteSpace(serviceName) && string.IsNullOrWhiteSpace(systemID)
				? throw new InformationRequiredException("Service name or system identity is required")
				: RepositoryMediator.FindTrashContents<T>(context, dataSource, serviceName, systemID, null, null, null, pageSize, pageNumber);

		/// <summary>
		/// Finds trash contents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="serviceName">The name of service that associates with</param>
		/// <param name="systemID">The identity of system that associates with</param>
		/// <param name="pageSize">The identity of business repository entity that associates with</param>
		/// <param name="pageNumber">The identity of business repository entity that associates with</param>
		/// <returns></returns>
		public static List<TrashContent> FindTrashContents<T>(RepositoryContext context, string serviceName, string systemID, int pageSize = 20, int pageNumber = 1) where T : class
			=> RepositoryMediator.FindTrashContents<T>(context, null, serviceName, systemID, pageSize, pageNumber);

		/// <summary>
		/// Finds trash contents
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
		/// Finds trash contents
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
		public static async Task<List<TrashContent>> FindTrashContentsAsync<T>(RepositoryContext context, DataSource dataSource, string serviceName = null, string systemID = null, string repositoryID = null, string entityID = null, string userID = null, int pageSize = 0, int pageNumber = 1, CancellationToken cancellationToken = default) where T : class
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
			catch (OperationCanceledException ex)
			{
				context.Exception = ex;
				throw ex;
			}
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw ex;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw new RepositoryOperationException("Error occurred while fetching trash contents of an object", ex);
			}
		}

		/// <summary>
		/// Finds trash contents
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
		public static Task<List<TrashContent>> FindTrashContentsAsync<T>(RepositoryContext context, string serviceName = null, string systemID = null, string repositoryID = null, string entityID = null, string userID = null, int pageSize = 0, int pageNumber = 1, CancellationToken cancellationToken = default) where T : class
			=> RepositoryMediator.FindTrashContentsAsync<T>(context, null, serviceName, systemID, repositoryID, entityID, userID, pageSize, pageNumber, cancellationToken);

		/// <summary>
		/// Finds trash contents
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
		public static async Task<List<TrashContent>> FindTrashContentsAsync<T>(string serviceName = null, string systemID = null, string repositoryID = null, string entityID = null, string userID = null, int pageSize = 0, int pageNumber = 1, CancellationToken cancellationToken = default) where T : class
		{
			using (var context = new RepositoryContext(false))
			{
				return await RepositoryMediator.FindTrashContentsAsync<T>(context, serviceName, systemID, repositoryID, entityID, userID, pageSize, pageNumber, cancellationToken);
			}
		}

		/// <summary>
		/// Finds trash contents
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
		public static Task<List<TrashContent>> FindTrashContentsAsync<T>(RepositoryContext context, DataSource dataSource, string serviceName, string systemID, int pageSize = 20, int pageNumber = 1, CancellationToken cancellationToken = default) where T : class
			=> string.IsNullOrWhiteSpace(serviceName) && string.IsNullOrWhiteSpace(systemID)
				? Task.FromException<List<TrashContent>>(new InformationRequiredException("Service name or system identity is required"))
				: RepositoryMediator.FindTrashContentsAsync<T>(context, dataSource, serviceName, systemID, null, null, null, pageSize, pageNumber, cancellationToken);

		/// <summary>
		/// Finds trash contents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="serviceName">The name of service that associates with</param>
		/// <param name="systemID">The identity of system that associates with</param>
		/// <param name="pageSize">The identity of business repository entity that associates with</param>
		/// <param name="pageNumber">The identity of business repository entity that associates with</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<List<TrashContent>> FindTrashContentsAsync<T>(RepositoryContext context, string serviceName, string systemID, int pageSize = 20, int pageNumber = 1, CancellationToken cancellationToken = default) where T : class
			=> RepositoryMediator.FindTrashContentsAsync<T>(context, null, serviceName, systemID, pageSize, pageNumber, cancellationToken);

		/// <summary>
		/// Finds trash contents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="systemID">The identity of system that associates with</param>
		/// <param name="pageSize">The identity of business repository entity that associates with</param>
		/// <param name="pageNumber">The identity of business repository entity that associates with</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<List<TrashContent>> FindTrashContentsAsync<T>(string serviceName, string systemID, int pageSize = 20, int pageNumber = 1, CancellationToken cancellationToken = default) where T : class
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
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw ex;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
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
			=> RepositoryMediator.CleanTrashContents(!string.IsNullOrWhiteSpace(dataSource) && RepositoryMediator.DataSources.ContainsKey(dataSource) ? RepositoryMediator.DataSources[dataSource] : null, days);

		/// <summary>
		/// Cleans old trash contents
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="days">The remain days</param>
		public static async Task CleanTrashContentsAsync(RepositoryContext context, DataSource dataSource, int days = 30, CancellationToken cancellationToken = default)
		{
			try
			{
				context.Operation = RepositoryOperation.Delete;
				await TrashContent.DeleteAsync(dataSource ?? context.GetTrashDataSource(), "Trashs", Filters<TrashContent>.LessThanOrEquals("Created", DateTime.Now.AddDays(0 - (days > 0 ? days : 30))), cancellationToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException ex)
			{
				context.Exception = ex;
				throw ex;
			}
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw ex;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw new RepositoryOperationException("Error occurred while cleaning old trash contents", ex);
			}
		}

		/// <summary>
		/// Cleans old trash contents
		/// </summary>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="days">The remain days</param>
		public static async Task CleanTrashContentsAsync(DataSource dataSource, int days = 30, CancellationToken cancellationToken = default)
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
		public static Task CleanTrashContentsAsync(string dataSource, int days = 30, CancellationToken cancellationToken = default)
			=> RepositoryMediator.CleanTrashContentsAsync(!string.IsNullOrWhiteSpace(dataSource) && RepositoryMediator.DataSources.ContainsKey(dataSource) ? RepositoryMediator.DataSources[dataSource] : null, days, cancellationToken);
		#endregion

		#region Sync to other data sources
		internal static async Task SyncAsync<T>(IFilterBy<T> filter, string aliasTypeName, string businessEntityID, CancellationToken cancellationToken = default) where T : class
		{
			using (var context = new RepositoryContext())
			{
				try
				{
					var dataSources = context.GetSyncDataSources().Concat(new[] { context.GetSecondaryDataSource() }).Where(dataSource => dataSource != null).ToList();
					if (dataSources.Count > 0)
					{
						var stopwatch = new Stopwatch();
						stopwatch.Start();

						context.Prepare(RepositoryOperation.Delete, RepositoryMediator.GetEntityDefinition<T>(), aliasTypeName);
						await Task.WhenAll(dataSources.Select(dataSource =>
						{
							return dataSource.Mode.Equals(RepositoryMode.NoSQL)
								? context.DeleteManyAsync(dataSource, filter, businessEntityID, null, cancellationToken)
								: dataSource.Mode.Equals(RepositoryMode.SQL)
									? context.DeleteManyAsync(dataSource, filter, businessEntityID, cancellationToken)
									: null;
						}).Where(task => task != null).ToList()).ConfigureAwait(false);

						stopwatch.Stop();
						if (RepositoryMediator.IsDebugEnabled)
							RepositoryMediator.WriteLogs(new List<string>()
							{
								$"SYNC: Delete multiple objects successful [{typeof(T)}]",
								$"- Synced data sources: {dataSources.Select(dataSource => dataSource.Name + " (" + dataSource.Mode + ")").ToString(", ")}",
								$"- Execution times: {stopwatch.GetElapsedTimes()}",
							});
					}
				}
				catch (OperationCanceledException)
				{
					RepositoryMediator.WriteLogs($"SYNC: Operation was cancelled while deleting multiple objects [{typeof(T)}]");
				}
				catch (Exception ex)
				{
					context.Exception = ex;
					RepositoryMediator.WriteLogs($"SYNC: Error occurred while deleting multiple objects [{typeof(T)}]", ex);
				}
			}
		}

		internal static async Task SyncAsync<T>(T @object, string aliasTypeName, EntityDefinition entityDefinition, CancellationToken cancellationToken = default) where T : class
		{
			using (var context = new RepositoryContext(false))
			{
				try
				{
					var stopwatch = new Stopwatch();
					stopwatch.Start();

					context.Prepare(RepositoryOperation.Create, entityDefinition, aliasTypeName);
					var primaryDataSource = context.GetPrimaryDataSource();
					if (primaryDataSource.Mode.Equals(RepositoryMode.NoSQL))
						await context.CreateAsync(primaryDataSource, @object, null, cancellationToken);
					else if (primaryDataSource.Mode.Equals(RepositoryMode.SQL))
						await context.CreateAsync(primaryDataSource, @object, cancellationToken);

					stopwatch.Stop();
					if (RepositoryMediator.IsDebugEnabled)
						RepositoryMediator.WriteLogs($"SYNC: Sync the object to primary data source successful [{@object?.GetType()}#{@object?.GetEntityID()}] @ {primaryDataSource.Name} - Execution times: {stopwatch.GetElapsedTimes()}");
				}
				catch (OperationCanceledException)
				{
					RepositoryMediator.WriteLogs($"SYNC: Operation was cancelled while syncing the object to primary data source [{@object?.GetType()}#{@object?.GetEntityID()}]");
				}
				catch (Exception ex)
				{
					RepositoryMediator.WriteLogs($"SYNC: Error occurred while syncing the object to primary data source [{@object?.GetType()}#{@object?.GetEntityID()}]", ex);
				}
			}
		}

		internal static async Task SyncAsync<T>(T @object, string aliasTypeName, bool checkExisting = true, bool isDelete = false, CancellationToken cancellationToken = default) where T : class
		{
			using (var context = new RepositoryContext(isDelete))
			{
				try
				{
					var dataSources = context.GetSyncDataSources().Concat(new[] { context.GetSecondaryDataSource() }).Where(dataSource => dataSource != null).ToList();
					if (dataSources.Count > 0)
					{
						var stopwatch = new Stopwatch();
						stopwatch.Start();

						context.Prepare(RepositoryOperation.Update, RepositoryMediator.GetEntityDefinition<T>(), aliasTypeName);
						var tasks = new List<Task>();

						// delete
						if (isDelete)
							tasks = dataSources.Select(dataSource =>
							{
								return dataSource.Mode.Equals(RepositoryMode.NoSQL)
									? context.DeleteAsync(dataSource, @object, null, cancellationToken)
									: dataSource.Mode.Equals(RepositoryMode.SQL)
										? context.DeleteAsync(dataSource, @object, cancellationToken)
										: null;
							}).Where(task => task != null).ToList();

						// sync to other data sources
						else
							foreach (var dataSource in dataSources)
							{
								// check existing
								var current = checkExisting
									? dataSource.Mode.Equals(RepositoryMode.NoSQL)
										? await context.GetAsync<T>(dataSource, @object.GetEntityID(), null, cancellationToken).ConfigureAwait(false)
										: dataSource.Mode.Equals(RepositoryMode.SQL)
											? await context.GetAsync<T>(dataSource, @object.GetEntityID(), cancellationToken).ConfigureAwait(false)
											: null
									: null;

								// create new
								if (current == null)
								{
									if (dataSource.Mode.Equals(RepositoryMode.NoSQL))
										tasks.Add(context.CreateAsync(dataSource, @object, null, cancellationToken));
									else if (dataSource.Mode.Equals(RepositoryMode.SQL))
										tasks.Add(context.CreateAsync(dataSource, @object, cancellationToken));
								}

								// replace
								else if (!isDelete)
								{
									if (dataSource.Mode.Equals(RepositoryMode.NoSQL))
										tasks.Add(context.ReplaceAsync(dataSource, @object, null, cancellationToken));
									else if (dataSource.Mode.Equals(RepositoryMode.SQL))
										tasks.Add(context.ReplaceAsync(dataSource, @object, cancellationToken));
								}
							}

						// wait for all completed
						await Task.WhenAll(tasks).ConfigureAwait(false);

						stopwatch.Stop();
						if (RepositoryMediator.IsDebugEnabled)
							RepositoryMediator.WriteLogs(new List<string>()
							{
								$"SYNC: {(isDelete ? "Delete" : "Sync")} the object successful [{typeof(T)}#{@object?.GetEntityID()}]",
								$"- Synced data sources: {dataSources.Select(dataSource => dataSource.Name + " (" + dataSource.Mode + ")").ToString(", ")}",
								$"- Execution times: {stopwatch.GetElapsedTimes()}",
							});
					}
				}
				catch (OperationCanceledException)
				{
					RepositoryMediator.WriteLogs($"SYNC: Operation was cancelled while {(isDelete ? "deleting" : "syncing")} the object [{typeof(T)}#{@object?.GetEntityID()}]");
				}
				catch (Exception ex)
				{
					if (isDelete)
						context.Exception = ex;
					RepositoryMediator.WriteLogs($"SYNC: Error occurred while {(isDelete ? "deleting" : "syncing")} the object [{typeof(T)}#{@object?.GetEntityID()}]", ex);
				}
			}
		}

		/// <summary>
		/// Syncs the original object (usually from primary data source) to other data sources (including secondary data source and sync data sources)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The context</param>
		/// <param name="object">The object</param>
		public static void Sync<T>(this RepositoryContext context, T @object) where T : class
		{
			if (@object != null)
				Task.Run(() => RepositoryMediator.SyncAsync(@object, context.AliasTypeName)).ConfigureAwait(false);
			else
				throw new ArgumentNullException(nameof(@object), "The syncing object is null");
		}

		/// <summary>
		/// Syncs the original object (usually from primary data source) to other data sources (including secondary data source and sync data sources)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="aliasTypeName">The alias type name</param>
		/// <param name="object">The object</param>
		public static void Sync<T>(string aliasTypeName, T @object) where T : class
		{
			if (@object != null)
				Task.Run(() => RepositoryMediator.SyncAsync(@object, aliasTypeName)).ConfigureAwait(false);
			else
				throw new ArgumentNullException(nameof(@object), "The syncing object is null");
		}

		/// <summary>
		/// Syncs the original object (usually from primary data source) to other data sources (including secondary data source and sync data sources)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The context</param>
		/// <param name="object">The object</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task SyncAsync<T>(this RepositoryContext context, T @object, CancellationToken cancellationToken = default) where T : class
			=> @object != null
				? RepositoryMediator.SyncAsync(@object, context.AliasTypeName, true, false, cancellationToken)
				: Task.FromException(new ArgumentNullException(nameof(@object), "The syncing object is null"));

		/// <summary>
		/// Syncs the original object (usually from primary data source) to other data sources (including secondary data source and sync data sources)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="aliasTypeName">The alias type name</param>
		/// <param name="object">The object</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task SyncAsync<T>(string aliasTypeName, T @object, CancellationToken cancellationToken = default) where T : class
			=> @object != null
				? RepositoryMediator.SyncAsync(@object, aliasTypeName, true, false, cancellationToken)
				: Task.FromException(new ArgumentNullException(nameof(@object), "The syncing object is null"));
		#endregion

		#region Call handlers of Create event
		static List<Type> GetHandlers(Func<Type, bool> predicate)
			=> RepositoryMediator.EventHandlers.Count < 1
				? new List<Type>()
				: RepositoryMediator.EventHandlers.Where(type => predicate(type)).ToList();

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
				catch (Exception ex)
				{
					RepositoryMediator.WriteLogs($"Error occurred while running the pre-create handler \"{handlers[index].ToString()}\" [{typeof(T)}#{@object?.GetEntityID()}]", ex);
				}
			return false;
		}

		static async Task<bool> CallPreCreateHandlersAsync<T>(this RepositoryContext context, T @object, bool isRestore = false, CancellationToken cancellationToken = default) where T : class
		{
			var handlers = RepositoryMediator.GetHandlers(type => typeof(IPreCreateHandler).IsAssignableFrom(type));
			for (var index = 0; index < handlers.Count; index++)
				try
				{
					var handler = ObjectService.CreateInstance(handlers[index]) as IPreCreateHandler;
					if (await handler.OnPreCreateAsync(context, @object, isRestore, cancellationToken).ConfigureAwait(false))
						return true;
				}
				catch (OperationCanceledException)
				{
					RepositoryMediator.WriteLogs($"Operation was cancelled while running the pre-create handler (async) \"{handlers[index].ToString()}\" [{typeof(T)}#{@object?.GetEntityID()}]");
				}
				catch (Exception ex)
				{
					RepositoryMediator.WriteLogs($"Error occurred while running the pre-create handler (async) \"{handlers[index].ToString()}\" [{typeof(T)}#{@object?.GetEntityID()}]", ex);
				}
			return false;
		}

		static void CallPostCreateHandlers<T>(this RepositoryContext context, T @object, bool isRestore = false) where T : class
			=> RepositoryMediator.GetHandlers(type => typeof(IPostCreateHandler).IsAssignableFrom(type))
				.Select(type => Task.Run(() =>
				{
					try
					{
						var handler = ObjectService.CreateInstance(type) as IPostCreateHandler;
						handler.OnPostCreate(context, @object, isRestore);
					}
					catch (Exception ex)
					{
						RepositoryMediator.WriteLogs($"Error occurred while running the post-create handler \"{type.ToString()}\" [{typeof(T)}#{@object?.GetEntityID()}]", ex);
					}
				}))
				.ToList();

		static async Task CallPostCreateHandlersAsync<T>(this RepositoryContext context, T @object, bool isRestore = false, CancellationToken cancellationToken = default) where T : class
			=> await RepositoryMediator.GetHandlers(type => typeof(IPostCreateHandler).IsAssignableFrom(type))
				.ForEachAsync(async (type, token) =>
				{
					try
					{
						var handler = ObjectService.CreateInstance(type) as IPostCreateHandler;
						await handler.OnPostCreateAsync(context, @object, isRestore, token).ConfigureAwait(false);
					}
					catch (OperationCanceledException)
					{
						RepositoryMediator.WriteLogs($"Operation was cancelled while running the post-create handler (async) \"{type.ToString()}\" [{typeof(T)}#{@object?.GetEntityID()}]");
					}
					catch (Exception ex)
					{
						RepositoryMediator.WriteLogs($"Error occurred while running the post-create handler (async) \"{type.ToString()}\" [{typeof(T)}#{@object?.GetEntityID()}]", ex);
					}
				}, cancellationToken, false).ConfigureAwait(false);
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
				catch (Exception ex)
				{
					RepositoryMediator.WriteLogs($"Error occurred while running the pre-get handler \"{handlers[index].ToString()}\" [{typeof(T)}#{id}]", ex);
				}
			return false;
		}

		static async Task<bool> CallPreGetHandlersAsync<T>(this RepositoryContext context, string id, CancellationToken cancellationToken = default) where T : class
		{
			var handlers = RepositoryMediator.GetHandlers(type => typeof(IPreGetHandler).IsAssignableFrom(type));
			for (var index = 0; index < handlers.Count; index++)
				try
				{
					var handler = ObjectService.CreateInstance(handlers[index]) as IPreGetHandler;
					if (await handler.OnPreGetAsync<T>(context, id, cancellationToken).ConfigureAwait(false))
						return true;
				}
				catch (OperationCanceledException)
				{
					RepositoryMediator.WriteLogs($"Operation was cancelled while running the pre-get handler (async) \"{handlers[index].ToString()}\" [{typeof(T)}#{id}]");
				}
				catch (Exception ex)
				{
					RepositoryMediator.WriteLogs($"Error occurred while running the pre-get handler (async) \"{handlers[index].ToString()}\" [{typeof(T)}#{id}]", ex);
				}
			return false;
		}

		static void CallPostGetHandlers<T>(this RepositoryContext context, T @object) where T : class
			=> RepositoryMediator.GetHandlers(type => typeof(IPostGetHandler).IsAssignableFrom(type))
				.Select(type => Task.Run(() =>
				{
					try
					{
						var handler = ObjectService.CreateInstance(type) as IPostGetHandler;
						handler.OnPostGet(context, @object);
					}
					catch (Exception ex)
					{
						RepositoryMediator.WriteLogs($"Error occurred while running the post-get handler \"{type.ToString()}\" [{typeof(T)}#{@object?.GetEntityID()}]", ex);
					}
				}))
				.ToList();

		static async Task CallPostGetHandlersAsync<T>(this RepositoryContext context, T @object, CancellationToken cancellationToken = default) where T : class
			=> await RepositoryMediator.GetHandlers(type => typeof(IPostGetHandler).IsAssignableFrom(type))
				.ForEachAsync(async (type, token) =>
				{
					try
					{
						var handler = ObjectService.CreateInstance(type) as IPostGetHandler;
						await handler.OnPostGetAsync(context, @object, token).ConfigureAwait(false);
					}
					catch (OperationCanceledException)
					{
						RepositoryMediator.WriteLogs($"Operation was cancelled while running the post-get handler (async) \"{type.ToString()}\" [{typeof(T)}#{@object?.GetEntityID()}]");
					}
					catch (Exception ex)
					{
						RepositoryMediator.WriteLogs($"Error occurred while running the post-get handler (async) \"{type.ToString()}\" [{typeof(T)}#{@object?.GetEntityID()}]", ex);
					}
				}, cancellationToken, false).ConfigureAwait(false);
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
				catch (Exception ex)
				{
					RepositoryMediator.WriteLogs($"Error occurred while running the pre-update handler \"{handlers[index].ToString()}\" [{typeof(T)}#{@object?.GetEntityID()}]", ex);
				}
			return false;
		}

		static async Task<bool> CallPreUpdateHandlersAsync<T>(this RepositoryContext context, T @object, HashSet<string> changed, bool isRestore = false, CancellationToken cancellationToken = default) where T : class
		{
			var handlers = RepositoryMediator.GetHandlers(type => typeof(IPreUpdateHandler).IsAssignableFrom(type));
			for (var index = 0; index < handlers.Count; index++)
				try
				{
					var handler = ObjectService.CreateInstance(handlers[index]) as IPreUpdateHandler;
					if (await handler.OnPreUpdateAsync(context, @object, changed, isRestore, cancellationToken).ConfigureAwait(false))
						return true;
				}
				catch (OperationCanceledException)
				{
					RepositoryMediator.WriteLogs($"Operation was cancelled while running the pre-update handler (async) \"{handlers[index].ToString()}\" [{typeof(T)}#{@object?.GetEntityID()}]");
				}
				catch (Exception ex)
				{
					RepositoryMediator.WriteLogs($"Error occurred while running the pre-update handler (async) \"{handlers[index].ToString()}\" [{typeof(T)}#{@object?.GetEntityID()}]", ex);
				}
			return false;
		}

		static void CallPostUpdateHandlers<T>(this RepositoryContext context, T @object, HashSet<string> changed, bool isRestore = false) where T : class
			=> RepositoryMediator.GetHandlers(type => typeof(IPostUpdateHandler).IsAssignableFrom(type))
				.Select(type => Task.Run(() =>
				{
					try
					{
						var handler = ObjectService.CreateInstance(type) as IPostUpdateHandler;
						handler.OnPostUpdate(context, @object, changed, isRestore);
					}
					catch (Exception ex)
					{
						RepositoryMediator.WriteLogs($"Error occurred while running the post-update handler \"{type.ToString()}\" [{typeof(T)}#{@object?.GetEntityID()}]", ex);
					}
				}))
				.ToList();

		static async Task CallPostUpdateHandlersAsync<T>(this RepositoryContext context, T @object, HashSet<string> changed, bool isRestore = false, CancellationToken cancellationToken = default) where T : class
			=> await RepositoryMediator.GetHandlers(type => typeof(IPostUpdateHandler).IsAssignableFrom(type))
				.ForEachAsync(async (type, token) =>
				{
					try
					{
						var handler = ObjectService.CreateInstance(type) as IPostUpdateHandler;
						await handler.OnPostUpdateAsync(context, @object, changed, isRestore, token).ConfigureAwait(false);
					}
					catch (OperationCanceledException)
					{
						RepositoryMediator.WriteLogs($"Operation was cancelled while running the post-update handler (async) \"{type.ToString()}\" [{typeof(T)}#{@object?.GetEntityID()}]");
					}
					catch (Exception ex)
					{
						RepositoryMediator.WriteLogs($"Error occurred while running the post-update handler (async) \"{type.ToString()}\" [{typeof(T)}#{@object?.GetEntityID()}]", ex);
					}
				}, cancellationToken, false).ConfigureAwait(false);
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
				catch (Exception ex)
				{
					RepositoryMediator.WriteLogs($"Error occurred while running the pre-delete handler \"{handlers[index].ToString()}\" [{typeof(T)}#{@object?.GetEntityID()}]", ex);
				}
			return false;
		}

		static async Task<bool> CallPreDeleteHandlersAsync<T>(this RepositoryContext context, T @object, CancellationToken cancellationToken = default) where T : class
		{
			var handlers = RepositoryMediator.GetHandlers(type => typeof(IPreDeleteHandler).IsAssignableFrom(type));
			for (var index = 0; index < handlers.Count; index++)
				try
				{
					var handler = ObjectService.CreateInstance(handlers[index]) as IPreDeleteHandler;
					if (await handler.OnPreDeleteAsync(context, @object, cancellationToken).ConfigureAwait(false))
						return true;
				}
				catch (OperationCanceledException)
				{
					RepositoryMediator.WriteLogs($"Operation was cancelled while running the pre-delete handler (async) \"{handlers[index].ToString()}\" [{typeof(T)}#{@object?.GetEntityID()}]");
				}
				catch (Exception ex)
				{
					RepositoryMediator.WriteLogs($"Error occurred while running the pre-delete handler (async) \"{handlers[index].ToString()}\" [{typeof(T)}#{@object?.GetEntityID()}]", ex);
				}
			return false;
		}

		static void CallPostDeleteHandlers<T>(this RepositoryContext context, T @object) where T : class
			=> RepositoryMediator.GetHandlers(type => typeof(IPostDeleteHandler).IsAssignableFrom(type))
				.Select(type => Task.Run(() =>
				{
					try
					{
						var handler = ObjectService.CreateInstance(type) as IPostDeleteHandler;
						handler.OnPostDelete(context, @object);
					}
					catch (Exception ex)
					{
						RepositoryMediator.WriteLogs($"Error occurred while running the post-delete handler \"{type.ToString()}\" [{typeof(T)}#{@object?.GetEntityID()}]", ex);
					}
				}))
				.ToList();

		static async Task CallPostDeleteHandlersAsync<T>(this RepositoryContext context, T @object, CancellationToken cancellationToken = default) where T : class
			=> await RepositoryMediator.GetHandlers(type => typeof(IPostDeleteHandler).IsAssignableFrom(type))
				.ForEachAsync(async (type, token) =>
				{
					try
					{
						var handler = ObjectService.CreateInstance(type) as IPostDeleteHandler;
						await handler.OnPostDeleteAsync(context, @object, token).ConfigureAwait(false);
					}
					catch (OperationCanceledException)
					{
						RepositoryMediator.WriteLogs($"Operation was cancelled while running the post-delete handler (async) \"{type.ToString()}\" [{typeof(T)}#{@object?.GetEntityID()}]");
					}
					catch (Exception ex)
					{
						RepositoryMediator.WriteLogs($"Error occurred while running the post-delete handler (async) \"{type.ToString()}\" [{typeof(T)}#{@object?.GetEntityID()}]", ex);
					}
				}, cancellationToken, false).ConfigureAwait(false);
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
			=> objects == null || objects.Count < 1
				? new JArray()
				: objects.ToJArray(@object => @object is RepositoryBase ? (@object as RepositoryBase)?.ToJson(addTypeOfExtendedProperties, onItemPreCompleted) : @object?.ToJson());

		/// <summary>
		/// Serializes the collection of objects to an array of JSON objects
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="objects">The object to serialize</param>
		/// <param name="onItemPreCompleted">The action to run on item pre-completed</param>
		/// <returns></returns>
		public static JArray ToJsonArray<T>(this List<T> objects, Action<JObject> onItemPreCompleted) where T : class
			=> objects.ToJsonArray(false, onItemPreCompleted);

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
			objects.ForEach(@object => json.Add(new JProperty(@object?.GetEntityID(), @object is RepositoryBase ? (@object as RepositoryBase)?.ToJson(addTypeOfExtendedProperties, onItemPreCompleted) : @object?.ToJson())));
			return json;
		}

		/// <summary>
		/// Serializes the collection of objects to a JSON object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="objects">The object to serialize</param>
		/// <param name="onItemPreCompleted">The action to run on item pre-completed</param>
		/// <returns></returns>
		public static JObject ToJsonObject<T>(this List<T> objects, Action<JObject> onItemPreCompleted) where T : class
			=> objects.ToJsonObject(false, onItemPreCompleted);

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
				objects.ForEach(@object => xml.Add(@object is RepositoryBase ? (@object as RepositoryBase)?.ToXml(addTypeOfExtendedProperties, onItemPreCompleted) : @object?.ToXml()));
			return xml;
		}

		/// <summary>
		/// Serializes the collection of objects to XML
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="objects">The object to serialize</param>
		/// <param name="onItemPreCompleted">The action to run on item pre-completed</param>
		/// <returns></returns>
		public static XElement ToXml<T>(this List<T> objects, Action<XElement> onItemPreCompleted) where T : class
			=> objects.ToXml(null, false, onItemPreCompleted);
		#endregion

		#region Generate form controls
		internal static Dictionary<Type, string> FormControlDataTypes { get; } = new Dictionary<Type, string>()
		{
			{ typeof(string), "text" },
			{ typeof(char), "text" },
			{ typeof(byte[]), "" },
			{ typeof(byte), "number" },
			{ typeof(sbyte), "number" },
			{ typeof(short), "number" },
			{ typeof(ushort), "number" },
			{ typeof(int), "number" },
			{ typeof(uint), "number" },
			{ typeof(long), "number" },
			{ typeof(ulong), "number" },
			{ typeof(float), "number" },
			{ typeof(double), "number" },
			{ typeof(decimal), "number" },
			{ typeof(bool), "" },
			{ typeof(Guid), "" },
			{ typeof(DateTime), "date" },
			{ typeof(DateTimeOffset), "date" }
		};

		internal static List<AttributeInfo> GetFormAttributes(Type type)
		{
			try
			{
				return RepositoryMediator.GetEntityDefinition(type).Attributes;
			}
			catch
			{
				return ObjectService.GetProperties(type).Select(attribute => new AttributeInfo(attribute)).ToList();
			}
		}

		internal static JToken GenerateFormControl(this AttributeInfo attribute, int index = 0)
		{
			var formControlInfo = attribute.Info.GetCustomAttributes(typeof(FormControlAttribute), true).FirstOrDefault() as FormControlAttribute;
			if (formControlInfo != null ? formControlInfo.Excluded : attribute.IsIgnored())
				return null;

			JObject control;
			var hidden = formControlInfo != null
				? formControlInfo.Hidden
				: attribute.Info.GetCustomAttributes(typeof(PrimaryKeyAttribute), true).FirstOrDefault() != null;
			var order = formControlInfo != null && formControlInfo.Order > -1
				? formControlInfo.Order
				: index;
			var label = (formControlInfo?.Label ?? attribute.Name).Replace(StringComparison.OrdinalIgnoreCase, "[name]", attribute.Name);

			if (attribute.Type.IsClassType())
			{
				var subControls = new JArray();
				RepositoryMediator.GetFormAttributes(attribute.Type).ForEach((subattribute, subindex) =>
				{
					var subControl = subattribute.GenerateFormControl(subindex);
					if (subControl != null)
						subControls.Add(subControl);
				});

				control = new JObject
				{
					{ "Name", attribute.Name },
					{ "Order", order },
					{ "Segment", formControlInfo?.Segment },
					{ "Options", new JObject
						{
							{ "Label", label }
						}
					},
					{ "SubControls", new JObject
						{
							{ "AsArray", formControlInfo != null ? formControlInfo.AsArray : false },
							{ "Controls", subControls }
						}
					}
				};
				if (hidden)
					control["Hidden"] = hidden;

				if (RepositoryMediator.IsDebugEnabled)
					RepositoryMediator.WriteLogs($"Control of a class-type attribute was generated => {control.ToString(Newtonsoft.Json.Formatting.None)}");

				return control;
			}

			var controlType = !string.IsNullOrWhiteSpace(formControlInfo?.ControlType)
				? formControlInfo.ControlType
				: attribute.IsCLOB != null && attribute.IsCLOB.Value
					? "TextEditor"
					: attribute.Type.IsEnum || attribute.IsEnumString()
						? "Select"
						: attribute.Type.IsPrimitiveType()
							? attribute.Type == typeof(DateTime) || attribute.Type == typeof(DateTimeOffset)
								? "DatePicker"
								: attribute.Type == typeof(bool)
									? "YesNo"
									: "TextBox"
							: "";

			var options = new JObject
			{
				{ "Label", label }
			};

			var dataType = "Lookup".IsEquals(formControlInfo?.ControlType) && !string.IsNullOrWhiteSpace(formControlInfo?.LookupType)
				? formControlInfo.LookupType
				: !string.IsNullOrWhiteSpace(formControlInfo?.DataType)
					? formControlInfo.DataType
					: attribute.Type.IsEnum || attribute.IsEnumString()
						? "text"
						: RepositoryMediator.FormControlDataTypes.TryGetValue(attribute.Type, out string predefinedDataType)
							? predefinedDataType
							: null;
			if (!string.IsNullOrWhiteSpace(dataType))
				options["Type"] = dataType;

			if (formControlInfo?.PlaceHolder != null)
				options["PlaceHolder"] = formControlInfo.PlaceHolder.Replace(StringComparison.OrdinalIgnoreCase, "[name]", attribute.Name);
			if (formControlInfo?.Description != null)
				options["Description"] = formControlInfo.Description.Replace(StringComparison.OrdinalIgnoreCase, "[name]", attribute.Name);
			if (formControlInfo?.ValidatePattern != null)
				options["ValidatePattern"] = formControlInfo.ValidatePattern;
			if (formControlInfo != null && formControlInfo.Disabled)
				options["Disabled"] = true;
			if (formControlInfo != null && formControlInfo.ReadOnly)
				options["ReadOnly"] = true;
			if (formControlInfo != null && formControlInfo.AutoFocus)
				options["AutoFocus"] = true;

			var minValue = formControlInfo?.MinValue ?? attribute?.MinValue;
			if (minValue != null)
				try
				{
					if (attribute.Type.IsIntegralType())
						options["MinValue"] = minValue.CastAs<int>();
					else if (attribute.Type.IsFloatingPointType())
						options["MinValue"] = minValue.CastAs<double>();
					else
						options["MinValue"] = minValue;
				}
				catch (Exception ex)
				{
					RepositoryMediator.WriteLogs($"Error occurred while prepare options [{attribute.Name}] => {ex.Message}", ex);
				}

			var maxValue = formControlInfo?.MaxValue ?? attribute?.MaxValue;
			if (maxValue != null)
				try
				{
					if (attribute.Type.IsIntegralType())
						options["MaxValue"] = maxValue.CastAs<int>();
					else if (attribute.Type.IsFloatingPointType())
						options["MaxValue"] = maxValue.CastAs<double>();
					else
						options["MaxValue"] = maxValue;
				}
				catch (Exception ex)
				{
					RepositoryMediator.WriteLogs($"Error occurred while prepare options [{attribute.Name}] => {ex.Message}", ex);
				}

			var minLength = formControlInfo != null && formControlInfo.MinLength > 0
				? formControlInfo.MinLength.ToString()
				: attribute.MinLength?.ToString();
			if (minLength != null && Int32.TryParse(minLength, out int minLen))
				options["MinLength"] = minLen;

			var maxLength = formControlInfo != null && formControlInfo.MaxLength > 0
				? formControlInfo.MaxLength.ToString()
				: attribute.MaxLength.ToString();
			if (maxLength != null && Int32.TryParse(maxLength, out int maxLen))
				options["MaxLength"] = maxLen;

			var datepickerOptions = "DatePicker".IsEquals(controlType) && formControlInfo != null && formControlInfo.DatePickerWithTimes
				? new JObject
				{
					{ "AllowTimes", true }
				}
				: null;
			if (datepickerOptions != null)
				options["DatePickerOptions"] = datepickerOptions;

			JObject selectOptions = null;
			if ("Select".IsEquals(controlType) || attribute.Type.IsEnum || attribute.IsEnumString())
			{
				selectOptions = new JObject
				{
					{ "Values", formControlInfo?.SelectValues ?? (attribute.Type.IsEnum || attribute.IsEnumString() ? Enum.GetValues(attribute.Type).ToEnumerable().Select(o => o.ToString()).Join(",") : null) },
					{ "Multiple", formControlInfo != null && formControlInfo.Multiple },
					{ "SelectAsBoxes", formControlInfo != null && formControlInfo.SelectAsBoxes }
				};
				if (formControlInfo?.SelectValuesRemoteURI != null)
					selectOptions["RemoteURI"] = formControlInfo?.SelectValuesRemoteURI;
				if (formControlInfo?.SelectInterface != null)
					selectOptions["Interface"] = formControlInfo?.SelectInterface;
			}
			if (selectOptions != null)
				options["SelectOptions"] = selectOptions;

			var required = attribute.Info.GetCustomAttributes(typeof(PrimaryKeyAttribute), true).FirstOrDefault() != null
				? false
				: attribute.NotNull || (attribute.NotEmpty != null && attribute.NotEmpty.Value) || (formControlInfo != null && formControlInfo.Required);

			control = new JObject
			{
				{ "Name", attribute.Name },
				{ "Order", order },
				{ "Segment", formControlInfo?.Segment },
				{ "Options", options }
			};

			if (!string.IsNullOrWhiteSpace(controlType))
				control["Type"] = controlType;
			if (required)
				control["Required"] = required;
			if (hidden)
				control["Hidden"] = hidden;

			if (formControlInfo != null && formControlInfo.AsArray)
			{
				control = new JObject
				{
					{ "Name", attribute.Name },
					{ "Order", order },
					{ "Segment", formControlInfo?.Segment },
					{ "Options", new JObject
						{
							{ "Label", label }
						}
					},
					{ "SubControls", new JObject
						{
							{ "AsArray", true },
							{ "Controls", new JArray { control } }
						}
					}
				};
				if (hidden)
					control["Hidden"] = hidden;
			}

			if (RepositoryMediator.IsDebugEnabled)
				RepositoryMediator.WriteLogs($"Control of a primitive-type attribute was generated => {control.ToString(Newtonsoft.Json.Formatting.None)}");

			return control;
		}

		/// <summary>
		/// Generates the form controls from this object attribute
		/// </summary>
		/// <param name="attribute"></param>
		/// <param name="index"></param>
		/// <returns></returns>
		public static JToken GenerateFormControl(this ObjectService.AttributeInfo attribute, int index = 0)
			=> new AttributeInfo(attribute).GenerateFormControl(index);

		/// <summary>
		/// Generates the form controls of this type
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <returns></returns>
		public static JToken GenerateFormControls<T>() where T : class
		{
			var controls = new JArray();
			var attributes = RepositoryMediator.GetFormAttributes(typeof(T));
			if (RepositoryMediator.IsDebugEnabled)
				RepositoryMediator.WriteLogs($"Start to generate form controls ({typeof(T).GetTypeName(true)}) => {attributes.Select(attribute => attribute.Name).Join(", ")}]");

			attributes.ForEach((attribute, index) =>
			{
				try
				{
					var control = attribute.GenerateFormControl(index);
					if (control != null)
						controls.Add(control);
				}
				catch (Exception ex)
				{
					RepositoryMediator.WriteLogs($"Error occurred while generating form control [{attribute.Name}] => {ex.Message}", ex);
				}
			});
			return controls;
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
			if (entity == null)
				return null;

			var identity = entity is RepositoryBase
				? (entity as RepositoryBase).ID
				: entity.GetAttributeValue(keyAttribute ?? "ID") as string;

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
			=> typeof(T).GetTypeName(!useFullTypeName) + "#" + identity.Trim().ToLower();

		/// <summary>
		/// Adds an object into cache storage
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="cache">The cache storage</param>
		/// <param name="object">The object to update into cache storage</param>
		/// <param name="expirationTime">The number that presents time for caching (in minutes)</param>
		public static bool Set<T>(this Cache cache, T @object, int expirationTime = 0) where T : class
			=> @object != null
				? cache.Set(@object.GetCacheKey(), @object, expirationTime)
				: false;

		/// <summary>
		/// Adds an object into cache storage
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="cache">The cache storage</param>
		/// <param name="object">The object to update into cache storage</param>
		/// <param name="expirationTime">The number that presents time for caching (in minutes)</param>
		public static Task<bool> SetAsync<T>(this Cache cache, T @object, int expirationTime = 0, CancellationToken cancellationToken = default) where T : class
			=> @object != null
				? cache.SetAsync(@object.GetCacheKey(), @object, expirationTime, cancellationToken)
				: Task.FromResult(false);

		/// <summary>
		/// Adds an object into cache storage
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="cache">The cache storage</param>
		/// <param name="object">The object to update into cache storage</param>
		public static Task<bool> SetAsync<T>(this Cache cache, T @object, CancellationToken cancellationToken) where T : class
			=> cache.SetAsync(@object, 0, cancellationToken);

		/// <summary>
		/// Adds an object into cache storage (when its no cached)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="cache">The cache storage</param>
		/// <param name="object">The object to update into cache storage</param>
		/// <param name="expirationTime">The number that presents time for caching (in minutes)</param>
		public static bool Add<T>(this Cache cache, T @object, int expirationTime = 0) where T : class
			=> @object != null
				? cache.Add(@object.GetCacheKey(), @object, expirationTime)
				: false;

		/// <summary>
		/// Adds an object into cache storage (when its no cached)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="cache">The cache storage</param>
		/// <param name="object">The object to update into cache storage</param>
		/// <param name="expirationTime">The number that presents time for caching (in minutes)</param>
		public static Task<bool> AddAsync<T>(this Cache cache, T @object, int expirationTime = 0, CancellationToken cancellationToken = default) where T : class
			=> @object != null
				? cache.AddAsync(@object.GetCacheKey(), @object, expirationTime, cancellationToken)
				: Task.FromResult(false);

		/// <summary>
		/// Adds an object into cache storage (when its no cached)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="cache">The cache storage</param>
		/// <param name="object">The object to update into cache storage</param>
		public static Task<bool> AddAsync<T>(this Cache cache, T @object, CancellationToken cancellationToken) where T : class
			=> cache.AddAsync(@object, 0, cancellationToken);

		/// <summary>
		/// Replaces an object in the cache storage
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="cache">The cache storage</param>
		/// <param name="object">The object to update into cache storage</param>
		/// <param name="expirationTime">The number that presents time for caching (in minutes)</param>
		public static bool Replace<T>(this Cache cache, T @object, int expirationTime = 0) where T : class
			=> @object != null
				? cache.Replace(@object.GetCacheKey(), @object, expirationTime)
				: false;

		/// <summary>
		/// Replaces an object in the cache storage
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="cache">The cache storage</param>
		/// <param name="object">The object to update into cache storage</param>
		/// <param name="expirationTime">The number that presents time for caching (in minutes)</param>
		public static Task<bool> ReplaceAsync<T>(this Cache cache, T @object, int expirationTime = 0, CancellationToken cancellationToken = default) where T : class
			=> @object != null
				? cache.ReplaceAsync(@object.GetCacheKey(), @object, expirationTime, cancellationToken)
				: Task.FromResult(false);

		/// <summary>
		/// Replaces an object in the cache storage
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="cache">The cache storage</param>
		/// <param name="object">The object to update into cache storage</param>
		public static Task<bool> ReplaceAsync<T>(this Cache cache, T @object, CancellationToken cancellationToken) where T : class
			=> cache.ReplaceAsync(@object, 0, cancellationToken);

		/// <summary>
		/// Adds the collection of objects into cache storage
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="cache">The cache storage</param>
		/// <param name="objects">The collection of objects</param>
		public static void Set<T>(this Cache cache, List<T> objects) where T : class
		{
			if (objects != null)
				cache.Set(objects.Where(@object => @object != null).ToDictionary(@object => @object.GetCacheKey()));
		}

		/// <summary>
		/// Adds the collection of objects into cache storage
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="cache">The cache storage</param>
		/// <param name="objects">The collection of objects</param>
		public static Task SetAsync<T>(this Cache cache, List<T> objects, CancellationToken cancellationToken = default) where T : class
			=> objects != null
				? cache.SetAsync(objects.Where(@object => @object != null).ToDictionary(@object => @object.GetCacheKey()), null, 0, cancellationToken)
				: Task.CompletedTask;

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
				cache.Set(objects.Where(@object => @object != null).ToDictionary(@object => @object.GetCacheKey()), null, expirationTime);
		}

		/// <summary>
		/// Adds the collection of objects into cache storage
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="cache">The cache storage</param>
		/// <param name="objects">The collection of objects</param>
		/// <param name="expirationTime">The number that presents time for caching (in minutes)</param>
		public static Task SetAsync<T>(this Cache cache, IEnumerable<T> objects, int expirationTime = 0, CancellationToken cancellationToken = default) where T : class
			=> objects != null
				? cache.SetAsync(objects.Where(@object => @object != null).ToDictionary(@object => @object.GetCacheKey()), null, expirationTime, cancellationToken)
				: Task.CompletedTask;

		/// <summary>
		/// Adds the collection of objects into cache storage
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="cache">The cache storage</param>
		/// <param name="objects">The collection of objects</param>
		public static Task SetAsync<T>(this Cache cache, IEnumerable<T> objects, CancellationToken cancellationToken) where T : class
			=> cache.SetAsync(objects, 0, cancellationToken);

		/// <summary>
		/// Fetchs an object from cache storage
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="cache">The cache storage</param>
		/// <param name="identity">The string that presents identity of object need to get</param>
		/// <returns></returns>
		public static T Fetch<T>(this Cache cache, string identity) where T : class
			=> !string.IsNullOrWhiteSpace(identity)
				? cache.Get<T>(identity.GetCacheKey<T>())
				: null;

		/// <summary>
		/// Fetchs an object from cache storage
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="cache">The cache storage</param>
		/// <param name="identity">The string that presents identity of object need to get</param>
		/// <returns></returns>
		public static Task<T> FetchAsync<T>(this Cache cache, string identity, CancellationToken cancellationToken = default) where T : class
			=> !string.IsNullOrWhiteSpace(identity)
				? cache.GetAsync<T>(identity.GetCacheKey<T>(), cancellationToken)
				: Task.FromResult<T>(null);

		/// <summary>
		/// Removes a cached object from cache storage
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="cache">The cache storage</param>
		/// <param name="object">The object need to delete from cache storage</param>
		public static bool Remove<T>(this Cache cache, T @object) where T : class
			=> @object != null
				? cache.Remove(@object.GetCacheKey())
				: false;

		/// <summary>
		/// Removes a cached object from cache storage
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="cache">The cache storage</param>
		/// <param name="identity">The string that presents identity of object need to delete</param>
		public static bool Remove<T>(this Cache cache, string identity) where T : class
			=> !string.IsNullOrWhiteSpace(identity)
				? cache.Remove(identity.GetCacheKey<T>())
				: false;

		/// <summary>
		/// Remove a cached object from cache storage
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="cache">The cache storage</param>
		/// <param name="object">The object need to delete from cache storage</param>
		public static Task<bool> RemoveAsync<T>(this Cache cache, T @object, CancellationToken cancellationToken = default) where T : class
			=> @object != null
				? cache.RemoveAsync(@object.GetCacheKey(), cancellationToken)
				: Task.FromResult(false);

		/// <summary>
		/// Removes a cached object from cache storage
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="cache">The cache storage</param>
		/// <param name="identity">The string that presents identity of object need to delete</param>
		public static Task<bool> RemoveAsync<T>(this Cache cache, string identity, CancellationToken cancellationToken = default) where T : class
			=> !string.IsNullOrWhiteSpace(identity)
				? cache.RemoveAsync(identity.GetCacheKey<T>(), cancellationToken)
				: Task.FromResult(false);

		/// <summary>
		/// Checks existing of a cached object in cache storage
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="cache">The cache storage</param>
		/// <param name="identity">The string that presents identity of object</param>
		public static bool Exists<T>(this Cache cache, string identity) where T : class
			=> !string.IsNullOrWhiteSpace(identity)
				? cache.Exists(identity.GetCacheKey<T>())
				: false;

		/// <summary>
		/// Checks existing of a cached object in cache storage
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="cache">The cache storage</param>
		/// <param name="identity">The string that presents identity of object</param>
		public static Task<bool> ExistsAsync<T>(this Cache cache, string identity, CancellationToken cancellationToken = default) where T : class
			=> !string.IsNullOrWhiteSpace(identity)
				? cache.ExistsAsync(identity.GetCacheKey<T>(), cancellationToken)
				: Task.FromResult(false);
		#endregion

		#region Property extension methods
		internal static Tuple<Dictionary<string, AttributeInfo>, Dictionary<string, ExtendedPropertyDefinition>> GetProperties<T>(string businessEntityID, EntityDefinition definition = null, bool lowerCaseKeys = false) where T : class
		{
			definition = definition ?? RepositoryMediator.GetEntityDefinition(typeof(T));

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
			definition = definition ?? RepositoryMediator.GetEntityDefinition<T>();

			if (definition == null || definition.ParentType == null || string.IsNullOrWhiteSpace(definition.ParentAssociatedProperty))
				return null;

			if (filter is FilterBy<T>)
				return (filter as FilterBy<T>).Attribute.Equals(definition.ParentAssociatedProperty)
					? new List<string> { (filter as FilterBy<T>).Value as string }
					: null;

			var children = (filter as FilterBys<T>).Children;
			if (children == null || children.Count < 1)
				return null;

			var parentIDs = new List<string>();

			children.ForEach(info =>
			{
				if (info is FilterBy<T> && (info as FilterBy<T>).Attribute.Equals(definition.ParentAssociatedProperty))
					parentIDs.Add((info as FilterBy<T>).Value as string);
				else
				{
					var nextchildren = info is FilterBys<T> ? (info as FilterBys<T>).Children : null;
					if (nextchildren != null && nextchildren.Count > 0)
					{
						var ids = info.GetAssociatedParentIDs(definition);
						if (ids != null)
							parentIDs.Append(ids);
					}
				}
			});

			return parentIDs.Count > 0
				? parentIDs.Distinct().ToList()
				: null;
		}

		/// <summary>
		/// Gets the state that determines this object is got extended properties or not
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="object"></param>
		/// <param name="businessEntityID"></param>
		/// <param name="definition"></param>
		/// <returns></returns>
		public static bool IsGotExtendedProperties<T>(this T @object, string businessEntityID = null, EntityDefinition definition = null) where T : class
		{
			if (!(@object is IBusinessEntity))
				return false;

			businessEntityID = string.IsNullOrWhiteSpace(businessEntityID)
				? (@object as IBusinessEntity).EntityID
				: businessEntityID;

			if (string.IsNullOrWhiteSpace(businessEntityID))
				return false;

			definition = definition ?? RepositoryMediator.GetEntityDefinition(typeof(T));
			if (definition == null || definition.RuntimeEntities == null)
				return false;

			var attributes = definition.RuntimeEntities.ContainsKey(businessEntityID)
				? definition.RuntimeEntities[businessEntityID].ExtendedPropertyDefinitions
				: null;

			return attributes != null && attributes.Count > 0;
		}
		#endregion

		#region [Logs]
		/// <summary>
		/// Gest or sets the logger
		/// </summary>
		public static ILogger Logger { get; set; } = Utility.Logger.CreateLogger<RepositoryBase>();

		/// <summary>
		/// Gets the state that determines log level is trace or not
		/// </summary>
		public static bool IsTraceEnabled
			=> RepositoryMediator.Logger != null && RepositoryMediator.Logger.IsEnabled(LogLevel.Trace);

		/// <summary>
		/// Gets the state that determines log level is debug or not
		/// </summary>
		public static bool IsDebugEnabled
			=> RepositoryMediator.Logger != null && RepositoryMediator.Logger.IsEnabled(LogLevel.Debug);

		internal static void WriteLogs(IEnumerable<string> logs, Exception ex = null, LogLevel logLevel = LogLevel.Debug)
		{
			logs?.Where(log => !string.IsNullOrWhiteSpace(log))?.ForEach(log => RepositoryMediator.Logger?.Log(logLevel, log));
			if (ex != null)
				RepositoryMediator.Logger?.LogError(ex.Message, ex);
		}

		internal static void WriteLogs(string log, Exception ex = null, LogLevel logLevel = LogLevel.Debug)
			=> RepositoryMediator.WriteLogs(string.IsNullOrWhiteSpace(log) ? null : new[] { log }, ex, logLevel);

		internal static void WriteLogs(Exception ex, LogLevel logLevel = LogLevel.Debug)
			=> RepositoryMediator.WriteLogs(new List<string>(), ex, logLevel);
		#endregion

	}
}