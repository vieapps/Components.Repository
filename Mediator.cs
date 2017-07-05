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
using Newtonsoft.Json.Linq;

using net.vieapps.Components.Caching;
using net.vieapps.Components.Utility;
#endregion

namespace net.vieapps.Components.Repository
{
	/// <summary>
	/// Allow to use the Repository functionality without direct reference to <see cref="RepositoryBase">RepositoryBase</see>
	/// </summary>
	public static class RepositoryMediator
	{

		#region Caching
		/// <summary>
		/// Gets the identity/primary-key of the entity object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="entity">The entity object (usually is object of sub-class of <see cref="RepositoryBase">RepositoryBase</see>)</param>
		/// <returns></returns>
		public static string GetEntityID<T>(this T entity) where T : class
		{
			var identity = entity is RepositoryBase
				? (entity as RepositoryBase).ID
				: entity.GetAttributeValue("ID") as string;
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
		/// <param name="cacheStorage">The cache storage</param>
		/// <param name="object">The object</param>
		public static bool Set<T>(this CacheManager cacheStorage, T @object) where T : class
		{
			return !object.ReferenceEquals(@object, null)
				? cacheStorage.Set(@object.GetCacheKey(), @object)
				: false;
		}

		/// <summary>
		/// Adds the collection of objects into cache storage
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="cacheStorage">The cache storage</param>
		/// <param name="objects">The collection of objects</param>
		public static void Set<T>(this CacheManager cacheStorage, List<T> objects) where T : class
		{
			if (!object.ReferenceEquals(objects, null))
				cacheStorage.Set(objects.ToDictionary(o => o.GetCacheKey()));
		}

		/// <summary>
		/// Adds an object into cache storage
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="cacheStorage">The cache storage</param>
		/// <param name="object">The object</param>
		public static Task<bool> SetAsync<T>(this CacheManager cacheStorage, T @object) where T : class
		{
			return !object.ReferenceEquals(@object, null)
				? cacheStorage.SetAsync(@object.GetCacheKey(), @object)
				: Task.FromResult<bool>(false);
		}

		/// <summary>
		/// Adds the collection of objects into cache storage
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="cacheStorage">The cache storage</param>
		/// <param name="objects">The collection of objects</param>
		public static Task SetAsync<T>(this CacheManager cacheStorage, List<T> objects) where T : class
		{
			return !object.ReferenceEquals(objects, null)
				? cacheStorage.SetAsync(objects.ToDictionary(o => o.GetCacheKey()))
				: Task.CompletedTask;
		}

		/// <summary>
		/// Adds an object into cache storage as absolute expire item
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="cacheStorage">The cache storage</param>
		/// <param name="object">The object</param>
		/// <param name="expirationTime">The integer number that present expiration times (in minutes)</param>
		public static bool SetAbsolute<T>(this CacheManager cacheStorage, T @object, int expirationTime = 0) where T : class
		{
			return !object.ReferenceEquals(@object, null)
				? cacheStorage.SetAbsolute(@object.GetCacheKey(), @object, expirationTime)
				: false;
		}

		/// <summary>
		/// Adds an object into cache storage as absolute expire item
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="cacheStorage">The cache storage</param>
		/// <param name="identity">The identity of an object</param>
		/// <param name="value">The value</param>
		/// <param name="expirationTime">The integer number that present expiration times (in minutes)</param>
		public static bool SetAbsolute<T>(this CacheManager cacheStorage, string identity, object value, int expirationTime = 0) where T : class
		{
			return !object.ReferenceEquals(identity, null)
				? cacheStorage.SetAbsolute(identity.GetCacheKey<T>(), value, expirationTime)
				: false;
		}

		/// <summary>
		/// Adds an object into cache storage as absolute expire item
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="cacheStorage">The cache storage</param>
		/// <param name="object">The object</param>
		/// <param name="expirationTime">The integer number that present expiration times (in minutes)</param>
		public static Task<bool> SetAbsoluteAsync<T>(this CacheManager cacheStorage, T @object, int expirationTime = 0) where T : class
		{
			return !object.ReferenceEquals(@object, null)
				? cacheStorage.SetAbsoluteAsync(@object.GetCacheKey(), @object, expirationTime)
				: Task.FromResult<bool>(false);
		}

		/// <summary>
		/// Adds an object into cache storage as absolute expire item
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="cacheStorage">The cache storage</param>
		/// <param name="identity">The identity of an object</param>
		/// <param name="value">The value</param>
		/// <param name="expirationTime">The integer number that present expiration times (in minutes)</param>
		public static Task<bool> SetAbsoluteAsync<T>(this CacheManager cacheStorage, string identity, object value, int expirationTime = 0) where T : class
		{
			return !object.ReferenceEquals(identity, null)
				? cacheStorage.SetAbsoluteAsync(identity.GetCacheKey<T>(), value, expirationTime)
				: Task.FromResult<bool>(false);
		}

		/// <summary>
		/// Adds an object into cache storage (when its no cached)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="cacheStorage">The cache storage</param>
		/// <param name="object">The object</param>
		public static bool SetIfNotExists<T>(this CacheManager cacheStorage, T @object) where T : class
		{
			return !object.ReferenceEquals(@object, null)
				? cacheStorage.SetIfNotExists(@object.GetCacheKey(), @object)
				: false;
		}

		/// <summary>
		/// Adds an object into cache storage (when its no cached)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="cacheStorage">The cache storage</param>
		/// <param name="object">The object</param>
		public static Task<bool> SetIfNotExistsAsync<T>(this CacheManager cacheStorage, T @object) where T : class
		{
			return !object.ReferenceEquals(@object, null)
				? cacheStorage.SetIfNotExistsAsync(@object.GetCacheKey(), @object)
				: Task.FromResult<bool>(false);
		}

		/// <summary>
		/// Adds an object into cache storage (when its cached, means replace an existed item)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="cacheStorage">The cache storage</param>
		/// <param name="object">The object</param>
		public static bool SetIfAlreadyExists<T>(this CacheManager cacheStorage, T @object) where T : class
		{
			return !object.ReferenceEquals(@object, null)
				? cacheStorage.SetIfAlreadyExists(@object.GetCacheKey(), @object)
				: false;
		}

		/// <summary>
		/// Adds an object into cache storage (when its cached, means replace an existed item)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="cacheStorage">The cache storage</param>
		/// <param name="object">The object</param>
		public static Task<bool> SetIfAlreadyExistsAsync<T>(this CacheManager cacheStorage, T @object) where T : class
		{
			return !object.ReferenceEquals(@object, null)
				? cacheStorage.SetIfAlreadyExistsAsync(@object.GetCacheKey(), @object)
				: Task.FromResult<bool>(false);
		}

		/// <summary>
		/// Gets an object from cache storage
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="cacheStorage">The cache storage</param>
		/// <param name="identity">The string that presents identity of object need to get</param>
		/// <returns></returns>
		public static T Get<T>(this CacheManager cacheStorage, string identity) where T : class
		{
			return !string.IsNullOrWhiteSpace(identity)
				? cacheStorage.Get<T>(identity.GetCacheKey<T>())
				: null;
		}

		/// <summary>
		/// Gets an object from cache storage
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="cacheStorage">The cache storage</param>
		/// <param name="identity">The string that presents identity of object need to get</param>
		/// <returns></returns>
		public static Task<T> GetAsync<T>(this CacheManager cacheStorage, string identity) where T : class
		{
			return !string.IsNullOrWhiteSpace(identity)
				? cacheStorage.GetAsync<T>(identity.GetCacheKey<T>())
				: Task.FromResult<T>(null);
		}

		/// <summary>
		/// Removes a cached object from cache storage
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="cacheStorage">The cache storage</param>
		/// <param name="object">The object need to delete from cache storage</param>
		public static bool Remove<T>(this CacheManager cacheStorage, T @object) where T : class
		{
			return !object.ReferenceEquals(@object, null)
				? cacheStorage.Remove(@object.GetCacheKey())
				: false;
		}

		/// <summary>
		/// Removes a cached object from cache storage
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="cacheStorage">The cache storage</param>
		/// <param name="identity">The string that presents identity of object need to delete</param>
		public static bool Remove<T>(this CacheManager cacheStorage, string identity) where T : class
		{
			return !string.IsNullOrWhiteSpace(identity)
				? cacheStorage.Remove(identity.GetCacheKey<T>())
				: false;
		}

		/// <summary>
		/// Remove a cached object from cache storage
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="cacheStorage">The cache storage</param>
		/// <param name="object">The object need to delete from cache storage</param>
		public static Task<bool> RemoveAsync<T>(this CacheManager cacheStorage, T @object) where T : class
		{
			return !object.ReferenceEquals(@object, null)
				? cacheStorage.RemoveAsync(@object.GetCacheKey())
				: Task.FromResult<bool>(false);
		}

		/// <summary>
		/// Removes a cached object from cache storage
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="cacheStorage">The cache storage</param>
		/// <param name="identity">The string that presents identity of object need to delete</param>
		public static Task<bool> RemoveAsync<T>(this CacheManager cacheStorage, string identity) where T : class
		{
			return !string.IsNullOrWhiteSpace(identity)
				? cacheStorage.RemoveAsync(identity.GetCacheKey<T>())
				: Task.FromResult<bool>(false);
		}

		/// <summary>
		/// Checks existing of a cached object in cache storage
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="cacheStorage">The cache storage</param>
		/// <param name="identity">The string that presents identity of object</param>
		public static bool Exists<T>(this CacheManager cacheStorage, string identity) where T : class
		{
			return !string.IsNullOrWhiteSpace(identity)
				? cacheStorage.Exists(identity.GetCacheKey<T>())
				: false;
		}

		/// <summary>
		/// Checks existing of a cached object in cache storage
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="cacheStorage">The cache storage</param>
		/// <param name="identity">The string that presents identity of object</param>
		public static Task<bool> ExistsAsync<T>(this CacheManager cacheStorage, string identity) where T : class
		{
			return !string.IsNullOrWhiteSpace(identity)
				? cacheStorage.ExistsAsync(identity.GetCacheKey<T>())
				: Task.FromResult<bool>(false);
		}
		#endregion

		#region Collection
		/// <summary>
		/// Adds an object into this collection
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="dictionary"></param>
		/// <param name="object"></param>
		public static void Add<T>(this Dictionary<string, T> dictionary, T @object) where T : RepositoryBase
		{
			if (!dictionary.ContainsKey(@object.ID))
				dictionary.Add(@object.ID, @object);
		}

		/// <summary>
		/// Adds an object into this collection
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="object"></param>
		public static void Add<T>(this Collection<string, T> collection, T @object) where T : RepositoryBase
		{
			if (!collection.ContainsKey(@object.ID))
				collection.Add(@object.ID, @object);
		}
		#endregion

		#region Check ignored
		internal static bool IsIgnoredWhenSql(this ObjectService.AttributeInfo attribute)
		{
			return attribute.Info.GetCustomAttributes(typeof(IgnoreWhenSqlAttribute), true).Length > 0
				? true
				: false;
		}

		internal static bool IsIgnoredWhenNoSql(this ObjectService.AttributeInfo attribute)
		{
			var attrs = attribute.Info.GetCustomAttributes(typeof(IgnoreWhenNoSqlAttribute), true);
			if (attrs.Length > 0)
				return true;

			// check ignore with settings of MongoDB
			attrs = attribute.Info.GetCustomAttributes(typeof(MongoDB.Bson.Serialization.Attributes.BsonIgnoreAttribute), true);
			if (attrs.Length > 0)
				return true;

			attrs = attribute.Info.GetCustomAttributes(typeof(MongoDB.Bson.Serialization.Attributes.BsonIgnoreIfNullAttribute), true);
			if (attrs.Length > 0)
				return true;

			return false;
		}

		internal static bool IsIgnored(this ObjectService.AttributeInfo attribute)
		{
			return attribute.Info.GetCustomAttributes(typeof(IgnoreAttribute), true).Length > 0
				? true
				: attribute.IsIgnoredWhenSql() || attribute.IsIgnoredWhenNoSql();
		}
		#endregion

#if DEBUG
		public static Dictionary<string, RepositoryDefinition> RepositoryDefinitions = new Dictionary<string, RepositoryDefinition>();
		public static Dictionary<string, RepositoryEntityDefinition> EntityDefinitions = new Dictionary<string, RepositoryEntityDefinition>();
		public static Dictionary<string, RepositoryDataSource> DataSources = new Dictionary<string, RepositoryDataSource>();
#else
		internal static Dictionary<string, RepositoryDefinition> RepositoryDefinitions = new Dictionary<string, RepositoryDefinition>();
		internal static Dictionary<string, EntityDefinition> EntityDefinitions = new Dictionary<string, EntityDefinition>();
		internal static Dictionary<string, DataSource> DataSources = new Dictionary<string, DataSource>();
#endif

		#region Definitions
		/// <summary>
		/// Gets the repository definition that matched with the type
		/// </summary>
		/// <param name="name">The string that presents the type of repository</param>
		/// <returns></returns>
		public static RepositoryDefinition GetRepositoryDefinition(string name)
		{
			return !string.IsNullOrWhiteSpace(name) && RepositoryMediator.RepositoryDefinitions.ContainsKey(name)
				? RepositoryMediator.RepositoryDefinitions[name]
				: null;
		}

		/// <summary>
		/// Gets the repository entity definition that matched with the type
		/// </summary>
		/// <param name="type"></param>
		/// <returns></returns>
		public static EntityDefinition GetEntityDefinition(Type type)
		{
			var name = type != null ? type.GetTypeName() : null;
			return name != null && RepositoryMediator.EntityDefinitions.ContainsKey(name)
				? RepositoryMediator.EntityDefinitions[name]
				: null;
		}

		/// <summary>
		/// Gets the repository entity definition that matched with the type of a class
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <returns></returns>
		public static EntityDefinition GetEntityDefinition<T>() where T : class
		{
			return RepositoryMediator.GetEntityDefinition(typeof(T));
		}
		#endregion

		#region Data Source
		/// <summary>
		/// Gets the primary data source
		/// </summary>
		/// <param name="aliasTypeName">The string that presents alias of a type name</param>
		/// <param name="definition">The definition of a repository entity</param>
		/// <returns></returns>
		public static DataSource GetPrimaryDataSource(string aliasTypeName, EntityDefinition definition)
		{
			var dataSource = definition.PrimaryDataSource;
			if (dataSource == null)
			{
				var parent = !string.IsNullOrWhiteSpace(aliasTypeName)
					? RepositoryMediator.GetRepositoryDefinition(aliasTypeName)
					: definition.Parent;

				if (parent != null)
					dataSource = parent.PrimaryDataSource;
			}

			if (dataSource == null)
				throw new RepositoryOperationException("The primary data-source named '" + definition.PrimaryDataSourceName + "' (of '" + definition.Type.GetTypeName() + "') is not found");
			return dataSource;
		}

		/// <summary>
		/// Gets the primary data source
		/// </summary>
		/// <param name="context">The working context of a repository entity</param>
		/// <returns></returns>
		public static DataSource GetPrimaryDataSource(RepositoryContext context)
		{
			return RepositoryMediator.GetPrimaryDataSource(context.AliasTypeName, context.EntityDefinition);
		}

		/// <summary>
		/// Gets the primary data source
		/// </summary>
		/// <param name="definition">The definition of a repository entity</param>
		/// <returns></returns>
		public static DataSource GetPrimaryDataSource(EntityDefinition definition)
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
			var dataSource = definition.SecondaryDataSource;
			if (dataSource == null)
			{
				var parent = !string.IsNullOrWhiteSpace(aliasTypeName)
					? RepositoryMediator.GetRepositoryDefinition(aliasTypeName)
					: definition.Parent;

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
		public static DataSource GetSecondaryDataSource(RepositoryContext context)
		{
			return RepositoryMediator.GetSecondaryDataSource(context.AliasTypeName, context.EntityDefinition);
		}

		/// <summary>
		/// Gets the secondary data source
		/// </summary>
		/// <param name="definition">The definition of a repository entity</param>
		/// <returns></returns>
		public static DataSource GetSecondaryDataSource(EntityDefinition definition)
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
		public static ConnectionStringSettings GetConnectionStringSettings(DataSource dataSource)
		{
			return dataSource != null
				? RepositoryMediator.GetConnectionStringSettings(dataSource.ConnectionStringName)
				: null;
		}

		/// <summary>
		/// Gets the connection string of the data source (for working with SQL/NoSQL database)
		/// </summary>
		/// <param name="dataSource">The data source</param>
		/// <returns></returns>
		public static string GetConnectionString(DataSource dataSource)
		{
			var connectionStringSettings = RepositoryMediator.GetConnectionStringSettings(dataSource);
			return connectionStringSettings != null
				? connectionStringSettings.ConnectionString
				: null;
		}

		/// <summary>
		/// Gets the connection string of primary data source of a entity definition
		/// </summary>
		/// <param name="aliasTypeName">The string that presents alias of a type name</param>
		/// <param name="definition">The definition of a repository entity</param>
		/// <returns></returns>
		public static string GetPrimaryConnectionString(string aliasTypeName, EntityDefinition definition)
		{
			return RepositoryMediator.GetConnectionString(RepositoryMediator.GetPrimaryDataSource(aliasTypeName, definition));
		}

		/// <summary>
		/// Gets the connection string of primary data source of a entity definition
		/// </summary>
		/// <param name="definition">The definition of a repository entity</param>
		/// <returns></returns>
		public static string GetPrimaryConnectionString(EntityDefinition definition)
		{
			return RepositoryMediator.GetPrimaryConnectionString(null, definition);
		}

		/// <summary>
		/// Gets the connection string of secondary data source of a entity definition
		/// </summary>
		/// <param name="aliasTypeName">The string that presents alias of a type name</param>
		/// <param name="definition">The definition of a repository entity</param>
		/// <returns></returns>
		public static string GetSecondaryConnectionString(string aliasTypeName, EntityDefinition definition)
		{
			return RepositoryMediator.GetConnectionString(RepositoryMediator.GetSecondaryDataSource(aliasTypeName, definition));
		}

		/// <summary>
		/// Gets the connection string of secondary data source of a entity definition
		/// </summary>
		/// <param name="definition">The definition of a repository entity</param>
		/// <returns></returns>
		public static string GetSecondaryConnectionString(EntityDefinition definition)
		{
			return RepositoryMediator.GetSecondaryConnectionString(null, definition);
		}
		#endregion

		#region Validate
		internal static bool Validate(EntityDefinition definition, Dictionary<string, object> stateData)
		{
			var changed = false;

			foreach (var attribute in definition.Attributes)
			{
				if (!attribute.CanRead || !attribute.CanWrite)
					continue;

				object value = stateData[attribute.Name];

				if (object.ReferenceEquals(value, null))
				{
					if (attribute.Name.Equals(definition.PrimaryKey))
						throw new InformationRequiredException("The primary-key value is required");
					else if (attribute.NotNull)
						throw new InformationRequiredException("The " + (attribute.IsPublic ? "property" : "attribute") + " named '" + attribute.Name + "' is required (doesn't allow null)");
				}

				else if (attribute.Type.IsStringType() && !attribute.IsCLOB)
				{
					var maxLength = attribute.MaxLength > 0 ? attribute.MaxLength : 4000;
					if ((value as string).Length > maxLength)
					{
						changed = true;
						stateData[attribute.Name] = (value as string).Left(maxLength);
					}
				}
			}

			return changed;
		}
		#endregion

		#region Create
		/// <summary>
		/// Creates new instance of object in repository
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object to create new instance in repository</param>
		public static void Create<T>(RepositoryContext context, string aliasTypeName, T @object) where T : class
		{
			// prepare
			context.Operation = RepositoryOperations.Create;
			context.EntityDefinition = RepositoryMediator.GetEntityDefinition<T>();
			context.AliasTypeName = aliasTypeName;

			// validate & re-update object
			var currentState = context.SetCurrentState(@object);
			if (RepositoryMediator.Validate(context.EntityDefinition, currentState))
			{
				// re-update object
				currentState.ForEach(data =>
				{
					@object.SetAttributeValue(data.Key, data.Value);
				});

				// update state
				context.SetCurrentState(@object, currentState);
			}

			// call pre-handlers
			if (RepositoryMediator.CallPreHandlers(context, @object))
				return;

			// create
			var primaryDataSource = RepositoryMediator.GetPrimaryDataSource(context);
			if (primaryDataSource.Mode.Equals(RepositoryModes.NoSQL))
				NoSqlHelper.Create<T>(context, primaryDataSource, @object);
			else if (primaryDataSource.Mode.Equals(RepositoryModes.SQL))
				SqlHelper.Create<T>(context, primaryDataSource, @object);

			// update in cache storage
			if (!object.ReferenceEquals(context.EntityDefinition.CacheStorage, null))
#if DEBUG
				if (context.EntityDefinition.CacheStorage.Set(@object))
					RepositoryMediator.WriteLogs("CREATE: Add the object into the cache storage successful [" + @object.GetCacheKey(false) + "]");
#else
				context.EntityDefinition.CacheStorage.Set(@object);
#endif

			// call post-handlers
			RepositoryMediator.CallPostHandlers(context, @object);

			// TO DO: sync to other data sources
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
				try
				{
					RepositoryMediator.Create(context, aliasTypeName, @object);
				}
				catch (Exception ex)
				{
					context.Exception = ex;
					throw new RepositoryOperationException(ex);
				}
			}
		}

		/// <summary>
		/// Creates new instance of object in repository
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object to create instance in repository</param>
		/// <param name="cancellationToken">The cancellation token</param>
		public static async Task CreateAsync<T>(RepositoryContext context, string aliasTypeName, T @object, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			context.Operation = RepositoryOperations.Create;
			context.EntityDefinition = RepositoryMediator.GetEntityDefinition<T>();
			context.AliasTypeName = aliasTypeName;

			// validate & re-update object
			var currentState = context.SetCurrentState(@object);
			if (RepositoryMediator.Validate(context.EntityDefinition, currentState))
			{
				// re-update object
				currentState.ForEach(data =>
				{
					@object.SetAttributeValue(data.Key, data.Value);
				});

				// update state
				context.SetCurrentState(@object, currentState);
			}

			// call pre-handlers
			if (await RepositoryMediator.CallPreHandlersAsync(context, @object, cancellationToken))
				return;

			// create
			var primaryDataSource = RepositoryMediator.GetPrimaryDataSource(context);
			if (primaryDataSource.Mode.Equals(RepositoryModes.NoSQL))
				await NoSqlHelper.CreateAsync<T>(context, primaryDataSource, @object, null, cancellationToken);
			else if (primaryDataSource.Mode.Equals(RepositoryModes.SQL))
				await SqlHelper.CreateAsync<T>(context, primaryDataSource, @object, cancellationToken);

			// update in cache storage
			if (!object.ReferenceEquals(context.EntityDefinition.CacheStorage, null))
#if DEBUG
				if (await context.EntityDefinition.CacheStorage.SetAsync(@object))
					RepositoryMediator.WriteLogs("CREATE: Add the object into the cache storage successful [" + @object.GetCacheKey(false) + "]");
#else
				await context.EntityDefinition.CacheStorage.SetAsync(@object);
#endif

			// call post-handlers
			await RepositoryMediator.CallPostHandlersAsync(context, @object, cancellationToken);

			// TO DO: sync to other data sources

		}

		/// <summary>
		/// Creates new instance of object in repository
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object to create instance in repository</param>
		public static Task CreateAsync<T>(string aliasTypeName, T @object, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			using (var context = new RepositoryContext())
			{
				try
				{
					return RepositoryMediator.CreateAsync(context, aliasTypeName, @object, cancellationToken);
				}
				catch (Exception ex)
				{
					context.Exception = ex;
					return Task.FromException(new RepositoryOperationException(ex));
				}
			}
		}
		#endregion

		#region Get
		/// <summary>
		/// Finds the first instance of object that matched with the filter
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">Filter expression</param>
		/// <param name="sort">Sort expression</param>
		/// <returns></returns>
		public static T Get<T>(RepositoryContext context, string aliasTypeName, IFilterBy<T> filter, SortBy<T> sort = null) where T : class
		{
			// prepare
			context.Operation = RepositoryOperations.Get;
			context.EntityDefinition = RepositoryMediator.GetEntityDefinition<T>();
			context.AliasTypeName = aliasTypeName;

			// find
			var primaryDataSource = RepositoryMediator.GetPrimaryDataSource(context);
			return primaryDataSource.Mode.Equals(RepositoryModes.NoSQL)
				? NoSqlHelper.Get<T>(context, primaryDataSource, filter, sort)
				: primaryDataSource.Mode.Equals(RepositoryModes.SQL)
					? SqlHelper.Get<T>(context, primaryDataSource, filter, sort)
					: null;
		}

		/// <summary>
		/// Finds the first instance of object that matched with the filter
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">Filter expression</param>
		/// <param name="sort">Sort expression</param>
		/// <returns></returns>
		public static T Get<T>(string aliasTypeName, IFilterBy<T> filter, SortBy<T> sort = null) where T : class
		{
			using (var context = new RepositoryContext(false))
			{
				try
				{
					return RepositoryMediator.Get<T>(context, aliasTypeName, filter, sort);
				}
				catch (Exception ex)
				{
					context.Exception = ex;
					throw new RepositoryOperationException(ex);
				}
			}
		}

		/// <summary>
		/// Gets the instance of a object from repository
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="id">The string that present identity (primary-key)</param>
		/// <param name="callHandlers">true to call event-handlers before processing</param>
		/// <param name="processCache">true to process cache (first check existed object, then update cache)</param>
		/// <returns></returns>
		public static T Get<T>(RepositoryContext context, string aliasTypeName, string id, bool callHandlers = true, bool processCache = true) where T : class
		{
			// prepare
			if (callHandlers)
			{
				context.Operation = RepositoryOperations.Get;
				context.EntityDefinition = RepositoryMediator.GetEntityDefinition<T>();
				context.AliasTypeName = aliasTypeName;
			}

			// call pre-handlers
			if (callHandlers && RepositoryMediator.CallPreHandlers(context, id))
				return null;

			// get cached object
			var @object = processCache && !object.ReferenceEquals(context.EntityDefinition.CacheStorage, null)
				? Get<T>(context.EntityDefinition.CacheStorage, id)
				: null;

#if DEBUG
			if (!object.ReferenceEquals(@object, null))
				RepositoryMediator.WriteLogs("GET: The cached object is found [" + @object.GetCacheKey(false) + "]");
#endif

			// load from data store if got no cached
			if (object.ReferenceEquals(@object, null))
			{
				var primaryDataSource = RepositoryMediator.GetPrimaryDataSource(context);
				@object = primaryDataSource.Mode.Equals(RepositoryModes.NoSQL)
					? NoSqlHelper.Get<T>(context, primaryDataSource, id)
					: primaryDataSource.Mode.Equals(RepositoryModes.SQL)
						? SqlHelper.Get<T>(context, primaryDataSource, id)
						: null;

				// TO DO: check to get instance from secondary source if primary source is not available

				// update into cache storage
				if (!object.ReferenceEquals(@object, null) && processCache && !object.ReferenceEquals(context.EntityDefinition.CacheStorage, null))
#if DEBUG
					if (context.EntityDefinition.CacheStorage.Set(@object))
						RepositoryMediator.WriteLogs("GET: Add the object into the cache storage successful [" + @object.GetCacheKey(false) + "]");
#else
					context.EntityDefinition.CacheStorage.Set(@object);
#endif
			}

			// update state & call post-handlers
			if (callHandlers && !object.ReferenceEquals(@object, null))
			{
				context.SetCurrentState(@object);
				RepositoryMediator.CallPostHandlers(context, @object);
			}

			// return the instance of object
			return @object;
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
				try
				{
					return RepositoryMediator.Get<T>(context, aliasTypeName, id);
				}
				catch (Exception ex)
				{
					context.Exception = ex;
					throw new RepositoryOperationException(ex);
				}
			}
		}

