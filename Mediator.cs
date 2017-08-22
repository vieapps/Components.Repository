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
	/// Allow to use the Repository functionality without direct reference to <see cref="RepositoryBase">RepositoryBase</see>
	/// </summary>
	public static class RepositoryMediator
	{
#if DEBUG
		public static Dictionary<string, RepositoryDefinition> RepositoryDefinitions = new Dictionary<string, RepositoryDefinition>();
		public static Dictionary<string, EntityDefinition> EntityDefinitions = new Dictionary<string, EntityDefinition>();
		public static Dictionary<string, DataSource> DataSources = new Dictionary<string, DataSource>();
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
					: definition.RepositoryDefinition;

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
			var dataSource = definition.SecondaryDataSource;
			if (dataSource == null)
			{
				var parent = !string.IsNullOrWhiteSpace(aliasTypeName)
					? RepositoryMediator.GetRepositoryDefinition(aliasTypeName)
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
			return dataSource != null
				? RepositoryMediator.GetConnectionStringSettings(dataSource.ConnectionStringName)
				: null;
		}

		/// <summary>
		/// Gets the connection string of the data source (for working with SQL/NoSQL database)
		/// </summary>
		/// <param name="dataSource">The data source</param>
		/// <returns></returns>
		public static string GetConnectionString(this DataSource dataSource)
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
		public static string GetPrimaryConnectionString(this EntityDefinition definition)
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
		public static string GetSecondaryConnectionString(this EntityDefinition definition)
		{
			return RepositoryMediator.GetSecondaryConnectionString(null, definition);
		}
		#endregion

		#region Validate
		internal static bool Validate(EntityDefinition definition, Dictionary<string, object> stateData)
		{
			var changed = false;

			// standard properties
			foreach (var attribute in definition.Attributes)
			{
				if (!attribute.CanRead || !attribute.CanWrite)
					continue;

				object value = stateData[attribute.Name];

				if (object.ReferenceEquals(value, null))
				{
					if (attribute.Name.Equals(definition.PrimaryKey))
						throw new InformationRequiredException("The value of the primary-key is required");
					else if (attribute.NotNull)
						throw new InformationRequiredException("The value of the " + (attribute.IsPublic ? "property" : "attribute") + " named '" + attribute.Name + "' is required (doesn't allow null)");
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

			// extended attribute

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
			context.Operation = RepositoryOperation.Create;
			context.EntityDefinition = RepositoryMediator.GetEntityDefinition<T>();
			context.AliasTypeName = aliasTypeName;

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
			if (RepositoryMediator.CallPreHandlers(context, @object))
				return;

			// create
			var primaryDataSource = RepositoryMediator.GetPrimaryDataSource(context);
			if (primaryDataSource.Mode.Equals(RepositoryMode.NoSQL))
				context.Create<T>(primaryDataSource, @object);
			else if (primaryDataSource.Mode.Equals(RepositoryMode.SQL))
				context.Create<T>(primaryDataSource, @object);

			// update in cache storage
			if (context.EntityDefinition.CacheStorage != null)
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
			context.Operation = RepositoryOperation.Create;
			context.EntityDefinition = RepositoryMediator.GetEntityDefinition<T>();
			context.AliasTypeName = aliasTypeName;

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
			if (await RepositoryMediator.CallPreHandlersAsync(context, @object, cancellationToken))
				return;

			// create
			var primaryDataSource = RepositoryMediator.GetPrimaryDataSource(context);
			if (primaryDataSource.Mode.Equals(RepositoryMode.NoSQL))
				await context.CreateAsync<T>(primaryDataSource, @object, null, cancellationToken);
			else if (primaryDataSource.Mode.Equals(RepositoryMode.SQL))
				await context.CreateAsync<T>(primaryDataSource, @object, cancellationToken);

			// update in cache storage
			if (context.EntityDefinition.CacheStorage != null)
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
				context.Operation = RepositoryOperation.Get;
				context.EntityDefinition = RepositoryMediator.GetEntityDefinition<T>();
				context.AliasTypeName = aliasTypeName;
			}

			// call pre-handlers
			if (callHandlers && RepositoryMediator.CallPreHandlers(context, id))
				return null;

			// get cached object
			var @object = processCache && context.EntityDefinition.CacheStorage != null
				? context.EntityDefinition.CacheStorage.Fetch<T>(id)
				: null;

#if DEBUG
			if (!object.ReferenceEquals(@object, null))
				RepositoryMediator.WriteLogs("GET: The cached object is found [" + @object.GetCacheKey(false) + "]");
#endif

			// load from data store if got no cached
			if (object.ReferenceEquals(@object, null))
			{
				var primaryDataSource = RepositoryMediator.GetPrimaryDataSource(context);
				@object = primaryDataSource.Mode.Equals(RepositoryMode.NoSQL)
					? context.Get<T>(primaryDataSource, id, null)
					: primaryDataSource.Mode.Equals(RepositoryMode.SQL)
						? context.Get<T>(primaryDataSource, id)
						: null;

				// TO DO: check to get instance from secondary source if primary source is not available

				// update into cache storage
				if (!object.ReferenceEquals(@object, null) && processCache && context.EntityDefinition.CacheStorage != null)
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
				context.Operation = RepositoryOperation.Get;
				context.EntityDefinition = RepositoryMediator.GetEntityDefinition<T>();
				context.AliasTypeName = aliasTypeName;
			}

			// call pre-handlers
			if (callHandlers && await RepositoryMediator.CallPreHandlersAsync(context, id, cancellationToken))
				return null;

			// get cached object
			var @object = processCache && context.EntityDefinition.CacheStorage != null
				? await context.EntityDefinition.CacheStorage.FetchAsync<T>(id)
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
				@object = primaryDataSource.Mode.Equals(RepositoryMode.NoSQL)
					? await context.GetAsync<T>(primaryDataSource, id, null, cancellationToken)
					: primaryDataSource.Mode.Equals(RepositoryMode.SQL)
						? await context.GetAsync<T>(primaryDataSource, id, cancellationToken)
						: null;

				// TO DO: check to get instance from secondary source if primary source is not available

				// update into cache storage
				if (!object.ReferenceEquals(@object, null) && processCache && context.EntityDefinition.CacheStorage != null)
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

		#region Get (first match)
		/// <summary>
		/// Finds the first instance of object that matched with the filter
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">Filter expression</param>
		/// <param name="sort">Sort expression</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns></returns>
		public static T Get<T>(RepositoryContext context, string aliasTypeName, IFilterBy<T> filter, SortBy<T> sort = null, string businessEntityID = null) where T : class
		{
			// prepare
			context.Operation = RepositoryOperation.Get;
			context.EntityDefinition = RepositoryMediator.GetEntityDefinition<T>();
			context.AliasTypeName = aliasTypeName;

			// find
			var primaryDataSource = RepositoryMediator.GetPrimaryDataSource(context);
			return primaryDataSource.Mode.Equals(RepositoryMode.NoSQL)
				? context.Get<T>(primaryDataSource, filter, sort, businessEntityID, null)
				: primaryDataSource.Mode.Equals(RepositoryMode.SQL)
					? context.Get<T>(primaryDataSource, filter, sort, businessEntityID)
					: null;
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
		/// Finds the first instance of object that matched with the filter
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">Filter expression</param>
		/// <param name="sort">Sort expression</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<T> GetAsync<T>(RepositoryContext context, string aliasTypeName, IFilterBy<T> filter, SortBy<T> sort = null, string businessEntityID = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			// prepare
			context.Operation = RepositoryOperation.Get;
			context.EntityDefinition = RepositoryMediator.GetEntityDefinition<T>();
			context.AliasTypeName = aliasTypeName;

			// find
			var primaryDataSource = RepositoryMediator.GetPrimaryDataSource(context);
			return primaryDataSource.Mode.Equals(RepositoryMode.NoSQL)
				? context.GetAsync<T>(primaryDataSource, filter, sort, businessEntityID, null, cancellationToken)
				: primaryDataSource.Mode.Equals(RepositoryMode.SQL)
					? context.GetAsync<T>(primaryDataSource, filter, sort, businessEntityID, cancellationToken)
					: Task.FromResult<T>(null);
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
		public static Task<T> GetAsync<T>(string aliasTypeName, IFilterBy<T> filter, SortBy<T> sort = null, string businessEntityID = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			using (var context = new RepositoryContext(false))
			{
				try
				{
					return RepositoryMediator.GetAsync<T>(context, aliasTypeName, filter, sort, businessEntityID, cancellationToken);
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
			context.Operation = RepositoryOperation.Update;
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
					if (data.Key.StartsWith("ExtendedProperties."))
						(@object as IBusinessEntity).ExtendedProperties[data.Key.Replace("ExtendedProperties.", "")] = data.Value;
					else
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
			if (primaryDataSource.Mode.Equals(RepositoryMode.NoSQL))
				context.Replace<T>(primaryDataSource, @object, null);
			else if (primaryDataSource.Mode.Equals(RepositoryMode.SQL))
				context.Replace<T>(primaryDataSource, @object);

			// update into cache storage
			if (context.EntityDefinition.CacheStorage != null)
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
			context.Operation = RepositoryOperation.Update;
			context.EntityDefinition = RepositoryMediator.GetEntityDefinition<T>();
			context.AliasTypeName = aliasTypeName;

			// check state
			var previousInstance = @object != null
				? await RepositoryMediator.GetAsync<T>(context, aliasTypeName, @object.GetEntityID(), false, cancellationToken)
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
					if (data.Key.StartsWith("ExtendedProperties."))
						(@object as IBusinessEntity).ExtendedProperties[data.Key.Replace("ExtendedProperties.", "")] = data.Value;
					else
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
			if (primaryDataSource.Mode.Equals(RepositoryMode.NoSQL))
				await context.ReplaceAsync<T>(primaryDataSource, @object, null, cancellationToken);
			else if (primaryDataSource.Mode.Equals(RepositoryMode.SQL))
				await context.ReplaceAsync<T>(primaryDataSource, @object, cancellationToken);

			// update into cache storage
			if (context.EntityDefinition.CacheStorage != null)
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
			context.Operation = RepositoryOperation.Update;
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
					if (data.Key.StartsWith("ExtendedProperties."))
						(@object as IBusinessEntity).ExtendedProperties[data.Key.Replace("ExtendedProperties.", "")] = data.Value;
					else
						@object.SetAttributeValue(data.Key, data.Value);
				});

				// update state
				context.SetCurrentState(@object, currentState);
			}

			// call pre-handlers
			if (RepositoryMediator.CallPreHandlers(context, @object))
				return;

			// update
			var updatedAttributes = dirtyAttributes.Select(item => item.StartsWith("ExtendedProperties.") ? item.Replace("ExtendedProperties.", "") : item).ToList();

			var primaryDataSource = RepositoryMediator.GetPrimaryDataSource(context);
			if (primaryDataSource.Mode.Equals(RepositoryMode.NoSQL))
				context.Update<T>(primaryDataSource, @object, updatedAttributes, null);
			else if (primaryDataSource.Mode.Equals(RepositoryMode.SQL))
				context.Update<T>(primaryDataSource, @object, updatedAttributes);

			// update into cache storage
			if (context.EntityDefinition.CacheStorage != null)
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
			context.Operation = RepositoryOperation.Update;
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
					if (data.Key.StartsWith("ExtendedProperties."))
						(@object as IBusinessEntity).ExtendedProperties[data.Key.Replace("ExtendedProperties.", "")] = data.Value;
					else
						@object.SetAttributeValue(data.Key, data.Value);
				});

				// update state
				context.SetCurrentState(@object, currentState);
			}

			// call pre-handlers
			if (await RepositoryMediator.CallPreHandlersAsync(context, @object, cancellationToken))
				return;

			// update
			var updatedAttributes = dirtyAttributes.Select(item => item.StartsWith("ExtendedProperties.") ? item.Replace("ExtendedProperties.", "") : item).ToList();

			var primaryDataSource = RepositoryMediator.GetPrimaryDataSource(context);
			if (primaryDataSource.Mode.Equals(RepositoryMode.NoSQL))
				await context.UpdateAsync<T>(primaryDataSource, @object, updatedAttributes, null, cancellationToken);
			else if (primaryDataSource.Mode.Equals(RepositoryMode.SQL))
				await context.UpdateAsync<T>(primaryDataSource, @object, updatedAttributes, cancellationToken);

			// update into cache storage
			if (context.EntityDefinition.CacheStorage != null)
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
			context.Operation = RepositoryOperation.Delete;
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
			if (primaryDataSource.Mode.Equals(RepositoryMode.NoSQL))
				context.Delete<T>(primaryDataSource, @object, null);
			else if (primaryDataSource.Mode.Equals(RepositoryMode.SQL))
				context.Delete<T>(primaryDataSource, @object);

			// remove from cache storage
			if (context.EntityDefinition.CacheStorage != null)
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
			context.Operation = RepositoryOperation.Delete;
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
			if (primaryDataSource.Mode.Equals(RepositoryMode.NoSQL))
				await context.DeleteAsync<T>(primaryDataSource, @object, null, cancellationToken);
			else if (primaryDataSource.Mode.Equals(RepositoryMode.SQL))
				await context.DeleteAsync<T>(primaryDataSource, @object, cancellationToken);

			// remove from cache storage
			if (context.EntityDefinition.CacheStorage != null)
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
		#endregion

		#region Delete (many)
		/// <summary>
		/// Deletes many instances of objects from repository
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">Filter expression to filter instances to delete</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		public static void DeleteMany<T>(RepositoryContext context, string aliasTypeName, IFilterBy<T> filter, string businessEntityID = null) where T : class
		{
			// prepare
			context.Operation = RepositoryOperation.Delete;
			context.EntityDefinition = RepositoryMediator.GetEntityDefinition<T>();
			context.AliasTypeName = aliasTypeName;

			// delete
			var primaryDataSource = RepositoryMediator.GetPrimaryDataSource(context);
			if (primaryDataSource.Mode.Equals(RepositoryMode.NoSQL))
				context.DeleteMany<T>(primaryDataSource, filter, businessEntityID, null);
			else if (primaryDataSource.Mode.Equals(RepositoryMode.SQL))
				context.DeleteMany<T>(primaryDataSource, filter, businessEntityID);

			// TO DO: sync to other data sources

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
				try
				{
					RepositoryMediator.DeleteMany<T>(context, aliasTypeName, filter, businessEntityID);
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
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		public static async Task DeleteManyAsync<T>(RepositoryContext context, string aliasTypeName, IFilterBy<T> filter, string businessEntityID = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			// prepare
			context.Operation = RepositoryOperation.Delete;
			context.EntityDefinition = RepositoryMediator.GetEntityDefinition<T>();
			context.AliasTypeName = aliasTypeName;

			// delete
			var primaryDataSource = RepositoryMediator.GetPrimaryDataSource(context);
			if (primaryDataSource.Mode.Equals(RepositoryMode.NoSQL))
				await context.DeleteManyAsync<T>(primaryDataSource, filter, businessEntityID, null, cancellationToken);
			else if (primaryDataSource.Mode.Equals(RepositoryMode.SQL))
				await context.DeleteManyAsync<T>(primaryDataSource, filter, businessEntityID, cancellationToken);

			// TO DO: sync to other data sources

		}

		/// <summary>
		/// Deletes many instances of objects from repository
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">Filter expression to filter instances to delete</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		public static Task DeleteManyAsync<T>(string aliasTypeName, IFilterBy<T> filter, string businessEntityID = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			using (var context = new RepositoryContext())
			{
				try
				{
					return RepositoryMediator.DeleteManyAsync<T>(context, aliasTypeName, filter, businessEntityID, cancellationToken);
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
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <param name="cacheExpirationType">The string that presents the expiration type (Sliding | Absolute)</param>
		/// <param name="cacheExpirationTime">The number that presents the expiration time (in minutes)</param>
		/// <returns></returns>
		public static List<T> Find<T>(RepositoryContext context, string aliasTypeName, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessEntityID = null, bool autoAssociateWithMultipleParents = true, string cacheKey = null, string cacheExpirationType = null, int cacheExpirationTime = 0) where T : class
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
				: !string.IsNullOrWhiteSpace(cacheKey)
					? context.EntityDefinition.CacheStorage.Get<List<string>>(cacheKey)
					: primaryDataSource.Mode.Equals(RepositoryMode.NoSQL)
						? context.SelectIdentities<T>(primaryDataSource, filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents, null)
						: primaryDataSource.Mode.Equals(RepositoryMode.SQL)
							? context.SelectIdentities<T>(primaryDataSource, filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents)
							: new List<string>();

			// process cache
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
						var missing = primaryDataSource.Mode.Equals(RepositoryMode.NoSQL)
							? context.Find<T>(primaryDataSource, identities, sort, businessEntityID, null)
							: primaryDataSource.Mode.Equals(RepositoryMode.SQL)
								? context.Find<T>(primaryDataSource, identities, sort, businessEntityID)
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
					? primaryDataSource.Mode.Equals(RepositoryMode.NoSQL)
						? context.Find<T>(primaryDataSource, filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents, null)
						: primaryDataSource.Mode.Equals(RepositoryMode.SQL)
							? context.Find<T>(primaryDataSource, filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents)
							: new List<T>()
					: new List<T>();

				if (context.EntityDefinition.CacheStorage != null && objects.Count > 0)
				{
					if (!string.IsNullOrWhiteSpace(cacheKey))
						context.EntityDefinition.CacheStorage.Set(cacheKey, objects.Select(o => o.GetEntityID()).ToList(), string.IsNullOrWhiteSpace(cacheExpirationType) ? "Absolute" : cacheExpirationType, cacheExpirationTime);
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
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <param name="cacheExpirationType">The string that presents the expiration type (Sliding | Absolute)</param>
		/// <param name="cacheExpirationTime">The number that presents the expiration time (in minutes)</param>
		/// <returns></returns>
		public static List<T> Find<T>(string aliasTypeName, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessEntityID = null, bool autoAssociateWithMultipleParents = true, string cacheKey = null, string cacheExpirationType = null, int cacheExpirationTime = 0) where T : class
		{
			using (var context = new RepositoryContext(false))
			{
				try
				{
					return RepositoryMediator.Find<T>(context, aliasTypeName, filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents, cacheKey, cacheExpirationType, cacheExpirationTime);
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
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <param name="cacheExpirationType">The string that presents the expiration type (Sliding | Absolute)</param>
		/// <param name="cacheExpirationTime">The number that presents the expiration time (in minutes)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<List<T>> FindAsync<T>(RepositoryContext context, string aliasTypeName, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessEntityID = null, bool autoAssociateWithMultipleParents = true, string cacheKey = null, string cacheExpirationType = null, int cacheExpirationTime = 0, CancellationToken cancellationToken = default(CancellationToken)) where T : class
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
				: !string.IsNullOrWhiteSpace(cacheKey)
					? await context.EntityDefinition.CacheStorage.GetAsync<List<string>>(cacheKey)
					: primaryDataSource.Mode.Equals(RepositoryMode.NoSQL)
						? await context.SelectIdentitiesAsync<T>(primaryDataSource, filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents, null, cancellationToken)
						: primaryDataSource.Mode.Equals(RepositoryMode.SQL)
							? await context.SelectIdentitiesAsync<T>(primaryDataSource, filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents, cancellationToken)
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
						var missing = primaryDataSource.Mode.Equals(RepositoryMode.NoSQL)
							? await context.FindAsync<T>(primaryDataSource, identities, sort, businessEntityID, null, cancellationToken)
							: primaryDataSource.Mode.Equals(RepositoryMode.SQL)
								? await context.FindAsync<T>(primaryDataSource, identities, sort, businessEntityID, cancellationToken)
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
					? primaryDataSource.Mode.Equals(RepositoryMode.NoSQL)
						? await context.FindAsync<T>(primaryDataSource, filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents, null, cancellationToken)
						: primaryDataSource.Mode.Equals(RepositoryMode.SQL)
							? await context.FindAsync<T>(primaryDataSource, filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents, cancellationToken)
							: new List<T>()
					: new List<T>();

				if (context.EntityDefinition.CacheStorage != null && objects.Count > 0)
				{
					if (!string.IsNullOrWhiteSpace(cacheKey))
						await context.EntityDefinition.CacheStorage.SetAsync(cacheKey, objects.Select(o => o.GetEntityID()).ToList(), string.IsNullOrWhiteSpace(cacheExpirationType) ? "Absolute" : cacheExpirationType, cacheExpirationTime);
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
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <param name="cacheExpirationType">The string that presents the expiration type (Sliding | Absolute)</param>
		/// <param name="cacheExpirationTime">The number that presents the expiration time (in minutes)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<List<T>> FindAsync<T>(string aliasTypeName, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessEntityID = null, bool autoAssociateWithMultipleParents = true, string cacheKey = null, string cacheExpirationType = null, int cacheExpirationTime = 0, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			using (var context = new RepositoryContext(false))
			{
				try
				{
					return RepositoryMediator.FindAsync<T>(context, aliasTypeName, filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents, cacheKey, cacheExpirationType, cacheExpirationTime, cancellationToken);
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
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of total number of objects</param>
		/// <param name="cacheExpirationType">The string that presents the expiration type (Sliding | Absolute)</param>
		/// <param name="cacheExpirationTime">The number that presents the expiration time (in minutes)</param>
		/// <returns>The integer number that presents total of objects that matched with the filter expression</returns>
		public static long Count<T>(RepositoryContext context, string aliasTypeName, IFilterBy<T> filter, string businessEntityID = null, bool autoAssociateWithMultipleParents = true, string cacheKey = null, string cacheExpirationType = null, int cacheExpirationTime = 0) where T : class
		{
			// prepare
			context.EntityDefinition = RepositoryMediator.GetEntityDefinition<T>();
			context.AliasTypeName = aliasTypeName;

			// check cache
			var total = !string.IsNullOrWhiteSpace(cacheKey) && context.EntityDefinition.CacheStorage != null && context.EntityDefinition.CacheStorage.Exists(cacheKey)
				? context.EntityDefinition.CacheStorage.Get<long>(cacheKey)
				: -1;
			if (total > -1)
				return total;
			
			// count
			var primaryDataSource = RepositoryMediator.GetPrimaryDataSource(context);
			total = primaryDataSource.Mode.Equals(RepositoryMode.NoSQL)
				? context.Count<T>(primaryDataSource, filter, businessEntityID, autoAssociateWithMultipleParents, null)
				: primaryDataSource.Mode.Equals(RepositoryMode.SQL)
					? context.Count<T>(primaryDataSource, filter, businessEntityID, autoAssociateWithMultipleParents)
					: 0;

			// update cache and return
			if (!string.IsNullOrWhiteSpace(cacheKey) && context.EntityDefinition.CacheStorage != null)
				context.EntityDefinition.CacheStorage.Set(cacheKey, total, string.IsNullOrWhiteSpace(cacheExpirationType) ? "Absolute" : cacheExpirationType, cacheExpirationTime);
			return total;
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
		/// <param name="cacheExpirationType">The string that presents the expiration type (Sliding | Absolute)</param>
		/// <param name="cacheExpirationTime">The number that presents the expiration time (in minutes)</param>
		/// <returns>The integer number that presents total of objects that matched with the filter expression</returns>
		public static long Count<T>(string aliasTypeName, IFilterBy<T> filter, string businessEntityID = null, bool autoAssociateWithMultipleParents = true, string cacheKey = null, string cacheExpirationType = null, int cacheExpirationTime = 0) where T : class
		{
			using (var context = new RepositoryContext(false))
			{
				try
				{
					return RepositoryMediator.Count<T>(context, aliasTypeName, filter, businessEntityID, autoAssociateWithMultipleParents, cacheKey, cacheExpirationType, cacheExpirationTime);
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
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of total number of objects</param>
		/// <param name="cacheExpirationType">The string that presents the expiration type (Sliding | Absolute)</param>
		/// <param name="cacheExpirationTime">The number that presents the expiration time (in minutes)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The integer number that presents total of objects that matched with the filter expression</returns>
		public static async Task<long> CountAsync<T>(RepositoryContext context, string aliasTypeName, IFilterBy<T> filter, string businessEntityID = null, bool autoAssociateWithMultipleParents = true, string cacheKey = null, string cacheExpirationType = null, int cacheExpirationTime = 0, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			// prepare
			context.EntityDefinition = RepositoryMediator.GetEntityDefinition<T>();
			context.AliasTypeName = aliasTypeName;

			// check cache
			var total = !string.IsNullOrWhiteSpace(cacheKey) && context.EntityDefinition.CacheStorage != null && await context.EntityDefinition.CacheStorage.ExistsAsync(cacheKey)
				? await context.EntityDefinition.CacheStorage.GetAsync<long>(cacheKey)
				: -1;
			if (total > -1)
				return total;

			// count
			var primaryDataSource = RepositoryMediator.GetPrimaryDataSource(context);
			total = primaryDataSource.Mode.Equals(RepositoryMode.NoSQL)
				? await context.CountAsync<T>(primaryDataSource, filter, businessEntityID, autoAssociateWithMultipleParents, null, cancellationToken)
				: primaryDataSource.Mode.Equals(RepositoryMode.SQL)
					? await context.CountAsync<T>(primaryDataSource, filter, businessEntityID, autoAssociateWithMultipleParents, cancellationToken)
					: 0;

			// update cache and return
			if (!string.IsNullOrWhiteSpace(cacheKey) && context.EntityDefinition.CacheStorage != null)
				await context.EntityDefinition.CacheStorage.SetAsync(cacheKey, total, string.IsNullOrWhiteSpace(cacheExpirationType) ? "Absolute" : cacheExpirationType, cacheExpirationTime);
			return total;
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
		/// <param name="cacheExpirationType">The string that presents the expiration type (Sliding | Absolute)</param>
		/// <param name="cacheExpirationTime">The number that presents the expiration time (in minutes)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The integer number that presents total of objects that matched with the filter expression</returns>
		public static Task<long> CountAsync<T>(string aliasTypeName, IFilterBy<T> filter, string businessEntityID = null, bool autoAssociateWithMultipleParents = true, string cacheKey = null, string cacheExpirationType = null, int cacheExpirationTime = 0, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			using (var context = new RepositoryContext(false))
			{
				try
				{
					return RepositoryMediator.CountAsync<T>(context, aliasTypeName, filter, businessEntityID, autoAssociateWithMultipleParents, cacheKey, cacheExpirationType, cacheExpirationTime, cancellationToken);
				}
				catch (Exception ex)
				{
					context.Exception = ex;
					return Task.FromException<long>(new RepositoryOperationException(ex));
				}
			}
		}
		#endregion

		#region Search (by query)
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
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns></returns>
		public static List<T> Search<T>(RepositoryContext context, string aliasTypeName, string query, IFilterBy<T> filter, int pageSize, int pageNumber, string businessEntityID = null) where T : class
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
				: primaryDataSource.Mode.Equals(RepositoryMode.NoSQL)
					? context.SearchIdentities<T>(primaryDataSource, query, filter, pageSize, pageNumber, businessEntityID, null)
					: primaryDataSource.Mode.Equals(RepositoryMode.SQL)
						? context.SearchIdentities<T>(primaryDataSource, query, filter, pageSize, pageNumber, businessEntityID)
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
						var missing = primaryDataSource.Mode.Equals(RepositoryMode.NoSQL)
							? context.Find<T>(primaryDataSource, identities, null, businessEntityID, null)
							: primaryDataSource.Mode.Equals(RepositoryMode.SQL)
								? context.Find<T>(primaryDataSource, identities, null, businessEntityID)
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
					? primaryDataSource.Mode.Equals(RepositoryMode.NoSQL)
						? context.Search<T>(primaryDataSource, query, filter, pageSize, pageNumber, businessEntityID, null)
						: primaryDataSource.Mode.Equals(RepositoryMode.SQL)
							? context.Search<T>(primaryDataSource, query, filter, pageSize, pageNumber, businessEntityID)
							: new List<T>()
					: new List<T>();

				if (context.EntityDefinition.CacheStorage != null && objects.Count > 0)
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
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns></returns>
		public static List<T> Search<T>(string aliasTypeName, string query, IFilterBy<T> filter, int pageSize, int pageNumber, string businessEntityID = null) where T : class
		{
			using (var context = new RepositoryContext(false))
			{
				try
				{
					return RepositoryMediator.Search<T>(context, aliasTypeName, query, filter, pageSize, pageNumber, businessEntityID);
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
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<List<T>> SearchAsync<T>(RepositoryContext context, string aliasTypeName, string query, IFilterBy<T> filter, int pageSize, int pageNumber, string businessEntityID = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
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
				: primaryDataSource.Mode.Equals(RepositoryMode.NoSQL)
					? await context.SearchIdentitiesAsync<T>(primaryDataSource, query, filter, pageSize, pageNumber, businessEntityID, null, cancellationToken)
					: primaryDataSource.Mode.Equals(RepositoryMode.SQL)
						? await context.SearchIdentitiesAsync<T>(primaryDataSource, query, filter, pageSize, pageNumber, businessEntityID, cancellationToken)
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
						var missing = primaryDataSource.Mode.Equals(RepositoryMode.NoSQL)
							? await context.FindAsync<T>(primaryDataSource, identities, null, businessEntityID, null, cancellationToken)
							: primaryDataSource.Mode.Equals(RepositoryMode.SQL)
								? await context.FindAsync<T>(primaryDataSource, identities, null, businessEntityID, cancellationToken)
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
					? primaryDataSource.Mode.Equals(RepositoryMode.NoSQL)
						? await context.SearchAsync<T>(primaryDataSource, query, filter, pageSize, pageNumber, businessEntityID, null, cancellationToken)
						: primaryDataSource.Mode.Equals(RepositoryMode.SQL)
							? await context.SearchAsync<T>(primaryDataSource, query, filter, pageSize, pageNumber, businessEntityID, cancellationToken)
							: new List<T>()
					: new List<T>();

				if (context.EntityDefinition.CacheStorage != null && objects.Count > 0)
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
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<List<T>> SearchAsync<T>(string aliasTypeName, string query, IFilterBy<T> filter, int pageSize, int pageNumber, string businessEntityID = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			using (var context = new RepositoryContext(false))
			{
				try
				{
					return RepositoryMediator.SearchAsync<T>(context, aliasTypeName, query, filter, pageSize, pageNumber, businessEntityID, cancellationToken);
				}
				catch (Exception ex)
				{
					context.Exception = ex;
					return Task.FromException<List<T>>(new RepositoryOperationException(ex));
				}
			}
		}
		#endregion

		#region Count (by query)
		/// <summary>
		/// Counts the number of intances of objects (using full-text search)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="query">The searching query (like Google searching query)</param>
		/// <param name="filter">The object that presents other filter expression to combine with seaching query</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns>The integer number that presents total of objects that matched with searching query (and filter expression)</returns>
		public static long Count<T>(RepositoryContext context, string aliasTypeName, string query, IFilterBy<T> filter, string businessEntityID = null) where T : class
		{
			// prepare
			context.EntityDefinition = RepositoryMediator.GetEntityDefinition<T>();
			context.AliasTypeName = aliasTypeName;

			// count
			var primaryDataSource = RepositoryMediator.GetPrimaryDataSource(context);
			return primaryDataSource.Mode.Equals(RepositoryMode.NoSQL)
				? context.Count<T>(primaryDataSource, query, filter, businessEntityID, null)
				: primaryDataSource.Mode.Equals(RepositoryMode.SQL)
					? context.Count<T>(primaryDataSource, query, filter, businessEntityID)
					: 0;
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
				try
				{
					return RepositoryMediator.Count<T>(context, aliasTypeName, query, filter, businessEntityID);
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
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The integer number that presents total of objects that matched with searching query (and filter expression)</returns>
		public static Task<long> CountAsync<T>(RepositoryContext context, string aliasTypeName, string query, IFilterBy<T> filter, string businessEntityID = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			// prepare
			context.EntityDefinition = RepositoryMediator.GetEntityDefinition<T>();
			context.AliasTypeName = aliasTypeName;

			// count
			var primaryDataSource = RepositoryMediator.GetPrimaryDataSource(context);
			return primaryDataSource.Mode.Equals(RepositoryMode.NoSQL)
				? context.CountAsync<T>(primaryDataSource, query, filter, businessEntityID, null, cancellationToken)
				: primaryDataSource.Mode.Equals(RepositoryMode.SQL)
					? context.CountAsync<T>(primaryDataSource, query, filter, businessEntityID, cancellationToken)
					: Task.FromResult<long>(0);
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
		public static Task<long> CountAsync<T>(string aliasTypeName, string query, IFilterBy<T> filter, string businessEntityID = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			using (var context = new RepositoryContext(false))
			{
				try
				{
					return RepositoryMediator.CountAsync<T>(context, aliasTypeName, query, filter, businessEntityID, cancellationToken);
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

		#region JSON/XML conversions
		/// <summary>
		/// Serializes the collection of objects to an array of JSON objects
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="objects">The object to serialize</param>
		/// <param name="addTypeOfExtendedProperties">true to add type of all extended properties when generate elements</param>
		/// <returns></returns>
		public static JArray ToJsonArray<T>(this List<T> objects, bool addTypeOfExtendedProperties = false) where T : class
		{
			var array = new JArray();
			if (objects != null)
				objects.ForEach(@object =>
				{
					array.Add(@object is RepositoryBase
							? addTypeOfExtendedProperties
								? (@object as RepositoryBase).ToJson(addTypeOfExtendedProperties)
								: (@object as RepositoryBase).ToJson()
							: @object.ToJson()
						);
				});
			return array;
		}

		/// <summary>
		/// Serializes the collection of objects to a JSON object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="objects">The object to serialize</param>
		/// <param name="addTypeOfExtendedProperties">true to add type of all extended properties when generate elements</param>
		/// <returns></returns>
		public static JObject ToJsonObject<T>(this List<T> objects, bool addTypeOfExtendedProperties = false) where T : class
		{
			var json = new JObject();
			objects.ForEach(@object =>
			{
				json.Add(new JProperty(
						@object.GetEntityID(),
						@object is RepositoryBase
							? addTypeOfExtendedProperties
								? (@object as RepositoryBase).ToJson(addTypeOfExtendedProperties)
								: (@object as RepositoryBase).ToJson()
							: @object.ToJson())
					);
			});
			return json;
		}

		/// <summary>
		/// Serializes the collection of objects to XML
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="objects">The object to serialize</param>
		/// <param name="name">The string that presents name of root tag, null to use default</param>
		/// <param name="addTypeOfExtendedProperties">true to add type of all extended properties when generate elements</param>
		/// <returns></returns>
		public static XElement ToXML<T>(this List<T> objects, string name = null, bool addTypeOfExtendedProperties = false) where T : class
		{
			var xml = new XElement(XName.Get(string.IsNullOrWhiteSpace(name) ? typeof(T).GetTypeName(true) : name));
			if (objects != null)
				objects.ForEach(@object => {
					xml.Add(
							@object is RepositoryBase
								? addTypeOfExtendedProperties
									? (@object as RepositoryBase).ToXml(addTypeOfExtendedProperties)
									: (@object as RepositoryBase).ToXml()
								: @object.ToXml()
						);
				});
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
		/// <param name="cacheStorage">The cache storage</param>
		/// <param name="object">The object to update into cache storage</param>
		/// <param name="expirationTime">The expiration time (in minutes)</param>
		public static bool Set<T>(this CacheManager cacheStorage, T @object, int expirationTime = 0) where T : class
		{
			return !object.ReferenceEquals(@object, null)
				? cacheStorage.Set(@object.GetCacheKey(), @object, expirationTime)
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
		/// <param name="object">The object to update into cache storage</param>
		/// <param name="expirationTime">The expiration time (in minutes)</param>
		public static Task<bool> SetAsync<T>(this CacheManager cacheStorage, T @object, int expirationTime = 0) where T : class
		{
			return !object.ReferenceEquals(@object, null)
				? cacheStorage.SetAsync(@object.GetCacheKey(), @object, expirationTime)
				: Task.FromResult<bool>(false);
		}

		/// <summary>
		/// Adds the collection of objects into cache storage
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="cacheStorage">The cache storage</param>
		/// <param name="objects">The collection of objects</param>
		public static Task SetAsync<T>(this CacheManager cacheStorage, IEnumerable<T> objects) where T : class
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
		/// <param name="object">The object to update into cache storage</param>
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
		/// <param name="object">The object to update into cache storage</param>
		/// <param name="expirationTime">The integer number that present expiration times (in minutes)</param>
		public static Task<bool> SetAbsoluteAsync<T>(this CacheManager cacheStorage, T @object, int expirationTime = 0) where T : class
		{
			return !object.ReferenceEquals(@object, null)
				? cacheStorage.SetAbsoluteAsync(@object.GetCacheKey(), @object, expirationTime)
				: Task.FromResult<bool>(false);
		}

		/// <summary>
		/// Adds an object into cache storage (when its no cached)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="cacheStorage">The cache storage</param>
		/// <param name="object">The object to update into cache storage</param>
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
		/// <param name="object">The object to update into cache storage</param>
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
		/// <param name="object">The object to update into cache storage</param>
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
		/// <param name="object">The object to update into cache storage</param>
		public static Task<bool> SetIfAlreadyExistsAsync<T>(this CacheManager cacheStorage, T @object) where T : class
		{
			return !object.ReferenceEquals(@object, null)
				? cacheStorage.SetIfAlreadyExistsAsync(@object.GetCacheKey(), @object)
				: Task.FromResult<bool>(false);
		}

		/// <summary>
		/// Fetchs an object from cache storage
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="cacheStorage">The cache storage</param>
		/// <param name="identity">The string that presents identity of object need to get</param>
		/// <returns></returns>
		public static T Fetch<T>(this CacheManager cacheStorage, string identity) where T : class
		{
			return !string.IsNullOrWhiteSpace(identity)
				? cacheStorage.Get<T>(identity.GetCacheKey<T>())
				: null;
		}

		/// <summary>
		/// Fetchs an object from cache storage
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="cacheStorage">The cache storage</param>
		/// <param name="identity">The string that presents identity of object need to get</param>
		/// <returns></returns>
		public static Task<T> FetchAsync<T>(this CacheManager cacheStorage, string identity) where T : class
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

		#region Property/Attribute extension methods
		internal static Tuple<Dictionary<string, ObjectService.AttributeInfo>, Dictionary<string, ExtendedPropertyDefinition>> GetProperties<T>(string businessEntityID, EntityDefinition definition = null, bool lowerCaseKeys = false) where T : class
		{
			definition = definition != null
				? definition
				: RepositoryMediator.GetEntityDefinition<T>();

			var standardProperties = definition != null
				? definition.Attributes.ToDictionary(attribute => lowerCaseKeys ? attribute.Name.ToLower() : attribute.Name)
				: ObjectService.GetProperties(typeof(T)).ToDictionary(attribute => lowerCaseKeys ? attribute.Name.ToLower() : attribute.Name);

			var extendedProperties = definition != null && definition.Type.CreateInstance().IsGotExtendedProperties(businessEntityID, definition)
				? definition.RuntimeEntities[businessEntityID].ExtendedPropertyDefinitions.ToDictionary(attribute => lowerCaseKeys ? attribute.Name.ToLower() : attribute.Name)
				: null;

			return new Tuple<Dictionary<string, ObjectService.AttributeInfo>, Dictionary<string, ExtendedPropertyDefinition>>(standardProperties, extendedProperties);
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

			if ((filter as FilterBys<T>).Children == null || (filter as FilterBys<T>).Children.Count <1)
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

		internal static async Task WriteLogs(string filePath, List<string> logs, Exception ex)
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

		internal static void WriteLogs(List<string> logs, Exception ex)
		{
			// prepare path of all log files
			if (string.IsNullOrWhiteSpace(RepositoryMediator.LogsPath))
				try
				{
					RepositoryMediator.LogsPath = ConfigurationManager.AppSettings["vieapps:LogsPath"];
					if (!RepositoryMediator.LogsPath.EndsWith(@"\"))
						RepositoryMediator.LogsPath += @"\";
				}
				catch { }

			if (string.IsNullOrWhiteSpace(RepositoryMediator.LogsPath))
				try
				{
					RepositoryMediator.LogsPath = !string.IsNullOrWhiteSpace(System.Web.HttpRuntime.AppDomainAppPath)
						? System.Web.HttpRuntime.AppDomainAppPath + @"\Logs\"
						: null;
				}
				catch { }

			if (string.IsNullOrWhiteSpace(RepositoryMediator.LogsPath))
				try
				{
					RepositoryMediator.LogsPath = Directory.GetCurrentDirectory() + @"\Logs\";
				}
				catch { }

			// stop if a valid path is not found
			if (string.IsNullOrWhiteSpace(RepositoryMediator.LogsPath))
				return;

			// build file path and write logs via other thread
			var filePath = RepositoryMediator.LogsPath + DateTime.Now.ToString("yyyy-MM-dd-HH") + ".Repository.txt";
			Task.Run(async () =>
			{
				await RepositoryMediator.WriteLogs(filePath, logs, ex);
			}).ConfigureAwait(false);
		}

		internal static void WriteLogs(string log, Exception ex)
		{
			RepositoryMediator.WriteLogs(string.IsNullOrWhiteSpace(log) ? null : new List<string>() { log }, ex);
		}

		internal static void WriteLogs(string log)
		{
			RepositoryMediator.WriteLogs(log, null);
		}
		#endregion

	}
}