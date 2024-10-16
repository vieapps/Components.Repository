﻿#region Related components
using System;
using System.Data;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.Dynamic;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Newtonsoft.Json.Linq;
using net.vieapps.Components.Caching;
using net.vieapps.Components.Utility;
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

		#region Repositories & Entities
		/// <summary>
		/// Gets the repository definition that matched with the type
		/// </summary>
		/// <param name="type">The type of the definition</param>
		/// <param name="verify">true to verify</param>
		/// <returns></returns>
		public static RepositoryDefinition GetRepositoryDefinition(this Type type, bool verify = false)
		{
			if (type != null && RepositoryMediator.RepositoryDefinitions.TryGetValue(type, out var definition))
				return definition;
			if (verify)
				throw new InformationNotFoundException($"The repository definition [{type?.GetTypeName()}] is not found");
			return null;
		}

		/// <summary>
		/// Gets the repository definition that matched with the type
		/// </summary>
		/// <param name="typeName">The assembly-qualified name of the type to get</param>
		/// <param name="verify">true to verify</param>
		/// <returns></returns>
		public static RepositoryDefinition GetRepositoryDefinition(this string typeName, bool verify = false)
			=> Type.GetType(typeName)?.GetRepositoryDefinition(verify);

		/// <summary>
		/// Gets the repository definition that matched with the type name
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="verify">true to verify</param>
		/// <returns></returns>
		public static RepositoryDefinition GetRepositoryDefinition<T>(bool verify = true) where T : class
			=> typeof(T).GetRepositoryDefinition(verify);

		/// <summary>
		/// Gets the repository entity definition that matched with the type
		/// </summary>
		/// <param name="type">The type of the definition</param>
		/// <param name="verify">true to verify</param>
		/// <returns></returns>
		public static EntityDefinition GetEntityDefinition(this Type type, bool verify = false)
		{
			if (type != null && RepositoryMediator.EntityDefinitions.TryGetValue(type, out var definition))
				return definition;
			if (verify)
				throw new InformationNotFoundException($"The repository entity definition [{type?.GetTypeName()}] is not found");
			return null;
		}

		/// <summary>
		/// Gets the repository entity definition that matched with the type
		/// </summary>
		/// <param name="typeName">The assembly-qualified name of the type to get</param>
		/// <param name="verify">true to verify</param>
		/// <returns></returns>
		public static EntityDefinition GetEntityDefinition(this string typeName, bool verify = false)
			=> Type.GetType(typeName)?.GetEntityDefinition(verify);

		/// <summary>
		/// Gets the repository entity definition that matched with the type of a class
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="verify">true to verify</param>
		/// <returns></returns>
		public static EntityDefinition GetEntityDefinition<T>(bool verify = true) where T : class
			=> typeof(T).GetEntityDefinition(verify);

		/// <summary>
		/// Gets the collection of business repositories (means business modules at run-time)
		/// </summary>
		/// <param name="systemID">The identity of a system (means an organization) that the business repositories are belong to</param>
		/// <returns></returns>
		public static List<IBusinessRepository> GetBusinessRepositories(string systemID)
			=> !string.IsNullOrWhiteSpace(systemID)
				? RepositoryMediator.RepositoryDefinitions.Select(d => d.Value.BusinessRepositories.Select(r => r.Value)).SelectMany(r => r).Where(r => r.SystemID.Equals(systemID)).ToList()
				: new List<IBusinessRepository>();

		/// <summary>
		/// Gets a buiness repository (means a business module at run-time)
		/// </summary>
		/// <param name="businessRepositoryID">The identity of a specified business repository (means a business module at run-time)</param>
		/// <returns></returns>
		public static IBusinessRepository GetBusinessRepository(string businessRepositoryID)
			=> !string.IsNullOrWhiteSpace(businessRepositoryID)
				? RepositoryMediator.RepositoryDefinitions.Select(d => d.Value.BusinessRepositories.Select(r => r.Value)).SelectMany(r => r).FirstOrDefault(r => r.ID.Equals(businessRepositoryID))
				: null;

		/// <summary>
		/// Gets the collection of business repository entities (means business content-types at run-time)
		/// </summary>
		/// <param name="businessRepositoryID">The identity of a business repository (means a business module at run-time) that the business repository entities are belong to</param>
		/// <returns></returns>
		public static List<IBusinessRepositoryEntity> GetBusinessRepositoryEntites(string businessRepositoryID)
			=> !string.IsNullOrWhiteSpace(businessRepositoryID)
				? RepositoryMediator.EntityDefinitions.Select(d => d.Value.BusinessRepositoryEntities.Select(e => e.Value)).SelectMany(e => e).Where(e => e.RepositoryID.Equals(businessRepositoryID)).ToList()
				: new List<IBusinessRepositoryEntity>();

		/// <summary>
		/// Gets a business repository entity (means a business content-type at run-time)
		/// </summary>
		/// <param name="businessRepositoryEntityID">The identity of a specified business repository entity (means a business content-type at run-time)</param>
		/// <returns></returns>
		public static IBusinessRepositoryEntity GetBusinessRepositoryEntity(string businessRepositoryEntityID)
			=> !string.IsNullOrWhiteSpace(businessRepositoryEntityID)
				? RepositoryMediator.EntityDefinitions.Select(d => d.Value.BusinessRepositoryEntities.Select(e => e.Value)).SelectMany(e => e).FirstOrDefault(e => e.ID.Equals(businessRepositoryEntityID))
				: null;
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
			tracker?.Invoke($"Construct {RepositoryMediator.DataSources.Count} data sources [{RepositoryMediator.DataSources.ToString(", ", kvp => $"{kvp.Key} ({kvp.Value.Mode}/{kvp.Value.DatabaseName})")}]", null);
			if (tracker == null && RepositoryMediator.IsDebugEnabled)
				RepositoryMediator.WriteLogs($"Construct {RepositoryMediator.DataSources.Count} data sources [{RepositoryMediator.DataSources.ToString(", ", kvp => $"{kvp.Key} ({kvp.Value.Mode}/{kvp.Value.DatabaseName})")}]", null);
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

		#region Validate & Update object with state data
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
						throw new InformationRequiredException($"[{definition.Type.GetTypeName(true)}]: The value of the primary key is required");
					else if (attribute.NotNull)
						throw new InformationRequiredException($"[{definition.Type.GetTypeName(true)}]: The value of the {(attribute.IsPublic ? "property" : "attribute")} named '{attribute.Name}' is required (doesn't allow null)");
				}

				else if (attribute.Type.IsStringType())
				{
					var isCLOB = attribute.IsCLOB != null && attribute.IsCLOB.Value;
					if (!isCLOB)
					{
						var isNotEmpty = attribute.NotEmpty != null && attribute.NotEmpty.Value;
						if (isNotEmpty && string.IsNullOrWhiteSpace(value as string))
							throw new InformationRequiredException($"[{definition.Type.GetTypeName(true)}]: The value of the {(attribute.IsPublic ? "property" : "attribute")} named '{attribute.Name}' is required (doesn't allow empty or null)");

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
			stateData.TryGetValue("RepositoryEntityID", out object repositoryEntityID);
			if (repositoryEntityID != null && repositoryEntityID is string && !string.IsNullOrWhiteSpace(repositoryEntityID as string))
				if (definition.BusinessRepositoryEntities.TryGetValue(repositoryEntityID as string, out var repositoryEntity))
					foreach (var attribute in repositoryEntity?.ExtendedPropertyDefinitions ?? new List<ExtendedPropertyDefinition>())
					{
						if (attribute.Mode.Equals(ExtendedPropertyMode.YesNo) || attribute.Mode.Equals(ExtendedPropertyMode.IntegralNumber)
						|| attribute.Mode.Equals(ExtendedPropertyMode.FloatingPointNumber) || attribute.Mode.Equals(ExtendedPropertyMode.DateTime)
						|| attribute.Mode.Equals(ExtendedPropertyMode.LargeText))
							continue;

						if (!stateData.TryGetValue($"ExtendedProperties.{attribute.Name}", out object value))
							continue;

						if (value == null || string.IsNullOrWhiteSpace(value as string))
							continue;

						var maxLength = attribute.Mode.Equals(ExtendedPropertyMode.SmallText) || attribute.Mode.Equals(ExtendedPropertyMode.Select) ? 250 : 4000;
						if ((value as string).Length > maxLength)
						{
							changed = true;
							stateData[$"ExtendedProperties.{attribute.Name}"] = (value as string).Left(maxLength);
						}
					}

			return changed;
		}

		static void UpdateObject<T>(this T @object, Dictionary<string, object> stateData) where T : class
			=> stateData.ForEach(kvp =>
			{
				if (kvp.Key.StartsWith("ExtendedProperties."))
				{
					var name = kvp.Key.Replace("ExtendedProperties.", "");
					(@object as IBusinessEntity).ExtendedProperties[name] = kvp.Value;
					if (@object is IPropertyChangedNotifier)
						(@object as IPropertyChangedNotifier).NotifyPropertyChanged(name);
				}
				else
					@object.SetAttributeValue(kvp.Key, kvp.Value);
			});
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
					@object.UpdateObject(currentState);
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
				if (context.EntityDefinition.Cache != null)
				{
					context.EntityDefinition.Cache.SetAsync(@object).Run();
					if (RepositoryMediator.IsDebugEnabled)
						RepositoryMediator.WriteLogs($"CREATE: Add the object into the cache storage successful [{@object.GetCacheKey()}]");
				}

				// call post-handlers
				context.CallPostCreateHandlers(@object, false);
				return true;
			}
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw new RepositoryOperationException($"Error occurred while creating new [{typeof(T)}#{@object?.GetEntityID()}]", ex.Message.IsContains("duplicate key") ? new InformationExistedException("A key was existed", ex) : ex);
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
				RepositoryMediator.SyncAsync(@object, context.AliasTypeName, false).Run();
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
				RepositoryMediator.Create(context, aliasTypeName, @object);
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
					@object.UpdateObject(currentState);
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
				if (context.EntityDefinition.Cache != null)
				{
					context.EntityDefinition.Cache.SetAsync(@object).Run();
					if (RepositoryMediator.IsDebugEnabled)
						RepositoryMediator.WriteLogs($"CREATE: Add the object into the cache storage successful [{@object.GetCacheKey()}]");
				}

				// call post-handlers
				await context.CallPostCreateHandlersAsync(@object, false, cancellationToken).ConfigureAwait(false);
				return true;
			}
			catch (OperationCanceledException ex)
			{
				context.Exception = ex;
				throw;
			}
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw new RepositoryOperationException($"Error occurred while creating new [{typeof(T)}#{@object?.GetEntityID()}]", ex.Message.IsContains("duplicate key") ? new InformationExistedException("A key was existed", ex) : ex);
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
				RepositoryMediator.SyncAsync(@object, context.AliasTypeName, false).Run();
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
				await RepositoryMediator.CreateAsync(context, aliasTypeName, @object, cancellationToken).ConfigureAwait(false);
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
				if (@object != null && !@object.GetType().Equals(context.EntityDefinition.Type))
				{
					if (RepositoryMediator.IsDebugEnabled)
						RepositoryMediator.WriteLogs($"GET: Wrong cached [{context.EntityDefinition.Type.GetTypeName()} != {@object.GetTypeName()}]");
					@object = null;
				}

				// auto sync
				if (@object != null)
				{
					if (RepositoryMediator.IsDebugEnabled)
						RepositoryMediator.WriteLogs($"GET: The cached object is found [{@object.GetCacheKey()}]");
					if (context.EntityDefinition.AutoSync)
						RepositoryMediator.SyncAsync(@object, context.AliasTypeName).Run();
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
							RepositoryMediator.SyncAsync(@object, context.AliasTypeName).Run();
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
									RepositoryMediator.SyncAsync(@object, context.AliasTypeName, context.EntityDefinition).Run();
							}
						}
						catch (Exception ex)
						{
							RepositoryMediator.WriteLogs($"GET: Error occurred while fetching object from secondary data source [{typeof(T)}#{id}]", ex);
						}

					// update into cache storage
					if (@object != null && processCache && context.EntityDefinition.Cache != null)
					{
						context.EntityDefinition.Cache.SetAsync(@object).Run();
						if (RepositoryMediator.IsDebugEnabled)
							RepositoryMediator.WriteLogs($"GET: Add the object into the cache storage successful [{@object.GetCacheKey()}]");
					}
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
				throw;
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
		/// <param name="processCache">true to process cache (first check existed object, then update cache)</param>
		/// <param name="processSecondaryWhenNotFound">true to process with secondary data source when object is not found</param>
		/// <returns></returns>
		public static T Get<T>(string aliasTypeName, string id, bool processCache = true, bool processSecondaryWhenNotFound = true) where T : class
		{
			using (var context = new RepositoryContext(false))
				return RepositoryMediator.Get<T>(context, aliasTypeName, id, processCache, processSecondaryWhenNotFound);
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
				if (@object != null && !@object.GetType().Equals(context.EntityDefinition.Type))
				{
					if (RepositoryMediator.IsDebugEnabled)
						RepositoryMediator.WriteLogs($"GET: Wrong cached [{context.EntityDefinition.Type.GetTypeName()} != {@object.GetTypeName()}]");
					@object = null;
				}

				// auto sync
				if (@object != null)
				{
					if (RepositoryMediator.IsDebugEnabled)
						RepositoryMediator.WriteLogs($"GET: The cached object is found [{@object.GetCacheKey()}]");
					if (context.EntityDefinition.AutoSync)
						RepositoryMediator.SyncAsync(@object, context.AliasTypeName).Run();
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
							RepositoryMediator.SyncAsync(@object, context.AliasTypeName).Run();
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
									RepositoryMediator.SyncAsync(@object, context.AliasTypeName, context.EntityDefinition).Run();
							}
						}
						catch (Exception ex)
						{
							RepositoryMediator.WriteLogs($"GET: Error occurred while fetching object from secondary data source [{typeof(T)}#{id}]", ex);
						}

					// update into cache storage
					if (@object != null && processCache && context.EntityDefinition.Cache != null)
					{
						context.EntityDefinition.Cache.SetAsync(@object).Run();
						if (RepositoryMediator.IsDebugEnabled)
							RepositoryMediator.WriteLogs($"GET: Add the object into the cache storage successful [{@object.GetCacheKey()}]");
					}
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
				throw;
			}
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw;
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
		/// <param name="processCache">true to process cache (first check existed object, then update cache)</param>
		/// <param name="processSecondaryWhenNotFound">true to process with secondary data source when object is not found</param>
		/// <returns></returns>
		public static async Task<T> GetAsync<T>(string aliasTypeName, string id, CancellationToken cancellationToken = default, bool processCache = true, bool processSecondaryWhenNotFound = true) where T : class
		{
			using (var context = new RepositoryContext(false))
				return await RepositoryMediator.GetAsync<T>(context, aliasTypeName, id, true, cancellationToken, processCache, processSecondaryWhenNotFound).ConfigureAwait(false);
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
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns></returns>
		public static T Get<T>(RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, SortBy<T> sort = null, string businessRepositoryEntityID = null) where T : class
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
					? context.Get(dataSource, filter, sort, businessRepositoryEntityID, null)
					: dataSource.Mode.Equals(RepositoryMode.SQL)
						? context.Get(dataSource, filter, sort, businessRepositoryEntityID)
						: null;

				// auto sync
				if (@object != null && context.EntityDefinition.AutoSync)
					RepositoryMediator.SyncAsync(@object, context.AliasTypeName).Run();

				// return
				return @object;
			}
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw;
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
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns></returns>
		public static T Get<T>(RepositoryContext context, string aliasTypeName, IFilterBy<T> filter, SortBy<T> sort = null, string businessRepositoryEntityID = null) where T : class
		{
			context.AliasTypeName = aliasTypeName;
			return RepositoryMediator.Get<T>(context, context.GetPrimaryDataSource(), filter, sort, businessRepositoryEntityID);
		}

		/// <summary>
		/// Finds the first instance of an object that matched with the filter expression
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">Filter expression</param>
		/// <param name="sort">Sort expression</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns></returns>
		public static T Get<T>(string aliasTypeName, IFilterBy<T> filter, SortBy<T> sort = null, string businessRepositoryEntityID = null) where T : class
		{
			using (var context = new RepositoryContext(false))
				return RepositoryMediator.Get<T>(context, aliasTypeName, filter, sort, businessRepositoryEntityID);
		}

		/// <summary>
		/// Finds the first instance of an object that matched with the filter expression
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="filter">Filter expression</param>
		/// <param name="sort">Sort expression</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<T> GetAsync<T>(RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, SortBy<T> sort = null, string businessRepositoryEntityID = null, CancellationToken cancellationToken = default) where T : class
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
					? await context.GetAsync(dataSource, filter, sort, businessRepositoryEntityID, null, cancellationToken).ConfigureAwait(false)
					: dataSource.Mode.Equals(RepositoryMode.SQL)
						? await context.GetAsync(dataSource, filter, sort, businessRepositoryEntityID, cancellationToken).ConfigureAwait(false)
						: null;

				// auto sync
				if (@object != null && context.EntityDefinition.AutoSync)
					RepositoryMediator.SyncAsync(@object, context.AliasTypeName).Run();

				// return
				return @object;
			}
			catch (OperationCanceledException ex)
			{
				context.Exception = ex;
				throw;
			}
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw;
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
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<T> GetAsync<T>(RepositoryContext context, string aliasTypeName, IFilterBy<T> filter, SortBy<T> sort = null, string businessRepositoryEntityID = null, CancellationToken cancellationToken = default) where T : class
		{
			context.AliasTypeName = aliasTypeName;
			return RepositoryMediator.GetAsync<T>(context, context.GetPrimaryDataSource(), filter, sort, businessRepositoryEntityID, cancellationToken);
		}

		/// <summary>
		/// Finds the first instance of an object that matched with the filter expression
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">Filter expression</param>
		/// <param name="sort">Sort expression</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<T> GetAsync<T>(string aliasTypeName, IFilterBy<T> filter, SortBy<T> sort = null, string businessRepositoryEntityID = null, CancellationToken cancellationToken = default) where T : class
		{
			using (var context = new RepositoryContext(false))
				return await RepositoryMediator.GetAsync<T>(context, aliasTypeName, filter, sort, businessRepositoryEntityID, cancellationToken).ConfigureAwait(false);
		}
		#endregion

		#region Get (by definition and identity)
		/// <summary>
		/// Gets an object by definition and identity
		/// </summary>
		/// <param name="definition">The entity definition</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="id">The identity</param>
		/// <param name="processSecondaryWhenNotFound">true to process with secondary data source when object is not found</param>
		/// <returns></returns>
		public static RepositoryBase Get(EntityDefinition definition, DataSource dataSource, string id, bool processSecondaryWhenNotFound = true)
		{
			// check
			if (definition == null || string.IsNullOrWhiteSpace(id) || !id.IsValidUUID())
			{
				if (RepositoryMediator.IsDebugEnabled)
					RepositoryMediator.WriteLogs($"GET (by definition): The definition or identity is invalid [ID: {id ?? "N/A"} - Def: {definition?.ToJson()}]");
				return null;
			}

			// get cached object
			var @object = definition.Cache?.Get(definition.Type.GetTypeName(true) + "#" + id.Trim().ToLower());
			if (@object != null && !@object.GetType().Equals(definition.Type))
			{
				if (RepositoryMediator.IsDebugEnabled)
					RepositoryMediator.WriteLogs($"GET (by definition): Wrong cached [{definition.Type.GetTypeName()} != {@object.GetTypeName()}]");
				@object = null;
			}

			// auto sync
			if (@object != null)
			{
				if (RepositoryMediator.IsDebugEnabled)
					RepositoryMediator.WriteLogs($"GET (by definition): The cached object is found [{@object.GetCacheKey()}]");

				if (definition.AutoSync)
					RepositoryMediator.SyncAsync(@object, definition.RepositoryDefinition.IsAlias ? definition.RepositoryDefinition.Type.GetTypeName() : null).Run();
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
						RepositoryMediator.SyncAsync(@object, definition.RepositoryDefinition.IsAlias ? definition.RepositoryDefinition.Type.GetTypeName() : null).Run();
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
								RepositoryMediator.SyncAsync(@object, definition.RepositoryDefinition.IsAlias ? definition.RepositoryDefinition.Type.GetTypeName() : null, definition).Run();
						}
					}
					catch (Exception ex)
					{
						RepositoryMediator.WriteLogs($"GET (by definition): Error occurred while fetching object from secondary data source [{definition.Type}#{id}]", ex);
					}

				// update into cache storage
				if (@object != null && definition.Cache != null)
				{
					definition.Cache.SetAsync(@object).Run();
					if (RepositoryMediator.IsDebugEnabled)
						RepositoryMediator.WriteLogs($"GET (by definition): Add the object into the cache storage successful [{@object.GetCacheKey()}]");
				}
			}

			// return
			return @object as RepositoryBase;
		}

		/// <summary>
		/// Gets an object by definition and identity
		/// </summary>
		/// <param name="definition">The entity definition</param>
		/// <param name="id">The identity</param>
		/// <param name="processSecondaryWhenNotFound">true to process with secondary data source when object is not found</param>
		/// <returns></returns>
		public static RepositoryBase Get(EntityDefinition definition, string id, bool processSecondaryWhenNotFound = true)
			=> RepositoryMediator.Get(definition, null, id, processSecondaryWhenNotFound);

		/// <summary>
		/// Gets an object by definition and identity
		/// </summary>
		/// <param name="entityInfo">The identity of a specified business repository entity (means a business content-type at run-time) or type-name of an entity definition</param>
		/// <param name="objectID">The identity of the object</param>
		/// <param name="processSecondaryWhenNotFound">true to process with secondary data source when object is not found</param>
		/// <returns></returns>
		public static RepositoryBase Get(string entityInfo, string objectID, bool processSecondaryWhenNotFound = true)
			=> string.IsNullOrWhiteSpace(entityInfo) || string.IsNullOrWhiteSpace(objectID)
				? null
				: RepositoryMediator.Get(entityInfo.IsValidUUID() ? RepositoryMediator.GetBusinessRepositoryEntity(entityInfo)?.EntityDefinition : RepositoryMediator.GetEntityDefinition(entityInfo), objectID, processSecondaryWhenNotFound);

		/// <summary>
		/// Gets an object by definition and identity
		/// </summary>
		/// <param name="definition">The entity definition</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="id">The identity</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <param name="processSecondaryWhenNotFound">true to process with secondary data source when object is not found</param>
		/// <returns></returns>
		public static async Task<RepositoryBase> GetAsync(EntityDefinition definition, DataSource dataSource, string id, CancellationToken cancellationToken = default, bool processSecondaryWhenNotFound = true)
		{
			// check
			if (definition == null || string.IsNullOrWhiteSpace(id) || !id.IsValidUUID())
			{
				if (RepositoryMediator.IsDebugEnabled)
					RepositoryMediator.WriteLogs($"GET (by definition): The definition or identity is invalid [ID: {id ?? "N/A"} - Def: {definition?.ToJson()}]");
				return null;
			}

			// get cached object
			var @object = definition.Cache != null
				? await definition.Cache.GetAsync(definition.Type.GetTypeName(true) + "#" + id.Trim().ToLower(), cancellationToken).ConfigureAwait(false)
				: null;
			if (@object != null && !@object.GetType().Equals(definition.Type))
			{
				if (RepositoryMediator.IsDebugEnabled)
					RepositoryMediator.WriteLogs($"GET (by definition): Wrong cached [{definition.Type.GetTypeName()} != {@object.GetTypeName()}]");
				@object = null;
			}

			// auto sync
			if (@object != null)
			{
				if (RepositoryMediator.IsDebugEnabled)
					RepositoryMediator.WriteLogs($"GET (by definition): The cached object is found [{@object.GetCacheKey()}]");

				if (definition.AutoSync)
					RepositoryMediator.SyncAsync(@object, definition.RepositoryDefinition.IsAlias ? definition.RepositoryDefinition.Type.GetTypeName() : null).Run();
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
						RepositoryMediator.SyncAsync(@object, definition.RepositoryDefinition.IsAlias ? definition.RepositoryDefinition.Type.GetTypeName() : null).Run();
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
								RepositoryMediator.SyncAsync(@object, definition.RepositoryDefinition.IsAlias ? definition.RepositoryDefinition.Type.GetTypeName() : null, definition).Run();
						}
					}
					catch (OperationCanceledException)
					{
						throw;
					}
					catch (Exception ex)
					{
						RepositoryMediator.WriteLogs($"GET (by definition): Error occurred while fetching object from secondary data source [{definition.Type}#{id}]", ex);
					}

				// update into cache storage
				if (@object != null && definition.Cache != null)
				{
					definition.Cache.SetAsync(@object).Run();
					if (RepositoryMediator.IsDebugEnabled)
						RepositoryMediator.WriteLogs($"GET (by definition): Add the object into the cache storage successful [{@object.GetCacheKey()}]");
				}
			}

			// return the instance of object
			return @object as RepositoryBase;
		}

		/// <summary>
		/// Gets an object by definition and identity
		/// </summary>
		/// <param name="definition">The entity definition</param>
		/// <param name="id">The identity</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <param name="processSecondaryWhenNotFound">true to process with secondary data source when object is not found</param>
		/// <returns></returns>
		public static Task<RepositoryBase> GetAsync(EntityDefinition definition, string id, CancellationToken cancellationToken = default, bool processSecondaryWhenNotFound = true)
			=> RepositoryMediator.GetAsync(definition, null, id, cancellationToken, processSecondaryWhenNotFound);

		/// <summary>
		/// Gets an object by definition and identity
		/// </summary>
		/// <param name="entityInfo">The identity of a specified business repository entity (means a business content-type at run-time) or type-name of an entity definition</param>
		/// <param name="objectID">The identity of the object</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <param name="processSecondaryWhenNotFound">true to process with secondary data source when object is not found</param>
		/// <returns></returns>
		public static Task<RepositoryBase> GetAsync(string entityInfo, string objectID, CancellationToken cancellationToken = default, bool processSecondaryWhenNotFound = true)
			=> string.IsNullOrWhiteSpace(entityInfo) || string.IsNullOrWhiteSpace(objectID)
				? Task.FromResult<RepositoryBase>(null)
				: RepositoryMediator.GetAsync(entityInfo.IsValidUUID() ? RepositoryMediator.GetBusinessRepositoryEntity(entityInfo)?.EntityDefinition : RepositoryMediator.GetEntityDefinition(entityInfo), objectID, cancellationToken, processSecondaryWhenNotFound);
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
					@object.UpdateObject(currentState);
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
						var runtimeEntity = RepositoryMediator.GetBusinessRepositoryEntity((@object as IBusinessEntity).RepositoryEntityID);
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
				if (context.EntityDefinition.Cache != null)
				{
					context.EntityDefinition.Cache.SetAsync(@object).Run();
					if (RepositoryMediator.IsDebugEnabled)
						RepositoryMediator.WriteLogs($"REPLACE: Add the object into the cache storage successful [{@object.GetCacheKey()}]");
				}

				// call post-handlers
				context.CallPostUpdateHandlers(@object, dirtyAttributes, false);
				return true;
			}
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw new RepositoryOperationException($"Error occurred while replacing object [{typeof(T)}#{@object?.GetEntityID()}]", ex.Message.IsContains("duplicate key") ? new InformationExistedException("A key was existed", ex) : ex);
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
				RepositoryMediator.SyncAsync(@object, context.AliasTypeName).Run();
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
				RepositoryMediator.Replace<T>(context, aliasTypeName, @object, dontCreateNewVersion, userID);
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
					@object.UpdateObject(currentState);
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
						var runtimeEntity = RepositoryMediator.GetBusinessRepositoryEntity((@object as IBusinessEntity).RepositoryEntityID);
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
				if (context.EntityDefinition.Cache != null)
				{
					context.EntityDefinition.Cache.SetAsync(@object).Run();
					if (RepositoryMediator.IsDebugEnabled)
						RepositoryMediator.WriteLogs($"REPLACE: Add the object into the cache storage successful [{@object.GetCacheKey()}]");
				}

				// call post-handlers
				await context.CallPostUpdateHandlersAsync(@object, dirtyAttributes, false, cancellationToken).ConfigureAwait(false);
				return true;
			}
			catch (OperationCanceledException ex)
			{
				context.Exception = ex;
				throw;
			}
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw new RepositoryOperationException($"Error occurred while replacing object [{typeof(T)}#{@object?.GetEntityID()}]", ex.Message.IsContains("duplicate key") ? new InformationExistedException("A key was existed", ex) : ex);
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
				RepositoryMediator.SyncAsync(@object, context.AliasTypeName).Run();
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
				await RepositoryMediator.ReplaceAsync<T>(context, aliasTypeName, @object, dontCreateNewVersion, userID, cancellationToken).ConfigureAwait(false);
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

				if (RepositoryMediator.IsTraceEnabled)
					RepositoryMediator.WriteLogs($"Object state [{typeof(T)}#{@object.GetEntityID()}]\r\n* Previous:\r\n- {previousState?.Select(kvp => $"{kvp.Key}: {kvp.Value}").Join("\r\n- ")}\r\n* Current:\r\n- {currentState?.Select(kvp => $"{kvp.Key}: {kvp.Value}").Join("\r\n- ")}\r\n* Dirty attributes: {dirtyAttributes?.Join(", ")}");

				if (dirtyAttributes.Count < 1)
					return false;

				// validate & re-update object
				if (RepositoryMediator.Validate(context.EntityDefinition, currentState))
				{
					@object.UpdateObject(currentState);
					context.SetCurrentState(@object, currentState);
				}

				// call pre-handlers
				if (context.CallPreUpdateHandlers(@object, dirtyAttributes.Select(name => name.StartsWith("ExtendedProperties.") ? name.Replace("ExtendedProperties.", "") : name).ToHashSet(), false))
					return false;

				// create new version
				if (!dontCreateNewVersion && previousInstance != null)
				{
					userID = userID ?? (@object is IBusinessEntity ? (@object as IBusinessEntity).LastModifiedID : @object.GetAttributeValue("LastModifiedID") as string);
					var createNewVersion = context.EntityDefinition.CreateNewVersionWhenUpdated;
					if (@object is IBusinessEntity)
					{
						var runtimeEntity = RepositoryMediator.GetBusinessRepositoryEntity((@object as IBusinessEntity).RepositoryEntityID);
						createNewVersion = runtimeEntity != null
							? runtimeEntity.CreateNewVersionWhenUpdated
							: context.EntityDefinition.CreateNewVersionWhenUpdated;
					}

					if (createNewVersion)
						RepositoryMediator.CreateVersionAsync<T>(context, previousInstance, userID).Run();
				}

				// update
				dataSource = dataSource ?? context.GetPrimaryDataSource();
				if (dataSource == null)
					throw new InformationInvalidException("Data source is invalid, please check the configuration");

				if (dataSource.Mode.Equals(RepositoryMode.NoSQL))
					context.Update(dataSource, @object, dirtyAttributes.ToList(), null);
				else if (dataSource.Mode.Equals(RepositoryMode.SQL))
					context.Update(dataSource, @object, dirtyAttributes.ToList());

				// update into cache storage
				if (context.EntityDefinition.Cache != null)
				{
					context.EntityDefinition.Cache.SetAsync(@object).Run();
					if (RepositoryMediator.IsDebugEnabled)
						RepositoryMediator.WriteLogs($"UPDATE: Add the object into the cache storage successful [{@object.GetCacheKey()}]");
				}

				// call post-handlers
				context.CallPostUpdateHandlers(@object, dirtyAttributes.Select(name => name.StartsWith("ExtendedProperties.") ? name.Replace("ExtendedProperties.", "") : name).ToHashSet(), false);
				return true;
			}
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw new RepositoryOperationException($"Error occurred while updating object [{typeof(T)}#{@object?.GetEntityID()}]", ex.Message.IsContains("duplicate key") ? new InformationExistedException("A key was existed", ex) : ex);
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
				RepositoryMediator.SyncAsync(@object, context.AliasTypeName).Run();
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
				RepositoryMediator.Update<T>(context, aliasTypeName, @object, dontCreateNewVersion, userID);
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
				if (RepositoryMediator.IsTraceEnabled)
					RepositoryMediator.WriteLogs($"Object state [{typeof(T)}#{@object.GetEntityID()}]\r\n* Previous:\r\n- {previousState?.Select(kvp => $"{kvp.Key}: {kvp.Value}").Join("\r\n- ")}\r\n* Current:\r\n- {currentState?.Select(kvp => $"{kvp.Key}: {kvp.Value}").Join("\r\n- ")}\r\n* Dirty attributes: {dirtyAttributes?.Join(", ")}");

				if (dirtyAttributes.Count < 1)
					return false;

				// validate & re-update object
				if (RepositoryMediator.Validate(context.EntityDefinition, currentState))
				{
					@object.UpdateObject(currentState);
					context.SetCurrentState(@object, currentState);
				}

				// call pre-handlers
				if (await context.CallPreUpdateHandlersAsync(@object, dirtyAttributes.Select(name => name.StartsWith("ExtendedProperties.") ? name.Replace("ExtendedProperties.", "") : name).ToHashSet(), false, cancellationToken).ConfigureAwait(false))
					return false;

				// create new version
				if (!dontCreateNewVersion && previousInstance != null)
				{
					userID = userID ?? (@object is IBusinessEntity ? (@object as IBusinessEntity).LastModifiedID : @object.GetAttributeValue("LastModifiedID") as string);
					var createNewVersion = context.EntityDefinition.CreateNewVersionWhenUpdated;
					if (@object is IBusinessEntity)
					{
						var runtimeEntity = RepositoryMediator.GetBusinessRepositoryEntity((@object as IBusinessEntity).RepositoryEntityID);
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
					await context.UpdateAsync(dataSource, @object, dirtyAttributes.ToList(), null, cancellationToken).ConfigureAwait(false);
				else if (dataSource.Mode.Equals(RepositoryMode.SQL))
					await context.UpdateAsync(dataSource, @object, dirtyAttributes.ToList(), cancellationToken).ConfigureAwait(false);

				// update into cache storage
				if (context.EntityDefinition.Cache != null)
				{
					await context.EntityDefinition.Cache.SetAsync(@object, cancellationToken).ConfigureAwait(false);
					if (RepositoryMediator.IsDebugEnabled)
						RepositoryMediator.WriteLogs($"UPDATE: Add the object into the cache storage successful [{@object.GetCacheKey()}]");
				}

				// call post-handlers
				await context.CallPostUpdateHandlersAsync(@object, dirtyAttributes.Select(name => name.StartsWith("ExtendedProperties.") ? name.Replace("ExtendedProperties.", "") : name).ToHashSet(), false, cancellationToken).ConfigureAwait(false);
				return true;
			}
			catch (OperationCanceledException ex)
			{
				context.Exception = ex;
				throw;
			}
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw new RepositoryOperationException($"Error occurred while updating object [{typeof(T)}#{@object?.GetEntityID()}]", ex.Message.IsContains("duplicate key") ? new InformationExistedException("A key was existed", ex) : ex);
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
				RepositoryMediator.SyncAsync(@object, context.AliasTypeName).Run();
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
				await RepositoryMediator.UpdateAsync<T>(context, aliasTypeName, @object, dontCreateNewVersion, userID, cancellationToken).ConfigureAwait(false);
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
				if (context.EntityDefinition.Cache != null)
				{
					context.EntityDefinition.Cache.RemoveAsync(@object).Run();
					if (RepositoryMediator.IsDebugEnabled)
						RepositoryMediator.WriteLogs($"DELETE: Remove the cached object from the cache storage successful [{@object.GetCacheKey()}]");
				}

				// call post-handlers
				context.CallPostDeleteHandlers(@object);
				return @object;
			}
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw;
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
				RepositoryMediator.SyncAsync(@object, context.AliasTypeName, false, true).Run();
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
				RepositoryMediator.Delete<T>(context, aliasTypeName, id, userID);
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
				if (context.EntityDefinition.Cache != null)
				{
					context.EntityDefinition.Cache.RemoveAsync(@object).Run();
					if (RepositoryMediator.IsDebugEnabled)
						RepositoryMediator.WriteLogs($"DELETE: Remove the cached object from the cache storage successful [{@object.GetCacheKey()}]");
				}

				// call post-handlers
				await context.CallPostDeleteHandlersAsync(@object, cancellationToken).ConfigureAwait(false);
				return @object;
			}
			catch (OperationCanceledException ex)
			{
				context.Exception = ex;
				throw;
			}
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw;
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
				RepositoryMediator.SyncAsync(@object, context.AliasTypeName, false, true).Run();
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
				await RepositoryMediator.DeleteAsync<T>(context, aliasTypeName, id, userID, cancellationToken).ConfigureAwait(false);
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
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		public static void DeleteMany<T>(RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, string businessRepositoryEntityID = null) where T : class
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
					context.DeleteMany(dataSource, filter, businessRepositoryEntityID, null);
				else if (dataSource.Mode.Equals(RepositoryMode.SQL))
					context.DeleteMany(dataSource, filter, businessRepositoryEntityID);

				// delete other data sources
				RepositoryMediator.SyncAsync(filter, context.AliasTypeName, businessRepositoryEntityID).Run();
			}
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw;
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
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		public static void DeleteMany<T>(RepositoryContext context, string aliasTypeName, IFilterBy<T> filter, string businessRepositoryEntityID = null) where T : class
		{
			context.AliasTypeName = aliasTypeName;
			RepositoryMediator.DeleteMany<T>(context, context.GetPrimaryDataSource(), filter, businessRepositoryEntityID);
		}

		/// <summary>
		/// Deletes many instances of objects
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">Filter expression to filter instances to delete</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		public static void DeleteMany<T>(string aliasTypeName, IFilterBy<T> filter, string businessRepositoryEntityID = null) where T : class
		{
			using (var context = new RepositoryContext())
				RepositoryMediator.DeleteMany<T>(context, aliasTypeName, filter, businessRepositoryEntityID);
		}

		/// <summary>
		/// Deletes many instances of objects
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="filter">Filter expression to filter instances to delete</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		public static async Task DeleteManyAsync<T>(RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, string businessRepositoryEntityID = null, CancellationToken cancellationToken = default) where T : class
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
					await context.DeleteManyAsync(dataSource, filter, businessRepositoryEntityID, null, cancellationToken).ConfigureAwait(false);
				else if (dataSource.Mode.Equals(RepositoryMode.SQL))
					await context.DeleteManyAsync(dataSource, filter, businessRepositoryEntityID, cancellationToken).ConfigureAwait(false);

				// delete other data sources
				RepositoryMediator.SyncAsync(filter, context.AliasTypeName, businessRepositoryEntityID).Run();
			}
			catch (OperationCanceledException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw;
			}
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw;
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
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		public static Task DeleteManyAsync<T>(RepositoryContext context, string aliasTypeName, IFilterBy<T> filter, string businessRepositoryEntityID = null, CancellationToken cancellationToken = default) where T : class
		{
			context.AliasTypeName = aliasTypeName;
			return RepositoryMediator.DeleteManyAsync<T>(context, context.GetPrimaryDataSource(), filter, businessRepositoryEntityID, cancellationToken);
		}

		/// <summary>
		/// Deletes many instances of objects
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">Filter expression to filter instances to delete</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		public static async Task DeleteManyAsync<T>(string aliasTypeName, IFilterBy<T> filter, string businessRepositoryEntityID = null, CancellationToken cancellationToken = default) where T : class
		{
			using (var context = new RepositoryContext())
				await RepositoryMediator.DeleteManyAsync<T>(context, aliasTypeName, filter, businessRepositoryEntityID, cancellationToken).ConfigureAwait(false);
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
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <returns></returns>
		public static List<string> FindIdentities<T>(RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessRepositoryEntityID = null, bool autoAssociateWithMultipleParents = true, string cacheKey = null, int cacheTime = 0) where T : class
		{
			context.Prepare<T>(RepositoryOperation.Query, (dataSource ?? context.GetPrimaryDataSource())?.StartSession<T>());
			try
			{
				// prepare
				dataSource = dataSource ?? context.GetPrimaryDataSource();
				if (dataSource == null)
					throw new InformationInvalidException("Data source is invalid, please check the configuration");

				// find identities
				var identites = !string.IsNullOrWhiteSpace(cacheKey) && context.EntityDefinition.Cache != null
					? context.EntityDefinition.Cache.Get<List<string>>(cacheKey)
					: null;

				if (identites == null)
				{
					identites = dataSource.Mode.Equals(RepositoryMode.NoSQL)
						? context.SelectIdentities(dataSource, filter, sort, pageSize, pageNumber, businessRepositoryEntityID, autoAssociateWithMultipleParents, null)
						: dataSource.Mode.Equals(RepositoryMode.SQL)
							? context.SelectIdentities(dataSource, filter, sort, pageSize, pageNumber, businessRepositoryEntityID, autoAssociateWithMultipleParents)
							: new List<string>();
					if (!string.IsNullOrWhiteSpace(cacheKey) && context.EntityDefinition.Cache != null)
						context.EntityDefinition.Cache.SetAsync(cacheKey, identites, cacheTime).Run();
				}

				return identites;
			}
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw;
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
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <returns></returns>
		public static List<string> FindIdentities<T>(RepositoryContext context, string aliasTypeName, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessRepositoryEntityID = null, bool autoAssociateWithMultipleParents = true, string cacheKey = null, int cacheTime = 0) where T : class
		{
			context.AliasTypeName = aliasTypeName;
			return RepositoryMediator.FindIdentities<T>(context, context.GetPrimaryDataSource(), filter, sort, pageSize, pageNumber, businessRepositoryEntityID, autoAssociateWithMultipleParents, cacheKey, cacheTime);
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
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <returns></returns>
		public static List<string> FindIdentities<T>(string aliasTypeName, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessRepositoryEntityID = null, bool autoAssociateWithMultipleParents = true, string cacheKey = null, int cacheTime = 0) where T : class
		{
			using (var context = new RepositoryContext(false))
				return RepositoryMediator.FindIdentities<T>(context, aliasTypeName, filter, sort, pageSize, pageNumber, businessRepositoryEntityID, autoAssociateWithMultipleParents, cacheKey, cacheTime);
		}

		/// <summary>
		/// Finds the identity of objects
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="filter">Filter expression</param>
		/// <param name="sort">Sort expression</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <returns></returns>
		public static List<string> FindIdentities<T>(IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessRepositoryEntityID = null, bool autoAssociateWithMultipleParents = true, string cacheKey = null, int cacheTime = 0) where T : class
			=> RepositoryMediator.FindIdentities<T>(null, filter, sort, pageSize, pageNumber, businessRepositoryEntityID, autoAssociateWithMultipleParents, cacheKey, cacheTime);

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
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <returns></returns>
		public static List<T> Find<T>(RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessRepositoryEntityID = null, bool autoAssociateWithMultipleParents = true, string cacheKey = null, int cacheTime = 0) where T : class
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
					: RepositoryMediator.FindIdentities<T>(context, dataSource, filter, sort, pageSize, pageNumber, businessRepositoryEntityID, autoAssociateWithMultipleParents, cacheKey, cacheTime);

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
						var results = identities.Where(id => !string.IsNullOrWhiteSpace(id)).ToDictionary(id => id, id => default(T), StringComparer.OrdinalIgnoreCase);

						// add cached objects
						var ids = new List<string>();
						cached.ForEach(kvp =>
						{
							var id = kvp.Value.GetEntityID();
							if (!string.IsNullOrWhiteSpace(id))
							{
								ids.Add(id);
								results[id] = kvp.Value;
							}
						});

						// find missing objects
						identities = identities.Where(id => !string.IsNullOrWhiteSpace(id)).Except(ids, StringComparer.OrdinalIgnoreCase).ToList();
						if (identities.Count > 0)
						{
							var missing = dataSource.Mode.Equals(RepositoryMode.NoSQL)
								? context.Find(dataSource, identities, sort, businessRepositoryEntityID, null)
								: dataSource.Mode.Equals(RepositoryMode.SQL)
									? context.Find(dataSource, identities, sort, businessRepositoryEntityID)
									: new List<T>();

							// update results & cache
							missing.Where(@object => @object != null).ForEach(@object => results[@object.GetEntityID()] = @object);
							if (context.EntityDefinition.Cache != null)
							{
								context.EntityDefinition.Cache.SetAsync(missing, 0).Run();
								if (RepositoryMediator.IsDebugEnabled)
									RepositoryMediator.WriteLogs($"FIND: Add {missing.Count} missing object(s) into cache storage successful [{missing.Select(o => o.GetCacheKey()).ToString(" - ")}]");
							}
						}

						// update the collection of objects
						objects = results.Select(kvp => kvp.Value).ToList();
					}
				}

				// find missing objects
				if (objects == null)
				{
					objects = identities == null || identities.Count > 0
						? dataSource.Mode.Equals(RepositoryMode.NoSQL)
							? context.Find(dataSource, filter, sort, pageSize, pageNumber, businessRepositoryEntityID, autoAssociateWithMultipleParents, null)
							: dataSource.Mode.Equals(RepositoryMode.SQL)
								? context.Find(dataSource, filter, sort, pageSize, pageNumber, businessRepositoryEntityID, autoAssociateWithMultipleParents)
								: new List<T>()
						: new List<T>();

					// update results & cache
					if (context.EntityDefinition.Cache != null && objects.Count > 0)
					{
						if (!string.IsNullOrWhiteSpace(cacheKey))
							context.EntityDefinition.Cache.SetAsync(cacheKey, objects.Select(@object => @object.GetEntityID()).ToList(), cacheTime < 1 ? context.EntityDefinition.Cache.ExpirationTime / 2 : cacheTime).Run();
						context.EntityDefinition.Cache.SetAsync(objects, 0).Run();
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
				throw;
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
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <returns></returns>
		public static List<T> Find<T>(RepositoryContext context, string aliasTypeName, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessRepositoryEntityID = null, bool autoAssociateWithMultipleParents = true, string cacheKey = null, int cacheTime = 0) where T : class
		{
			context.AliasTypeName = aliasTypeName;
			return RepositoryMediator.Find<T>(context, context.GetPrimaryDataSource(), filter, sort, pageSize, pageNumber, businessRepositoryEntityID, autoAssociateWithMultipleParents, cacheKey, cacheTime);
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
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <returns></returns>
		public static List<T> Find<T>(string aliasTypeName, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessRepositoryEntityID = null, bool autoAssociateWithMultipleParents = true, string cacheKey = null, int cacheTime = 0) where T : class
		{
			using (var context = new RepositoryContext(false))
				return RepositoryMediator.Find<T>(context, aliasTypeName, filter, sort, pageSize, pageNumber, businessRepositoryEntityID, autoAssociateWithMultipleParents, cacheKey, cacheTime);
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
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<List<string>> FindIdentitiesAsync<T>(RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessRepositoryEntityID = null, bool autoAssociateWithMultipleParents = true, string cacheKey = null, int cacheTime = 0, CancellationToken cancellationToken = default) where T : class
		{
			context.Prepare<T>(RepositoryOperation.Query, (dataSource ?? context.GetPrimaryDataSource())?.StartSession<T>());
			try
			{
				// prepare
				dataSource = dataSource ?? context.GetPrimaryDataSource();
				if (dataSource == null)
					throw new InformationInvalidException("Data source is invalid, please check the configuration");

				// find identities
				var identites = !string.IsNullOrWhiteSpace(cacheKey) && context.EntityDefinition.Cache != null
					? await context.EntityDefinition.Cache.GetAsync<List<string>>(cacheKey, cancellationToken).ConfigureAwait(false)
					: null;

				if (identites == null)
				{
					identites = dataSource.Mode.Equals(RepositoryMode.NoSQL)
						? await context.SelectIdentitiesAsync(dataSource, filter, sort, pageSize, pageNumber, businessRepositoryEntityID, autoAssociateWithMultipleParents, null, cancellationToken)
						: dataSource.Mode.Equals(RepositoryMode.SQL)
							? await context.SelectIdentitiesAsync(dataSource, filter, sort, pageSize, pageNumber, businessRepositoryEntityID, autoAssociateWithMultipleParents, cancellationToken)
							: new List<string>();
					if (!string.IsNullOrWhiteSpace(cacheKey) && context.EntityDefinition.Cache != null)
						context.EntityDefinition.Cache.SetAsync(cacheKey, identites, cacheTime).Run();
				}

				return identites;
			}
			catch (OperationCanceledException ex)
			{
				context.Exception = ex;
				throw;
			}
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw;
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
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<List<string>> FindIdentitiesAsync<T>(RepositoryContext context, string aliasTypeName, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessRepositoryEntityID = null, bool autoAssociateWithMultipleParents = true, string cacheKey = null, int cacheTime = 0, CancellationToken cancellationToken = default) where T : class
		{
			context.AliasTypeName = aliasTypeName;
			return await RepositoryMediator.FindIdentitiesAsync<T>(context, context.GetPrimaryDataSource(), filter, sort, pageSize, pageNumber, businessRepositoryEntityID, autoAssociateWithMultipleParents, cacheKey, cacheTime, cancellationToken).ConfigureAwait(false);
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
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<List<string>> FindIdentitiesAsync<T>(string aliasTypeName, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessRepositoryEntityID = null, bool autoAssociateWithMultipleParents = true, string cacheKey = null, int cacheTime = 0, CancellationToken cancellationToken = default) where T : class
		{
			using (var context = new RepositoryContext(false))
				return await RepositoryMediator.FindIdentitiesAsync<T>(context, aliasTypeName, filter, sort, pageSize, pageNumber, businessRepositoryEntityID, autoAssociateWithMultipleParents, cacheKey, cacheTime, cancellationToken).ConfigureAwait(false);
		}

		/// <summary>
		/// Finds the identity of objects
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="filter">Filter expression</param>
		/// <param name="sort">Sort expression</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<List<string>> FindIdentitiesAsync<T>(IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessRepositoryEntityID = null, bool autoAssociateWithMultipleParents = true, string cacheKey = null, int cacheTime = 0, CancellationToken cancellationToken = default) where T : class
			=> RepositoryMediator.FindIdentitiesAsync<T>(null, filter, sort, pageSize, pageNumber, businessRepositoryEntityID, autoAssociateWithMultipleParents, cacheKey, cacheTime, cancellationToken);

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
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<List<T>> FindAsync<T>(RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessRepositoryEntityID = null, bool autoAssociateWithMultipleParents = true, string cacheKey = null, int cacheTime = 0, CancellationToken cancellationToken = default) where T : class
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
					: await RepositoryMediator.FindIdentitiesAsync<T>(context, dataSource, filter, sort, pageSize, pageNumber, businessRepositoryEntityID, autoAssociateWithMultipleParents, cacheKey, cacheTime, cancellationToken).ConfigureAwait(false);

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
						var results = identities.Where(id => !string.IsNullOrWhiteSpace(id)).ToDictionary(id => id, id => default(T), StringComparer.OrdinalIgnoreCase);

						// add cached objects
						var ids = new List<string>();
						cached.ForEach(kvp =>
						{
							var id = kvp.Value.GetEntityID();
							if (!string.IsNullOrWhiteSpace(id))
							{
								ids.Add(id);
								results[id] = kvp.Value;
							}
						});

						// find missing objects
						identities = identities.Where(id => !string.IsNullOrWhiteSpace(id)).Except(ids, StringComparer.OrdinalIgnoreCase).ToList();
						if (identities.Count > 0)
						{
							var missing = dataSource.Mode.Equals(RepositoryMode.NoSQL)
								? await context.FindAsync(dataSource, identities, sort, businessRepositoryEntityID, null, cancellationToken).ConfigureAwait(false)
								: dataSource.Mode.Equals(RepositoryMode.SQL)
									? await context.FindAsync(dataSource, identities, sort, businessRepositoryEntityID, cancellationToken).ConfigureAwait(false)
									: new List<T>();

							// update results & cache
							missing.Where(@object => @object != null).ForEach(@object => results[@object.GetEntityID()] = @object);
							if (context.EntityDefinition.Cache != null)
							{
								context.EntityDefinition.Cache.SetAsync(missing, 0).Run();
								if (RepositoryMediator.IsDebugEnabled)
									RepositoryMediator.WriteLogs($"FIND: Add {missing.Count} missing object(s) into cache storage successful [{missing.Select(o => o.GetCacheKey()).ToString(" - ")}]");
							}
						}

						// update the collection of objects
						objects = results.Select(kvp => kvp.Value).ToList();
					}
				}

				// fetch objects if has no cache
				if (objects == null)
				{
					objects = identities == null || identities.Count > 0
						? dataSource.Mode.Equals(RepositoryMode.NoSQL)
							? await context.FindAsync(dataSource, filter, sort, pageSize, pageNumber, businessRepositoryEntityID, autoAssociateWithMultipleParents, null, cancellationToken).ConfigureAwait(false)
							: dataSource.Mode.Equals(RepositoryMode.SQL)
								? await context.FindAsync(dataSource, filter, sort, pageSize, pageNumber, businessRepositoryEntityID, autoAssociateWithMultipleParents, cancellationToken).ConfigureAwait(false)
								: new List<T>()
						: new List<T>();

					if (context.EntityDefinition.Cache != null && objects.Count > 0)
					{
						if (!string.IsNullOrWhiteSpace(cacheKey))
							context.EntityDefinition.Cache.SetAsync(cacheKey, objects.Select(@object => @object.GetEntityID()).ToList(), cacheTime < 1 ? context.EntityDefinition.Cache.ExpirationTime / 2 : cacheTime).Run();
						context.EntityDefinition.Cache.SetAsync(objects, 0).Run();
						if (RepositoryMediator.IsDebugEnabled)
							RepositoryMediator.WriteLogs($"FIND: Add {objects.Count} raw object(s) into cache storage successful [{objects.Select(o => o.GetCacheKey()).ToString(" - ")}]");
					}
				}

				return objects;
			}
			catch (OperationCanceledException ex)
			{
				context.Exception = ex;
				throw;
			}
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw;
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
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<List<T>> FindAsync<T>(RepositoryContext context, string aliasTypeName, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessRepositoryEntityID = null, bool autoAssociateWithMultipleParents = true, string cacheKey = null, int cacheTime = 0, CancellationToken cancellationToken = default) where T : class
		{
			context.AliasTypeName = aliasTypeName;
			return RepositoryMediator.FindAsync<T>(context, context.GetPrimaryDataSource(), filter, sort, pageSize, pageNumber, businessRepositoryEntityID, autoAssociateWithMultipleParents, cacheKey, cacheTime, cancellationToken);
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
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<List<T>> FindAsync<T>(string aliasTypeName, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessRepositoryEntityID = null, bool autoAssociateWithMultipleParents = true, string cacheKey = null, int cacheTime = 0, CancellationToken cancellationToken = default) where T : class
		{
			using (var context = new RepositoryContext(false))
				return await RepositoryMediator.FindAsync<T>(context, aliasTypeName, filter, sort, pageSize, pageNumber, businessRepositoryEntityID, autoAssociateWithMultipleParents, cacheKey, cacheTime, cancellationToken).ConfigureAwait(false);
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
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of total number of objects</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <returns>The integer number that presents total of objects that matched with the filter expression</returns>
		public static long Count<T>(RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, string businessRepositoryEntityID = null, bool autoAssociateWithMultipleParents = true, string cacheKey = null, int cacheTime = 0) where T : class
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
					? context.Count(dataSource, filter, businessRepositoryEntityID, autoAssociateWithMultipleParents, null)
					: dataSource.Mode.Equals(RepositoryMode.SQL)
						? context.Count(dataSource, filter, businessRepositoryEntityID, autoAssociateWithMultipleParents)
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
					context.EntityDefinition.Cache.SetAsync(cacheKey, total, cacheTime < 1 ? context.EntityDefinition.Cache.ExpirationTime / 2 : cacheTime).Run();

				return total;
			}
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw;
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
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of total number of objects</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <returns>The integer number that presents total of objects that matched with the filter expression</returns>
		public static long Count<T>(RepositoryContext context, string aliasTypeName, IFilterBy<T> filter, string businessRepositoryEntityID = null, bool autoAssociateWithMultipleParents = true, string cacheKey = null, int cacheTime = 0) where T : class
		{
			context.AliasTypeName = aliasTypeName;
			return RepositoryMediator.Count<T>(context, context.GetPrimaryDataSource(), filter, businessRepositoryEntityID, autoAssociateWithMultipleParents, cacheKey, cacheTime);
		}

		/// <summary>
		/// Counts objects
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">Filter expression</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of total number of objects</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <returns>The integer number that presents total of objects that matched with the filter expression</returns>
		public static long Count<T>(string aliasTypeName, IFilterBy<T> filter, string businessRepositoryEntityID = null, bool autoAssociateWithMultipleParents = true, string cacheKey = null, int cacheTime = 0) where T : class
		{
			using (var context = new RepositoryContext(false))
				return RepositoryMediator.Count<T>(context, aliasTypeName, filter, businessRepositoryEntityID, autoAssociateWithMultipleParents, cacheKey, cacheTime);
		}

		/// <summary>
		/// Counts objects
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="filter">Filter expression</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of total number of objects</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The integer number that presents total of objects that matched with the filter expression</returns>
		public static async Task<long> CountAsync<T>(RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, string businessRepositoryEntityID = null, bool autoAssociateWithMultipleParents = true, string cacheKey = null, int cacheTime = 0, CancellationToken cancellationToken = default) where T : class
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
					? await context.CountAsync(dataSource, filter, businessRepositoryEntityID, autoAssociateWithMultipleParents, null, cancellationToken).ConfigureAwait(false)
					: dataSource.Mode.Equals(RepositoryMode.SQL)
						? await context.CountAsync(dataSource, filter, businessRepositoryEntityID, autoAssociateWithMultipleParents, cancellationToken).ConfigureAwait(false)
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
					context.EntityDefinition.Cache.SetAsync(cacheKey, total, cacheTime < 1 ? context.EntityDefinition.Cache.ExpirationTime / 2 : cacheTime).Run();

				return total;
			}
			catch (OperationCanceledException ex)
			{
				context.Exception = ex;
				throw;
			}
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw;
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
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of total number of objects</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The integer number that presents total of objects that matched with the filter expression</returns>
		public static Task<long> CountAsync<T>(RepositoryContext context, string aliasTypeName, IFilterBy<T> filter, string businessRepositoryEntityID = null, bool autoAssociateWithMultipleParents = true, string cacheKey = null, int cacheTime = 0, CancellationToken cancellationToken = default) where T : class
		{
			context.AliasTypeName = aliasTypeName;
			return RepositoryMediator.CountAsync<T>(context, context.GetPrimaryDataSource(), filter, businessRepositoryEntityID, autoAssociateWithMultipleParents, cacheKey, cacheTime, cancellationToken);
		}

		/// <summary>
		/// Counts objects
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">Filter expression</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of total number of objects</param>
		/// <param name="cacheTime">The number that presents the time for caching (in minutes)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The integer number that presents total of objects that matched with the filter expression</returns>
		public static async Task<long> CountAsync<T>(string aliasTypeName, IFilterBy<T> filter, string businessRepositoryEntityID = null, bool autoAssociateWithMultipleParents = true, string cacheKey = null, int cacheTime = 0, CancellationToken cancellationToken = default) where T : class
		{
			using (var context = new RepositoryContext(false))
				return await RepositoryMediator.CountAsync<T>(context, aliasTypeName, filter, businessRepositoryEntityID, autoAssociateWithMultipleParents, cacheKey, cacheTime, cancellationToken).ConfigureAwait(false);
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
		/// <param name="filter">The object that presents other filtering expression to combine with seaching query</param>
		/// <param name="sort">The object that presents other sorting expression to combine with seaching score</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns></returns>
		public static List<T> Search<T>(RepositoryContext context, DataSource dataSource, string query, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessRepositoryEntityID = null) where T : class
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
						? context.Search(dataSource, query, filter, sort, pageSize, pageNumber, businessRepositoryEntityID, null)
						: dataSource.Mode.Equals(RepositoryMode.SQL)
							? context.Search(dataSource, query, filter, sort, pageSize, pageNumber, businessRepositoryEntityID)
							: new List<T>();

				// search identities
				var identities = dataSource.Mode.Equals(RepositoryMode.NoSQL)
					? context.SearchIdentities(dataSource, query, filter, sort, pageSize, pageNumber, businessRepositoryEntityID, null)
					: dataSource.Mode.Equals(RepositoryMode.SQL)
						? context.SearchIdentities(dataSource, query, filter, sort, pageSize, pageNumber, businessRepositoryEntityID)
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
					var results = identities.Where(id => !string.IsNullOrWhiteSpace(id)).ToDictionary(id => id, id => default(T), StringComparer.OrdinalIgnoreCase);

					// add cached objects
					var ids = new List<string>();
					cached.Where(kvp => kvp.Value != null).ForEach(kvp =>
					{
						var id = kvp.Value.GetEntityID();
						if (!string.IsNullOrWhiteSpace(id))
						{
							ids.Add(id);
							results[id] = kvp.Value as T;
						}
					});

					// find missing objects
					identities = identities.Where(id => !string.IsNullOrWhiteSpace(id)).Except(ids, StringComparer.OrdinalIgnoreCase).ToList();
					if (identities.Count > 0)
					{
						var missing = dataSource.Mode.Equals(RepositoryMode.NoSQL)
							? context.Find<T>(dataSource, identities, null, businessRepositoryEntityID, null)
							: dataSource.Mode.Equals(RepositoryMode.SQL)
								? context.Find<T>(dataSource, identities, null, businessRepositoryEntityID)
								: new List<T>();

						// update results & cache
						missing.Where(@object => @object != null).ForEach(@object => results[@object.GetEntityID()] = @object);
						context.EntityDefinition.Cache.SetAsync(missing, 0).Run();
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
					? context.Search(dataSource, query, filter, sort, pageSize, pageNumber, businessRepositoryEntityID, null)
					: dataSource.Mode.Equals(RepositoryMode.SQL)
						? context.Search(dataSource, query, filter, sort, pageSize, pageNumber, businessRepositoryEntityID)
						: new List<T>();

				if (objects.Count > 0)
				{
					context.EntityDefinition.Cache.SetAsync(objects, 0).Run();
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
				throw;
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
		/// <param name="filter">The object that presents other filtering expression to combine with seaching query</param>
		/// <param name="sort">The object that presents other sorting expression to combine with seaching score</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns></returns>
		public static List<T> Search<T>(RepositoryContext context, string aliasTypeName, string query, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessRepositoryEntityID = null) where T : class
		{
			context.AliasTypeName = aliasTypeName;
			return RepositoryMediator.Search<T>(context, context.GetPrimaryDataSource(), query, filter, sort, pageSize, pageNumber, businessRepositoryEntityID);
		}

		/// <summary>
		/// Searchs objects (using full-text search)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="query">The searching query (like Google searching query)</param>
		/// <param name="filter">The object that presents other filtering expression to combine with seaching query</param>
		/// <param name="sort">The object that presents other sorting expression to combine with seaching score</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns></returns>
		public static List<T> Search<T>(string aliasTypeName, string query, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessRepositoryEntityID = null) where T : class
		{
			using (var context = new RepositoryContext(false))
				return RepositoryMediator.Search<T>(context, aliasTypeName, query, filter, sort, pageSize, pageNumber, businessRepositoryEntityID);
		}

		/// <summary>
		/// Searchs objects (using full-text search)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="query">The searching query (like Google searching query)</param>
		/// <param name="filter">The object that presents other filtering expression to combine with seaching query</param>
		/// <param name="sort">The object that presents other sorting expression to combine with seaching score</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<List<T>> SearchAsync<T>(RepositoryContext context, DataSource dataSource, string query, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessRepositoryEntityID = null, CancellationToken cancellationToken = default) where T : class
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
						? await context.SearchAsync(dataSource, query, filter, sort, pageSize, pageNumber, businessRepositoryEntityID, null, cancellationToken).ConfigureAwait(false)
						: dataSource.Mode.Equals(RepositoryMode.SQL)
							? await context.SearchAsync(dataSource, query, filter, sort, pageSize, pageNumber, businessRepositoryEntityID, cancellationToken).ConfigureAwait(false)
							: new List<T>();

				// search identities
				var identities = dataSource.Mode.Equals(RepositoryMode.NoSQL)
					? await context.SearchIdentitiesAsync(dataSource, query, filter, sort, pageSize, pageNumber, businessRepositoryEntityID, null, cancellationToken).ConfigureAwait(false)
					: dataSource.Mode.Equals(RepositoryMode.SQL)
						? await context.SearchIdentitiesAsync(dataSource, query, filter, sort, pageSize, pageNumber, businessRepositoryEntityID, cancellationToken).ConfigureAwait(false)
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
					var results = identities.Where(id => !string.IsNullOrWhiteSpace(id)).ToDictionary(id => id, id => default(T), StringComparer.OrdinalIgnoreCase);

					// add cached objects
					var ids = new List<string>();
					cached.Where(kvp => kvp.Value != null).ForEach(kvp =>
					{
						var id = kvp.Value.GetEntityID();
						if (!string.IsNullOrWhiteSpace(id))
						{
							ids.Add(id);
							results[id] = kvp.Value as T;
						}
					});

					// find missing objects
					identities = identities.Where(id => !string.IsNullOrWhiteSpace(id)).Except(ids, StringComparer.OrdinalIgnoreCase).ToList();
					if (identities.Count > 0)
					{
						var missing = dataSource.Mode.Equals(RepositoryMode.NoSQL)
							? await context.FindAsync<T>(dataSource, identities, null, businessRepositoryEntityID, null, cancellationToken).ConfigureAwait(false)
							: dataSource.Mode.Equals(RepositoryMode.SQL)
								? await context.FindAsync<T>(dataSource, identities, null, businessRepositoryEntityID, cancellationToken).ConfigureAwait(false)
								: new List<T>();

						// update results & cache
						missing.Where(@object => @object != null).ForEach(@object => results[@object.GetEntityID()] = @object);
						context.EntityDefinition.Cache.SetAsync(missing, 0).Run();
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
					? await context.SearchAsync(dataSource, query, filter, sort, pageSize, pageNumber, businessRepositoryEntityID, null, cancellationToken).ConfigureAwait(false)
					: dataSource.Mode.Equals(RepositoryMode.SQL)
						? await context.SearchAsync(dataSource, query, filter, sort, pageSize, pageNumber, businessRepositoryEntityID, cancellationToken).ConfigureAwait(false)
						: new List<T>();

				if (objects.Count > 0)
				{
					context.EntityDefinition.Cache.SetAsync(objects, 0).Run();
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
				throw;
			}
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw;
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
		/// <param name="filter">The object that presents other filtering expression to combine with seaching query</param>
		/// <param name="sort">The object that presents other sorting expression to combine with seaching score</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<List<T>> SearchAsync<T>(RepositoryContext context, string aliasTypeName, string query, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessRepositoryEntityID = null, CancellationToken cancellationToken = default) where T : class
		{
			context.AliasTypeName = aliasTypeName;
			return await RepositoryMediator.SearchAsync<T>(context, context.GetPrimaryDataSource(), query, filter, sort, pageSize, pageNumber, businessRepositoryEntityID, cancellationToken).ConfigureAwait(false);
		}

		/// <summary>
		/// Searchs objects (using full-text search)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="query">The searching query (like Google searching query)</param>
		/// <param name="filter">The object that presents other filtering expression to combine with seaching query</param>
		/// <param name="sort">The object that presents other sorting expression to combine with seaching score</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<List<T>> SearchAsync<T>(string aliasTypeName, string query, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessRepositoryEntityID = null, CancellationToken cancellationToken = default) where T : class
		{
			using (var context = new RepositoryContext(false))
				return await RepositoryMediator.SearchAsync<T>(context, aliasTypeName, query, filter, sort, pageSize, pageNumber, businessRepositoryEntityID, cancellationToken).ConfigureAwait(false);
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
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns>The integer number that presents total of objects that matched with searching query (and filter expression)</returns>
		public static long Count<T>(RepositoryContext context, DataSource dataSource, string query, IFilterBy<T> filter, string businessRepositoryEntityID = null) where T : class
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
					? context.Count(dataSource, query, filter, businessRepositoryEntityID, null)
					: dataSource.Mode.Equals(RepositoryMode.SQL)
						? context.Count(dataSource, query, filter, businessRepositoryEntityID)
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
				throw;
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
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns>The integer number that presents total of objects that matched with searching query (and filter expression)</returns>
		public static long Count<T>(RepositoryContext context, string aliasTypeName, string query, IFilterBy<T> filter, string businessRepositoryEntityID = null) where T : class
		{
			context.AliasTypeName = aliasTypeName;
			return RepositoryMediator.Count<T>(context, context.GetPrimaryDataSource(), query, filter, businessRepositoryEntityID);
		}

		/// <summary>
		/// Counts objects (using full-text search)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="query">The searching query (like Google searching query)</param>
		/// <param name="filter">The object that presents other filter expression to combine with seaching query</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns>The integer number that presents total of objects that matched with searching query (and filter expression)</returns>
		public static long Count<T>(string aliasTypeName, string query, IFilterBy<T> filter, string businessRepositoryEntityID = null) where T : class
		{
			using (var context = new RepositoryContext(false))
				return RepositoryMediator.Count<T>(context, aliasTypeName, query, filter, businessRepositoryEntityID);
		}

		/// <summary>
		/// Counts objects (using full-text search)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="query">The searching query (like Google searching query)</param>
		/// <param name="filter">The object that presents other filter expression to combine with seaching query</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The integer number that presents total of objects that matched with searching query (and filter expression)</returns>
		public static async Task<long> CountAsync<T>(RepositoryContext context, DataSource dataSource, string query, IFilterBy<T> filter, string businessRepositoryEntityID = null, CancellationToken cancellationToken = default) where T : class
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
					? await context.CountAsync(dataSource, query, filter, businessRepositoryEntityID, null, cancellationToken).ConfigureAwait(false)
					: dataSource.Mode.Equals(RepositoryMode.SQL)
						? await context.CountAsync(dataSource, query, filter, businessRepositoryEntityID, cancellationToken).ConfigureAwait(false)
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
				throw;
			}
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw;
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
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The integer number that presents total of objects that matched with searching query (and filter expression)</returns>
		public static Task<long> CountAsync<T>(RepositoryContext context, string aliasTypeName, string query, IFilterBy<T> filter, string businessRepositoryEntityID = null, CancellationToken cancellationToken = default) where T : class
		{
			context.AliasTypeName = aliasTypeName;
			return RepositoryMediator.CountAsync<T>(context, context.GetPrimaryDataSource(), query, filter, businessRepositoryEntityID, cancellationToken);
		}

		/// <summary>
		/// Counts objects (using full-text search)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="query">The searching query (like Google searching query)</param>
		/// <param name="filter">The object that presents other filter expression to combine with seaching query</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The integer number that presents total of objects that matched with searching query (and filter expression)</returns>
		public static async Task<long> CountAsync<T>(string aliasTypeName, string query, IFilterBy<T> filter, string businessRepositoryEntityID = null, CancellationToken cancellationToken = default) where T : class
		{
			using (var context = new RepositoryContext(false))
				return await RepositoryMediator.CountAsync<T>(context, aliasTypeName, query, filter, businessRepositoryEntityID, cancellationToken).ConfigureAwait(false);
		}
		#endregion

		#region Create version content
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

				context.EntityDefinition = context.EntityDefinition ?? RepositoryMediator.GetEntityDefinition<T>();
				dataSource = dataSource ?? context.GetVersionDataSource();
				if (dataSource == null)
					return null;

				// prepare
				var version = VersionContent.Prepare(@object);
				var filter = !string.IsNullOrWhiteSpace(version.ServiceName) || !string.IsNullOrWhiteSpace(version.RepositoryEntityID)
					? Filters<VersionContent>.And(
							Filters<VersionContent>.Equals("ObjectID", version.ObjectID),
							!string.IsNullOrWhiteSpace(version.RepositoryEntityID) ? Filters<VersionContent>.Equals("RepositoryEntityID", version.RepositoryEntityID) : Filters<VersionContent>.Equals("ServiceName", version.ServiceName)
						) as IFilterBy<VersionContent>
					: Filters<VersionContent>.Equals("ObjectID", version.ObjectID);
				var latest = VersionContent.Find<VersionContent>(dataSource, "Versions", filter, Sorts<VersionContent>.Descending("VersionNumber"), 1, 1);
				version.VersionNumber = latest != null && latest.Count > 0 ? latest[0].VersionNumber + 1 : 1;
				version.CreatedID = userID ?? "";

				// create new
				version = VersionContent.Create(dataSource, "Versions", version);
				context.EntityDefinition.Cache?.Remove($"{@object.GetCacheKey()}:Versions");
				return version;
			}
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw;
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
				return RepositoryMediator.CreateVersion<T>(context, @object, userID);
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

				context.EntityDefinition = context.EntityDefinition ?? RepositoryMediator.GetEntityDefinition<T>();
				dataSource = dataSource ?? context.GetVersionDataSource();
				if (dataSource == null)
					return null;

				// prepare
				var version = VersionContent.Prepare(@object);
				var filter = !string.IsNullOrWhiteSpace(version.ServiceName) || !string.IsNullOrWhiteSpace(version.RepositoryEntityID)
					? Filters<VersionContent>.And(
							Filters<VersionContent>.Equals("ObjectID", version.ObjectID),
							!string.IsNullOrWhiteSpace(version.RepositoryEntityID) ? Filters<VersionContent>.Equals("RepositoryEntityID", version.RepositoryEntityID) : Filters<VersionContent>.Equals("ServiceName", version.ServiceName)
						) as IFilterBy<VersionContent>
					: Filters<VersionContent>.Equals("ObjectID", version.ObjectID);
				var latest = await VersionContent.FindAsync<VersionContent>(dataSource, "Versions", filter, Sorts<VersionContent>.Descending("VersionNumber"), 1, 1, cancellationToken).ConfigureAwait(false);
				version.VersionNumber = latest != null && latest.Count > 0 ? latest[0].VersionNumber + 1 : 1;
				version.CreatedID = userID ?? "";

				// create new
				version = await VersionContent.CreateAsync(dataSource, "Versions", version, cancellationToken).ConfigureAwait(false);
				if (context.EntityDefinition.Cache != null)
					await context.EntityDefinition.Cache.RemoveAsync($"{@object.GetCacheKey()}:Versions", cancellationToken).ConfigureAwait(false);
				return version;
			}
			catch (OperationCanceledException ex)
			{
				context.Exception = ex;
				throw;
			}
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw;
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
				return await RepositoryMediator.CreateVersionAsync<T>(context, @object, userID, cancellationToken).ConfigureAwait(false);
		}
		#endregion

		#region Get version content
		/// <summary>
		/// Get a version content
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="id">The identity of versioning object</param>
		public static VersionContent GetVersion<T>(RepositoryContext context, DataSource dataSource, string id) where T : class
		{
			context.Prepare<T>(RepositoryOperation.Query, (dataSource ?? context.GetVersionDataSource())?.StartSession<T>());
			try
			{
				context.EntityDefinition = context.EntityDefinition ?? RepositoryMediator.GetEntityDefinition<T>();
				dataSource = dataSource ?? context.GetVersionDataSource();
				return dataSource != null ? VersionContent.GetByID<VersionContent>(dataSource, "Versions", id) : null;
			}
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw new RepositoryOperationException("Error occurred while getting a version content", ex);
			}
		}

		/// <summary>
		/// Get a version content
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		public static VersionContent GetVersion<T>(RepositoryContext context, string id) where T : class
			=> RepositoryMediator.GetVersion<T>(context, null, id);

		/// <summary>
		/// Get a version content
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="id">The identity of versioning object</param>
		public static VersionContent GetVersion<T>(string id) where T : class
		{
			using (var context = new RepositoryContext())
				return RepositoryMediator.GetVersion<T>(context, id);
		}

		/// <summary>
		/// Get a version content
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="id">The identity of versioning object</param>
		/// <param name="cancellationToken">The cancellation token</param>
		public static async Task<VersionContent> GetVersionAsync<T>(RepositoryContext context, DataSource dataSource, string id, CancellationToken cancellationToken = default) where T : class
		{
			context.Prepare<T>(RepositoryOperation.Query, (dataSource ?? context.GetVersionDataSource())?.StartSession<T>());
			try
			{
				context.EntityDefinition = context.EntityDefinition ?? RepositoryMediator.GetEntityDefinition<T>();
				dataSource = dataSource ?? context.GetVersionDataSource();
				return dataSource != null ? await VersionContent.GetByIDAsync<VersionContent>(dataSource, "Versions", id, cancellationToken).ConfigureAwait(false) : null;
			}
			catch (OperationCanceledException ex)
			{
				context.Exception = ex;
				throw;
			}
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw new RepositoryOperationException("Error occurred while getting a version content", ex);
			}
		}

		/// <summary>
		/// Get a version content
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="id">The identity of versioning object</param>
		/// <param name="cancellationToken">The cancellation token</param>
		public static Task<VersionContent> GetVersionAsync<T>(RepositoryContext context, string id, CancellationToken cancellationToken = default) where T : class
			=> RepositoryMediator.GetVersionAsync<T>(context, null, id, cancellationToken);

		/// <summary>
		/// Get a version content
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="id">The identity of versioning object</param>
		/// <param name="cancellationToken">The cancellation token</param>
		public static async Task<VersionContent> GetVersionAsync<T>(string id, CancellationToken cancellationToken = default) where T : class
		{
			using (var context = new RepositoryContext())
				return await RepositoryMediator.GetVersionAsync<T>(context, id, cancellationToken).ConfigureAwait(false);
		}
		#endregion

		#region Rollback from version content
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
			if (version == null || version.Object == null)
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
				if (@object is RepositoryBase baseObject)
					baseObject.SearchScore = null;
				RepositoryMediator.CreateVersion(context, @object, !string.IsNullOrWhiteSpace(userID) && userID.IsValidUUID() ? userID : null);

				// audits
				if (version.Object is IBusinessEntity && !string.IsNullOrWhiteSpace(userID) && userID.IsValidUUID())
					try
					{
						version.Object.SetAttributeValue("_LastModified", DateTime.Now);
						version.Object.SetAttributeValue("_LastModifiedID", userID);
					}
					catch (Exception ex)
					{
						RepositoryMediator.WriteLogs($"ROLLBACK: Cannot update time and identity of user who performs the rollback action [{(version.Object as T).GetCacheKey(false)}] => {ex.Message}", ex);
					}

				// rollback (update) with original object
				if (dataSource.Mode.Equals(RepositoryMode.NoSQL))
					context.Replace(dataSource, version.Object as T, null);
				else if (dataSource.Mode.Equals(RepositoryMode.SQL))
					context.Replace(dataSource, version.Object as T);

				// update into cache storage
				if (context.EntityDefinition.Cache != null && context.EntityDefinition.Cache.Set(version.Object as T) && RepositoryMediator.IsDebugEnabled)
					RepositoryMediator.WriteLogs($"ROLLBACK: Add the object into the cache storage successful [{(version.Object as T).GetCacheKey(false)}]");

				// call post-handlers
				context.CallPostUpdateHandlers(version.Object as T, changed, true);

				// update other data sources
				Task.Run(() => RepositoryMediator.SyncAsync(version.Object as T, context.AliasTypeName)).ConfigureAwait(false);

				// notify changed
				(version.Object as RepositoryBase)?.NotifyPropertyChanged("_Restored");

				// return the original object
				return version.Object as T;
			}
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw;
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
				return RepositoryMediator.Rollback<T>(context, version, userID);
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
			if (string.IsNullOrWhiteSpace(versionID))
				throw new ArgumentNullException(nameof(versionID), "The identity of version content is invalid");

			context.EntityDefinition = context.EntityDefinition ?? RepositoryMediator.GetEntityDefinition<T>();
			var versions = VersionContent.Find(context.GetVersionDataSource(), "Versions", Filters<VersionContent>.Equals("ID", versionID), null, 0, 1);
			return versions != null && versions.Count > 0
				? RepositoryMediator.Rollback<T>(context, versions.First(), userID)
				: throw new InformationInvalidException("The identity of version content is invalid");
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
				return RepositoryMediator.Rollback<T>(context, versionID, userID);
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
			if (version == null || version.Object == null)
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
				if (@object is RepositoryBase baseObject)
					baseObject.SearchScore = null;
				await RepositoryMediator.CreateVersionAsync(context, @object, !string.IsNullOrWhiteSpace(userID) && userID.IsValidUUID() ? userID : null, cancellationToken).ConfigureAwait(false);

				// audits
				if (version.Object is IBusinessEntity && !string.IsNullOrWhiteSpace(userID) && userID.IsValidUUID())
					try
					{
						version.Object.SetAttributeValue("_LastModified", DateTime.Now);
						version.Object.SetAttributeValue("_LastModifiedID", userID);
					}
					catch (Exception ex)
					{
						RepositoryMediator.WriteLogs($"ROLLBACK: Cannot update time and identity of user who performs the rollback action [{(version.Object as T).GetCacheKey(false)}] => {ex.Message}", ex);
					}

				// rollback (update) with original object
				if (dataSource.Mode.Equals(RepositoryMode.NoSQL))
					await context.ReplaceAsync(dataSource, version.Object as T, null, cancellationToken).ConfigureAwait(false);
				else if (dataSource.Mode.Equals(RepositoryMode.SQL))
					await context.ReplaceAsync(dataSource, version.Object as T, cancellationToken).ConfigureAwait(false);

				// update into cache storage
				if (context.EntityDefinition.Cache != null && await context.EntityDefinition.Cache.SetAsync(version.Object as T, 0, cancellationToken).ConfigureAwait(false) && RepositoryMediator.IsDebugEnabled)
					RepositoryMediator.WriteLogs($"ROLLBACK: Add the object into the cache storage successful [{(version.Object as T).GetCacheKey(false)}]");

				// call post-handlers
				await context.CallPostUpdateHandlersAsync(version.Object as T, changed, true, cancellationToken).ConfigureAwait(false);

				// update other data sources
				var sync = Task.Run(() => RepositoryMediator.SyncAsync(version.Object as T, context.AliasTypeName, true, false, cancellationToken)).ConfigureAwait(false);

				// notify changed
				(version.Object as RepositoryBase)?.NotifyPropertyChanged("_Restored");

				// return the original object
				return version.Object as T;
			}
			catch (OperationCanceledException ex)
			{
				context.Exception = ex;
				throw;
			}
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw;
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
				return await RepositoryMediator.RollbackAsync<T>(context, version, userID, cancellationToken).ConfigureAwait(false);
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

			context.EntityDefinition = context.EntityDefinition ?? RepositoryMediator.GetEntityDefinition<T>();
			var versions = await VersionContent.FindAsync(context.GetVersionDataSource(), "Versions", Filters<VersionContent>.Equals("ID", versionID), null, 0, 1, cancellationToken).ConfigureAwait(false);
			return versions != null && versions.Count > 0
				? await RepositoryMediator.RollbackAsync<T>(context, versions.First(), userID, cancellationToken).ConfigureAwait(false)
				: throw new InformationInvalidException("The identity of version content is invalid");
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
				return await RepositoryMediator.RollbackAsync<T>(context, versionID, userID, cancellationToken).ConfigureAwait(false);
		}
		#endregion

		#region Count version contents
		static IFilterBy<VersionContent> PrepareVersionFilter(string objectID, string serviceName = null, string systemID = null, string repositoryID = null, string repositoryEntityID = null, string userID = null)
		{
			if (!string.IsNullOrWhiteSpace(objectID))
				return Filters<VersionContent>.Equals("ObjectID", objectID);

			var filter = Filters<VersionContent>.And();
			if (!string.IsNullOrWhiteSpace(serviceName))
				filter.Add(Filters<VersionContent>.Equals("ServiceName", serviceName));
			if (!string.IsNullOrWhiteSpace(systemID))
				filter.Add(Filters<VersionContent>.Equals("SystemID", systemID));
			if (!string.IsNullOrWhiteSpace(repositoryID))
				filter.Add(Filters<VersionContent>.Equals("RepositoryID", repositoryID));
			if (!string.IsNullOrWhiteSpace(repositoryEntityID))
				filter.Add(Filters<VersionContent>.Equals("RepositoryEntityID", repositoryEntityID));
			if (!string.IsNullOrWhiteSpace(userID))
				filter.Add(Filters<VersionContent>.Equals("CreatedID", userID));
			return filter;
		}

		/// <summary>
		/// Counts the number of version contents
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="objectID">The identity of object that associates with</param>
		/// <param name="serviceName">The name of service that associates with</param>
		/// <param name="systemID">The identity of system that associates with</param>
		/// <param name="repositoryID">The identity of repository that associates with</param>
		/// <param name="repositoryEntityID">The identity of business repository entity that associates with</param>
		/// <param name="userID">The identity of user who created this verion of the object</param>
		/// <returns></returns>
		public static long CountVersionContents(RepositoryContext context, DataSource dataSource, string objectID, string serviceName = null, string systemID = null, string repositoryID = null, string repositoryEntityID = null, string userID = null)
		{
			try
			{
				dataSource = dataSource ?? context.GetVersionDataSource();
				return dataSource != null
					? VersionContent.Count(dataSource, "Versions", RepositoryMediator.PrepareVersionFilter(objectID, serviceName, systemID, repositoryID, repositoryEntityID, userID))
					: 0;
			}
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw;
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
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="objectID">The identity of object that associates with</param>
		/// <param name="serviceName">The name of service that associates with</param>
		/// <param name="systemID">The identity of system that associates with</param>
		/// <param name="repositoryID">The identity of repository that associates with</param>
		/// <param name="repositoryEntityID">The identity of business repository entity that associates with</param>
		/// <param name="userID">The identity of user who created this verion of the object</param>
		/// <returns></returns>
		public static long CountVersionContents(RepositoryContext context, string objectID, string serviceName = null, string systemID = null, string repositoryID = null, string repositoryEntityID = null, string userID = null)
			=> RepositoryMediator.CountVersionContents(context, null, objectID, serviceName, systemID, repositoryID, repositoryEntityID, userID);

		/// <summary>
		/// Counts the number of version contents
		/// </summary>
		/// <param name="objectID">The identity of object that associates with</param>
		/// <param name="serviceName">The name of service that associates with</param>
		/// <param name="systemID">The identity of system that associates with</param>
		/// <param name="repositoryID">The identity of repository that associates with</param>
		/// <param name="repositoryEntityID">The identity of business repository entity that associates with</param>
		/// <param name="userID">The identity of user who created this verion of the object</param>
		/// <returns></returns>
		public static long CountVersionContents(string objectID, string serviceName = null, string systemID = null, string repositoryID = null, string repositoryEntityID = null, string userID = null)
		{
			using (var context = new RepositoryContext(false))
				return RepositoryMediator.CountVersionContents(context, objectID, serviceName, systemID, repositoryID, repositoryEntityID, userID);
		}

		/// <summary>
		/// Counts the number of version contents
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="objectID">The identity of object that associates with</param>
		/// <param name="serviceName">The name of service that associates with</param>
		/// <param name="systemID">The identity of system that associates with</param>
		/// <param name="repositoryID">The identity of repository that associates with</param>
		/// <param name="repositoryEntityID">The identity of business repository entity that associates with</param>
		/// <param name="userID">The identity of user who created this verion of the object</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<long> CountVersionContentsAsync(RepositoryContext context, DataSource dataSource, string objectID, string serviceName = null, string systemID = null, string repositoryID = null, string repositoryEntityID = null, string userID = null, CancellationToken cancellationToken = default)
		{
			try
			{
				dataSource = dataSource ?? context.GetVersionDataSource();
				return dataSource != null
					? await VersionContent.CountAsync(dataSource, "Versions", RepositoryMediator.PrepareVersionFilter(objectID, serviceName, systemID, repositoryID, repositoryEntityID, userID), cancellationToken).ConfigureAwait(false)
					: 0;
			}
			catch (OperationCanceledException ex)
			{
				context.Exception = ex;
				throw;
			}
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw;
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
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="objectID">The identity of object that associates with</param>
		/// <param name="serviceName">The name of service that associates with</param>
		/// <param name="systemID">The identity of system that associates with</param>
		/// <param name="repositoryID">The identity of repository that associates with</param>
		/// <param name="repositoryEntityID">The identity of business repository entity that associates with</param>
		/// <param name="userID">The identity of user who created this verion of the object</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<long> CountVersionContentsAsync(RepositoryContext context, string objectID, string serviceName = null, string systemID = null, string repositoryID = null, string repositoryEntityID = null, string userID = null, CancellationToken cancellationToken = default)
			=> RepositoryMediator.CountVersionContentsAsync(context, null, objectID, serviceName, systemID, repositoryID, repositoryEntityID, userID, cancellationToken);

		/// <summary>
		/// Counts the number of version contents
		/// </summary>
		/// <param name="objectID">The identity of object that associates with</param>
		/// <param name="serviceName">The name of service that associates with</param>
		/// <param name="systemID">The identity of system that associates with</param>
		/// <param name="repositoryID">The identity of repository that associates with</param>
		/// <param name="repositoryEntityID">The identity of business repository entity that associates with</param>
		/// <param name="userID">The identity of user who created this verion of the object</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<long> CountVersionContentsAsync(string objectID, string serviceName = null, string systemID = null, string repositoryID = null, string repositoryEntityID = null, string userID = null, CancellationToken cancellationToken = default)
		{
			using (var context = new RepositoryContext(false))
				return await RepositoryMediator.CountVersionContentsAsync(context, objectID, serviceName, systemID, repositoryID, repositoryEntityID, userID, cancellationToken).ConfigureAwait(false);
		}

		/// <summary>
		/// Counts the number of version contents
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="objectID">The identity of object that associates with</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<long> CountVersionContentsAsync(RepositoryContext context, DataSource dataSource, string objectID, CancellationToken cancellationToken = default)
			=> RepositoryMediator.CountVersionContentsAsync(context, dataSource, objectID, null, null, null, null, null, cancellationToken);

		/// <summary>
		/// Counts the number of version contents
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="objectID">The identity of object that associates with</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<long> CountVersionContentsAsync(RepositoryContext context, string objectID, CancellationToken cancellationToken = default)
			=> RepositoryMediator.CountVersionContentsAsync(context, null, objectID, cancellationToken);

		/// <summary>
		/// Counts the number of version contents
		/// </summary>
		/// <param name="objectID">The identity of object that associates with</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<long> CountVersionContentsAsync(string objectID, CancellationToken cancellationToken = default)
		{
			using (var context = new RepositoryContext(false))
				return await RepositoryMediator.CountVersionContentsAsync(context, objectID, cancellationToken).ConfigureAwait(false);
		}
		#endregion

		#region Find version contents
		/// <summary>
		/// Finds version contents of an object
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="objectID">The identity of object that associates with</param>
		/// <param name="serviceName">The name of service that associates with</param>
		/// <param name="systemID">The identity of system that associates with</param>
		/// <param name="repositoryID">The identity of repository that associates with</param>
		/// <param name="repositoryEntityID">The identity of business repository entity that associates with</param>
		/// <param name="userID">The identity of user who created this verion of the object</param>
		/// <param name="pageSize">The identity of business repository entity that associates with</param>
		/// <param name="pageNumber">The identity of business repository entity that associates with</param>
		/// <returns></returns>
		public static List<VersionContent> FindVersionContents(RepositoryContext context, DataSource dataSource, string objectID, string serviceName = null, string systemID = null, string repositoryID = null, string repositoryEntityID = null, string userID = null, int pageSize = 0, int pageNumber = 1)
		{
			try
			{
				dataSource = dataSource ?? context.GetVersionDataSource();
				return dataSource != null
					? VersionContent.Find(dataSource, "Versions", RepositoryMediator.PrepareVersionFilter(objectID, serviceName, systemID, repositoryID, repositoryEntityID, userID), Sorts<VersionContent>.Descending(string.IsNullOrWhiteSpace(objectID) ? "Created" : "VersionNumber"), pageSize, pageNumber)
					: new List<VersionContent>();
			}
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw;
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
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="objectID">The identity of object that associates with</param>
		/// <param name="serviceName">The name of service that associates with</param>
		/// <param name="systemID">The identity of system that associates with</param>
		/// <param name="repositoryID">The identity of repository that associates with</param>
		/// <param name="repositoryEntityID">The identity of business repository entity that associates with</param>
		/// <param name="userID">The identity of user who created this verion of the object</param>
		/// <param name="pageSize">The identity of business repository entity that associates with</param>
		/// <param name="pageNumber">The identity of business repository entity that associates with</param>
		/// <returns></returns>
		public static List<VersionContent> FindVersionContents(RepositoryContext context, string objectID, string serviceName = null, string systemID = null, string repositoryID = null, string repositoryEntityID = null, string userID = null, int pageSize = 0, int pageNumber = 1)
			=> RepositoryMediator.FindVersionContents(context, null, objectID, serviceName, systemID, repositoryID, repositoryEntityID, userID, pageSize, pageNumber);

		/// <summary>
		/// Finds version contents of an object
		/// </summary>
		/// <param name="objectID">The identity of object that associates with</param>
		/// <param name="serviceName">The name of service that associates with</param>
		/// <param name="systemID">The identity of system that associates with</param>
		/// <param name="repositoryID">The identity of repository that associates with</param>
		/// <param name="repositoryEntityID">The identity of business repository entity that associates with</param>
		/// <param name="userID">The identity of user who created this verion of the object</param>
		/// <param name="pageSize">The identity of business repository entity that associates with</param>
		/// <param name="pageNumber">The identity of business repository entity that associates with</param>
		/// <returns></returns>
		public static List<VersionContent> FindVersionContents(string objectID, string serviceName, string systemID = null, string repositoryID = null, string repositoryEntityID = null, string userID = null, int pageSize = 0, int pageNumber = 1)
		{
			using (var context = new RepositoryContext(false))
				return RepositoryMediator.FindVersionContents(context, objectID, serviceName, systemID, repositoryID, repositoryEntityID, userID, pageSize, pageNumber);
		}

		/// <summary>
		/// Finds version contents of an object
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="objectID">The identity of object that associates with</param>
		/// <returns></returns>
		public static List<VersionContent> FindVersionContents(RepositoryContext context, DataSource dataSource, string objectID)
			=> string.IsNullOrWhiteSpace(objectID) || !objectID.IsValidUUID()
				? throw new ArgumentNullException(nameof(objectID), "Object identity is invalid")
				: RepositoryMediator.FindVersionContents(context, dataSource, objectID, null, null, null, null, null, 0, 1);

		/// <summary>
		/// Finds version contents of an object
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="objectID">The identity of object that associates with</param>
		/// <returns></returns>
		public static List<VersionContent> FindVersionContents(RepositoryContext context, string objectID)
			=> RepositoryMediator.FindVersionContents(context, null, objectID);

		/// <summary>
		/// Finds version contents of an object
		/// </summary>
		/// <param name="objectID">The identity of object that associates with</param>
		/// <returns></returns>
		public static List<VersionContent> FindVersionContents(string objectID)
		{
			using (var context = new RepositoryContext(false))
				return RepositoryMediator.FindVersionContents(context, objectID);
		}

		/// <summary>
		/// Finds version contents of an object
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="objectID">The identity of object that associates with</param>
		/// <param name="serviceName">The name of service that associates with</param>
		/// <param name="systemID">The identity of system that associates with</param>
		/// <param name="repositoryID">The identity of repository that associates with</param>
		/// <param name="repositoryEntityID">The identity of business repository entity that associates with</param>
		/// <param name="userID">The identity of user who created this verion of the object</param>
		/// <param name="pageSize">The identity of business repository entity that associates with</param>
		/// <param name="pageNumber">The identity of business repository entity that associates with</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<List<VersionContent>> FindVersionContentsAsync(RepositoryContext context, DataSource dataSource, string objectID, string serviceName = null, string systemID = null, string repositoryID = null, string repositoryEntityID = null, string userID = null, int pageSize = 0, int pageNumber = 1, CancellationToken cancellationToken = default)
		{
			try
			{
				dataSource = dataSource ?? context.GetVersionDataSource();
				return dataSource != null
					? VersionContent.FindAsync<VersionContent>(dataSource, "Versions", RepositoryMediator.PrepareVersionFilter(objectID, serviceName, systemID, repositoryID, repositoryEntityID, userID), Sorts<VersionContent>.Descending(string.IsNullOrWhiteSpace(objectID) ? "Created" : "VersionNumber"), pageSize, pageNumber, cancellationToken)
					: Task.FromResult(new List<VersionContent>());
			}
			catch (OperationCanceledException ex)
			{
				context.Exception = ex;
				throw;
			}
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw;
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
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="objectID">The identity of object that associates with</param>
		/// <param name="serviceName">The name of service that associates with</param>
		/// <param name="systemID">The identity of system that associates with</param>
		/// <param name="repositoryID">The identity of repository that associates with</param>
		/// <param name="repositoryEntityID">The identity of business repository entity that associates with</param>
		/// <param name="userID">The identity of user who created this verion of the object</param>
		/// <param name="pageSize">The identity of business repository entity that associates with</param>
		/// <param name="pageNumber">The identity of business repository entity that associates with</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<List<VersionContent>> FindVersionContentsAsync(RepositoryContext context, string objectID, string serviceName = null, string systemID = null, string repositoryID = null, string repositoryEntityID = null, string userID = null, int pageSize = 0, int pageNumber = 1, CancellationToken cancellationToken = default)
			=> RepositoryMediator.FindVersionContentsAsync(context, null, objectID, serviceName, systemID, repositoryID, repositoryEntityID, userID, pageSize, pageNumber, cancellationToken);

		/// <summary>
		/// Finds version contents of an object
		/// </summary>
		/// <param name="objectID">The identity of object that associates with</param>
		/// <param name="serviceName">The name of service that associates with</param>
		/// <param name="systemID">The identity of system that associates with</param>
		/// <param name="repositoryID">The identity of repository that associates with</param>
		/// <param name="repositoryEntityID">The identity of business repository entity that associates with</param>
		/// <param name="userID">The identity of user who created this verion of the object</param>
		/// <param name="pageSize">The identity of business repository entity that associates with</param>
		/// <param name="pageNumber">The identity of business repository entity that associates with</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<List<VersionContent>> FindVersionContentsAsync(string objectID, string serviceName, string systemID = null, string repositoryID = null, string repositoryEntityID = null, string userID = null, int pageSize = 0, int pageNumber = 1, CancellationToken cancellationToken = default)
		{
			using (var context = new RepositoryContext(false))
				return await RepositoryMediator.FindVersionContentsAsync(context, objectID, serviceName, systemID, repositoryID, repositoryEntityID, userID, pageSize, pageNumber, cancellationToken);
		}

		/// <summary>
		/// Finds version contents of an object
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="objectID">The identity of object that associates with</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<List<VersionContent>> FindVersionContentsAsync(RepositoryContext context, DataSource dataSource, string objectID, CancellationToken cancellationToken = default)
			=> string.IsNullOrWhiteSpace(objectID) || !objectID.IsValidUUID()
				? Task.FromException<List<VersionContent>>(new ArgumentNullException(nameof(objectID), "Object identity is invalid"))
				: RepositoryMediator.FindVersionContentsAsync(context, dataSource, objectID, null, null, null, null, null, 0, 1, cancellationToken);

		/// <summary>
		/// Finds version contents of an object
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="objectID">The identity of object that associates with</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<List<VersionContent>> FindVersionContentsAsync(RepositoryContext context, string objectID, CancellationToken cancellationToken = default)
			=> RepositoryMediator.FindVersionContentsAsync(context, null, objectID, cancellationToken);

		/// <summary>
		/// Finds version contents of an object
		/// </summary>
		/// <param name="objectID">The identity of object that associates with</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<List<VersionContent>> FindVersionContentsAsync(string objectID, CancellationToken cancellationToken = default)
		{
			using (var context = new RepositoryContext(false))
				return await RepositoryMediator.FindVersionContentsAsync(context, objectID, cancellationToken);
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
				throw;
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
				RepositoryMediator.CleanVersionContents(context, dataSource, days);
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
				throw;
			}
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw;
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
				await RepositoryMediator.CleanVersionContentsAsync(context, dataSource, days, cancellationToken).ConfigureAwait(false);
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

				context.EntityDefinition = context.EntityDefinition ?? RepositoryMediator.GetEntityDefinition<T>();
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
				throw;
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
				return RepositoryMediator.CreateTrashContent(context, @object, userID);
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

				context.EntityDefinition = context.EntityDefinition ?? RepositoryMediator.GetEntityDefinition<T>();
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
				throw;
			}
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw;
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
				return await RepositoryMediator.CreateTrashContentAsync(context, @object, userID, cancellationToken).ConfigureAwait(false);
		}
		#endregion

		#region Get trash content
		/// <summary>
		/// Get a trash content
		/// </summary>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="id">The identity of trashing object</param>
		public static TrashContent GetTrash<T>(RepositoryContext context, DataSource dataSource, string id) where T : class
		{
			context.Prepare<T>(RepositoryOperation.Query, (dataSource ?? context.GetTrashDataSource())?.StartSession<T>());
			try
			{
				context.EntityDefinition = context.EntityDefinition ?? RepositoryMediator.GetEntityDefinition<T>();
				dataSource = dataSource ?? context.GetTrashDataSource();
				return dataSource != null ? TrashContent.GetByID<TrashContent>(dataSource, "Trashs", id) : null;
			}
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw new RepositoryOperationException("Error occurred while getting a trash content", ex);
			}
		}

		/// <summary>
		/// Get a trash content
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		public static TrashContent GetTrash<T>(RepositoryContext context, string id) where T : class
			=> RepositoryMediator.GetTrash<T>(context, null, id);

		/// <summary>
		/// Get a trash content
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="id">The identity of trashing object</param>
		public static TrashContent GetTrash<T>(string id) where T : class
		{
			using (var context = new RepositoryContext())
				return RepositoryMediator.GetTrash<T>(context, id);
		}

		/// <summary>
		/// Get a trash content
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="dataSource">The repository's data source that use to store object</param>
		/// <param name="id">The identity of trashing object</param>
		/// <param name="cancellationToken">The cancellation token</param>
		public static async Task<TrashContent> GetTrashAsync<T>(RepositoryContext context, DataSource dataSource, string id, CancellationToken cancellationToken = default) where T : class
		{
			context.Prepare<T>(RepositoryOperation.Query, (dataSource ?? context.GetTrashDataSource())?.StartSession<T>());
			try
			{
				context.EntityDefinition = context.EntityDefinition ?? RepositoryMediator.GetEntityDefinition<T>();
				dataSource = dataSource ?? context.GetTrashDataSource();
				return dataSource != null ? await TrashContent.GetByIDAsync<TrashContent>(dataSource, "Trashs", id, cancellationToken).ConfigureAwait(false) : null;
			}
			catch (OperationCanceledException ex)
			{
				context.Exception = ex;
				throw;
			}
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw new RepositoryOperationException("Error occurred while getting a trash content", ex);
			}
		}

		/// <summary>
		/// Get a trash content
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The repository's context that hold the transaction and state data</param>
		/// <param name="id">The identity of trashing object</param>
		/// <param name="cancellationToken">The cancellation token</param>
		public static Task<TrashContent> GetTrashAsync<T>(RepositoryContext context, string id, CancellationToken cancellationToken = default) where T : class
			=> RepositoryMediator.GetTrashAsync<T>(context, null, id, cancellationToken);

		/// <summary>
		/// Get a trash content
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="id">The identity of trashing object</param>
		/// <param name="cancellationToken">The cancellation token</param>
		public static async Task<TrashContent> GetTrashAsync<T>(string id, CancellationToken cancellationToken = default) where T : class
		{
			using (var context = new RepositoryContext())
				return await RepositoryMediator.GetTrashAsync<T>(context, id, cancellationToken).ConfigureAwait(false);
		}
		#endregion

		#region Restore from trash content
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
			if (trashContent == null || trashContent.Object == null)
				throw new ArgumentNullException(nameof(trashContent), "Trash content is invalid");
			else if (!(trashContent.Object is T))
				throw new InformationInvalidException($"Original object of the trash content is not matched with type [{typeof(T)}]");

			// process
			try
			{
				// prepare
				context.Operation = RepositoryOperation.Create;
				context.EntityDefinition = context.EntityDefinition ?? RepositoryMediator.GetEntityDefinition<T>();
				var dataSource = context.GetPrimaryDataSource();
				if (dataSource == null)
					return null;

				// call pre-handlers
				if (context.CallPreCreateHandlers(trashContent.Object, true))
					return null;

				// audits
				if (trashContent.Object is IBusinessEntity && !string.IsNullOrWhiteSpace(userID) && userID.IsValidUUID())
					try
					{
						trashContent.Object.SetAttributeValue("_LastModified", DateTime.Now);
						trashContent.Object.SetAttributeValue("_LastModifiedID", userID);
					}
					catch (Exception ex)
					{
						RepositoryMediator.WriteLogs($"RESTORE: Cannot update time and identity of user who performs the restore action [{(trashContent.Object as T).GetCacheKey(false)}] => {ex.Message}", ex);
					}

				// restore (create) with original object
				if (dataSource.Mode.Equals(RepositoryMode.NoSQL))
					context.Create(dataSource, trashContent.Object as T, null);
				else if (dataSource.Mode.Equals(RepositoryMode.SQL))
					context.Create(dataSource, trashContent.Object as T);

				// update into cache storage
				if (context.EntityDefinition.Cache != null && context.EntityDefinition.Cache.Set(trashContent.Object as T) && RepositoryMediator.IsDebugEnabled)
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
				throw;
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
				return RepositoryMediator.Restore<T>(context, trashContent, userID);
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
			if (string.IsNullOrWhiteSpace(trashContentID))
				throw new ArgumentNullException(nameof(trashContentID), "The identity of trash content is invalid");

			context.EntityDefinition = context.EntityDefinition ?? RepositoryMediator.GetEntityDefinition<T>();
			var trashs = TrashContent.Find(RepositoryMediator.GetTrashDataSource(context), "Trashs", Filters<TrashContent>.Equals("ID", trashContentID), null, 0, 1);
			return trashs != null && trashs.Count > 0
				? RepositoryMediator.Restore<T>(context, trashs.First(), userID)
				: throw new InformationInvalidException("The identity of trash content is invalid");
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
				return RepositoryMediator.Restore<T>(context, trashContentID, userID);
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
				context.EntityDefinition = context.EntityDefinition ?? RepositoryMediator.GetEntityDefinition<T>();
				var dataSource = context.GetPrimaryDataSource();
				if (dataSource == null)
					return null;

				// call pre-handlers
				if (await context.CallPreCreateHandlersAsync(trashContent.Object, true, cancellationToken).ConfigureAwait(false))
					return null;

				// audits
				if (trashContent.Object is IBusinessEntity && !string.IsNullOrWhiteSpace(userID) && userID.IsValidUUID())
					try
					{
						trashContent.Object.SetAttributeValue("_LastModified", DateTime.Now);
						trashContent.Object.SetAttributeValue("_LastModifiedID", userID);
					}
					catch (Exception ex)
					{
						RepositoryMediator.WriteLogs($"RESTORE: Cannot update time and identity of user who performs the restore action [{(trashContent.Object as T).GetCacheKey(false)}] => {ex.Message}", ex);
					}

				// restore (create) with original object
				if (dataSource.Mode.Equals(RepositoryMode.NoSQL))
					await context.CreateAsync(dataSource, trashContent.Object as T, null, cancellationToken).ConfigureAwait(false);
				else if (dataSource.Mode.Equals(RepositoryMode.SQL))
					await context.CreateAsync(dataSource, trashContent.Object as T, cancellationToken).ConfigureAwait(false);

				// update into cache storage
				if (context.EntityDefinition.Cache != null && await context.EntityDefinition.Cache.SetAsync(trashContent.Object as T, 0, cancellationToken).ConfigureAwait(false) && RepositoryMediator.IsDebugEnabled)
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
				throw;
			}
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw;
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
				return await RepositoryMediator.RestoreAsync<T>(context, trashContent, userID, cancellationToken).ConfigureAwait(false);
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
			if (string.IsNullOrWhiteSpace(trashContentID))
				throw new ArgumentNullException(nameof(trashContentID), "The identity of trash content is invalid");

			context.EntityDefinition = context.EntityDefinition ?? RepositoryMediator.GetEntityDefinition<T>();
			var trashs = await TrashContent.FindAsync(RepositoryMediator.GetTrashDataSource(context), "Trashs", Filters<TrashContent>.Equals("ID", trashContentID), null, 0, 1, cancellationToken).ConfigureAwait(false);
			return trashs != null && trashs.Count > 0
				? await RepositoryMediator.RestoreAsync<T>(context, trashs.First(), userID, cancellationToken).ConfigureAwait(false)
				: throw new InformationInvalidException("The identity of trash content is invalid");
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
				return await RepositoryMediator.RestoreAsync<T>(context, trashContentID, userID, cancellationToken).ConfigureAwait(false);
		}
		#endregion

		#region Count trash contents
		static IFilterBy<TrashContent> PrepareTrashFilter(string serviceName, string systemID, string repositoryID, string repositoryEntityID, string userID)
		{
			var filter = Filters<TrashContent>.And();
			if (!string.IsNullOrWhiteSpace(serviceName))
				filter.Add(Filters<TrashContent>.Equals("ServiceName", serviceName));
			if (!string.IsNullOrWhiteSpace(systemID))
				filter.Add(Filters<TrashContent>.Equals("SystemID", systemID));
			if (!string.IsNullOrWhiteSpace(repositoryID))
				filter.Add(Filters<TrashContent>.Equals("RepositoryID", repositoryID));
			if (!string.IsNullOrWhiteSpace(repositoryEntityID))
				filter.Add(Filters<TrashContent>.Equals("RepositoryEntityID", repositoryEntityID));
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
		/// <param name="repositoryEntityID">The identity of business repository entity that associates with</param>
		/// <param name="userID">The identity of user who created this verion of the object</param>
		/// <returns></returns>
		public static long CountTrashContents<T>(RepositoryContext context, DataSource dataSource, string serviceName = null, string systemID = null, string repositoryID = null, string repositoryEntityID = null, string userID = null) where T : class
		{
			try
			{
				context.EntityDefinition = context.EntityDefinition ?? RepositoryMediator.GetEntityDefinition<T>();
				dataSource = dataSource ?? RepositoryMediator.GetTrashDataSource(context);
				return dataSource != null
					? TrashContent.Count(dataSource, "Trashs", RepositoryMediator.PrepareTrashFilter(serviceName, systemID, repositoryID, repositoryEntityID, userID))
					: 0;
			}
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw;
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
		/// <param name="repositoryEntityID">The identity of business repository entity that associates with</param>
		/// <param name="userID">The identity of user who created this verion of the object</param>
		/// <returns></returns>
		public static long CountTrashContents<T>(RepositoryContext context, string serviceName = null, string systemID = null, string repositoryID = null, string repositoryEntityID = null, string userID = null) where T : class
			=> RepositoryMediator.CountTrashContents<T>(context, null, serviceName, systemID, repositoryID, repositoryEntityID, userID);

		/// <summary>
		/// Counts the number of trash contents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="serviceName">The name of service that associates with</param>
		/// <param name="systemID">The identity of system that associates with</param>
		/// <param name="repositoryID">The identity of repository that associates with</param>
		/// <param name="repositoryEntityID">The identity of business repository entity that associates with</param>
		/// <param name="userID">The identity of user who created this verion of the object</param>
		/// <returns></returns>
		public static long CountTrashContents<T>(string serviceName = null, string systemID = null, string repositoryID = null, string repositoryEntityID = null, string userID = null) where T : class
		{
			using (var context = new RepositoryContext(false))
				return RepositoryMediator.CountTrashContents<T>(context, serviceName, systemID, repositoryID, repositoryEntityID, userID);
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
		/// <param name="repositoryEntityID">The identity of business repository entity that associates with</param>
		/// <param name="userID">The identity of user who created this verion of the object</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<long> CountTrashContentsAsync<T>(RepositoryContext context, DataSource dataSource, string serviceName = null, string systemID = null, string repositoryID = null, string repositoryEntityID = null, string userID = null, CancellationToken cancellationToken = default) where T : class
		{
			try
			{
				context.EntityDefinition = context.EntityDefinition ?? RepositoryMediator.GetEntityDefinition<T>();
				dataSource = dataSource ?? RepositoryMediator.GetTrashDataSource(context);
				return dataSource != null
					? await TrashContent.CountAsync(dataSource, "Trashs", RepositoryMediator.PrepareTrashFilter(serviceName, systemID, repositoryID, repositoryEntityID, userID), cancellationToken).ConfigureAwait(false)
					: 0;
			}
			catch (OperationCanceledException ex)
			{
				context.Exception = ex;
				throw;
			}
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw;
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
		/// <param name="repositoryEntityID">The identity of business repository entity that associates with</param>
		/// <param name="userID">The identity of user who created this verion of the object</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<long> CountTrashContentsAsync<T>(RepositoryContext context, string serviceName = null, string systemID = null, string repositoryID = null, string repositoryEntityID = null, string userID = null, CancellationToken cancellationToken = default) where T : class
			=> RepositoryMediator.CountTrashContentsAsync<T>(context, null, serviceName, systemID, repositoryID, repositoryEntityID, userID, cancellationToken);

		/// <summary>
		/// Counts the number of trash contents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="serviceName">The name of service that associates with</param>
		/// <param name="systemID">The identity of system that associates with</param>
		/// <param name="repositoryID">The identity of repository that associates with</param>
		/// <param name="repositoryEntityID">The identity of business repository entity that associates with</param>
		/// <param name="userID">The identity of user who created this verion of the object</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<long> CountTrashContentsAsync<T>(string serviceName = null, string systemID = null, string repositoryID = null, string repositoryEntityID = null, string userID = null, CancellationToken cancellationToken = default) where T : class
		{
			using (var context = new RepositoryContext(false))
				return await RepositoryMediator.CountTrashContentsAsync<T>(context, serviceName, systemID, repositoryID, repositoryEntityID, userID, cancellationToken).ConfigureAwait(false);
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
				return await RepositoryMediator.CountTrashContentsAsync<T>(context, serviceName, systemID, cancellationToken).ConfigureAwait(false);
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
		/// <param name="repositoryEntityID">The identity of business repository entity that associates with</param>
		/// <param name="userID">The identity of user who created this verion of the object</param>
		/// <param name="pageSize">The identity of business repository entity that associates with</param>
		/// <param name="pageNumber">The identity of business repository entity that associates with</param>
		/// <returns></returns>
		public static List<TrashContent> FindTrashContents<T>(RepositoryContext context, DataSource dataSource, string serviceName = null, string systemID = null, string repositoryID = null, string repositoryEntityID = null, string userID = null, int pageSize = 0, int pageNumber = 1) where T : class
		{
			try
			{
				context.EntityDefinition = context.EntityDefinition ?? RepositoryMediator.GetEntityDefinition<T>();
				dataSource = dataSource ?? RepositoryMediator.GetTrashDataSource(context);
				return dataSource != null
					? TrashContent.Find(dataSource, "Trashs", RepositoryMediator.PrepareTrashFilter(serviceName, systemID, repositoryID, repositoryEntityID, userID), Sorts<TrashContent>.Descending("Created"), pageSize, pageNumber)
					: new List<TrashContent>();
			}
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw;
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
		/// <param name="repositoryEntityID">The identity of business repository entity that associates with</param>
		/// <param name="userID">The identity of user who created this verion of the object</param>
		/// <param name="pageSize">The identity of business repository entity that associates with</param>
		/// <param name="pageNumber">The identity of business repository entity that associates with</param>
		/// <returns></returns>
		public static List<TrashContent> FindTrashContents<T>(RepositoryContext context, string serviceName = null, string systemID = null, string repositoryID = null, string repositoryEntityID = null, string userID = null, int pageSize = 0, int pageNumber = 1) where T : class
			=> RepositoryMediator.FindTrashContents<T>(context, null, serviceName, systemID, repositoryID, repositoryEntityID, userID, pageSize, pageNumber);

		/// <summary>
		/// Finds trash contents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="serviceName">The name of service that associates with</param>
		/// <param name="systemID">The identity of system that associates with</param>
		/// <param name="repositoryID">The identity of repository that associates with</param>
		/// <param name="repositoryEntityID">The identity of business repository entity that associates with</param>
		/// <param name="userID">The identity of user who created this verion of the object</param>
		/// <param name="pageSize">The identity of business repository entity that associates with</param>
		/// <param name="pageNumber">The identity of business repository entity that associates with</param>
		/// <returns></returns>
		public static List<TrashContent> FindTrashContents<T>(string serviceName = null, string systemID = null, string repositoryID = null, string repositoryEntityID = null, string userID = null, int pageSize = 0, int pageNumber = 1) where T : class
		{
			using (var context = new RepositoryContext(false))
				return RepositoryMediator.FindTrashContents<T>(context, serviceName, systemID, repositoryID, repositoryEntityID, userID, pageSize, pageNumber);
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
				return RepositoryMediator.FindTrashContents<T>(context, serviceName, systemID, pageSize, pageNumber);
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
		/// <param name="repositoryEntityID">The identity of business repository entity that associates with</param>
		/// <param name="userID">The identity of user who created this verion of the object</param>
		/// <param name="pageSize">The identity of business repository entity that associates with</param>
		/// <param name="pageNumber">The identity of business repository entity that associates with</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<List<TrashContent>> FindTrashContentsAsync<T>(RepositoryContext context, DataSource dataSource, string serviceName = null, string systemID = null, string repositoryID = null, string repositoryEntityID = null, string userID = null, int pageSize = 0, int pageNumber = 1, CancellationToken cancellationToken = default) where T : class
		{
			try
			{
				context.EntityDefinition = context.EntityDefinition ?? RepositoryMediator.GetEntityDefinition<T>();
				dataSource = dataSource ?? RepositoryMediator.GetTrashDataSource(context);
				return dataSource != null
					? TrashContent.FindAsync(dataSource, "Trashs", RepositoryMediator.PrepareTrashFilter(serviceName, systemID, repositoryID, repositoryEntityID, userID), Sorts<TrashContent>.Descending("Created"), pageSize, pageNumber, cancellationToken)
					: Task.FromResult(new List<TrashContent>());
			}
			catch (OperationCanceledException ex)
			{
				context.Exception = ex;
				throw;
			}
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw;
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
		/// <param name="repositoryEntityID">The identity of business repository entity that associates with</param>
		/// <param name="userID">The identity of user who created this verion of the object</param>
		/// <param name="pageSize">The identity of business repository entity that associates with</param>
		/// <param name="pageNumber">The identity of business repository entity that associates with</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<List<TrashContent>> FindTrashContentsAsync<T>(RepositoryContext context, string serviceName = null, string systemID = null, string repositoryID = null, string repositoryEntityID = null, string userID = null, int pageSize = 0, int pageNumber = 1, CancellationToken cancellationToken = default) where T : class
			=> RepositoryMediator.FindTrashContentsAsync<T>(context, null, serviceName, systemID, repositoryID, repositoryEntityID, userID, pageSize, pageNumber, cancellationToken);

		/// <summary>
		/// Finds trash contents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="serviceName">The name of service that associates with</param>
		/// <param name="systemID">The identity of system that associates with</param>
		/// <param name="repositoryID">The identity of repository that associates with</param>
		/// <param name="repositoryEntityID">The identity of business repository entity that associates with</param>
		/// <param name="userID">The identity of user who created this verion of the object</param>
		/// <param name="pageSize">The identity of business repository entity that associates with</param>
		/// <param name="pageNumber">The identity of business repository entity that associates with</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<List<TrashContent>> FindTrashContentsAsync<T>(string serviceName = null, string systemID = null, string repositoryID = null, string repositoryEntityID = null, string userID = null, int pageSize = 0, int pageNumber = 1, CancellationToken cancellationToken = default) where T : class
		{
			using (var context = new RepositoryContext(false))
				return await RepositoryMediator.FindTrashContentsAsync<T>(context, serviceName, systemID, repositoryID, repositoryEntityID, userID, pageSize, pageNumber, cancellationToken);
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
				return await RepositoryMediator.FindTrashContentsAsync<T>(context, serviceName, systemID, pageSize, pageNumber, cancellationToken);
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
				throw;
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
				RepositoryMediator.CleanTrashContents(context, dataSource, days);
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
				throw;
			}
			catch (RepositoryOperationException ex)
			{
				context.Exception = ex;
				RepositoryMediator.WriteLogs(ex);
				throw;
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
				await RepositoryMediator.CleanTrashContentsAsync(context, dataSource, days, cancellationToken).ConfigureAwait(false);
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
		internal static async Task SyncAsync<T>(IFilterBy<T> filter, string aliasTypeName, string businessRepositoryEntityID, CancellationToken cancellationToken = default) where T : class
		{
			using (var context = new RepositoryContext())
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
								? context.DeleteManyAsync(dataSource, filter, businessRepositoryEntityID, null, cancellationToken)
								: dataSource.Mode.Equals(RepositoryMode.SQL)
									? context.DeleteManyAsync(dataSource, filter, businessRepositoryEntityID, cancellationToken)
									: null;
						}).Where(task => task != null).ToList()).ConfigureAwait(false);

						stopwatch.Stop();
						if (RepositoryMediator.IsDebugEnabled)
							RepositoryMediator.WriteLogs(new[]
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

		internal static async Task SyncAsync<T>(T @object, string aliasTypeName, EntityDefinition entityDefinition, CancellationToken cancellationToken = default) where T : class
		{
			using (var context = new RepositoryContext(false))
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

		internal static async Task SyncAsync<T>(T @object, string aliasTypeName, bool checkExisting = true, bool isDelete = false, CancellationToken cancellationToken = default) where T : class
		{
			using (var context = new RepositoryContext(isDelete))
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
							RepositoryMediator.WriteLogs(new[]
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
					RepositoryMediator.WriteLogs($"Error occurred while running the pre-create handler \"{handlers[index]}\" [{typeof(T)}#{@object?.GetEntityID()}]", ex);
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
					RepositoryMediator.WriteLogs($"Operation was cancelled while running the pre-create handler (async) \"{handlers[index]}\" [{typeof(T)}#{@object?.GetEntityID()}]");
				}
				catch (Exception ex)
				{
					RepositoryMediator.WriteLogs($"Error occurred while running the pre-create handler (async) \"{handlers[index]}\" [{typeof(T)}#{@object?.GetEntityID()}]", ex);
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
						RepositoryMediator.WriteLogs($"Error occurred while running the post-create handler \"{type}\" [{typeof(T)}#{@object?.GetEntityID()}]", ex);
					}
				}))
				.ToList();

		static async Task CallPostCreateHandlersAsync<T>(this RepositoryContext context, T @object, bool isRestore = false, CancellationToken cancellationToken = default) where T : class
			=> await RepositoryMediator.GetHandlers(type => typeof(IPostCreateHandler).IsAssignableFrom(type))
				.ForEachAsync(async type =>
				{
					try
					{
						var handler = ObjectService.CreateInstance(type) as IPostCreateHandler;
						await handler.OnPostCreateAsync(context, @object, isRestore, cancellationToken).ConfigureAwait(false);
					}
					catch (OperationCanceledException)
					{
						RepositoryMediator.WriteLogs($"Operation was cancelled while running the post-create handler (async) \"{type}\" [{typeof(T)}#{@object?.GetEntityID()}]");
					}
					catch (Exception ex)
					{
						RepositoryMediator.WriteLogs($"Error occurred while running the post-create handler (async) \"{type}\" [{typeof(T)}#{@object?.GetEntityID()}]", ex);
					}
				}, false).ConfigureAwait(false);
		#endregion

		#region Call handlers of Get event
		static bool CallPreGetHandlers<T>(this RepositoryContext context, string id) where T : class
		{
			var handlers = RepositoryMediator.GetHandlers(type => typeof(IPreGetHandler).IsAssignableFrom(type));
			for (var index = 0; index < handlers.Count; index++)
				try
				{
					var handler = handlers[index].CreateInstance() as IPreGetHandler;
					if (handler.OnPreGet<T>(context, id))
						return true;
				}
				catch (Exception ex)
				{
					RepositoryMediator.WriteLogs($"Error occurred while running the pre-get handler \"{handlers[index]}\" [{typeof(T)}#{id}]", ex);
				}
			return false;
		}

		static async Task<bool> CallPreGetHandlersAsync<T>(this RepositoryContext context, string id, CancellationToken cancellationToken = default) where T : class
		{
			var handlers = RepositoryMediator.GetHandlers(type => typeof(IPreGetHandler).IsAssignableFrom(type));
			for (var index = 0; index < handlers.Count; index++)
				try
				{
					var handler = handlers[index].CreateInstance() as IPreGetHandler;
					if (await handler.OnPreGetAsync<T>(context, id, cancellationToken).ConfigureAwait(false))
						return true;
				}
				catch (OperationCanceledException)
				{
					RepositoryMediator.WriteLogs($"Operation was cancelled while running the pre-get handler (async) \"{handlers[index]}\" [{typeof(T)}#{id}]");
				}
				catch (Exception ex)
				{
					RepositoryMediator.WriteLogs($"Error occurred while running the pre-get handler (async) \"{handlers[index]}\" [{typeof(T)}#{id}]", ex);
				}
			return false;
		}

		static void CallPostGetHandlers<T>(this RepositoryContext context, T @object) where T : class
			=> RepositoryMediator.GetHandlers(type => typeof(IPostGetHandler).IsAssignableFrom(type))
				.Select(type => Task.Run(() =>
				{
					try
					{
						var handler = type.CreateInstance() as IPostGetHandler;
						handler.OnPostGet(context, @object);
					}
					catch (Exception ex)
					{
						RepositoryMediator.WriteLogs($"Error occurred while running the post-get handler \"{type}\" [{typeof(T)}#{@object?.GetEntityID()}]", ex);
					}
				}))
				.ToList();

		static async Task CallPostGetHandlersAsync<T>(this RepositoryContext context, T @object, CancellationToken cancellationToken = default) where T : class
			=> await RepositoryMediator.GetHandlers(type => typeof(IPostGetHandler).IsAssignableFrom(type))
				.ForEachAsync(async type =>
				{
					try
					{
						var handler = type.CreateInstance() as IPostGetHandler;
						await handler.OnPostGetAsync(context, @object, cancellationToken).ConfigureAwait(false);
					}
					catch (OperationCanceledException)
					{
						RepositoryMediator.WriteLogs($"Operation was cancelled while running the post-get handler (async) \"{type}\" [{typeof(T)}#{@object?.GetEntityID()}]");
					}
					catch (Exception ex)
					{
						RepositoryMediator.WriteLogs($"Error occurred while running the post-get handler (async) \"{type}\" [{typeof(T)}#{@object?.GetEntityID()}]", ex);
					}
				}, false).ConfigureAwait(false);
		#endregion

		#region Call handlers of Update event
		static bool CallPreUpdateHandlers<T>(this RepositoryContext context, T @object, HashSet<string> changed, bool isRollback = false) where T : class
		{
			var handlers = RepositoryMediator.GetHandlers(type => typeof(IPreUpdateHandler).IsAssignableFrom(type));
			for (var index = 0; index < handlers.Count; index++)
				try
				{
					var handler = handlers[index].CreateInstance() as IPreUpdateHandler;
					if (handler.OnPreUpdate(context, @object, changed, isRollback))
						return true;
				}
				catch (Exception ex)
				{
					RepositoryMediator.WriteLogs($"Error occurred while running the pre-update handler \"{handlers[index]}\" [{typeof(T)}#{@object?.GetEntityID()}]", ex);
				}
			return false;
		}

		static async Task<bool> CallPreUpdateHandlersAsync<T>(this RepositoryContext context, T @object, HashSet<string> changed, bool isRestore = false, CancellationToken cancellationToken = default) where T : class
		{
			var handlers = RepositoryMediator.GetHandlers(type => typeof(IPreUpdateHandler).IsAssignableFrom(type));
			for (var index = 0; index < handlers.Count; index++)
				try
				{
					var handler = handlers[index].CreateInstance() as IPreUpdateHandler;
					if (await handler.OnPreUpdateAsync(context, @object, changed, isRestore, cancellationToken).ConfigureAwait(false))
						return true;
				}
				catch (OperationCanceledException)
				{
					RepositoryMediator.WriteLogs($"Operation was cancelled while running the pre-update handler (async) \"{handlers[index]}\" [{typeof(T)}#{@object?.GetEntityID()}]");
				}
				catch (Exception ex)
				{
					RepositoryMediator.WriteLogs($"Error occurred while running the pre-update handler (async) \"{handlers[index]}\" [{typeof(T)}#{@object?.GetEntityID()}]", ex);
				}
			return false;
		}

		static void CallPostUpdateHandlers<T>(this RepositoryContext context, T @object, HashSet<string> changed, bool isRestore = false) where T : class
			=> RepositoryMediator.GetHandlers(type => typeof(IPostUpdateHandler).IsAssignableFrom(type))
				.Select(type => Task.Run(() =>
				{
					try
					{
						var handler = type.CreateInstance() as IPostUpdateHandler;
						handler.OnPostUpdate(context, @object, changed, isRestore);
					}
					catch (Exception ex)
					{
						RepositoryMediator.WriteLogs($"Error occurred while running the post-update handler \"{type}\" [{typeof(T)}#{@object?.GetEntityID()}]", ex);
					}
				}))
				.ToList();

		static async Task CallPostUpdateHandlersAsync<T>(this RepositoryContext context, T @object, HashSet<string> changed, bool isRestore = false, CancellationToken cancellationToken = default) where T : class
			=> await RepositoryMediator.GetHandlers(type => typeof(IPostUpdateHandler).IsAssignableFrom(type))
				.ForEachAsync(async type =>
				{
					try
					{
						var handler = type.CreateInstance() as IPostUpdateHandler;
						await handler.OnPostUpdateAsync(context, @object, changed, isRestore, cancellationToken).ConfigureAwait(false);
					}
					catch (OperationCanceledException)
					{
						RepositoryMediator.WriteLogs($"Operation was cancelled while running the post-update handler (async) \"{type}\" [{typeof(T)}#{@object?.GetEntityID()}]");
					}
					catch (Exception ex)
					{
						RepositoryMediator.WriteLogs($"Error occurred while running the post-update handler (async) \"{type}\" [{typeof(T)}#{@object?.GetEntityID()}]", ex);
					}
				}, false).ConfigureAwait(false);
		#endregion

		#region Call handlers of Delete event
		static bool CallPreDeleteHandlers<T>(this RepositoryContext context, T @object) where T : class
		{
			var handlers = RepositoryMediator.GetHandlers(type => typeof(IPreDeleteHandler).IsAssignableFrom(type));
			for (var index = 0; index < handlers.Count; index++)
				try
				{
					var handler = handlers[index].CreateInstance() as IPreDeleteHandler;
					if (handler.OnPreDelete(context, @object))
						return true;
				}
				catch (Exception ex)
				{
					RepositoryMediator.WriteLogs($"Error occurred while running the pre-delete handler \"{handlers[index]}\" [{typeof(T)}#{@object?.GetEntityID()}]", ex);
				}
			return false;
		}

		static async Task<bool> CallPreDeleteHandlersAsync<T>(this RepositoryContext context, T @object, CancellationToken cancellationToken = default) where T : class
		{
			var handlers = RepositoryMediator.GetHandlers(type => typeof(IPreDeleteHandler).IsAssignableFrom(type));
			for (var index = 0; index < handlers.Count; index++)
				try
				{
					var handler = handlers[index].CreateInstance() as IPreDeleteHandler;
					if (await handler.OnPreDeleteAsync(context, @object, cancellationToken).ConfigureAwait(false))
						return true;
				}
				catch (OperationCanceledException)
				{
					RepositoryMediator.WriteLogs($"Operation was cancelled while running the pre-delete handler (async) \"{handlers[index]}\" [{typeof(T)}#{@object?.GetEntityID()}]");
				}
				catch (Exception ex)
				{
					RepositoryMediator.WriteLogs($"Error occurred while running the pre-delete handler (async) \"{handlers[index]}\" [{typeof(T)}#{@object?.GetEntityID()}]", ex);
				}
			return false;
		}

		static void CallPostDeleteHandlers<T>(this RepositoryContext context, T @object) where T : class
			=> RepositoryMediator.GetHandlers(type => typeof(IPostDeleteHandler).IsAssignableFrom(type))
				.Select(type => Task.Run(() =>
				{
					try
					{
						var handler = type.CreateInstance() as IPostDeleteHandler;
						handler.OnPostDelete(context, @object);
					}
					catch (Exception ex)
					{
						RepositoryMediator.WriteLogs($"Error occurred while running the post-delete handler \"{type}\" [{typeof(T)}#{@object?.GetEntityID()}]", ex);
					}
				}))
				.ToList();

		static async Task CallPostDeleteHandlersAsync<T>(this RepositoryContext context, T @object, CancellationToken cancellationToken = default) where T : class
			=> await RepositoryMediator.GetHandlers(type => typeof(IPostDeleteHandler).IsAssignableFrom(type))
				.ForEachAsync(async type =>
				{
					try
					{
						var handler = type.CreateInstance() as IPostDeleteHandler;
						await handler.OnPostDeleteAsync(context, @object, cancellationToken).ConfigureAwait(false);
					}
					catch (OperationCanceledException)
					{
						RepositoryMediator.WriteLogs($"Operation was cancelled while running the post-delete handler (async) \"{type}\" [{typeof(T)}#{@object?.GetEntityID()}]");
					}
					catch (Exception ex)
					{
						RepositoryMediator.WriteLogs($"Error occurred while running the post-delete handler (async) \"{type}\" [{typeof(T)}#{@object?.GetEntityID()}]", ex);
					}
				}, false).ConfigureAwait(false);
		#endregion

		#region JSON/XML/ExpandoObject conversions
		/// <summary>
		/// Serializes this object to JSON
		/// </summary>
		/// <param name="object"></param>
		/// <param name="onCompleted"></param>
		/// <returns></returns>
		public static JToken ToJson(this IBusinessEntity @object, Action<JToken> onCompleted = null)
			=> @object is RepositoryBase bizObject ? bizObject.ToJson(onCompleted) : (@object as object)?.ToJson(onCompleted);

		/// <summary>
		/// Serializes this object to ExpandoObject
		/// </summary>
		/// <param name="object"></param>
		/// <param name="onCompleted"></param>
		/// <returns></returns>
		public static ExpandoObject ToExpandoObject(this IBusinessEntity @object, Action<ExpandoObject> onCompleted = null)
			=> @object is RepositoryBase bizObject ? bizObject.ToExpandoObject(onCompleted) : (@object as object)?.ToExpandoObject(onCompleted);

		/// <summary>
		/// Serializes this object to XML
		/// </summary>
		/// <param name="object"></param>
		/// <param name="onCompleted"></param>
		/// <returns></returns>
		public static XElement ToXml(this IBusinessEntity @object, Action<XElement> onCompleted = null)
			=> @object is RepositoryBase bizObject ? bizObject.ToXml(onCompleted) : (@object as object)?.ToXml(onCompleted);

		/// <summary>
		/// Serializes the collection of objects to an array of JSON objects
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="objects">The object to serialize</param>
		/// <param name="addTypeOfExtendedProperties">true to add type of all extended properties when generate elements</param>
		/// <param name="onItemCompleted">The action to run on item completed</param>
		/// <returns></returns>
		public static JArray ToJsonArray<T>(this List<T> objects, bool addTypeOfExtendedProperties = false, Action<JObject> onItemCompleted = null) where T : class
			=> objects != null && objects.Any()
				? objects.ToJArray(@object => @object is RepositoryBase ? (@object as RepositoryBase)?.ToJson(addTypeOfExtendedProperties, onItemCompleted) : @object?.ToJson())
				: new JArray();

		/// <summary>
		/// Serializes the collection of objects to an array of JSON objects
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="objects">The object to serialize</param>
		/// <param name="onItemCompleted">The action to run on item completed</param>
		/// <returns></returns>
		public static JArray ToJsonArray<T>(this List<T> objects, Action<JObject> onItemCompleted) where T : class
			=> objects.ToJsonArray(false, onItemCompleted);

		/// <summary>
		/// Serializes the collection of objects to a JSON object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="objects">The object to serialize</param>
		/// <param name="addTypeOfExtendedProperties">true to add type of all extended properties when generate elements</param>
		/// <param name="onItemCompleted">The action to run on item completed</param>
		/// <returns></returns>
		public static JObject ToJsonObject<T>(this List<T> objects, bool addTypeOfExtendedProperties = false, Action<JObject> onItemCompleted = null) where T : class
		{
			var json = new JObject();
			objects?.ForEach(@object =>
			{
				var itemjson = @object is RepositoryBase
					? (@object as RepositoryBase)?.ToJson(addTypeOfExtendedProperties, null)
					: @object?.ToJson() as JObject;
				onItemCompleted?.Invoke(itemjson);
				json.Add(new JProperty(@object?.GetEntityID(), itemjson));
			});
			return json;
		}

		/// <summary>
		/// Serializes the collection of objects to a JSON object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="objects">The object to serialize</param>
		/// <param name="onItemCompleted">The action to run on item completed</param>
		/// <returns></returns>
		public static JObject ToJsonObject<T>(this List<T> objects, Action<JObject> onItemCompleted) where T : class
			=> objects.ToJsonObject(false, onItemCompleted);

		/// <summary>
		/// Serializes the collection of objects to XML
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="objects">The object to serialize</param>
		/// <param name="name">The string that presents name of root tag, null to use default</param>
		/// <param name="addTypeOfExtendedProperties">true to add type of all extended properties when generate elements</param>
		/// <param name="onItemCompleted">The action to run on item completed</param>
		/// <returns></returns>
		public static XElement ToXml<T>(this List<T> objects, string name = null, bool addTypeOfExtendedProperties = false, Action<XElement> onItemCompleted = null) where T : class
		{
			var xml = new XElement(string.IsNullOrWhiteSpace(name) ? typeof(T).GetTypeName(true) : name);
			objects?.ForEach(@object =>
			{
				var element = @object is RepositoryBase
					? (@object as RepositoryBase)?.ToXml(addTypeOfExtendedProperties, null)
					: @object?.ToXml();
				onItemCompleted?.Invoke(element);
				xml.Add(element);
			});
			return xml;
		}

		/// <summary>
		/// Serializes the collection of objects to XML
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="objects">The object to serialize</param>
		/// <param name="onItemCompleted">The action to run on item completed</param>
		/// <returns></returns>
		public static XElement ToXml<T>(this List<T> objects, Action<XElement> onItemCompleted) where T : class
			=> objects.ToXml(null, false, onItemCompleted);

		/// <summary>
		/// Updates date-time attributes of this element
		/// </summary>
		/// <param name="element"></param>
		/// <param name="cultureInfo"></param>
		/// <param name="customFormat"></param>
		/// <param name="onCompleted"></param>
		/// <returns></returns>
		public static XElement UpdateDateTime(this XElement element, CultureInfo cultureInfo, string customFormat = null, Action<XElement> onCompleted = null)
		{
			if (element != null && DateTime.TryParse(element.Value, out var value))
			{
				var attribute = element.Attributes().FirstOrDefault(attr => attr.Name.LocalName.Equals("Full"));
				if (attribute == null)
					element.Add(new XAttribute("Full", value.ToString("hh:mm tt @ dddd - dd MMMM, yyyy", cultureInfo)));
				else
					attribute.Value = value.ToString("hh:mm tt @ dddd - dd MMMM, yyyy", cultureInfo);

				attribute = element.Attributes().FirstOrDefault(attr => attr.Name.LocalName.Equals("Long"));
				if (attribute == null)
					element.Add(new XAttribute("Long", value.ToString("dd/MM/yyyy HH:mm:ss")));
				else
					attribute.Value = value.ToString("dd/MM/yyyy HH:mm:ss", cultureInfo);

				attribute = element.Attributes().FirstOrDefault(attr => attr.Name.LocalName.Equals("LongAlternative"));
				if (attribute == null)
					element.Add(new XAttribute("LongAlternative", value.ToString("MM/dd/yyyy HH:mm:ss")));
				else
					attribute.Value = value.ToString("MM/dd/yyyy HH:mm:ss", cultureInfo);

				attribute = element.Attributes().FirstOrDefault(attr => attr.Name.LocalName.Equals("Short"));
				if (attribute == null)
					element.Add(new XAttribute("Short", value.ToString("hh:mm tt @ dd/MM/yyyy", cultureInfo)), new XAttribute("ShortAlternative", value.ToString("hh:mm tt @ MM/dd/yyyy", cultureInfo)));
				else
					attribute.Value = value.ToString("hh:mm tt @ dd/MM/yyyy", cultureInfo);

				attribute = element.Attributes().FirstOrDefault(attr => attr.Name.LocalName.Equals("ShortAlternative"));
				if (attribute == null)
					element.Add(new XAttribute(new XAttribute("ShortAlternative", value.ToString("hh:mm tt @ MM/dd/yyyy", cultureInfo))));
				else
					attribute.Value = value.ToString("hh:mm tt @ MM/dd/yyyy", cultureInfo);

				attribute = element.Attributes().FirstOrDefault(attr => attr.Name.LocalName.Equals("DateOnly"));
				if (attribute == null)
					element.Add(new XAttribute("DateOnly", value.ToString("dd/MM/yyyy")), new XAttribute("DateOnlyAlternative", value.ToString("MM/dd/yyyy")));
				else
					attribute.Value = value.ToString("dd/MM/yyyy", cultureInfo);

				attribute = element.Attributes().FirstOrDefault(attr => attr.Name.LocalName.Equals("DateOnlyAlternative"));
				if (attribute == null)
					element.Add(new XAttribute("DateOnlyAlternative", value.ToString("MM/dd/yyyy")));
				else
					attribute.Value = value.ToString("MM/dd/yyyy", cultureInfo);

				attribute = element.Attributes().FirstOrDefault(attr => attr.Name.LocalName.Equals("TimeOnly"));
				if (attribute == null)
					element.Add(new XAttribute("TimeOnly", value.ToString("HH:mm:ss")));
				else
					attribute.Value = value.ToString("HH:mm:ss", cultureInfo);

				attribute = element.Attributes().FirstOrDefault(attr => attr.Name.LocalName.Equals("TimeOnlyAlternative"));
				if (attribute == null)
					element.Add(new XAttribute("TimeOnlyAlternative", value.ToString("hh:mm tt", cultureInfo)));
				else
					attribute.Value = value.ToString("hh:mm tt", cultureInfo);

				if (!string.IsNullOrWhiteSpace(customFormat))
				{
					attribute = element.Attributes().FirstOrDefault(attr => attr.Name.LocalName.Equals("Custom"));
					if (attribute == null)
						element.Add(new XAttribute("Custom", value.ToString(customFormat)));
					else
						attribute.Value = value.ToString(customFormat);
				}
			}
			onCompleted?.Invoke(element);
			return element;
		}

		/// <summary>
		/// Updates numberic attributes of this element
		/// </summary>
		/// <param name="element"></param>
		/// <param name="isFloatingPointNumber"></param>
		/// <param name="cultureInfo"></param>
		/// <param name="customFormat"></param>
		/// <param name="onCompleted"></param>
		/// <returns></returns>
		public static XElement UpdateNumber(this XElement element, bool isFloatingPointNumber, CultureInfo cultureInfo, string customFormat = null, Action<XElement> onCompleted = null)
		{
			if (element != null)
			{
				if (isFloatingPointNumber && Decimal.TryParse(element.Value, out var floatingNumber))
				{
					var attribute = element.Attributes().FirstOrDefault(attr => attr.Name.LocalName.Equals("Formatted"));
					if (attribute == null)
						element.Add(new XAttribute("Formatted", floatingNumber.ToString("###,###,###,###,##0.##", cultureInfo)));
					else
						attribute.Value = floatingNumber.ToString("###,###,###,###,##0.##", cultureInfo);
					if (!string.IsNullOrWhiteSpace(customFormat))
					{
						attribute = element.Attributes().FirstOrDefault(attr => attr.Name.LocalName.Equals("Custom"));
						if (attribute == null)
							element.Add(new XAttribute("Custom", floatingNumber.ToString(customFormat)));
						else
							attribute.Value = floatingNumber.ToString(customFormat);
					}
				}
				else if (!isFloatingPointNumber && Int64.TryParse(element.Value, out var integralNumber))
				{
					var attribute = element.Attributes().FirstOrDefault(attr => attr.Name.LocalName.Equals("Formatted"));
					if (attribute == null)
						element.Add(new XAttribute("Formatted", integralNumber.ToString("###,###,###,###,##0", cultureInfo)));
					else
						attribute.Value = integralNumber.ToString("###,###,###,###,##0", cultureInfo);
					if (!string.IsNullOrWhiteSpace(customFormat))
					{
						attribute = element.Attributes().FirstOrDefault(attr => attr.Name.LocalName.Equals("Custom"));
						if (attribute == null)
							element.Add(new XAttribute("Custom", integralNumber.ToString(customFormat)));
						else
							attribute.Value = integralNumber.ToString(customFormat);
					}
				}
				else if (Decimal.TryParse(element.Value, out var number))
				{
					var attribute = element.Attributes().FirstOrDefault(attr => attr.Name.LocalName.Equals("Formatted"));
					if (attribute == null)
						element.Add(new XAttribute("Formatted", number.ToString("###,###,###,###,##0.##", cultureInfo)));
					else
						attribute.Value = number.ToString("###,###,###,###,##0.##", cultureInfo);
					if (!string.IsNullOrWhiteSpace(customFormat))
					{
						attribute = element.Attributes().FirstOrDefault(attr => attr.Name.LocalName.Equals("Custom"));
						if (attribute == null)
							element.Add(new XAttribute("Custom", number.ToString(customFormat)));
						else
							attribute.Value = number.ToString(customFormat);
					}
				}
			}
			onCompleted?.Invoke(element);
			return element;
		}
		#endregion

		#region Generate form controls
		static Dictionary<Type, string> FormControlDataTypes { get; } = new Dictionary<Type, string>()
		{
			{ typeof(string), "text" },
			{ typeof(char), "text" },
			{ typeof(char?), "text" },
			{ typeof(byte), "number" },
			{ typeof(byte?), "number" },
			{ typeof(sbyte), "number" },
			{ typeof(sbyte?), "number" },
			{ typeof(short), "number" },
			{ typeof(short?), "number" },
			{ typeof(ushort), "number" },
			{ typeof(ushort?), "number" },
			{ typeof(int), "number" },
			{ typeof(int?), "number" },
			{ typeof(uint), "number" },
			{ typeof(uint?), "number" },
			{ typeof(long), "number" },
			{ typeof(long?), "number" },
			{ typeof(ulong), "number" },
			{ typeof(ulong?), "number" },
			{ typeof(float), "number" },
			{ typeof(float?), "number" },
			{ typeof(double), "number" },
			{ typeof(double?), "number" },
			{ typeof(decimal), "number" },
			{ typeof(decimal?), "number" },
			{ typeof(DateTime), "date" },
			{ typeof(DateTime?), "date" },
			{ typeof(DateTimeOffset), "date" },
			{ typeof(DateTimeOffset?), "date" },
			{ typeof(bool), "" },
			{ typeof(Guid), "" },
			{ typeof(byte[]), "" }
		};

		static List<AttributeInfo> GetFormAttributes(Type type)
		{
			try
			{
				return RepositoryMediator.GetEntityDefinition(type)?.FormAttributes ?? type.GetPublicAttributes(attribute => !attribute.IsStatic).Select(attribute => new AttributeInfo(attribute)).ToList();
			}
			catch (Exception ex)
			{
				RepositoryMediator.WriteLogs($"Error occurred while preparing attributes to generate form controls => {ex.Message}", ex, LogLevel.Error);
				return type.GetPublicAttributes(attribute => !attribute.IsStatic).Select(attribute => new AttributeInfo(attribute)).ToList();
			}
		}

		static JObject GenerateControlOptions(this Type type, AttributeInfo attribute, string controlType, string label, string description, string placeHolder)
		{
			var info = attribute.GetCustomAttribute<FormControlAttribute>();
			var hidden = info != null ? info.Hidden : attribute.GetCustomAttribute<PrimaryKeyAttribute>() != null;

			var dataType = "Lookup".IsEquals(controlType) && !string.IsNullOrWhiteSpace(info?.LookupType)
				? info.LookupType
				: !string.IsNullOrWhiteSpace(info?.DataType)
					? info.DataType
					: attribute.IsEnum() || attribute.IsStringEnum()
						? "text"
						: "DatePicker".IsEquals(controlType)
							? "date"
							: RepositoryMediator.FormControlDataTypes.TryGetValue(attribute.Type, out var predefinedDataType)
								? predefinedDataType
								: null;

			var options = new JObject();
			if (!string.IsNullOrWhiteSpace(dataType))
				options["Type"] = dataType;

			options["Label"] = label;
			if (description != null)
				options["Description"] = description;
			if (placeHolder != null)
				options["PlaceHolder"] = placeHolder;

			if (!hidden)
			{
				if (info != null && info.Disabled)
					options["Disabled"] = true;
				if (info != null && info.ReadOnly)
					options["ReadOnly"] = true;
				if (info != null && info.AutoFocus)
					options["AutoFocus"] = true;
			}

			if (info?.ValidatePattern != null)
				options["ValidatePattern"] = info.ValidatePattern;

			var minValue = info?.MinValue ?? attribute?.MinValue;
			if (minValue != null)
				try
				{
					if (attribute.IsIntegralType())
						options["MinValue"] = minValue.CastAs<int>();
					else if (attribute.IsFloatingPointType())
						options["MinValue"] = minValue.CastAs<double>();
					else
						options["MinValue"] = minValue;
				}
				catch (Exception ex)
				{
					RepositoryMediator.WriteLogs($"Error occurred while prepare options [{attribute.Name}] => {ex.Message}", ex, LogLevel.Error);
				}

			var maxValue = info?.MaxValue ?? attribute?.MaxValue;
			if (maxValue != null)
				try
				{
					if (attribute.IsIntegralType())
						options["MaxValue"] = maxValue.CastAs<int>();
					else if (attribute.IsFloatingPointType())
						options["MaxValue"] = maxValue.CastAs<double>();
					else
						options["MaxValue"] = maxValue;
				}
				catch (Exception ex)
				{
					RepositoryMediator.WriteLogs($"Error occurred while prepare options [{attribute.Name}] => {ex.Message}", ex, LogLevel.Error);
				}

			var minLength = info != null && info.MinLength > 0
				? info.MinLength.ToString()
				: attribute.MinLength?.ToString();
			if (minLength != null && Int32.TryParse(minLength, out int minLen))
				options["MinLength"] = minLen;

			var maxLength = info != null && info.MaxLength > 0
				? info.MaxLength.ToString()
				: attribute.MaxLength.ToString();
			if (maxLength != null && Int32.TryParse(maxLength, out int maxLen))
				options["MaxLength"] = maxLen;

			if ("DatePicker".IsEquals(controlType) && info != null)
				options["DatePickerOptions"] = new JObject
				{
					{ "AllowTimes", info.DatePickerWithTimes }
				};

			if ("Select".IsEquals(controlType) || attribute.IsEnum() || attribute.IsStringEnum())
			{
				var selectValues = info?.SelectValues;
				if (selectValues == null && (attribute.IsEnum() || attribute.IsStringEnum()))
					try
					{
						selectValues = attribute.IsNullable()
							? attribute.IsStringEnum()
								? Enum.GetNames(attribute.Type.GetEnumUnderlyingType() ?? attribute.Type).Join("#;")
								: Enum.GetValues(attribute.Type.GetEnumUnderlyingType() ?? attribute.Type).ToEnumerable().Select(e => e.ToString()).Join("#;")
							: attribute.IsStringEnum()
								? Enum.GetNames(attribute.Type).Join("#;")
								: Enum.GetValues(attribute.Type).ToEnumerable().Select(e => e.ToString()).Join("#;");
					}
					catch (Exception ex)
					{
						RepositoryMediator.WriteLogs($"Error occurred while generating enums [{attribute.Name}] => {ex.Message}", ex, LogLevel.Error);
					}
				options["SelectOptions"] = new JObject
				{
					{ "Values", selectValues },
					{ "Multiple", info != null && info.Multiple },
					{ "AsBoxes", info != null && info.SelectAsBoxes },
					{ "Interface", info?.SelectInterface ?? "alert" },
					{ "RemoteURI", info?.SelectValuesRemoteURI }
				};
			}

			if ("Lookup".IsEquals(controlType) && info != null)
			{
				var definition = RepositoryMediator.GetEntityDefinition(type, false);
				var objectName = definition != null ? $"{definition.ObjectNamePrefix ?? ""}{definition.ObjectName ?? ""}{definition.ObjectNameSuffix ?? ""}" : type.GetTypeName(true);
				if (attribute.IsParentMapping() || attribute.IsMultipleParentMappings())
				{
					var parentType = definition.ParentType;
					definition = RepositoryMediator.GetEntityDefinition(parentType, false);
					objectName = definition != null ? $"{definition.ObjectNamePrefix ?? ""}{definition.ObjectName ?? ""}{definition.ObjectNameSuffix ?? ""}" : (parentType ?? type).GetTypeName(true);
				}
				else if (attribute.IsChildrenMappings())
				{
					var childType = attribute.GetCustomAttribute<ChildrenMappingsAttribute>().Type;
					definition = RepositoryMediator.GetEntityDefinition(childType, false);
					objectName = definition != null ? $"{definition.ObjectNamePrefix ?? ""}{definition.ObjectName ?? ""}{definition.ObjectNameSuffix ?? ""}" : (childType ?? type).GetTypeName(true);
				}
				options["LookupOptions"] = new JObject
				{
					{ "Multiple", info.Multiple },
					{ "ModalOptions", new JObject
						{
							{ "Component", null },
							{ "ComponentProps", new JObject
								{
									{ "organizationID", null },
									{ "moduleID", null },
									{ "contentTypeID", null },
									{ "objectName", objectName },
									{ "nested", info.LookupObjectIsNested },
									{ "multiple", info.Multiple }
								}
							}
						}
					}
				};
			}

			return options;
		}

		static string NormalizeLabel(this Type type, string attributeName, string label)
		{
			if (string.IsNullOrWhiteSpace(label))
				return null;

			label = label.Replace(StringComparison.OrdinalIgnoreCase, "[name]", attributeName).Replace(StringComparison.OrdinalIgnoreCase, "[nameLower]", attributeName.ToLower());
			label = label.Replace(StringComparison.OrdinalIgnoreCase, "[type]", type.GetTypeName(true)).Replace(StringComparison.OrdinalIgnoreCase, "[typeLower]", type.GetTypeName(true).ToLower());
			return label;
		}

		static JToken GenerateFormControl(this Type type, AttributeInfo attribute, int index = 0, string parentName = null, string parentLabel = null, string parentDescription = null, string parentPlaceHolder = null)
		{
			var info = attribute.GetCustomAttribute<FormControlAttribute>();

			if (info != null ? info.Excluded || !info.AsViewControl : attribute.IsIgnored())
			{
				if (RepositoryMediator.IsDebugEnabled)
					RepositoryMediator.WriteLogs($"By-pass the form control [{attribute.Name}] => {(info != null ? "Excluded" : "Ignored")}");
				return null;
			}

			JObject control;

			var isPrimaryKey = attribute.GetCustomAttribute<PrimaryKeyAttribute>() != null;
			var hidden = info != null ? info.Hidden : isPrimaryKey;
			var order = info != null && info.Order > -1 ? info.Order : index;

			var attributeName = $"{(string.IsNullOrWhiteSpace(parentName) ? "" : $"{parentName}.")}{attribute.Name}";
			var label = type.NormalizeLabel(attributeName, info?.Label ?? parentLabel ?? attribute.Name);
			var description = type.NormalizeLabel(attributeName, info?.Description ?? parentDescription);
			var placeHolder = type.NormalizeLabel(attributeName, info?.PlaceHolder ?? parentPlaceHolder);

			if (attribute.IsClassType() && !attribute.IsMappings())
			{
				var subControls = new JArray();
				if (attribute.IsGenericListOrHashSet() && attribute.GetFirstGenericTypeArgument().IsClassType())
				{
					var complexSubControls = new JArray();
					RepositoryMediator.GetFormAttributes(attribute.GetFirstGenericTypeArgument()).ForEach((subAttribute, subIndex) =>
					{
						var subControl = type.GenerateFormControl(subAttribute, subIndex, attributeName, info?.Label ?? parentLabel, info?.Description ?? parentDescription, info?.PlaceHolder ?? parentPlaceHolder);
						if (subControl != null)
							complexSubControls.Add(subControl);
					});
					subControls.Add(new JObject
					{
						{ "Extras", new JObject() },
						{ "Options", new JObject() },
						{ "SubControls", new JObject
							{
								{ "Controls", complexSubControls }
							}
						}
					});
				}
				else
					RepositoryMediator.GetFormAttributes(attribute.IsGenericListOrHashSet() ? attribute.GetFirstGenericTypeArgument() : attribute.Type).ForEach((subAttribute, subIndex) =>
					{
						var subControl = type.GenerateFormControl(subAttribute, subIndex, attributeName, info?.Label ?? parentLabel, info?.Description ?? parentDescription, info?.PlaceHolder ?? parentPlaceHolder);
						if (subControl != null)
							subControls.Add(subControl);
					});

				control = new JObject
				{
					{ "Name", attribute.Name },
					{ "Order", order },
					{ "Type", info?.ControlType },
					{ "Extras", new JObject() },
					{ "Options", new JObject
						{
							{ "Label", label },
							{ "Description", description }
						}
					},
					{ "SubControls", new JObject
						{
							{ "AsArray", info != null && info.AsArray },
							{ "Controls", subControls }
						}
					}
				};

				if (hidden)
					control["Hidden"] = hidden;
				else if (!string.IsNullOrWhiteSpace(info?.Segment))
					control["Segment"] = info.Segment;

				if (RepositoryMediator.IsDebugEnabled)
					RepositoryMediator.WriteLogs($"A form control of a class-type attribute was generated => {control.ToString(Newtonsoft.Json.Formatting.None)}");

				return control;
			}

			var controlType = !string.IsNullOrWhiteSpace(info?.ControlType)
				? info.ControlType
				: attribute.IsLargeString()
					? "TextEditor"
					: attribute.IsEnum() || attribute.IsStringEnum()
						? "Select"
						: attribute.IsMappings()
							? "Lookup"
							: attribute.IsPrimitiveType()
								? attribute.IsDateTimeType()
									? "DatePicker"
									: attribute.Type == typeof(bool)
										? "YesNo"
										: "TextBox"
								: null;

			control = new JObject
			{
				{ "Name", attribute.Name },
				{ "Order", order },
				{ "Type", controlType },
				{ "Extras", new JObject() },
				{ "Options", type.GenerateControlOptions(attribute, controlType, label, description, placeHolder) }
			};

			var required = !isPrimaryKey && (attribute.NotNull || (attribute.NotEmpty != null && attribute.NotEmpty.Value) || (info != null && info.Required));

			if (hidden)
				control["Hidden"] = hidden;
			else if (required)
				control["Required"] = required;
			if (!string.IsNullOrWhiteSpace(info?.Segment))
				control["Segment"] = info.Segment;

			if (info != null && info.AsArray)
			{
				control = new JObject
				{
					{ "Name", attribute.Name },
					{ "Order", order },
					{ "Extras", new JObject() },
					{ "Options", new JObject
						{
							{ "Label", label },
							{ "Description", description }
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
				if (!string.IsNullOrWhiteSpace(info?.Segment))
					control["Segment"] = info.Segment;
			}

			if (RepositoryMediator.IsDebugEnabled)
				RepositoryMediator.WriteLogs($"A form control of a primitive-type attribute was generated => {control.ToString(Newtonsoft.Json.Formatting.None)}");

			return control;
		}

		/// <summary>
		/// Generates the form controls of this type
		/// </summary>
		/// <param name="type"></param>
		/// <param name="onCompleted"></param>
		/// <returns></returns>
		public static JToken GenerateFormControls(Type type, Action<JToken> onCompleted = null)
		{
			var controls = new JArray();
			var attributes = RepositoryMediator.GetFormAttributes(type);
			if (RepositoryMediator.IsDebugEnabled)
				RepositoryMediator.WriteLogs($"Start to generate form controls ({type.GetTypeName(true)}) => {attributes.Select(attribute => attribute.Name).Join(", ")}]");

			attributes.ForEach((attribute, index) =>
			{
				try
				{
					var control = type.GenerateFormControl(attribute, index);
					if (control != null)
						controls.Add(control);
				}
				catch (Exception ex)
				{
					RepositoryMediator.WriteLogs($"Error occurred while generating form control [{attribute.Name}] => {ex.Message}", ex, LogLevel.Error);
				}
			});
			onCompleted?.Invoke(controls);
			return controls;
		}

		/// <summary>
		/// Generates the form controls of this type
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <returns></returns>
		public static JToken GenerateFormControls<T>() where T : class
			=> RepositoryMediator.GenerateFormControls(typeof(T));
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

			var identity = entity is RepositoryBase entityBase
				? entityBase.ID
				: entity.GetAttributeValue<string>(keyAttribute ?? "ID");

			return !string.IsNullOrWhiteSpace(identity)
				? identity
				: entity.GetAttributeValue<string>("Id");
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
		public static bool Set<T>(this ICache cache, T @object, int expirationTime = 0) where T : class
			=> @object != null && cache.Set(@object.GetCacheKey(), @object, expirationTime);

		/// <summary>
		/// Adds an object into cache storage
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="cache">The cache storage</param>
		/// <param name="object">The object to update into cache storage</param>
		/// <param name="expirationTime">The number that presents time for caching (in minutes)</param>
		public static Task<bool> SetAsync<T>(this ICache cache, T @object, int expirationTime = 0, CancellationToken cancellationToken = default) where T : class
			=> @object != null
				? cache.SetAsync(@object.GetCacheKey(), @object, expirationTime, cancellationToken)
				: Task.FromResult(false);

		/// <summary>
		/// Adds an object into cache storage
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="cache">The cache storage</param>
		/// <param name="object">The object to update into cache storage</param>
		public static Task<bool> SetAsync<T>(this ICache cache, T @object, CancellationToken cancellationToken) where T : class
			=> cache.SetAsync(@object, 0, cancellationToken);

		/// <summary>
		/// Adds an object into cache storage (when its no cached)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="cache">The cache storage</param>
		/// <param name="object">The object to update into cache storage</param>
		/// <param name="expirationTime">The number that presents time for caching (in minutes)</param>
		public static bool Add<T>(this ICache cache, T @object, int expirationTime = 0) where T : class
			=> @object != null && cache.Add(@object.GetCacheKey(), @object, expirationTime);

		/// <summary>
		/// Adds an object into cache storage (when its no cached)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="cache">The cache storage</param>
		/// <param name="object">The object to update into cache storage</param>
		/// <param name="expirationTime">The number that presents time for caching (in minutes)</param>
		public static Task<bool> AddAsync<T>(this ICache cache, T @object, int expirationTime = 0, CancellationToken cancellationToken = default) where T : class
			=> @object != null
				? cache.AddAsync(@object.GetCacheKey(), @object, expirationTime, cancellationToken)
				: Task.FromResult(false);

		/// <summary>
		/// Adds an object into cache storage (when its no cached)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="cache">The cache storage</param>
		/// <param name="object">The object to update into cache storage</param>
		public static Task<bool> AddAsync<T>(this ICache cache, T @object, CancellationToken cancellationToken) where T : class
			=> cache.AddAsync(@object, 0, cancellationToken);

		/// <summary>
		/// Replaces an object in the cache storage
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="cache">The cache storage</param>
		/// <param name="object">The object to update into cache storage</param>
		/// <param name="expirationTime">The number that presents time for caching (in minutes)</param>
		public static bool Replace<T>(this ICache cache, T @object, int expirationTime = 0) where T : class
			=> @object != null && cache.Replace(@object.GetCacheKey(), @object, expirationTime);

		/// <summary>
		/// Replaces an object in the cache storage
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="cache">The cache storage</param>
		/// <param name="object">The object to update into cache storage</param>
		/// <param name="expirationTime">The number that presents time for caching (in minutes)</param>
		public static Task<bool> ReplaceAsync<T>(this ICache cache, T @object, int expirationTime = 0, CancellationToken cancellationToken = default) where T : class
			=> @object != null
				? cache.ReplaceAsync(@object.GetCacheKey(), @object, expirationTime, cancellationToken)
				: Task.FromResult(false);

		/// <summary>
		/// Replaces an object in the cache storage
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="cache">The cache storage</param>
		/// <param name="object">The object to update into cache storage</param>
		public static Task<bool> ReplaceAsync<T>(this ICache cache, T @object, CancellationToken cancellationToken) where T : class
			=> cache.ReplaceAsync(@object, 0, cancellationToken);

		/// <summary>
		/// Adds the collection of objects into cache storage
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="cache">The cache storage</param>
		/// <param name="objects">The collection of objects</param>
		public static void Set<T>(this ICache cache, List<T> objects) where T : class
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
		public static Task SetAsync<T>(this ICache cache, List<T> objects, CancellationToken cancellationToken = default) where T : class
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
		public static void Set<T>(this ICache cache, IEnumerable<T> objects, int expirationTime = 0) where T : class
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
		public static Task SetAsync<T>(this ICache cache, IEnumerable<T> objects, int expirationTime = 0, CancellationToken cancellationToken = default) where T : class
			=> objects != null
				? cache.SetAsync(objects.Where(@object => @object != null).ToDictionary(@object => @object.GetCacheKey()), null, expirationTime, cancellationToken)
				: Task.CompletedTask;

		/// <summary>
		/// Adds the collection of objects into cache storage
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="cache">The cache storage</param>
		/// <param name="objects">The collection of objects</param>
		public static Task SetAsync<T>(this ICache cache, IEnumerable<T> objects, CancellationToken cancellationToken) where T : class
			=> cache.SetAsync(objects, 0, cancellationToken);

		/// <summary>
		/// Fetchs an object from cache storage
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="cache">The cache storage</param>
		/// <param name="identity">The string that presents identity of object need to get</param>
		/// <returns></returns>
		public static T Fetch<T>(this ICache cache, string identity) where T : class
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
		public static Task<T> FetchAsync<T>(this ICache cache, string identity, CancellationToken cancellationToken = default) where T : class
			=> !string.IsNullOrWhiteSpace(identity)
				? cache.GetAsync<T>(identity.GetCacheKey<T>(), cancellationToken)
				: Task.FromResult<T>(null);

		/// <summary>
		/// Removes a cached object from cache storage
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="cache">The cache storage</param>
		/// <param name="object">The object need to delete from cache storage</param>
		public static bool Remove<T>(this ICache cache, T @object) where T : class
			=> @object != null && (cache.Remove(@object.GetCacheKey()) || cache.Remove(@object.GetCacheKey() + ":Versions"));

		/// <summary>
		/// Removes a cached object from cache storage
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="cache">The cache storage</param>
		/// <param name="identity">The string that presents identity of object need to delete</param>
		public static bool Remove<T>(this ICache cache, string identity) where T : class
			=> !string.IsNullOrWhiteSpace(identity) && (cache.Remove(identity.GetCacheKey<T>()) || cache.Remove(identity.GetCacheKey<T>() + ":Versions"));

		/// <summary>
		/// Remove a cached object from cache storage
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="cache">The cache storage</param>
		/// <param name="object">The object need to delete from cache storage</param>
		public static async Task<bool> RemoveAsync<T>(this ICache cache, T @object, CancellationToken cancellationToken = default) where T : class
			=> @object != null && (await cache.RemoveAsync(@object.GetCacheKey(), cancellationToken).ConfigureAwait(false) || await cache.RemoveAsync(@object.GetCacheKey() + ":Versions", cancellationToken).ConfigureAwait(false));

		/// <summary>
		/// Removes a cached object from cache storage
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="cache">The cache storage</param>
		/// <param name="identity">The string that presents identity of object need to delete</param>
		public static async Task<bool> RemoveAsync<T>(this ICache cache, string identity, CancellationToken cancellationToken = default) where T : class
			=> !string.IsNullOrWhiteSpace(identity) && (await cache.RemoveAsync(identity.GetCacheKey<T>(), cancellationToken).ConfigureAwait(false) || await cache.RemoveAsync(identity.GetCacheKey<T>() + ":Versions", cancellationToken).ConfigureAwait(false));

		/// <summary>
		/// Checks existing of a cached object in cache storage
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="cache">The cache storage</param>
		/// <param name="identity">The string that presents identity of object</param>
		public static bool Exists<T>(this ICache cache, string identity) where T : class
			=> !string.IsNullOrWhiteSpace(identity) && cache.Exists(identity.GetCacheKey<T>());

		/// <summary>
		/// Checks existing of a cached object in cache storage
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="cache">The cache storage</param>
		/// <param name="identity">The string that presents identity of object</param>
		public static Task<bool> ExistsAsync<T>(this ICache cache, string identity, CancellationToken cancellationToken = default) where T : class
			=> !string.IsNullOrWhiteSpace(identity)
				? cache.ExistsAsync(identity.GetCacheKey<T>(), cancellationToken)
				: Task.FromResult(false);
		#endregion

		#region Property extension methods
		internal static List<ObjectService.AttributeInfo> GetPublicProperties(this Type type, Func<ObjectService.AttributeInfo, bool> predicate = null)
		{
			var attributes = ObjectService.GetAttributes(type, attribute => attribute.IsProperty && attribute.IsPublic && !attribute.IsStatic);
			return predicate != null ? attributes?.Where(attribute => predicate(attribute)).ToList() : attributes;
		}

		internal static List<ObjectService.AttributeInfo> GetPublicProperties<T>(Func<ObjectService.AttributeInfo, bool> predicate = null) where T : class
			=> RepositoryMediator.GetPublicProperties(typeof(T), predicate);

		internal static List<ObjectService.AttributeInfo> GetPublicProperties(this object @object, Func<ObjectService.AttributeInfo, bool> predicate = null)
			=> RepositoryMediator.GetPublicProperties(@object?.GetType(), predicate);

		internal static Tuple<Dictionary<string, AttributeInfo>, Dictionary<string, ExtendedPropertyDefinition>> GetProperties<T>(string businessRepositoryEntityID, EntityDefinition definition = null, bool lowerCaseKeys = false) where T : class
		{
			definition = definition ?? RepositoryMediator.GetEntityDefinition(typeof(T));

			var standardProperties = definition != null
				? definition.Attributes.Where(attribute => !attribute.IsMappings()).ToDictionary(attribute => lowerCaseKeys ? attribute.Name.ToLower() : attribute.Name)
				: RepositoryMediator.GetPublicProperties<T>().ToDictionary(attribute => lowerCaseKeys ? attribute.Name.ToLower() : attribute.Name, attribute => new AttributeInfo(attribute));

			var extendedProperties = definition != null && definition.Type.CreateInstance().IsGotExtendedProperties(businessRepositoryEntityID, definition)
				? definition.BusinessRepositoryEntities[businessRepositoryEntityID].ExtendedPropertyDefinitions.ToDictionary(attribute => lowerCaseKeys ? attribute.Name.ToLower() : attribute.Name)
				: null;

			return new Tuple<Dictionary<string, AttributeInfo>, Dictionary<string, ExtendedPropertyDefinition>>(standardProperties, extendedProperties);
		}

		internal static List<string> GetAssociatedParentIDs<T>(this IFilterBy<T> filter, EntityDefinition definition = null) where T : class
		{
			definition = definition ?? RepositoryMediator.GetEntityDefinition<T>();
			var parentMappingProperty = definition.GetParentMappingAttributeName();
			if (string.IsNullOrWhiteSpace(parentMappingProperty))
				return null;

			if (filter is FilterBy<T>)
				return (filter as FilterBy<T>).Attribute.Equals(parentMappingProperty)
					? new List<string> { (filter as FilterBy<T>).Value as string }
					: null;

			var children = (filter as FilterBys<T>).Children;
			if (children == null || children.Count < 1)
				return null;

			var parentIDs = new List<string>();

			children.ForEach(info =>
			{
				if (info is FilterBy<T> && (info as FilterBy<T>).Attribute.Equals(parentMappingProperty))
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
				? parentIDs.Distinct(StringComparer.OrdinalIgnoreCase).ToList()
				: null;
		}

		/// <summary>
		/// Gets the state that determines this object is got extended properties or not
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="object"></param>
		/// <param name="businessRepositoryEntityID"></param>
		/// <param name="definition"></param>
		/// <returns></returns>
		public static bool IsGotExtendedProperties<T>(this T @object, string businessRepositoryEntityID = null, EntityDefinition definition = null) where T : class
		{
			if (!(@object is IBusinessEntity))
				return false;

			businessRepositoryEntityID = string.IsNullOrWhiteSpace(businessRepositoryEntityID)
				? (@object as IBusinessEntity).RepositoryEntityID
				: businessRepositoryEntityID;

			if (string.IsNullOrWhiteSpace(businessRepositoryEntityID))
				return false;

			definition = definition ?? RepositoryMediator.GetEntityDefinition(typeof(T));
			if (definition == null || definition.BusinessRepositoryEntities == null)
				return false;

			var attributes = definition.BusinessRepositoryEntities.ContainsKey(businessRepositoryEntityID)
				? definition.BusinessRepositoryEntities[businessRepositoryEntityID].ExtendedPropertyDefinitions
				: null;

			return attributes != null && attributes.Count > 0;
		}

		internal static Tuple<string, string, string> GetMapInfo(this AttributeInfo attribute, EntityDefinition definition)
		{
			var info = attribute?.GetCustomAttribute<MappingsAttribute>();
			return info != null
				? new Tuple<string, string, string>
				(
					string.IsNullOrWhiteSpace(info.TableName) ? $"{definition.TableName}_{attribute.Name}_Mappings" : info.TableName,
					string.IsNullOrWhiteSpace(info.LinkColumn) ? $"{definition.Type.GetTypeName(true)}ID" : info.LinkColumn,
					string.IsNullOrWhiteSpace(info.MapColumn) ? $"{attribute.Name}ID" : info.MapColumn
				)
				: null;
		}

		internal static AttributeInfo GetParentMappingAttribute(this EntityDefinition definition)
			=> definition?.Attributes.FirstOrDefault(attribute => attribute.IsParentMapping());

		internal static string GetParentMappingAttributeName(this EntityDefinition definition)
			=> definition?.GetParentMappingAttribute()?.Name;

		internal static AttributeInfo GetMultiParentMappingsAttribute(this EntityDefinition definition)
			=> definition?.Attributes.FirstOrDefault(attribute => attribute.IsMultipleParentMappings());

		internal static bool IsGotMultipleParentMappings(this EntityDefinition definition)
			=> definition?.GetMultiParentMappingsAttribute() != null;

		internal static string GetMultiParentMappingsAttributeName(this EntityDefinition definition)
			=> definition?.GetMultiParentMappingsAttribute()?.Name;

		/// <summary>
		/// Validates all the name of the extended property definitions of an entity definition
		/// </summary>
		/// <param name="entityDefinition"></param>
		/// <param name="repositoryEntityID"></param>
		/// <remarks>An exception will be thrown if a name is invalid</remarks>
		public static void ValidateExtendedPropertyDefinitions(this EntityDefinition entityDefinition, string repositoryEntityID)
		{
			if (entityDefinition == null || !entityDefinition.BusinessRepositoryEntities.TryGetValue(repositoryEntityID, out var repositoryEntity) || repositoryEntity == null || repositoryEntity.ExtendedPropertyDefinitions == null)
				return;

			var attributes = entityDefinition.Attributes.Select(attribute => attribute.Name.ToLower()).ToHashSet();
			repositoryEntity.ExtendedPropertyDefinitions.ForEach(propertyDefinition =>
			{
				if (attributes.Contains(propertyDefinition.Name.ToLower()))
					throw new InformationInvalidException($"The name ({propertyDefinition.Name}) is already used");

				try
				{
					ExtendedPropertyDefinition.Validate(propertyDefinition.Name);
				}
				catch (Exception ex)
				{
					throw new InformationInvalidException($"{propertyDefinition.Name}) => {ex.Message}", ex);
				}
			});
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