		/// <summary>
		/// Finds the first instance of object that matched with the filter
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">Filter expression</param>
		/// <param name="sort">Sort expression</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<T> GetAsync<T>(RepositoryContext context, string aliasTypeName, IFilterBy<T> filter, SortBy<T> sort, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			// prepare
			context.Operation = RepositoryOperations.Get;
			context.EntityDefinition = RepositoryMediator.GetEntityDefinition<T>();
			context.AliasTypeName = aliasTypeName;

			// find
			var primaryDataSource = RepositoryMediator.GetPrimaryDataSource(context);
			return primaryDataSource.Mode.Equals(RepositoryModes.NoSQL)
				? NoSqlHelper.GetAsync<T>(context, primaryDataSource, filter, sort, cancellationToken)
				: primaryDataSource.Mode.Equals(RepositoryModes.SQL)
					? SqlHelper.GetAsync<T>(context, primaryDataSource, filter, sort)
					: Task.FromResult<T>(null);
		}

		/// <summary>
		/// Finds the first instance of object that matched with the filter
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">Filter expression</param>
		/// <param name="sort">Sort expression</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<T> GetAsync<T>(string aliasTypeName, IFilterBy<T> filter, SortBy<T> sort, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			using (var context = new RepositoryContext(false))
			{
				try
				{
					return RepositoryMediator.GetAsync<T>(context, aliasTypeName, filter, sort, cancellationToken);
				}
				catch (Exception ex)
				{
					context.Exception = ex;
					return Task.FromException<T>(new RepositoryOperationException(ex));
				}
			}
		}

		/// <summary>
		/// Gets the instance of a object from repository
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="id">The string that present identity (primary-key)</param>
		/// <param name="callHandlers">true to call event-handlers before processing</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <param name="processCache">true to process cache (first check existed object, then update cache)</param>
		/// <returns></returns>
		public static async Task<T> GetAsync<T>(RepositoryContext context, string aliasTypeName, string id, bool callHandlers = true, CancellationToken cancellationToken = default(CancellationToken), bool processCache = true) where T : class
		{
			// prepare
			if (callHandlers)
			{
				context.Operation = RepositoryOperations.Get;
				context.EntityDefinition = RepositoryMediator.GetEntityDefinition<T>();
				context.AliasTypeName = aliasTypeName;
			}

			// call pre-handlers
			if (callHandlers && await RepositoryMediator.CallPreHandlersAsync(context, id, cancellationToken))
				return null;

			// get cached object
			var @object = processCache && !object.ReferenceEquals(context.EntityDefinition.CacheStorage, null)
				? await GetAsync<T>(context.EntityDefinition.CacheStorage, id)
				: null;

#if DEBUG
			if (!object.ReferenceEquals(@object, null))
				RepositoryMediator.WriteLogs("GET: The cached object is found [" + @object.GetCacheKey(false) + "]");
#endif

			// load from data store if got no cached
			if (object.ReferenceEquals(@object, null))
			{
				// load from primary data source
				var primaryDataSource = RepositoryMediator.GetPrimaryDataSource(context);
				@object = primaryDataSource.Mode.Equals(RepositoryModes.NoSQL)
					? await NoSqlHelper.GetAsync<T>(context, primaryDataSource, id, cancellationToken)
					: primaryDataSource.Mode.Equals(RepositoryModes.SQL)
						? await SqlHelper.GetAsync<T>(context, primaryDataSource, id, cancellationToken)
						: null;

				// TO DO: check to get instance from secondary source if primary source is not available

				// update into cache storage
				if (!object.ReferenceEquals(@object, null) && processCache && !object.ReferenceEquals(context.EntityDefinition.CacheStorage, null))
#if DEBUG
					if (await context.EntityDefinition.CacheStorage.SetAsync(@object))
						RepositoryMediator.WriteLogs("GET: Add the object into the cache storage successful [" + @object.GetCacheKey(false) + "]");
#else
					await context.EntityDefinition.CacheStorage.SetAsync(@object);
#endif
			}

			// update state & call post-handlers
			if (callHandlers && !object.ReferenceEquals(@object, null))
			{
				context.SetCurrentState(@object);
				await RepositoryMediator.CallPostHandlersAsync(context, @object, cancellationToken);
			}

			// return the instance of object
			return @object;
		}

		/// <summary>
		/// Gets the instance of a object from repository
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="id">The string that present identity (primary-key)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<T> GetAsync<T>(string aliasTypeName, string id, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			using (var context = new RepositoryContext(false))
			{
				try
				{
					return RepositoryMediator.GetAsync<T>(context, aliasTypeName, id, true, cancellationToken);
				}
				catch (Exception ex)
				{
					context.Exception = ex;
					return Task.FromException<T>(new RepositoryOperationException(ex));
				}
			}
		}
		#endregion

		#region Replace
		/// <summary>
		/// Updates instance of object into repository (using replace method)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object that presents the instance in repository need to be updated</param>
		public static void Replace<T>(RepositoryContext context, string aliasTypeName, T @object) where T : class
		{
			// prepare
			context.Operation = RepositoryOperations.Update;
			context.EntityDefinition = RepositoryMediator.GetEntityDefinition<T>();
			context.AliasTypeName = aliasTypeName;

			// check state
			var previousInstance = @object != null
				? RepositoryMediator.Get<T>(context, aliasTypeName, @object.GetEntityID(), false)
				: null;

			var previousState = previousInstance != null
				? context.SetPreviousState(previousInstance)
				: null;

			var currentState = context.SetCurrentState(@object);
			var dirtyAttributes = context.FindDirty(previousState, currentState);
			if (dirtyAttributes.Count < 1)
				return;

			// validate & re-update object
			if (RepositoryMediator.Validate(context.EntityDefinition, currentState))
			{
				// re-update object
				currentState.ForEach(data =>
				{
					@object.SetAttributeValue(data.Key, data.Value);
				});

				// update state
				context.SetCurrentState(@object, currentState);
			}

			// call pre-handlers
			if (RepositoryMediator.CallPreHandlers(context, @object))
				return;

			// reset search score
			if (@object is RepositoryBase)
				(@object as RepositoryBase).SearchScore = null;

			// update
			var primaryDataSource = RepositoryMediator.GetPrimaryDataSource(context);
			if (primaryDataSource.Mode.Equals(RepositoryModes.NoSQL))
				NoSqlHelper.Replace<T>(context, primaryDataSource, @object);
			else if (primaryDataSource.Mode.Equals(RepositoryModes.SQL))
				SqlHelper.Replace<T>(context, primaryDataSource, @object);

			// update into cache storage
			if (!object.ReferenceEquals(context.EntityDefinition.CacheStorage, null))
#if DEBUG
				if (context.EntityDefinition.CacheStorage.Set(@object))
					RepositoryMediator.WriteLogs("REPLACE: Add the object into the cache storage successful [" + @object.GetCacheKey(false) + "]");
#else
					context.EntityDefinition.CacheStorage.Set(@object);
#endif

			// call post-handlers
			RepositoryMediator.CallPostHandlers(context, @object);

			// TO DO: sync to other data sources
		}

		/// <summary>
		/// Updates instance of object into repository (using replace method)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object that presents the instance in repository need to be updated</param>
		public static void Replace<T>(string aliasTypeName, T @object) where T : class
		{
			using (var context = new RepositoryContext())
			{
				try
				{
					RepositoryMediator.Replace<T>(context, aliasTypeName, @object);
				}
				catch (Exception ex)
				{
					context.Exception = ex;
					throw new RepositoryOperationException(ex);
				}
			}
		}

		/// <summary>
		/// Updates instance of object into repository (using replace method)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object that presents the instance in repository need to be updated</param>
		/// <param name="cancellationToken">The cancellation token</param>
		public static async Task ReplaceAsync<T>(RepositoryContext context, string aliasTypeName, T @object, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			// prepare
			context.Operation = RepositoryOperations.Update;
			context.EntityDefinition = RepositoryMediator.GetEntityDefinition<T>();
			context.AliasTypeName = aliasTypeName;

			// check state
			var previousInstance = @object != null
				? await RepositoryMediator.GetAsync<T>(context, aliasTypeName, @object.GetEntityID(), false)
				: null;

			var previousState = previousInstance != null
				? context.SetPreviousState(previousInstance)
				: null;

			var currentState = context.SetCurrentState(@object);
			var dirtyAttributes = context.FindDirty(previousState, currentState);
			if (dirtyAttributes.Count < 1)
				return;

			// validate & re-update object
			if (RepositoryMediator.Validate(context.EntityDefinition, currentState))
			{
				// re-update object
				currentState.ForEach(data =>
				{
					@object.SetAttributeValue(data.Key, data.Value);
				});

				// update state
				context.SetCurrentState(@object, currentState);
			}

			// call pre-handlers
			if (await RepositoryMediator.CallPreHandlersAsync(context, @object, cancellationToken))
				return;

			// reset search score
			if (@object is RepositoryBase)
				(@object as RepositoryBase).SearchScore = null;

			// update
			var primaryDataSource = RepositoryMediator.GetPrimaryDataSource(context);
			if (primaryDataSource.Mode.Equals(RepositoryModes.NoSQL))
				await NoSqlHelper.ReplaceAsync<T>(context, primaryDataSource, @object, null, cancellationToken);
			else if (primaryDataSource.Mode.Equals(RepositoryModes.SQL))
				await SqlHelper.ReplaceAsync<T>(context, primaryDataSource, @object, cancellationToken);

			// update into cache storage
			if (!object.ReferenceEquals(context.EntityDefinition.CacheStorage, null))
#if DEBUG
				if (await context.EntityDefinition.CacheStorage.SetAsync(@object))
					RepositoryMediator.WriteLogs("REPLACE: Add the object into the cache storage successful [" + @object.GetCacheKey(false) + "]");
#else
				await context.EntityDefinition.CacheStorage.SetAsync(@object);
#endif

			// call post-handlers
			await RepositoryMediator.CallPostHandlersAsync(context, @object, cancellationToken);

			// TO DO: sync to other data sources
		}

		/// <summary>
		/// Updates instance of object into repository (using replace method)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object that presents the instance in repository need to be updated</param>
		/// <param name="cancellationToken">The cancellation token</param>
		public static Task ReplaceAsync<T>(string aliasTypeName, T @object, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			using (var context = new RepositoryContext())
			{
				try
				{
					return RepositoryMediator.ReplaceAsync<T>(context, aliasTypeName, @object, cancellationToken);
				}
				catch (Exception ex)
				{
					context.Exception = ex;
					return Task.FromException<T>(new RepositoryOperationException(ex));
				}
			}
		}
		#endregion

		#region Update
		/// <summary>
		/// Updates instance of object into repository (only update changed attributes)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object that presents the instance in repository need to be updated</param>
		public static void Update<T>(RepositoryContext context, string aliasTypeName, T @object) where T : class
		{
			// prepare
			context.Operation = RepositoryOperations.Update;
			context.EntityDefinition = RepositoryMediator.GetEntityDefinition<T>();
			context.AliasTypeName = aliasTypeName;

			// check state
			var previousInstance = @object != null
				? RepositoryMediator.Get<T>(context, aliasTypeName, @object.GetEntityID(), false)
				: null;

			var previousState = previousInstance != null
				? context.SetPreviousState(previousInstance)
				: null;

			var currentState = context.SetCurrentState(@object);
			var dirtyAttributes = context.FindDirty(previousState, currentState);
			if (dirtyAttributes.Count < 1)
				return;

			// validate & re-update object
			if (RepositoryMediator.Validate(context.EntityDefinition, currentState))
			{
				// re-update object
				currentState.ForEach(data =>
				{
					@object.SetAttributeValue(data.Key, data.Value);
				});

				// update state
				context.SetCurrentState(@object, currentState);
			}

			// call pre-handlers
			if (RepositoryMediator.CallPreHandlers(context, @object))
				return;

			// update
			var primaryDataSource = RepositoryMediator.GetPrimaryDataSource(context);
			if (primaryDataSource.Mode.Equals(RepositoryModes.NoSQL))
				NoSqlHelper.Update<T>(context, primaryDataSource, @object, dirtyAttributes.ToList());
			else if (primaryDataSource.Mode.Equals(RepositoryModes.SQL))
				SqlHelper.Update<T>(context, primaryDataSource, @object, dirtyAttributes.ToList());

			// update into cache storage
			if (!object.ReferenceEquals(context.EntityDefinition.CacheStorage, null))
#if DEBUG
				if (context.EntityDefinition.CacheStorage.Set(@object))
					RepositoryMediator.WriteLogs("UPDATE: Add the object into the cache storage successful [" + @object.GetCacheKey(false) + "]");
#else
					context.EntityDefinition.CacheStorage.Set(@object);
#endif

			// call post-handlers
			RepositoryMediator.CallPostHandlers(context, @object);

			// TO DO: sync to other data sources
		}

		/// <summary>
		/// Updates instance of object into repository (only update changed attributes)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object that presents the instance in repository need to be updated</param>
		public static void Update<T>(string aliasTypeName, T @object) where T : class
		{
			using (var context = new RepositoryContext())
			{
				try
				{
					RepositoryMediator.Update<T>(context, aliasTypeName, @object);
				}
				catch (Exception ex)
				{
					context.Exception = ex;
					throw new RepositoryOperationException(ex);
				}
			}
		}

		/// <summary>
		/// Updates instance of object into repository (only update changed attributes)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object that presents the instance in repository need to be updated</param>
		/// <param name="cancellationToken">The cancellation token</param>
		public static async Task UpdateAsync<T>(RepositoryContext context, string aliasTypeName, T @object, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			// prepare
			context.Operation = RepositoryOperations.Update;
			context.EntityDefinition = RepositoryMediator.GetEntityDefinition<T>();
			context.AliasTypeName = aliasTypeName;

			// check state
			var previousInstance = @object != null
				? await RepositoryMediator.GetAsync<T>(context, aliasTypeName, @object.GetEntityID(), false)
				: null;

			var previousState = previousInstance != null
				? context.SetPreviousState(previousInstance)
				: null;

			var currentState = context.SetCurrentState(@object);
			var dirtyAttributes = context.FindDirty(previousState, currentState);
			if (dirtyAttributes.Count < 1)
				return;

			// validate & re-update object
			if (RepositoryMediator.Validate(context.EntityDefinition, currentState))
			{
				// re-update object
				currentState.ForEach(data =>
				{
					@object.SetAttributeValue(data.Key, data.Value);
				});

				// update state
				context.SetCurrentState(@object, currentState);
			}

			// call pre-handlers
			if (await RepositoryMediator.CallPreHandlersAsync(context, @object, cancellationToken))
				return;

			// update
			var primaryDataSource = RepositoryMediator.GetPrimaryDataSource(context);
			if (primaryDataSource.Mode.Equals(RepositoryModes.NoSQL))
				await NoSqlHelper.UpdateAsync<T>(context, primaryDataSource, @object, dirtyAttributes.ToList(), null, cancellationToken);
			else if (primaryDataSource.Mode.Equals(RepositoryModes.SQL))
				await SqlHelper.UpdateAsync<T>(context, primaryDataSource, @object, dirtyAttributes.ToList(), cancellationToken);

			// update into cache storage
			if (!object.ReferenceEquals(context.EntityDefinition.CacheStorage, null))
#if DEBUG
				if (await context.EntityDefinition.CacheStorage.SetAsync(@object))
					RepositoryMediator.WriteLogs("UPDATE: Add the object into the cache storage successful [" + @object.GetCacheKey(false) + "]");
#else
					await context.EntityDefinition.CacheStorage.SetAsync(@object);
#endif

			// call post-handlers
			await RepositoryMediator.CallPostHandlersAsync(context, @object, cancellationToken);

			// TO DO: sync to other data sources
		}

		/// <summary>
		/// Updates instance of object into repository (only update changed attributes)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object that presents the instance in repository need to be updated</param>
		/// <param name="cancellationToken">The cancellation token</param>
		public static Task UpdateAsync<T>(string aliasTypeName, T @object, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			using (var context = new RepositoryContext())
			{
				try
				{
					return RepositoryMediator.UpdateAsync<T>(context, aliasTypeName, @object);
				}
				catch (Exception ex)
				{
					context.Exception = ex;
					return Task.FromException<T>(new RepositoryOperationException(ex));
				}
			}
		}
		#endregion

		#region Delete
		/// <summary>
		/// Deletes instance of object from repository
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="id">The string that presents object identity that want to delete instance from repository</param>
		public static void Delete<T>(RepositoryContext context, string aliasTypeName, string id) where T : class
		{
			// prepare
			context.Operation = RepositoryOperations.Delete;
			context.EntityDefinition = RepositoryMediator.GetEntityDefinition<T>();
			context.AliasTypeName = aliasTypeName;

			// check existing
			var @object = RepositoryMediator.Get<T>(context, aliasTypeName, id, false);
			if (@object == null)
				return;

			// call pre-handlers
			context.SetCurrentState(@object);
			if (RepositoryMediator.CallPreHandlers(context, @object))
				return;

			// delete
			var primaryDataSource = RepositoryMediator.GetPrimaryDataSource(context);
			if (primaryDataSource.Mode.Equals(RepositoryModes.NoSQL))
				NoSqlHelper.Delete<T>(context, primaryDataSource, id);
			else if (primaryDataSource.Mode.Equals(RepositoryModes.SQL))
				SqlHelper.Delete<T>(context, primaryDataSource, id);

			// remove from cache storage
			if (!object.ReferenceEquals(context.EntityDefinition.CacheStorage, null))
#if DEBUG
				if (context.EntityDefinition.CacheStorage.Remove(@object))
					RepositoryMediator.WriteLogs("DELETE: Remove the cached object from the cache storage successful [" + @object.GetCacheKey(false) + "]");
#else
					context.EntityDefinition.CacheStorage.Remove(@object);
#endif

			// call post-handlers
			RepositoryMediator.CallPostHandlers(context, @object);

			// TO DO: sync to other data sources
		}

		/// <summary>
		/// Deletes instance of object from repository
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="id">The string that presents object identity that want to delete instance from repository</param>
		public static void Delete<T>(string aliasTypeName, string id) where T : class
		{
			using (var context = new RepositoryContext())
			{
				try
				{
					RepositoryMediator.Delete<T>(context, aliasTypeName, id);
				}
				catch (Exception ex)
				{
					context.Exception = ex;
					throw new RepositoryOperationException(ex);
				}
			}
		}

		/// <summary>
		/// Deletes instance of object from repository
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="id">The string that presents object identity that want to delete instance from repository</param>
		/// <param name="cancellationToken">The cancellation token</param>
		public static async Task DeleteAsync<T>(RepositoryContext context, string aliasTypeName, string id, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			// prepare
			context.Operation = RepositoryOperations.Delete;
			context.EntityDefinition = RepositoryMediator.GetEntityDefinition<T>();
			context.AliasTypeName = aliasTypeName;

			// check existing
			var @object = await RepositoryMediator.GetAsync<T>(context, aliasTypeName, id, false, cancellationToken);
			if (@object == null)
				return;

			// call pre-handlers
			context.SetCurrentState(@object);
			if (await RepositoryMediator.CallPreHandlersAsync(context, @object, cancellationToken))
				return;

			// delete
			var primaryDataSource = RepositoryMediator.GetPrimaryDataSource(context);
			if (primaryDataSource.Mode.Equals(RepositoryModes.NoSQL))
				await NoSqlHelper.DeleteAsync<T>(context, primaryDataSource, id, null, cancellationToken);
			else if (primaryDataSource.Mode.Equals(RepositoryModes.SQL))
				await SqlHelper.DeleteAsync<T>(context, primaryDataSource, id, cancellationToken);

			// remove from cache storage
			if (!object.ReferenceEquals(context.EntityDefinition.CacheStorage, null))
#if DEBUG
				if (await context.EntityDefinition.CacheStorage.RemoveAsync(@object))
					RepositoryMediator.WriteLogs("DELETE: Remove the cached object from the cache storage successful [" + @object.GetCacheKey(false) + "]");
#else
				await context.EntityDefinition.CacheStorage.RemoveAsync(@object);
#endif

			// call post-handlers
			await RepositoryMediator.CallPostHandlersAsync(context, @object, cancellationToken);

			// TO DO: sync to other data sources
		}

		/// <summary>
		/// Deletes instance of object from repository
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="id">The string that presents object identity that want to delete instance from repository</param>
		/// <param name="cancellationToken">The cancellation token</param>
		public static Task DeleteAsync<T>(string aliasTypeName, string id, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			using (var context = new RepositoryContext())
			{
				try
				{
					return RepositoryMediator.DeleteAsync<T>(context, aliasTypeName, id, cancellationToken);
				}
				catch (Exception ex)
				{
					context.Exception = ex;
					return Task.FromException<T>(new RepositoryOperationException(ex));
				}
			}
		}

		/// <summary>
		/// Deletes many instances of objects from repository
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">Filter expression to filter instances to delete</param>
		public static void DeleteMany<T>(RepositoryContext context, string aliasTypeName, IFilterBy<T> filter) where T : class
		{
			// prepare
			context.Operation = RepositoryOperations.Delete;
			context.EntityDefinition = RepositoryMediator.GetEntityDefinition<T>();
			context.AliasTypeName = aliasTypeName;

			// delete
			var primaryDataSource = RepositoryMediator.GetPrimaryDataSource(context);
			if (primaryDataSource.Mode.Equals(RepositoryModes.NoSQL))
				NoSqlHelper.DeleteMany<T>(context, primaryDataSource, filter);
			else if (primaryDataSource.Mode.Equals(RepositoryModes.SQL))
				SqlHelper.DeleteMany<T>(context, primaryDataSource, filter);

			// TO DO: sync to other data sources

		}

		/// <summary>
		/// Deletes many instances of objects from repository
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">Filter expression to filter instances to delete</param>
		public static void DeleteMany<T>(string aliasTypeName, IFilterBy<T> filter) where T : class
		{
			using (var context = new RepositoryContext())
			{
				try
				{
					RepositoryMediator.DeleteMany<T>(context, aliasTypeName, filter);
				}
				catch (Exception ex)
				{
					context.Exception = ex;
					throw new RepositoryOperationException(ex);
				}
			}
		}

		/// <summary>
		/// Deletes many instances of objects from repository
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">Filter expression to filter instances to delete</param>
		/// <param name="cancellationToken">The cancellation token</param>
		public static async Task DeleteManyAsync<T>(RepositoryContext context, string aliasTypeName, IFilterBy<T> filter, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			// prepare
			context.Operation = RepositoryOperations.Delete;
			context.EntityDefinition = RepositoryMediator.GetEntityDefinition<T>();
			context.AliasTypeName = aliasTypeName;

			// delete
			var primaryDataSource = RepositoryMediator.GetPrimaryDataSource(context);
			if (primaryDataSource.Mode.Equals(RepositoryModes.NoSQL))
				await NoSqlHelper.DeleteManyAsync<T>(context, primaryDataSource, filter, null, cancellationToken);
			else if (primaryDataSource.Mode.Equals(RepositoryModes.SQL))
				await SqlHelper.DeleteManyAsync<T>(context, primaryDataSource, filter, cancellationToken);

			// TO DO: sync to other data sources

		}

		/// <summary>
		/// Deletes many instances of objects from repository
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">Filter expression to filter instances to delete</param>
		/// <param name="cancellationToken">The cancellation token</param>
		public static Task DeleteManyAsync<T>(string aliasTypeName, IFilterBy<T> filter, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			using (var context = new RepositoryContext())
			{
				try
				{
					return RepositoryMediator.DeleteManyAsync<T>(context, aliasTypeName, filter, cancellationToken);
				}
				catch (Exception ex)
				{
					context.Exception = ex;
					return Task.FromException(new RepositoryOperationException(ex));
				}
			}
		}
		#endregion

		#region Find
		/// <summary>
		/// Finds intances of objects from repository
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">Filter expression</param>
		/// <param name="sort">Sort expression</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <returns></returns>
		public static List<T> Find<T>(RepositoryContext context, string aliasTypeName, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber) where T : class
		{
			// prepare
			context.EntityDefinition = RepositoryMediator.GetEntityDefinition<T>();
			context.AliasTypeName = aliasTypeName;

			var primaryDataSource = RepositoryMediator.GetPrimaryDataSource(context);
			List<T> objects = null;

#if DEBUG
			RepositoryMediator.WriteLogs(new List<string>(){
					"FIND: Find objects of [" + context.EntityDefinition.Type.GetTypeName() + "]",
					"- Mode: " + primaryDataSource.Mode.ToString(),
					"- Page Size: " + pageSize.ToString(),
					"- Page Number: " + pageNumber.ToString(),
					"- Filter By: " + (filter != null ? filter.ToString() : "None"),
					"- Sort By: " + (sort != null ? sort.ToString() : "None"),
				}, null);
#endif

			// find identities
			var identities = object.ReferenceEquals(context.EntityDefinition.CacheStorage, null)
				? null
				: primaryDataSource.Mode.Equals(RepositoryModes.NoSQL)
					? NoSqlHelper.SelectIdentities<T>(context, primaryDataSource, filter, sort, pageSize, pageNumber)
					: primaryDataSource.Mode.Equals(RepositoryModes.SQL)
						? SqlHelper.SelectIdentities<T>(context, primaryDataSource, filter, sort, pageSize, pageNumber)
						: new List<string>();

			// process
			if (identities != null && identities.Count > 0)
			{
#if DEBUG
				RepositoryMediator.WriteLogs("FIND: Total " + identities.Count + " identities are fetched [" + identities.ToString(" - ") + "]");
#endif
				// get cached objects
				var cached = context.EntityDefinition.CacheStorage.Get(identities.Select(id => id.GetCacheKey<T>()));
				if (cached != null)
				{
#if DEBUG
					RepositoryMediator.WriteLogs("FIND: Total " + cached.Count + " cached object(s) are found [" + cached.Select(item => item.Key).ToString(" - ") + "]");
#endif
					// prepare
					var results = identities.ToDictionary<string, string, T>(
							id => id,
							id => default(T)
						);

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
						var missing = primaryDataSource.Mode.Equals(RepositoryModes.NoSQL)
							? NoSqlHelper.Find<T>(context, primaryDataSource, identities, sort)
							: primaryDataSource.Mode.Equals(RepositoryModes.SQL)
								? SqlHelper.Find<T>(context, primaryDataSource, identities, sort)
								: new List<T>();

						// update results & cache
						missing.ForEach(obj =>
						{
							results[obj.GetEntityID()] = obj;
						});
						context.EntityDefinition.CacheStorage.Set(missing);
#if DEBUG
						RepositoryMediator.WriteLogs("FIND: Add " + missing.Count + " missing object(s) into cache storage successful [" + missing.Select(o => o.GetCacheKey()).ToString(" - ") + "]");
#endif
					}

					// update the collection of objects
					objects = results.Select(item => item.Value as T).ToList();
				}
			}

			// fetch objects if has no cache
			if (objects == null)
			{
				objects = identities == null || identities.Count > 0
					? primaryDataSource.Mode.Equals(RepositoryModes.NoSQL)
						? NoSqlHelper.Find<T>(context, primaryDataSource, filter, sort, pageSize, pageNumber)
						: primaryDataSource.Mode.Equals(RepositoryModes.SQL)
							? SqlHelper.Find<T>(context, primaryDataSource, filter, sort, pageSize, pageNumber)
							: new List<T>()
					: new List<T>();

				if (!object.ReferenceEquals(context.EntityDefinition.CacheStorage, null) && objects.Count > 0)
				{
					context.EntityDefinition.CacheStorage.Set(objects);
#if DEBUG
					RepositoryMediator.WriteLogs("FIND: Add " + objects.Count + " raw object(s) into cache storage successful [" + objects.Select(o => o.GetCacheKey()).ToString(" - ") + "]");
#endif
				}
			}

			return objects;
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
		/// <returns></returns>
		public static List<T> Find<T>(string aliasTypeName, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber) where T : class
		{
			using (var context = new RepositoryContext(false))
			{
				try
				{
					return RepositoryMediator.Find<T>(context, aliasTypeName, filter, sort, pageSize, pageNumber);
				}
				catch (Exception ex)
				{
					context.Exception = ex;
					throw new RepositoryOperationException(ex);
				}
			}
		}

		/// <summary>
		/// Finds intances of objects from repository
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">Filter expression</param>
		/// <param name="sort">Sort expression</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<List<T>> FindAsync<T>(RepositoryContext context, string aliasTypeName, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			// prepare
			context.EntityDefinition = RepositoryMediator.GetEntityDefinition<T>();
			context.AliasTypeName = aliasTypeName;

			var primaryDataSource = RepositoryMediator.GetPrimaryDataSource(context);
			List<T> objects = null;

#if DEBUG
			RepositoryMediator.WriteLogs(new List<string>(){
					"FIND: Find objects of [" + context.EntityDefinition.Type.GetTypeName() + "]",
					"- Mode: " + primaryDataSource.Mode.ToString(),
					"- Page Size: " + pageSize.ToString(),
					"- Page Number: " + pageNumber.ToString(),
					"- Filter By: " + (filter != null ? filter.ToString() : "None"),
					"- Sort By: " + (sort != null ? sort.ToString() : "None"),
				}, null);
#endif

			// find identities
			var identities = object.ReferenceEquals(context.EntityDefinition.CacheStorage, null)
				? null
				: primaryDataSource.Mode.Equals(RepositoryModes.NoSQL)
					? await NoSqlHelper.SelectIdentitiesAsync<T>(context, primaryDataSource, filter, sort, pageSize, pageNumber)
					: primaryDataSource.Mode.Equals(RepositoryModes.SQL)
						? await SqlHelper.SelectIdentitiesAsync<T>(context, primaryDataSource, filter, sort, pageSize, pageNumber)
						: new List<string>();

			// process
			if (identities != null && identities.Count > 0)
			{
#if DEBUG
				RepositoryMediator.WriteLogs("FIND: Total " + identities.Count + " identities are fetched [" + identities.ToString(" - ") + "]");
#endif
				// get cached objects
				var cached = await context.EntityDefinition.CacheStorage.GetAsync(identities.Select(id => id.GetCacheKey<T>()));
				if (cached != null)
				{
#if DEBUG
					RepositoryMediator.WriteLogs("FIND: Total " + cached.Count + " cached object(s) are found [" + cached.Select(item => item.Key).ToString(" - ") + "]");
#endif
					// prepare
					var results = identities.ToDictionary<string, string, T>(
							id => id,
							id => default(T)
						);

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
						var missing = primaryDataSource.Mode.Equals(RepositoryModes.NoSQL)
							? await NoSqlHelper.FindAsync<T>(context, primaryDataSource, identities, sort)
							: primaryDataSource.Mode.Equals(RepositoryModes.SQL)
								? await SqlHelper.FindAsync<T>(context, primaryDataSource, identities, sort)
								: new List<T>();

						// update results & cache
						missing.ForEach(obj =>
						{
							results[obj.GetEntityID()] = obj;
						});
						await context.EntityDefinition.CacheStorage.SetAsync(missing);
#if DEBUG
						RepositoryMediator.WriteLogs("FIND: Add " + missing.Count + " missing object(s) into cache storage successful [" + missing.Select(o => o.GetCacheKey()).ToString(" - ") + "]");
#endif
					}

					// update the collection of objects
					objects = results.Select(item => item.Value as T).ToList();
				}
			}

			// fetch objects if has no cache
			if (objects == null)
			{
				objects = identities == null || identities.Count > 0
					? primaryDataSource.Mode.Equals(RepositoryModes.NoSQL)
						? await NoSqlHelper.FindAsync<T>(context, primaryDataSource, filter, sort, pageSize, pageNumber)
						: primaryDataSource.Mode.Equals(RepositoryModes.SQL)
							? await SqlHelper.FindAsync<T>(context, primaryDataSource, filter, sort, pageSize, pageNumber)
							: new List<T>()
					: new List<T>();

				if (!object.ReferenceEquals(context.EntityDefinition.CacheStorage, null) && objects.Count > 0)
				{
					await context.EntityDefinition.CacheStorage.SetAsync(objects);
#if DEBUG
					RepositoryMediator.WriteLogs("FIND: Add " + objects.Count + " raw object(s) into cache storage successful [" + objects.Select(o => o.GetCacheKey()).ToString(" - ") + "]");
#endif
				}
			}

			return objects;
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
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<List<T>> FindAsync<T>(string aliasTypeName, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			using (var context = new RepositoryContext(false))
			{
				try
				{
					return RepositoryMediator.FindAsync<T>(context, aliasTypeName, filter, sort, pageSize, pageNumber, cancellationToken);
				}
				catch (Exception ex)
				{
					context.Exception = ex;
					return Task.FromException<List<T>>(new RepositoryOperationException(ex));
				}
			}
		}
		#endregion

		#region Search
		/// <summary>
		/// Searchs intances of objects from repository (using full-text search)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="query">The searching query (like Google searching query)</param>
		/// <param name="filter">The object that presents other filter expression to combine with seaching query</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <returns></returns>
		public static List<T> Search<T>(RepositoryContext context, string aliasTypeName, string query, IFilterBy<T> filter, int pageSize, int pageNumber) where T : class
		{
			// prepare
			context.EntityDefinition = RepositoryMediator.GetEntityDefinition<T>();
			context.AliasTypeName = aliasTypeName;

			var primaryDataSource = RepositoryMediator.GetPrimaryDataSource(context);
			List<T> objects = null;

#if DEBUG
			RepositoryMediator.WriteLogs(new List<string>(){
					"SEARCH: Search objects of [" + context.EntityDefinition.Type.GetTypeName() + "]",
					"- Mode: " + primaryDataSource.Mode.ToString(),
					"- Page Size: " + pageSize.ToString(),
					"- Page Number: " + pageNumber.ToString(),
					"- Query: " + (!string.IsNullOrWhiteSpace(query) ? query : "Unknown"),
					"- Filter By (Additional): " + (filter != null ? filter.ToString() : "None")
				}, null);
#endif

			// search identities
			var identities = object.ReferenceEquals(context.EntityDefinition.CacheStorage, null)
				? null
				: primaryDataSource.Mode.Equals(RepositoryModes.NoSQL)
					? NoSqlHelper.SearchIdentities<T>(context, primaryDataSource, query, filter, pageSize, pageNumber)
					: primaryDataSource.Mode.Equals(RepositoryModes.SQL)
						? SqlHelper.SearchIdentities<T>(context, primaryDataSource, query, filter, pageSize, pageNumber)
						: new List<string>();

			// process
			if (identities != null && identities.Count > 0)
			{
#if DEBUG
				RepositoryMediator.WriteLogs("SEARCH: Total " + identities.Count + " identities are searched [" + identities.ToString(" - ") + "]");
#endif
				// get cached objects
				var cached = context.EntityDefinition.CacheStorage.Get(identities.Select(id => id.GetCacheKey<T>()));
				if (cached != null)
				{
#if DEBUG
					RepositoryMediator.WriteLogs("SEARCH: Total " + cached.Count + " cached object(s) are found [" + cached.Select(item => item.Key).ToString(" - ") + "]");
#endif
					// prepare
					var results = identities.ToDictionary<string, string, T>(
							id => id,
							id => default(T)
						);

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
						var missing = primaryDataSource.Mode.Equals(RepositoryModes.NoSQL)
							? NoSqlHelper.Find<T>(context, primaryDataSource, identities)
							: primaryDataSource.Mode.Equals(RepositoryModes.SQL)
								? SqlHelper.Find<T>(context, primaryDataSource, identities)
								: new List<T>();

						// update results & cache
						missing.ForEach(obj =>
						{
							results[obj.GetEntityID()] = obj;
						});
						context.EntityDefinition.CacheStorage.Set(missing);
#if DEBUG
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
					? primaryDataSource.Mode.Equals(RepositoryModes.NoSQL)
						? NoSqlHelper.Search<T>(context, primaryDataSource, query, filter, pageSize, pageNumber)
						: primaryDataSource.Mode.Equals(RepositoryModes.SQL)
							? SqlHelper.Search<T>(context, primaryDataSource, query, filter, pageSize, pageNumber)
							: new List<T>()
					: new List<T>();

				if (!object.ReferenceEquals(context.EntityDefinition.CacheStorage, null) && objects.Count > 0)
				{
					context.EntityDefinition.CacheStorage.Set(objects);
#if DEBUG
					RepositoryMediator.WriteLogs("SEARCH: Add " + objects.Count + " raw object(s) into cache storage successful [" + objects.Select(o => o.GetCacheKey()).ToString(" - ") + "]");
#endif
				}
			}

			return objects;
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
		/// <returns></returns>
		public static List<T> Search<T>(string aliasTypeName, string query, IFilterBy<T> filter, int pageSize, int pageNumber) where T : class
		{
			using (var context = new RepositoryContext(false))
			{
				try
				{
					return RepositoryMediator.Search<T>(context, aliasTypeName, query, filter, pageSize, pageNumber);
				}
				catch (Exception ex)
				{
					context.Exception = ex;
					throw new RepositoryOperationException(ex);
				}
			}
		}

		/// <summary>
		/// Searchs intances of objects from repository (using full-text search)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="query">The searching query (like Google searching query)</param>
		/// <param name="filter">The object that presents other filter expression to combine with seaching query</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<List<T>> SearchAsync<T>(RepositoryContext context, string aliasTypeName, string query, IFilterBy<T> filter, int pageSize, int pageNumber, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			// prepare
			context.EntityDefinition = RepositoryMediator.GetEntityDefinition<T>();
			context.AliasTypeName = aliasTypeName;

			var primaryDataSource = RepositoryMediator.GetPrimaryDataSource(context);
			List<T> objects = null;

#if DEBUG
			RepositoryMediator.WriteLogs(new List<string>(){
					"SEARCH: Search objects of [" + context.EntityDefinition.Type.GetTypeName() + "]",
					"- Mode: " + primaryDataSource.Mode.ToString(),
					"- Page Size: " + pageSize.ToString(),
					"- Page Number: " + pageNumber.ToString(),
					"- Query: " + (!string.IsNullOrWhiteSpace(query) ? query : "Unknown"),
					"- Filter By (Additional): " + (filter != null ? filter.ToString() : "None")
				}, null);
#endif

			// search identities
			var identities = object.ReferenceEquals(context.EntityDefinition.CacheStorage, null)
				? null
				: primaryDataSource.Mode.Equals(RepositoryModes.NoSQL)
					? await NoSqlHelper.SearchIdentitiesAsync<T>(context, primaryDataSource, query, filter, pageSize, pageNumber)
					: primaryDataSource.Mode.Equals(RepositoryModes.SQL)
						? await SqlHelper.SearchIdentitiesAsync<T>(context, primaryDataSource, query, filter, pageSize, pageNumber)
						: new List<string>();

			// process
			if (identities != null && identities.Count > 0)
			{
#if DEBUG
				RepositoryMediator.WriteLogs("SEARCH: Total " + identities.Count + " identities are searched [" + identities.ToString(" - ") + "]");
#endif
				// get cached objects
				var cached = await context.EntityDefinition.CacheStorage.GetAsync(identities.Select(id => id.GetCacheKey<T>()));
				if (cached != null)
				{
#if DEBUG
					RepositoryMediator.WriteLogs("SEARCH: Total " + cached.Count + " cached object(s) are found [" + cached.Select(item => item.Key).ToString(" - ") + "]");
#endif
					// prepare
					var results = identities.ToDictionary<string, string, T>(
							id => id,
							id => default(T)
						);

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
						var missing = primaryDataSource.Mode.Equals(RepositoryModes.NoSQL)
							? await NoSqlHelper.FindAsync<T>(context, primaryDataSource, identities)
							: primaryDataSource.Mode.Equals(RepositoryModes.SQL)
								? await SqlHelper.FindAsync<T>(context, primaryDataSource, identities)
								: new List<T>();

						// update results & cache
						missing.ForEach(obj =>
						{
							results[obj.GetEntityID()] = obj;
						});
						await context.EntityDefinition.CacheStorage.SetAsync(missing);
#if DEBUG
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
					? primaryDataSource.Mode.Equals(RepositoryModes.NoSQL)
						? await NoSqlHelper.SearchAsync<T>(context, primaryDataSource, query, filter, pageSize, pageNumber)
						: primaryDataSource.Mode.Equals(RepositoryModes.SQL)
							? await SqlHelper.SearchAsync<T>(context, primaryDataSource, query, filter, pageSize, pageNumber)
							: new List<T>()
					: new List<T>();

				if (!object.ReferenceEquals(context.EntityDefinition.CacheStorage, null) && objects.Count > 0)
				{
					await context.EntityDefinition.CacheStorage.SetAsync(objects);
#if DEBUG
					RepositoryMediator.WriteLogs("SEARCH: Add " + objects.Count + " raw object(s) into cache storage successful [" + objects.Select(o => o.GetCacheKey()).ToString(" - ") + "]");
#endif
				}
			}

			return objects;
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
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<List<T>> SearchAsync<T>(string aliasTypeName, string query, IFilterBy<T> filter, int pageSize, int pageNumber, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			using (var context = new RepositoryContext(false))
			{
				try
				{
					return RepositoryMediator.SearchAsync<T>(context, aliasTypeName, query, filter, pageSize, pageNumber, cancellationToken);
				}
				catch (Exception ex)
				{
					context.Exception = ex;
					return Task.FromException<List<T>>(new RepositoryOperationException(ex));
				}
			}
		}
		#endregion

		#region Count
		/// <summary>
		/// Counts the number of intances of objects
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">Filter expression</param>
		/// <returns>The integer number that presents total of objects that matched with the filter expression</returns>
		public static long Count<T>(RepositoryContext context, string aliasTypeName, IFilterBy<T> filter) where T : class
		{
			// prepare
			context.EntityDefinition = RepositoryMediator.GetEntityDefinition<T>();
			context.AliasTypeName = aliasTypeName;

			// count
			var primaryDataSource = RepositoryMediator.GetPrimaryDataSource(context);
			return primaryDataSource.Mode.Equals(RepositoryModes.NoSQL)
				? NoSqlHelper.Count<T>(context, primaryDataSource, filter != null ? filter : Filters.And<T>())
				: primaryDataSource.Mode.Equals(RepositoryModes.SQL)
					? SqlHelper.Count<T>(context, primaryDataSource, filter)
					: 0;
		}

		/// <summary>
		/// Counts the number of intances of objects
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">Filter expression</param>
		/// <returns>The integer number that presents total of objects that matched with the filter expression</returns>
		public static long Count<T>(string aliasTypeName, IFilterBy<T> filter) where T : class
		{
			using (var context = new RepositoryContext(false))
			{
				try
				{
					return RepositoryMediator.Count<T>(context, aliasTypeName, filter);
				}
				catch (Exception ex)
				{
					context.Exception = ex;
					throw new RepositoryOperationException(ex);
				}
			}
		}

		/// <summary>
		/// Counts the number of intances of objects
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">Filter expression</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The integer number that presents total of objects that matched with the filter expression</returns>
		public static Task<long> CountAsync<T>(RepositoryContext context, string aliasTypeName, IFilterBy<T> filter, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			// prepare
			context.EntityDefinition = RepositoryMediator.GetEntityDefinition<T>();
			context.AliasTypeName = aliasTypeName;

			// count
			var primaryDataSource = RepositoryMediator.GetPrimaryDataSource(context);
			return primaryDataSource.Mode.Equals(RepositoryModes.NoSQL)
				? NoSqlHelper.CountAsync<T>(context, primaryDataSource, filter != null ? filter : Filters.And<T>(), null, cancellationToken)
				: primaryDataSource.Mode.Equals(RepositoryModes.SQL)
					? SqlHelper.CountAsync<T>(context, primaryDataSource, filter, cancellationToken)
					: Task.FromResult<long>(0);
		}

		/// <summary>
		/// Counts the number of intances of objects
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">Filter expression</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The integer number that presents total of objects that matched with the filter expression</returns>
		public static Task<long> CountAsync<T>(string aliasTypeName, IFilterBy<T> filter, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			using (var context = new RepositoryContext(false))
			{
				try
				{
					return RepositoryMediator.CountAsync<T>(context, aliasTypeName, filter, cancellationToken);
				}
				catch (Exception ex)
				{
					context.Exception = ex;
					return Task.FromException<long>(new RepositoryOperationException(ex));
				}
			}
		}

		/// <summary>
		/// Counts the number of intances of objects (using full-text search)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="query">The searching query (like Google searching query)</param>
		/// <param name="filter">The object that presents other filter expression to combine with seaching query</param>
		/// <returns>The integer number that presents total of objects that matched with searching query (and filter expression)</returns>
		public static long Count<T>(RepositoryContext context, string aliasTypeName, string query, IFilterBy<T> filter) where T : class
		{
			// prepare
			context.EntityDefinition = RepositoryMediator.GetEntityDefinition<T>();
			context.AliasTypeName = aliasTypeName;

			// count
			var primaryDataSource = RepositoryMediator.GetPrimaryDataSource(context);
			return primaryDataSource.Mode.Equals(RepositoryModes.NoSQL)
				? NoSqlHelper.Count<T>(context, primaryDataSource, query, filter)
				: primaryDataSource.Mode.Equals(RepositoryModes.SQL)
					? SqlHelper.Count<T>(context, primaryDataSource, query, filter)
					: 0;
		}

		/// <summary>
		/// Counts the number of intances of objects (using full-text search)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="query">The searching query (like Google searching query)</param>
		/// <param name="filter">The object that presents other filter expression to combine with seaching query</param>
		/// <returns>The integer number that presents total of objects that matched with searching query (and filter expression)</returns>
		public static long Count<T>(string aliasTypeName, string query, IFilterBy<T> filter) where T : class
		{
			using (var context = new RepositoryContext(false))
			{
				try
				{
					return RepositoryMediator.Count<T>(context, aliasTypeName, query, filter);
				}
				catch (Exception ex)
				{
					context.Exception = ex;
					throw new RepositoryOperationException(ex);
				}
			}
		}

		/// <summary>
		/// Counts the number of intances of objects (using full-text search)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="query">The searching query (like Google searching query)</param>
		/// <param name="filter">The object that presents other filter expression to combine with seaching query</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The integer number that presents total of objects that matched with searching query (and filter expression)</returns>
		public static Task<long> CountAsync<T>(RepositoryContext context, string aliasTypeName, string query, IFilterBy<T> filter, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			// prepare
			context.EntityDefinition = RepositoryMediator.GetEntityDefinition<T>();
			context.AliasTypeName = aliasTypeName;

			// count
			var primaryDataSource = RepositoryMediator.GetPrimaryDataSource(context);
			return primaryDataSource.Mode.Equals(RepositoryModes.NoSQL)
				? NoSqlHelper.CountAsync<T>(context, primaryDataSource, query, filter, null, cancellationToken)
				: primaryDataSource.Mode.Equals(RepositoryModes.SQL)
					? SqlHelper.CountAsync<T>(context, primaryDataSource, query, filter, cancellationToken)
					: Task.FromResult<long>(0);
		}

		/// <summary>
		/// Counts the number of intances of objects (using full-text search)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="query">The searching query (like Google searching query)</param>
		/// <param name="filter">The object that presents other filter expression to combine with seaching query</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The integer number that presents total of objects that matched with searching query (and filter expression)</returns>
		public static Task<long> CountAsync<T>(string aliasTypeName, string query, IFilterBy<T> filter, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			using (var context = new RepositoryContext(false))
			{
				try
				{
					return RepositoryMediator.CountAsync<T>(context, aliasTypeName, query, filter, cancellationToken);
				}
				catch (Exception ex)
				{
					context.Exception = ex;
					return Task.FromException<long>(new RepositoryOperationException(ex));
				}
			}
		}
		#endregion

		#region Call pre-handlers
		static bool CallPreHandlers(RepositoryContext context, object @object)
		{
			return false;
		}

		static Task<bool> CallPreHandlersAsync(RepositoryContext context, object @object, CancellationToken cancellationToken = default(CancellationToken))
		{
			return Task.FromResult<bool>(false);
		}
		#endregion

		#region Call post-handlers
		static void CallPostHandlers(RepositoryContext context, object @object)
		{

		}

		static Task CallPostHandlersAsync(RepositoryContext context, object @object, CancellationToken cancellationToken = default(CancellationToken))
		{
			return Task.CompletedTask;
		}
		#endregion

		#region JSON conversions
		/// <summary>
		/// Serializes the object to JSON object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="object">The object to serialize</param>
		/// <param name="onCompleted">The callback on completed</param>
		/// <returns></returns>
		public static JObject ToJson<T>(T @object, Action<T, JObject> onCompleted = null) where T : class
		{
			var json = @object.ToJson<T>();
			if (onCompleted != null)
				onCompleted(@object, json);
			return json;
		}

		/// <summary>
		/// Creates (Deserializes) an object from the JSON object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="json">The JSON object that contains information</param>
		/// <param name="onCompleted">The callback on completed</param>
		/// <returns></returns>
		public static T FromJson<T>(JToken json, Action<T, JToken> onCompleted = null) where T : class
		{
			var @object = json != null
				? json.FromJson<T>()
				: null;
			if (onCompleted != null)
				onCompleted(@object, json);
			return @object;
		}

		/// <summary>
		/// Serializes the collection of objects to an array of JSON objects
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="objects">The object to serialize</param>
		/// <returns></returns>
		public static JArray ToJArray<T>(List<T> objects) where T : class
		{
			var array = new JArray();
			if (objects != null)
				objects.ForEach(@object => {
					array.Add(@object is RepositoryBase ? (@object as RepositoryBase).ToJson() : @object.ToJson());
				});
			return array;
		}

		/// <summary>
		/// Serializes the collection of objects to an array of JSON objects
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="objects">The object to serialize</param>
		/// <returns></returns>
		public static JArray ToJsonArray<T>(this List<T> objects) where T : class
		{
			return RepositoryMediator.ToJArray(objects);
		}
		#endregion

		#region XML conversions
		/// <summary>
		/// Serializes the object to XML object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="object">The object to serialize</param>
		/// <param name="onCompleted">The callback on completed</param>
		/// <returns></returns>
		public static XElement ToXml<T>(T @object, Action<T, XElement> onCompleted = null) where T : class
		{
			var xml = @object.ToXml<T>();
			if (onCompleted != null)
				onCompleted(@object, xml);
			return xml;
		}

		/// <summary>
		/// Creates (Deserializes) an object from the XML object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="xml">The XML object that contains information</param>
		/// <param name="onCompleted">The callback on completed</param>
		/// <returns></returns>
		public static T FromXml<T>(XContainer xml, Action<T, XContainer> onCompleted = null) where T : class
		{
			var @object = xml != null
				? xml.FromXml<T>()
				: null;
			if (onCompleted != null)
				onCompleted(@object, xml);
			return @object;
		}
		#endregion

		#region Helper for working with logs
		static string LogsPath = null;

		static async Task WriteLogs(string filePath, List<string> logs, Exception ex)
		{
			// prepare
			var info = DateTime.Now.ToString("HH:mm:ss.fff") + "\t" + "[" + Process.GetCurrentProcess().Id.ToString()
				+ " : " + AppDomain.CurrentDomain.Id.ToString() + " : " + Thread.CurrentThread.ManagedThreadId.ToString() + "]" + "\t";

			var content = "";
			if (logs != null)
				logs.ForEach((log) =>
				{
					content += !string.IsNullOrWhiteSpace(log) ? info + log + "\r\n" : "";
				});

			if (ex != null)
			{
				content += info + "- " + (ex.Message != null ? ex.Message : "No error message") + " [" + ex.GetType().ToString() + "]" + "\r\n"
					+ info + "- " + (ex.StackTrace != null ? ex.StackTrace : "No stack trace");

				ex = ex.InnerException;
				var counter = 1;
				while (ex != null)
				{
					content += info + "- Inner (" + counter.ToString() + "): ----------------------------------" + "\r\n"
						+ info + "- " + (ex.Message != null ? ex.Message : "No error message") + " [" + ex.GetType().ToString() + "]" + "\r\n"
						+ info + "- " + (ex.StackTrace != null ? ex.StackTrace : "No stack trace");

					counter++;
					ex = ex.InnerException;
				}
			}

			// write logs into file
			try
			{
				using (var fileStream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite, 4096, true))
				{
					using (var fileWriter = new System.IO.StreamWriter(fileStream, System.Text.Encoding.UTF8))
					{
						await fileWriter.WriteLineAsync(content);
					}
				}
			}
			catch { }
		}

		static void WriteLogs(List<string> logs, Exception ex)
		{
			// prepare path of all log files
			if (RepositoryMediator.LogsPath == null)
				try
				{
					RepositoryMediator.LogsPath = !string.IsNullOrWhiteSpace(System.Web.HttpRuntime.AppDomainAppPath)
						? System.Web.HttpRuntime.AppDomainAppPath + @"\Logs\"
						: null;
				}
				catch { }

			// stop if not valid path is found
			if (RepositoryMediator.LogsPath == null)
				return;

			// build file path and write logs via other thread
			var filePath = RepositoryMediator.LogsPath + DateTime.Now.ToString("yyyy-MM-dd-HH") + ".Repository.txt";
			Task.Run(async () =>
			{
				await RepositoryMediator.WriteLogs(filePath, logs, ex);
			}).ConfigureAwait(false);
		}

		static void WriteLogs(string log, Exception ex)
		{
			RepositoryMediator.WriteLogs(string.IsNullOrWhiteSpace(log) ? null : new List<string>() { log }, ex);
		}

		static void WriteLogs(string log)
		{
			RepositoryMediator.WriteLogs(log, null);
		}
		#endregion

	}
}