﻿#region Related components
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

using MongoDB.Driver;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;

using net.vieapps.Components.Utility;
#endregion

namespace net.vieapps.Components.Repository
{
	/// <summary>
	/// Collection of methods for working with NoSQL database (MongoDB)
	/// </summary>
	public static class NoSqlHelper
	{

		#region Client
		internal static ConcurrentDictionary<string, IMongoClient> Clients { get; } = new ConcurrentDictionary<string, IMongoClient>();
		internal static ConcurrentDictionary<int, bool> ReplicaSetClients { get; } = new ConcurrentDictionary<int, bool>();

		/// <summary>
		/// Checks to see client is connected to a ReplicaSet or not
		/// </summary>
		/// <param name="mongoClient"></param>
		/// <returns></returns>
		public static bool IsReplicaSet(this IMongoClient mongoClient)
		{
			if (NoSqlHelper.ReplicaSetClients.TryGetValue(mongoClient.GetHashCode(), out bool status))
				return status;

			try
			{
				var statusDoc = mongoClient.GetDatabase("admin").RunCommand<BsonDocument>("{ replSetGetStatus: 1 }");
				status = statusDoc.Contains("replSet");
			}
			catch
			{
				status = false;
			}

			NoSqlHelper.ReplicaSetClients.TryAdd(mongoClient.GetHashCode(), status);
			return status;
		}

		/// <summary>
		/// Checks to see client is connected to a ReplicaSet or not
		/// </summary>
		/// <param name="mongoClient"></param>
		/// <returns></returns>
		public static async Task<bool> IsReplicaSetAsync(this IMongoClient mongoClient, CancellationToken cancellationToken = default(CancellationToken))
		{
			if (NoSqlHelper.ReplicaSetClients.TryGetValue(mongoClient.GetHashCode(), out bool status))
				return status;

			try
			{
				var statusDoc = await mongoClient.GetDatabase("admin").RunCommandAsync<BsonDocument>("{ replSetGetStatus: 1 }").ConfigureAwait(false);
				status = statusDoc.Contains("replSet");
			}
			catch
			{
				status = false;
			}

			NoSqlHelper.ReplicaSetClients.TryAdd(mongoClient.GetHashCode(), status);
			return status;
		}

		/// <summary>
		/// Gets a client for working with MongoDB
		/// </summary>
		/// <param name="connectionString"></param>
		/// <returns></returns>
		public static IMongoClient GetClient(string connectionString)
		{
			if (string.IsNullOrWhiteSpace(connectionString))
				return null;

			var key = "MongoClient#" + connectionString.Trim().ToLower().GenerateUUID();
			if (!NoSqlHelper.Clients.TryGetValue(key, out IMongoClient client))
				lock (NoSqlHelper.Clients)
				{
					if (!NoSqlHelper.Clients.TryGetValue(key, out client))
					{
						client = new MongoClient(connectionString);
						NoSqlHelper.Clients.TryAdd(key, client);
					}
				}
			return client;
		}
		#endregion

		#region Database
		/// <summary>
		/// Gets a database of MongoDB
		/// </summary>
		/// <param name="mongoClient"></param>
		/// <param name="databaseName"></param>
		/// <param name="databaseSettings"></param>
		/// <returns></returns>
		public static IMongoDatabase GetDatabase(IMongoClient mongoClient, string databaseName, MongoDatabaseSettings databaseSettings = null)
			=> !string.IsNullOrWhiteSpace(databaseName) && mongoClient != null
				? mongoClient.GetDatabase(databaseName, databaseSettings)
				: null;

		internal static ConcurrentDictionary<string, IMongoDatabase> Databases { get; } = new ConcurrentDictionary<string, IMongoDatabase>();

		/// <summary>
		/// Gets a database of MongoDB
		/// </summary>
		/// <param name="connectionString"></param>
		/// <param name="databaseName"></param>
		/// <param name="databaseSettings"></param>
		/// <returns></returns>
		public static IMongoDatabase GetDatabase(string connectionString, string databaseName, MongoDatabaseSettings databaseSettings = null)
		{
			if (string.IsNullOrWhiteSpace(connectionString) || string.IsNullOrWhiteSpace(databaseName))
				return null;

			var key = databaseName.Trim() + "#" + connectionString.Trim().ToLower().GenerateUUID();
			if (!NoSqlHelper.Databases.TryGetValue(key, out IMongoDatabase database))
				lock (NoSqlHelper.Databases)
				{
					if (!NoSqlHelper.Databases.TryGetValue(key, out database))
					{
						database = NoSqlHelper.GetDatabase(NoSqlHelper.GetClient(connectionString), databaseName, databaseSettings);
						NoSqlHelper.Databases.TryAdd(key, database);
					}
				}
			return database;
		}

		/// <summary>
		/// Starts a client session of this database
		/// </summary>
		/// <param name="database"></param>
		/// <param name="options"></param>
		/// <returns></returns>
		public static IClientSessionHandle StartSession(this IMongoDatabase database, ClientSessionOptions options = null)
			=> database.Client.StartSession(options);

		/// <summary>
		/// Starts a client session of this database
		/// </summary>
		/// <param name="database"></param>
		/// <param name="options"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public static Task<IClientSessionHandle> StartSessionAsync(this IMongoDatabase database, ClientSessionOptions options = null, CancellationToken cancellationToken = default(CancellationToken))
			=> database.Client.StartSessionAsync(options, cancellationToken);

		/// <summary>
		/// Checks to see database is belong to a client that is connected to a ReplicaSet or not
		/// </summary>
		/// <param name="database"></param>
		/// <returns></returns>
		public static bool IsReplicaSet(this IMongoDatabase database)
			=> database.Client.IsReplicaSet();

		/// <summary>
		/// Checks to see database is belong to a client that is connected to a ReplicaSet or not
		/// </summary>
		/// <param name="database"></param>
		/// <returns></returns>
		public static Task<bool> IsReplicaSetAsync(this IMongoDatabase database, CancellationToken cancellationToken = default(CancellationToken))
			=> database.Client.IsReplicaSetAsync(cancellationToken);
		#endregion

		#region Collection
		internal static ConcurrentDictionary<string, object> Collections { get; } = new ConcurrentDictionary<string, object>();

		/// <summary>
		/// Gets a collection of MongoDB
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="mongoDatabase"></param>
		/// <param name="collectionName"></param>
		/// <param name="collectionSettings"></param>
		/// <returns></returns>
		public static IMongoCollection<T> GetCollection<T>(IMongoDatabase mongoDatabase, string collectionName, MongoCollectionSettings collectionSettings = null) where T : class
			=> mongoDatabase != null && !string.IsNullOrWhiteSpace(collectionName)
				? mongoDatabase.GetCollection<T>(collectionName, collectionSettings)
				: null;

		/// <summary>
		/// Gets a collection of MongoDB
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="connectionString"></param>
		/// <param name="databaseName"></param>
		/// <param name="collectionName"></param>
		/// <param name="disableCache"></param>
		/// <param name="databaseSettings"></param>
		/// <param name="collectionSettings"></param>
		/// <returns></returns>
		public static IMongoCollection<T> GetCollection<T>(string connectionString, string databaseName, string collectionName, bool disableCache = false, MongoDatabaseSettings databaseSettings = null, MongoCollectionSettings collectionSettings = null) where T : class
		{
			if (string.IsNullOrWhiteSpace(connectionString) || string.IsNullOrWhiteSpace(databaseName) || string.IsNullOrWhiteSpace(collectionName))
				return null;

			if (disableCache)
				return NoSqlHelper.GetCollection<T>(NoSqlHelper.GetDatabase(connectionString, databaseName, databaseSettings), collectionName, collectionSettings);

			var key = collectionName.Trim() + "#" + (databaseName.Trim() + "#" + connectionString.Trim()).ToLower().GetMD5();
			if (!NoSqlHelper.Collections.TryGetValue(key, out object collection))
				lock (NoSqlHelper.Collections)
				{
					if (!NoSqlHelper.Collections.TryGetValue(key, out collection))
					{
						collection = NoSqlHelper.GetCollection<T>(NoSqlHelper.GetDatabase(connectionString, databaseName, databaseSettings), collectionName, collectionSettings);
						NoSqlHelper.Collections.TryAdd(key, collection);
					}
				}
			return collection as IMongoCollection<T>;
		}

		/// <summary>
		/// Gets a collection in NoSQL database (MongoDB collection)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="dataSource">The data source</param>
		/// <param name="entityDefinition">The entity definition</param>
		/// <returns></returns>
		public static IMongoCollection<T> GetCollection<T>(this DataSource dataSource, EntityDefinition entityDefinition) where T : class
			=> NoSqlHelper.GetCollection<T>(RepositoryMediator.GetConnectionString(dataSource), dataSource.DatabaseName, entityDefinition.CollectionName);

		/// <summary>
		/// Gets a collection in NoSQL database (MongoDB collection)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="dataSource">The data source</param>
		/// <returns></returns>
		public static IMongoCollection<T> GetCollection<T>(this DataSource dataSource) where T : class
			=> dataSource.GetCollection<T>(RepositoryMediator.GetEntityDefinition<T>());

		/// <summary>
		/// Gets a collection in NoSQL database (MongoDB collection)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <returns></returns>
		public static IMongoCollection<T> GetCollection<T>(this RepositoryContext context, DataSource dataSource) where T : class
			=> NoSqlHelper.GetCollection<T>(dataSource, context.EntityDefinition);

		/// <summary>
		/// Starts a client session of this collection
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="options"></param>
		/// <returns></returns>
		public static IClientSessionHandle StartSession<T>(this IMongoCollection<T> collection, ClientSessionOptions options = null) where T : class
			=> collection.Database.StartSession(options);

		/// <summary>
		/// Starts a client session of this collection
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="options"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public static Task<IClientSessionHandle> StartSessionAsync<T>(this IMongoCollection<T> collection, ClientSessionOptions options = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
			=> collection.Database.StartSessionAsync(options, cancellationToken);

		/// <summary>
		/// Starts a client session of this collection
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public static Task<IClientSessionHandle> StartSessionAsync<T>(this IMongoCollection<T> collection, CancellationToken cancellationToken) where T : class
			=> collection.Database.StartSessionAsync(null, cancellationToken);

		/// <summary>
		/// Checks to see collection is belong to a client that is connected to a ReplicaSet or not
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <returns></returns>
		public static bool IsReplicaSet<T>(this IMongoCollection<T> collection)
			=> collection.Database.IsReplicaSet();

		/// <summary>
		/// Checks to see collection is belong to a client that is connected to a ReplicaSet or not
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <returns></returns>
		public static Task<bool> IsReplicaSetAsync<T>(this IMongoCollection<T> collection, CancellationToken cancellationToken = default(CancellationToken))
			=> collection.Database.IsReplicaSetAsync(cancellationToken);
		#endregion

		#region Create
		/// <summary>
		/// Creates new document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="session"></param>
		/// <param name="object"></param>
		/// <param name="options"></param>
		public static void Create<T>(this IMongoCollection<T> collection, IClientSessionHandle session, T @object, InsertOneOptions options = null) where T : class
		{
			if (@object == null)
				throw new ArgumentNullException(nameof(@object), "The object is null");

			var stopwatch = Stopwatch.StartNew();
			collection.InsertOne(session ?? collection.StartSession(), @object, options);
			stopwatch.Stop();
			if (RepositoryMediator.IsDebugEnabled)
				RepositoryMediator.WriteLogs(new List<string>
				{
					$"NoSQL: Perform CREATE command successful [{typeof(T)}#{@object?.GetEntityID()}] @ {collection.CollectionNamespace.CollectionName}",
					$"{(@object != null ? "Objects' data:\r\n\t" + @object.GetProperties(attribute => !attribute.IsIgnored()).Select(attribute => $"+ @{attribute.Name} ({attribute.Type.GetTypeName(true)}) => [{@object.GetAttributeValue(attribute) ?? "(null)"}]").ToString("\r\n\t") + "\r\n" : "")}Execution times: {stopwatch.GetElapsedTimes()}"
				});
		}

		/// <summary>
		/// Creates new document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="object"></param>
		/// <param name="options"></param>
		public static void Create<T>(this IMongoCollection<T> collection, T @object, InsertOneOptions options = null) where T : class
			=> collection.Create(collection.StartSession(), @object, options);

		/// <summary>
		/// Creates new document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="dataSource">The data source</param>
		/// <param name="object">The object for creating new instance in storage</param>
		/// <param name="options"></param>
		public static void Create<T>(DataSource dataSource, T @object, InsertOneOptions options = null) where T : class
			=> NoSqlHelper.GetCollection<T>(dataSource, RepositoryMediator.GetEntityDefinition<T>()).Create(@object, options);

		/// <summary>
		/// Creates new document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="object">The object for creating new instance in storage</param>
		/// <param name="options"></param>
		public static void Create<T>(T @object, InsertOneOptions options = null) where T : class
			=> NoSqlHelper.Create(RepositoryMediator.GetEntityDefinition<T>().GetPrimaryDataSource(), @object, options);

		/// <summary>
		/// Creates new document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="object">The object for creating new instance in storage</param>
		/// <param name="options"></param>
		public static void Create<T>(this RepositoryContext context, DataSource dataSource, T @object, InsertOneOptions options = null) where T : class
			=> context.GetCollection<T>(dataSource).Create(context.NoSqlSession, @object, options);

		/// <summary>
		/// Creates new document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="session"></param>
		/// <param name="object"></param>
		/// <param name="options"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public static async Task CreateAsync<T>(this IMongoCollection<T> collection, IClientSessionHandle session, T @object, InsertOneOptions options = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			if (@object == null)
				throw new ArgumentNullException(nameof(@object), "The object is null");

			var stopwatch = Stopwatch.StartNew();
			await collection.InsertOneAsync(session ?? await collection.StartSessionAsync(null, cancellationToken).ConfigureAwait(false), @object, options, cancellationToken).ConfigureAwait(false);
			stopwatch.Stop();
			if (RepositoryMediator.IsDebugEnabled)
				RepositoryMediator.WriteLogs(new List<string>
				{
					$"NoSQL: Perform CREATE command successful [{typeof(T)}#{@object?.GetEntityID()}] @ {collection.CollectionNamespace.CollectionName}",
					$"{(@object != null ? "Objects' data:\r\n\t" + @object.GetProperties(attribute => !attribute.IsIgnored()).Select(attribute => $"+ @{attribute.Name} ({attribute.Type.GetTypeName(true)}) => [{@object.GetAttributeValue(attribute) ?? "(null)"}]").ToString("\r\n\t") + "\r\n" : "")}Execution times: {stopwatch.GetElapsedTimes()}"
				});
		}

		/// <summary>
		/// Creates new document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="object"></param>
		/// <param name="options"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public static async Task CreateAsync<T>(this IMongoCollection<T> collection, T @object, InsertOneOptions options = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
			=> await collection.CreateAsync(await collection.StartSessionAsync(null, cancellationToken).ConfigureAwait(false), @object, options, cancellationToken).ConfigureAwait(false);

		/// <summary>
		/// Creates new document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="dataSource">The data source</param>
		/// <param name="object">The object for creating new instance in storage</param>
		/// <param name="options"></param>
		public static Task CreateAsync<T>(DataSource dataSource, T @object, InsertOneOptions options = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
			=> NoSqlHelper.GetCollection<T>(dataSource, RepositoryMediator.GetEntityDefinition<T>()).CreateAsync(@object, options, cancellationToken);

		/// <summary>
		/// Creates new document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="object">The object for creating new instance in storage</param>
		/// <param name="options"></param>
		public static Task CreateAsync<T>(T @object, InsertOneOptions options = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
			=> NoSqlHelper.CreateAsync(RepositoryMediator.GetEntityDefinition<T>().GetPrimaryDataSource(), @object, options, cancellationToken);

		/// <summary>
		/// Creates new document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="object">The object for creating new instance in storage</param>
		/// <param name="options"></param>
		/// <param name="cancellationToken"></param>
		public static Task CreateAsync<T>(this RepositoryContext context, DataSource dataSource, T @object, InsertOneOptions options = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
			=> context.GetCollection<T>(dataSource).CreateAsync(context.NoSqlSession, @object, options, cancellationToken);
		#endregion

		#region Get
		/// <summary>
		/// Gets document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="id"></param>
		/// <param name="options">The options</param>
		/// <returns></returns>
		public static T Get<T>(this IMongoCollection<T> collection, string id, FindOptions options = null) where T : class
			=> !string.IsNullOrWhiteSpace(id)
				? collection.Get(Builders<T>.Filter.Eq("_id", id), null, options)
				: null;

		/// <summary>
		/// Gets document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="id">The string that presents identity</param>
		/// <param name="options">The options</param>
		/// <returns></returns>
		public static T Get<T>(this RepositoryContext context, DataSource dataSource, string id, FindOptions options = null) where T : class
			=> !string.IsNullOrWhiteSpace(id)
				? context.GetCollection<T>(dataSource).Get(id, options)
				: null;

		/// <summary>
		/// Gets document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="id">The string that presents identity of the object that need to get</param>
		/// <param name="options">The options</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<T> GetAsync<T>(this IMongoCollection<T> collection, string id, FindOptions options = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
			=> !string.IsNullOrWhiteSpace(id)
				? collection.GetAsync(Builders<T>.Filter.Eq("_id", id), null, options, cancellationToken)
				: Task.FromResult<T>(null);

		/// <summary>
		/// Gets document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="id">The string that presents identity of the object that need to get</param>
		/// <param name="options">The options</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<T> GetAsync<T>(this RepositoryContext context, DataSource dataSource, string id, FindOptions options = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
			=> context.GetCollection<T>(dataSource).GetAsync(id, options, cancellationToken);
		#endregion

		#region Get (first match)
		/// <summary>
		/// Gets document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="filter">The filter-by expression for filtering</param>
		/// <param name="sort">The order-by expression for ordering</param>
		/// <param name="options">The options</param>
		/// <returns></returns>
		public static T Get<T>(this IMongoCollection<T> collection, FilterDefinition<T> filter, SortDefinition<T> sort = null, FindOptions options = null) where T : class
			=> collection.Find(filter, sort, 1, 1, options).First();

		/// <summary>
		/// Gets document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="filter">The filter-by expression for filtering</param>
		/// <param name="sort">The order-by expression for ordering</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="options">The options</param>
		/// <returns></returns>
		public static T Get<T>(this RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, SortBy<T> sort = null, string businessEntityID = null, FindOptions options = null) where T : class
		{
			var info = RepositoryMediator.GetProperties<T>(businessEntityID);

			var filterBy = filter != null
				? filter is FilterBys<T>
					? (filter as FilterBys<T>).GetNoSqlStatement(info.Item1, info.Item2)
					: (filter as FilterBy<T>).GetNoSqlStatement(info.Item1, info.Item2)
				: null;

			if (!string.IsNullOrWhiteSpace(businessEntityID) && info.Item2 != null)
				filterBy = filterBy == null
					? Builders<T>.Filter.Eq("EntityID", businessEntityID)
					: filterBy & Builders<T>.Filter.Eq("EntityID", businessEntityID);

			var sortBy = sort?.GetNoSqlStatement(null, info.Item1, info.Item2);

			return context.GetCollection<T>(dataSource).Get(filterBy, sortBy, options);
		}

		/// <summary>
		/// Gets document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="filter">The filter-by expression for filtering</param>
		/// <param name="sort">The order-by expression for ordering</param>
		/// <param name="options">The options</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<T> GetAsync<T>(this IMongoCollection<T> collection, FilterDefinition<T> filter, SortDefinition<T> sort = null, FindOptions options = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
			=> (await collection.FindAsync(filter, sort, 1, 1, options, cancellationToken).ConfigureAwait(false)).First();

		/// <summary>
		/// Gets document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="filter">The filter-by expression for filtering</param>
		/// <param name="sort">The order-by expression for ordering</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="options">The options</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<T> GetAsync<T>(this RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, SortBy<T> sort = null, string businessEntityID = null, FindOptions options = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			var info = RepositoryMediator.GetProperties<T>(businessEntityID);

			var filterBy = filter != null
				? filter is FilterBys<T>
					? (filter as FilterBys<T>).GetNoSqlStatement(info.Item1, info.Item2)
					: (filter as FilterBy<T>).GetNoSqlStatement(info.Item1, info.Item2)
				: null;

			if (!string.IsNullOrWhiteSpace(businessEntityID) && info.Item2 != null)
				filterBy = filterBy == null
					? Builders<T>.Filter.Eq("EntityID", businessEntityID)
					: filterBy & Builders<T>.Filter.Eq("EntityID", businessEntityID);

			var sortBy = sort?.GetNoSqlStatement(null, info.Item1, info.Item2);

			return context.GetCollection<T>(dataSource).GetAsync(filterBy, sortBy, options, cancellationToken);
		}
		#endregion

		#region Get (by definition and identity)
		/// <summary>
		/// Gets document of an object
		/// </summary>
		/// <param name="dataSource">The data source</param>
		/// <param name="definition">The definition</param>
		/// <param name="id">The identity</param>
		/// <param name="options">The options</param>
		/// <returns></returns>
		public static object Get(DataSource dataSource, EntityDefinition definition, string id, FindOptions options = null)
		{
			dataSource = dataSource ?? definition.GetPrimaryDataSource();
			var collection = NoSqlHelper.GetCollection<BsonDocument>(RepositoryMediator.GetConnectionString(dataSource), dataSource.DatabaseName, definition.CollectionName, true);
			var document = collection.Get(id, options);
			return document != null
				? BsonSerializer.Deserialize(document, definition.Type)
				: null;
		}

		/// <summary>
		/// Gets document of an object
		/// </summary>
		/// <param name="definition">The definition</param>
		/// <param name="id">The identity</param>
		/// <param name="options">The options</param>
		/// <returns></returns>
		public static object Get(EntityDefinition definition, string id, FindOptions options = null)
			=> NoSqlHelper.Get(definition?.GetPrimaryDataSource(), definition, id);

		/// <summary>
		/// Gets document of an object
		/// </summary>
		/// <param name="dataSource">The data source</param>
		/// <param name="definition">The definition</param>
		/// <param name="id">The identity</param>
		/// <param name="options">The options</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<object> GetAsync(DataSource dataSource, EntityDefinition definition, string id, FindOptions options = null, CancellationToken cancellationToken = default(CancellationToken))
		{
			dataSource = dataSource ?? definition.GetPrimaryDataSource();
			var collection = NoSqlHelper.GetCollection<BsonDocument>(RepositoryMediator.GetConnectionString(dataSource), dataSource.DatabaseName, definition.CollectionName, true);
			var document = await collection.GetAsync(id, options, cancellationToken).ConfigureAwait(false);
			return document != null
				? BsonSerializer.Deserialize(document, definition.Type)
				: null;
		}

		/// <summary>
		/// Gets document of an object
		/// </summary>
		/// <param name="definition">The definition</param>
		/// <param name="id">The identity</param>
		/// <param name="options">The options</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<object> GetAsync(EntityDefinition definition, string id, FindOptions options = null, CancellationToken cancellationToken = default(CancellationToken))
			=> NoSqlHelper.GetAsync(definition?.GetPrimaryDataSource(), definition, id, options, cancellationToken);
		#endregion

		#region Replace
		/// <summary>
		/// Replaces document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="object">The object for updating</param>
		/// <param name="options">The options for updating</param>
		/// <returns></returns>
		public static ReplaceOneResult Replace<T>(this IMongoCollection<T> collection, T @object, UpdateOptions options = null) where T : class
		{
			var stopwatch = Stopwatch.StartNew();
			try
			{
				return @object != null
					? collection.ReplaceOne(Builders<T>.Filter.Eq("_id", @object.GetEntityID()), @object, options ?? new UpdateOptions() { IsUpsert = true })
					: throw new ArgumentNullException(nameof(@object), "The object is null");
			}
			catch (Exception)
			{
				throw;
			}
			finally
			{
				stopwatch.Stop();
				if (RepositoryMediator.IsDebugEnabled)
					RepositoryMediator.WriteLogs(new List<string>()
					{
						$"NoSQL: {(@object != null ? "Perform REPLACE command successful" : "No valid object to perform replace")} [{typeof(T)}#{@object?.GetEntityID()}] @ {collection.CollectionNamespace.CollectionName}",
						$"{(@object != null ? "Objects' data:\r\n\t" + @object.GetProperties(attribute => !attribute.IsIgnored()).Select(attribute => $"+ @{attribute.Name} ({attribute.Type.GetTypeName(true)}) => [{@object.GetAttributeValue(attribute) ?? "(null)"}]").ToString("\r\n\t") + "\r\n" : "")}Execution times: {stopwatch.GetElapsedTimes()}"
					});
			}
		}

		/// <summary>
		/// Replaces document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="object">The object for updating</param>
		/// <param name="options">The options for updating</param>
		/// <returns></returns>
		public static ReplaceOneResult Replace<T>(this RepositoryContext context, DataSource dataSource, T @object, UpdateOptions options = null) where T : class
			=> context.GetCollection<T>(dataSource).Replace(@object, options);

		/// <summary>
		/// Replaces document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="object">The object for updating</param>
		/// <param name="options">The options for updating</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<ReplaceOneResult> ReplaceAsync<T>(this IMongoCollection<T> collection, T @object, UpdateOptions options = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			var stopwatch = Stopwatch.StartNew();
			try
			{
				return @object != null
					? await collection.ReplaceOneAsync(Builders<T>.Filter.Eq("_id", @object.GetEntityID()), @object, options ?? new UpdateOptions() { IsUpsert = true }, cancellationToken).ConfigureAwait(false)
					: throw new ArgumentNullException(nameof(@object), "The object is null");
			}
			catch (Exception)
			{
				throw;
			}
			finally
			{
				stopwatch.Stop();
				if (RepositoryMediator.IsDebugEnabled)
					RepositoryMediator.WriteLogs(new List<string>()
					{
						$"NoSQL: {(@object != null ? "Perform REPLACE command successful" : "No valid object to perform replace")} [{typeof(T)}#{@object?.GetEntityID()}] @ {collection.CollectionNamespace.CollectionName}",
						$"{(@object != null ? "Objects' data:\r\n\t" + @object.GetProperties(attribute => !attribute.IsIgnored()).Select(attribute => $"+ @{attribute.Name} ({attribute.Type.GetTypeName(true)}) => [{@object.GetAttributeValue(attribute) ?? "(null)"}]").ToString("\r\n\t") + "\r\n" : "")}Execution times: {stopwatch.GetElapsedTimes()}"
					});
			}
		}

		/// <summary>
		/// Replaces document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="object">The object for updating</param>
		/// <param name="options">The options for updating</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<ReplaceOneResult> ReplaceAsync<T>(this RepositoryContext context, DataSource dataSource, T @object, UpdateOptions options = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
			=> context.GetCollection<T>(dataSource).ReplaceAsync(@object, options, cancellationToken);
		#endregion

		#region Update
		static BsonArray ToBsonArray(this IEnumerable @object, Type type)
		{
			var values = new List<BsonValue>();
			if (type.IsClassType())
				foreach (var item in @object)
					if (item == null)
						values.Add(BsonNull.Value);
					else
					{
						var document = item.ToBsonDocument();
						document.Remove("_t");
						values.Add(document);
					}
			else
				foreach (var item in @object)
					values.Add(item == null ? BsonNull.Value : BsonValue.Create(item));
			return new BsonArray(values);
		}

		/// <summary>
		/// Updates document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="object">The object for updating</param>
		/// <param name="attributes">The collection of attributes for updating individually</param>
		/// <param name="options">The options for updating</param>
		/// <returns>ReplaceOneResult or UpdateResult object</returns>
		public static void Update<T>(this IMongoCollection<T> collection, T @object, List<string> attributes, UpdateOptions options = null) where T : class
		{
			// check
			if (@object == null)
				throw new ArgumentNullException(nameof(@object), "The object is null");
			else if (attributes == null || attributes.Count < 1)
				throw new ArgumentException("No attribute to update");

			var stopwatch = Stopwatch.StartNew();

			// get collection of all attributes
			var objAttributes = @object.GetAttributes();

			// check generic of primitive (workaround)
			var gotGenericPrimitives = false;
			foreach (var name in attributes)
			{
				var attribute = !string.IsNullOrWhiteSpace(name)
					? objAttributes.FirstOrDefault(a => a.Name.IsEquals(name))
					: null;

				if (attribute != null)
				{
					gotGenericPrimitives = (attribute.Type.IsGenericListOrHashSet() && attribute.Type.GenericTypeArguments[0].IsClassType()) || (attribute.Type.IsArray && attribute.Type.GetElementType().IsClassType());
					if (gotGenericPrimitives)
						break;
				}
			}

			// replace whole document when got a generic of primitive (workaround)
			if (gotGenericPrimitives)
			{
				collection.ReplaceOne(Builders<T>.Filter.Eq("_id", @object.GetEntityID()), @object, options ?? new UpdateOptions() { IsUpsert = true });

				stopwatch.Stop();
				if (RepositoryMediator.IsDebugEnabled)
					RepositoryMediator.WriteLogs(new List<string>()
					{
						$"NoSQL: updated attributes got a generic primitive, then switch to use Replace instead of Update",
						$"- Execution times: {stopwatch.GetElapsedTimes()}",
						$"- Updated attributes: [{attributes.ToString(", ")}]",
						$"- Objects' data for replacing:\r\n\t" + @object.GetProperties(attribute => !attribute.IsIgnored()).Select(attribute => $"+ @{attribute.Name} ({attribute.Type.GetTypeName(true)}) => [{@object.GetAttributeValue(attribute) ?? "(null)"}]").ToString("\r\n\t")
					});

				return;
			}

			// update individually
			UpdateDefinition<T> updater = null;
			var updated = "";

			attributes.ForEach(name =>
			{
				var attribute = !string.IsNullOrWhiteSpace(name)
					? objAttributes.FirstOrDefault(a => a.Name.IsEquals(name))
					: null;

				if (attribute != null)
				{
					var value = @object.GetAttributeValue(attribute);
					var isList = value != null && ((attribute.Type.IsGenericListOrHashSet() && attribute.Type.GenericTypeArguments[0].IsClassType()) || (attribute.Type.IsArray && attribute.Type.GetElementType().IsClassType()));
					var type = isList
						? attribute.Type.IsGenericListOrHashSet()
							? attribute.Type.GenericTypeArguments[0]
							: attribute.Type.GetElementType()
						: null;

					updater = updater == null
						? Builders<T>.Update.Set(attribute.Name, isList ? (value as IEnumerable).ToBsonArray(type) : BsonValue.Create(value))
						: updater.Set(attribute.Name, isList ? (value as IEnumerable).ToBsonArray(type) : BsonValue.Create(value));

					if (RepositoryMediator.IsDebugEnabled)
						updated += $"\r\n\t+ @{attribute.Name} ({attribute.Type}) ==> [{value ?? "(null)"}]";
				}
			});

			if (updater != null)
				collection.UpdateOne(Builders<T>.Filter.Eq("_id", @object.GetEntityID()), updater, options);

			stopwatch.Stop();
			if (RepositoryMediator.IsDebugEnabled)
				RepositoryMediator.WriteLogs(new List<string>()
				{
					$"NoSQL: {(updater != null ? "Perform UPDATE command successful" : "No valid update to perform")} [{typeof(T)}#{@object?.GetEntityID()}] @ {collection.CollectionNamespace.CollectionName}",
					$"{(updater != null ? $"Updated attributes:{updated}\r\n" : "")}Execution times: {stopwatch.GetElapsedTimes()}"
				});
		}

		/// <summary>
		/// Updates document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="object">The object for updating</param>
		/// <param name="attributes">The collection of attributes for updating individually</param>
		/// <param name="options">The options for updating</param>
		/// <returns></returns>
		public static void Update<T>(this RepositoryContext context, DataSource dataSource, T @object, List<string> attributes, UpdateOptions options = null) where T : class
			=> context.GetCollection<T>(dataSource).Update(@object, attributes, options);

		/// <summary>
		/// Updates document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="object">The object for updating</param>
		/// <param name="update">The definition for updating</param>
		/// <param name="options">The options for updating</param>
		public static UpdateResult Update<T>(this IMongoCollection<T> collection, T @object, UpdateDefinition<T> update, UpdateOptions options = null) where T : class
			=> @object != null
				? update != null
					? collection.UpdateOne(Builders<T>.Filter.Eq("_id", @object.GetEntityID()), update, options)
					: throw new ArgumentNullException(nameof(update), "The update definition is null")
				: throw new ArgumentNullException(nameof(@object), "The object is null");

		/// <summary>
		/// Updates document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="object">The object for updating</param>
		/// <param name="update">The definition for updating</param>
		/// <param name="options">The options for updating</param>
		/// <returns></returns>
		public static UpdateResult Update<T>(this RepositoryContext context, DataSource dataSource, T @object, UpdateDefinition<T> update, UpdateOptions options = null) where T : class
			=> context.GetCollection<T>(dataSource).Update(@object, update, options);

		/// <summary>
		/// Updates document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="object">The object for updating</param>
		/// <param name="attributes">The collection of attributes for updating individually</param>
		/// <param name="options">The options for updating</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task UpdateAsync<T>(this IMongoCollection<T> collection, T @object, List<string> attributes, UpdateOptions options = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			if (@object == null)
				throw new ArgumentNullException(nameof(@object), "The object is null");
			else if (attributes == null || attributes.Count < 1)
				throw new ArgumentException("No attribute to update");

			var stopwatch = Stopwatch.StartNew();

			// get collection of all attributes
			var objAttributes = @object.GetAttributes();

			// check generic of primitive (workaround)
			var gotGenericPrimitives = false;
			foreach (var name in attributes)
			{
				var attribute = !string.IsNullOrWhiteSpace(name)
					? objAttributes.FirstOrDefault(a => a.Name.IsEquals(name))
					: null;

				if (attribute != null)
				{
					gotGenericPrimitives = (attribute.Type.IsGenericListOrHashSet() && attribute.Type.GenericTypeArguments[0].IsClassType()) || (attribute.Type.IsArray && attribute.Type.GetElementType().IsClassType());
					if (gotGenericPrimitives)
						break;
				}
			}

			// replace whole document when got a generic of primitive (workaround)
			if (gotGenericPrimitives)
			{
				await collection.ReplaceOneAsync(Builders<T>.Filter.Eq("_id", @object.GetEntityID()), @object, options ?? new UpdateOptions() { IsUpsert = true }, cancellationToken).ConfigureAwait(false);

				stopwatch.Stop();
				if (RepositoryMediator.IsDebugEnabled)
					RepositoryMediator.WriteLogs(new List<string>()
					{
						$"NoSQL: updated attributes got a generic primitive, then switch to use Replace instead of Update",
						$"- Execution times: {stopwatch.GetElapsedTimes()}",
						$"- Updated attributes: [{attributes.ToString(", ")}]",
						$"- Objects' data for replacing:\r\n\t" + @object.GetProperties(attribute => !attribute.IsIgnored()).Select(attribute => $"+ @{attribute.Name} ({attribute.Type.GetTypeName(true)}) => [{@object.GetAttributeValue(attribute) ?? "(null)"}]").ToString("\r\n\t")
					});

				return;
			}

			// update individually
			UpdateDefinition<T> updater = null;
			var updated = "";

			attributes.ForEach(name =>
			{
				var attribute = !string.IsNullOrWhiteSpace(name)
					? objAttributes.FirstOrDefault(a => a.Name.IsEquals(name))
					: null;

				if (attribute != null)
				{
					var value = @object.GetAttributeValue(attribute);
					var isList = value != null && ((attribute.Type.IsGenericListOrHashSet() && attribute.Type.GenericTypeArguments[0].IsClassType()) || (attribute.Type.IsArray && attribute.Type.GetElementType().IsClassType()));
					var type = isList
						? attribute.Type.IsGenericListOrHashSet()
							? attribute.Type.GenericTypeArguments[0]
							: attribute.Type.GetElementType()
						: null;

					updater = updater == null
						? Builders<T>.Update.Set(attribute.Name, isList ? (value as IEnumerable).ToBsonArray(type) : BsonValue.Create(value))
						: updater.Set(attribute.Name, isList ? (value as IEnumerable).ToBsonArray(type) : BsonValue.Create(value));

					if (RepositoryMediator.IsDebugEnabled)
						updated += $"\r\n\t+ @{attribute.Name} ({attribute.Type}) ==> [{value ?? "(null)"}]";
				}
			});

			if (updater != null)
				await collection.UpdateOneAsync(Builders<T>.Filter.Eq("_id", @object.GetEntityID()), updater, options, cancellationToken).ConfigureAwait(false);

			stopwatch.Stop();
			if (RepositoryMediator.IsDebugEnabled)
				RepositoryMediator.WriteLogs(new List<string>()
				{
					$"NoSQL: {(updater != null ? "Perform UPDATE command successful" : "No valid update to perform")} [{typeof(T)}#{@object?.GetEntityID()}] @ {collection.CollectionNamespace.CollectionName}",
					$"{(updater != null ? $"Updated attributes:{updated}\r\n" : "")}Execution times: {stopwatch.GetElapsedTimes()}"
				});
		}

		/// <summary>
		/// Updates document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="object">The object for updating</param>
		/// <param name="attributes">The collection of attributes for updating individually</param>
		/// <param name="options">The options for updating</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task UpdateAsync<T>(this RepositoryContext context, DataSource dataSource, T @object, List<string> attributes, UpdateOptions options = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
			=> context.GetCollection<T>(dataSource).UpdateAsync(@object, attributes, options, cancellationToken);

		/// <summary>
		/// Updates document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="object">The object for updating</param>
		/// <param name="update">The definition for updating</param>
		/// <param name="options">The options for updating</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<UpdateResult> UpdateAsync<T>(this IMongoCollection<T> collection, T @object, UpdateDefinition<T> update, UpdateOptions options = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
			=> @object != null
				? update != null
					? collection.UpdateOneAsync(Builders<T>.Filter.Eq("_id", @object.GetEntityID()), update, options, cancellationToken)
					: Task.FromException<UpdateResult>(new ArgumentNullException(nameof(update), "The update definition is null"))
				: Task.FromException<UpdateResult>(new ArgumentNullException(nameof(@object), "The object is null"));

		/// <summary>
		/// Updates document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="object">The object for updating</param>
		/// <param name="update">The definition for updating</param>
		/// <param name="options">The options for updating</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<UpdateResult> UpdateAsync<T>(this RepositoryContext context, DataSource dataSource, T @object, UpdateDefinition<T> update, UpdateOptions options = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
			=> context.GetCollection<T>(dataSource).UpdateAsync(@object, update, options, cancellationToken);
		#endregion

		#region Delete
		/// <summary>
		/// Deletes the document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="id"></param>
		/// <param name="options"></param>
		/// <returns></returns>
		public static DeleteResult Delete<T>(this IMongoCollection<T> collection, string id, DeleteOptions options = null) where T : class
			=> !string.IsNullOrWhiteSpace(id)
				? collection.DeleteOne(Builders<T>.Filter.Eq("_id", id), options)
				: null;

		/// <summary>
		/// Deletes the document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="id">The identity of the document of an object for deleting</param>
		/// <returns></returns>
		public static DeleteResult Delete<T>(this RepositoryContext context, DataSource dataSource, string id, DeleteOptions options = null) where T : class
			=> context.GetCollection<T>(dataSource).Delete(id, options);

		/// <summary>
		/// Deletes the document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="object"></param>
		/// <param name="options"></param>
		/// <returns></returns>
		public static DeleteResult Delete<T>(this IMongoCollection<T> collection, T @object, DeleteOptions options = null) where T : class
			=> @object != null
				? collection.Delete(@object.GetEntityID(), options)
				: throw new ArgumentNullException(nameof(@object), "The object is null");

		/// <summary>
		/// Deletes the document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="object"></param>
		/// <returns></returns>
		public static DeleteResult Delete<T>(this RepositoryContext context, DataSource dataSource, T @object, DeleteOptions options = null) where T : class
			=> context.GetCollection<T>(dataSource).Delete(@object, options);

		/// <summary>
		/// Deletes the document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="id"></param>
		/// <param name="options"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public static Task<DeleteResult> DeleteAsync<T>(this IMongoCollection<T> collection, string id, DeleteOptions options = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
			=> !string.IsNullOrWhiteSpace(id)
				? collection.DeleteOneAsync(Builders<T>.Filter.Eq("_id", id), options, cancellationToken)
				: Task.FromResult<DeleteResult>(null);

		/// <summary>
		/// Deletes the document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="id">The identity of the document of an object for deleting</param>
		/// <returns></returns>
		public static Task<DeleteResult> DeleteAsync<T>(this RepositoryContext context, DataSource dataSource, string id, DeleteOptions options = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
			=> context.GetCollection<T>(dataSource).DeleteAsync(id, options, cancellationToken);

		/// <summary>
		/// Deletes the document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="object"></param>
		/// <param name="options"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public static Task<DeleteResult> DeleteAsync<T>(this IMongoCollection<T> collection, T @object, DeleteOptions options = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
			=> @object != null
				? collection.DeleteAsync(@object.GetEntityID(), options, cancellationToken)
				: Task.FromException<DeleteResult>(new ArgumentNullException(nameof(@object), "The object is null"));

		/// <summary>
		/// Deletes the document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="object"></param>
		/// <returns></returns>
		public static Task<DeleteResult> DeleteAsync<T>(this RepositoryContext context, DataSource dataSource, T @object, DeleteOptions options = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
			=> context.GetCollection<T>(dataSource).DeleteAsync(@object, options, cancellationToken);
		#endregion

		#region Delete (many)
		/// <summary>
		/// Deletes document of multiple objects
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="filter">The filter for deleting</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="options">The options for deleting</param>
		/// <returns></returns>
		public static DeleteResult DeleteMany<T>(this RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, string businessEntityID = null, DeleteOptions options = null) where T : class
		{
			var info = RepositoryMediator.GetProperties<T>(businessEntityID);

			var filterBy = filter != null
				? filter is FilterBys<T>
					? (filter as FilterBys<T>).GetNoSqlStatement(info.Item1, info.Item2)
					: (filter as FilterBy<T>).GetNoSqlStatement(info.Item1, info.Item2)
				: null;

			if (!string.IsNullOrWhiteSpace(businessEntityID) && info.Item2 != null)
				filterBy = filterBy == null
					? Builders<T>.Filter.Eq("EntityID", businessEntityID)
					: filterBy & Builders<T>.Filter.Eq("EntityID", businessEntityID);

			return context.GetCollection<T>(dataSource).DeleteMany(filterBy != null ? filterBy : Builders<T>.Filter.Empty, options);
		}

		/// <summary>
		/// Deletes document of multiple objects
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="filter">The filter for deleting</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="options">The options for deleting</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<DeleteResult> DeleteManyAsync<T>(this RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, string businessEntityID = null, DeleteOptions options = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			var info = RepositoryMediator.GetProperties<T>(businessEntityID);

			var filterBy = filter != null
				? filter is FilterBys<T>
					? (filter as FilterBys<T>).GetNoSqlStatement(info.Item1, info.Item2)
					: (filter as FilterBy<T>).GetNoSqlStatement(info.Item1, info.Item2)
				: null;

			if (!string.IsNullOrWhiteSpace(businessEntityID) && info.Item2 != null)
				filterBy = filterBy == null
					? Builders<T>.Filter.Eq("EntityID", businessEntityID)
					: filterBy & Builders<T>.Filter.Eq("EntityID", businessEntityID);

			return context.GetCollection<T>(dataSource).DeleteManyAsync(filterBy != null ? filterBy : Builders<T>.Filter.Empty, options, cancellationToken);
		}
		#endregion

		#region Select
		/// <summary>
		/// Finds all the matched documents and return the collection of <see cref="BsonDocument">BsonDocument</see> objects with limited attributes
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="attributes">The collection of attributes to included in the results (set to null to include identity attribute only)</param>
		/// <param name="filter">The filter-by expression for filtering</param>
		/// <param name="sort">The order-by expression for ordering</param>
		/// <param name="pageSize">The size of one page</param>
		/// <param name="pageNumber">The number of page</param>
		/// <param name="options">The options</param>
		/// <returns></returns>
		public static List<BsonDocument> Select<T>(this IMongoCollection<T> collection, IEnumerable<string> attributes, FilterDefinition<T> filter, SortDefinition<T> sort, int pageSize, int pageNumber, FindOptions options = null) where T : class
		{
			ProjectionDefinition<T> projection = null;
			if (attributes == null)
				projection = Builders<T>.Projection.Include("_id");
			else
				attributes.ForEach(attribute => projection = projection == null ? Builders<T>.Projection.Include(attribute) : projection.Include(attribute));

			var results = collection
				.Find(filter ?? Builders<T>.Filter.Empty, options)
				.Sort(sort ?? Builders<T>.Sort.Ascending("_id"));

			if (pageSize > 0)
			{
				if (pageNumber > 1)
					results = results.Skip((pageNumber - 1) * pageSize);
				results = results.Limit(pageSize);
			}

			return results.Project(projection).ToList();
		}

		/// <summary>
		/// Finds all the matched documents and return the collection of <see cref="BsonDocument">BsonDocument</see> objects with limited attributes
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="attributes">The collection of attributes to included in the results (set to null to include identity attribute only)</param>
		/// <param name="filter">The filter-by expression for filtering</param>
		/// <param name="sort">The order-by expression for ordering</param>
		/// <param name="pageSize">The size of one page</param>
		/// <param name="pageNumber">The number of page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="options">The options</param>
		/// <returns></returns>
		public static List<BsonDocument> Select<T>(this RepositoryContext context, DataSource dataSource, IEnumerable<string> attributes, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessEntityID = null, bool autoAssociateWithMultipleParents = true, FindOptions options = null) where T : class
		{
			var info = Extensions.PrepareNoSqlStatements<T>(filter, sort, businessEntityID, autoAssociateWithMultipleParents);
			return context.GetCollection<T>(dataSource).Select(attributes, info.Item1, info.Item2, pageSize, pageNumber, options);
		}

		/// <summary>
		/// Finds all the matched documents and return the collection of <see cref="BsonDocument">BsonDocument</see> objects with limited attributes
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="attributes">The collection of attributes to included in the results (set to null to include identity attribute only)</param>
		/// <param name="filter">The filter-by expression for filtering</param>
		/// <param name="sort">The order-by expression for ordering</param>
		/// <param name="pageSize">The size of one page</param>
		/// <param name="pageNumber">The number of page</param>
		/// <param name="options">The options</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<List<BsonDocument>> SelectAsync<T>(this IMongoCollection<T> collection, IEnumerable<string> attributes, FilterDefinition<T> filter, SortDefinition<T> sort, int pageSize, int pageNumber, FindOptions options = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			ProjectionDefinition<T> projection = null;
			if (attributes == null)
				projection = Builders<T>.Projection.Include("_id");
			else
				attributes.ForEach(attribute => projection = projection == null ? Builders<T>.Projection.Include(attribute) : projection.Include(attribute));

			var results = collection
				.Find(filter ?? Builders<T>.Filter.Empty, options)
				.Sort(sort ?? Builders<T>.Sort.Ascending("_id"));

			if (pageSize > 0)
			{
				if (pageNumber > 1)
					results = results.Skip((pageNumber - 1) * pageSize);
				results = results.Limit(pageSize);
			}

			return results.Project(projection).ToListAsync(cancellationToken);
		}

		/// <summary>
		/// Finds all the matched documents and return the collection of <see cref="BsonDocument">BsonDocument</see> objects with limited attributes
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="attributes">The collection of attributes to included in the results (set to null to include identity attribute only)</param>
		/// <param name="filter">The filter-by expression for filtering</param>
		/// <param name="sort">The order-by expression for ordering</param>
		/// <param name="pageSize">The size of one page</param>
		/// <param name="pageNumber">The number of page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="options">The options</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<List<BsonDocument>> SelectAsync<T>(this RepositoryContext context, DataSource dataSource, IEnumerable<string> attributes, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessEntityID = null, bool autoAssociateWithMultipleParents = true, FindOptions options = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			var info = Extensions.PrepareNoSqlStatements<T>(filter, sort, businessEntityID, autoAssociateWithMultipleParents);
			return context.GetCollection<T>(dataSource).SelectAsync(attributes, info.Item1, info.Item2, pageSize, pageNumber, options, cancellationToken);
		}
		#endregion

		#region Select (identities)
		/// <summary>
		/// Finds all the matched documents and return the collection of identity attributes
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="filter">The filter-by expression for filtering</param>
		/// <param name="sort">The order-by expression for ordering</param>
		/// <param name="pageSize">The size of one page</param>
		/// <param name="pageNumber">The number of page</param>
		/// <param name="options">The options</param>
		/// <returns></returns>
		public static List<string> SelectIdentities<T>(this IMongoCollection<T> collection, FilterDefinition<T> filter, SortDefinition<T> sort, int pageSize, int pageNumber, FindOptions options = null) where T : class
			=> collection.Select(null, filter, sort, pageSize, pageNumber, options)
				.Select(doc => doc["_id"].AsString)
				.ToList();

		/// <summary>
		/// Finds all the matched documents and return the collection of identity attributes
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="filter">The filter-by expression for filtering</param>
		/// <param name="sort">The order-by expression for ordering</param>
		/// <param name="pageSize">The size of one page</param>
		/// <param name="pageNumber">The number of page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="options">The options</param>
		/// <returns></returns>
		public static List<string> SelectIdentities<T>(this RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessEntityID = null, bool autoAssociateWithMultipleParents = true, FindOptions options = null) where T : class
		{
			var info = Extensions.PrepareNoSqlStatements(filter, sort, businessEntityID, autoAssociateWithMultipleParents);
			return context.GetCollection<T>(dataSource).SelectIdentities(info.Item1, info.Item2, pageSize, pageNumber, options);
		}

		/// <summary>
		/// Finds all the matched documents and return the collection of identity attributes
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="filter">The filter-by expression for filtering</param>
		/// <param name="sort">The order-by expression for ordering</param>
		/// <param name="pageSize">The size of one page</param>
		/// <param name="pageNumber">The number of page</param>
		/// <param name="options">The options</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<List<string>> SelectIdentitiesAsync<T>(this IMongoCollection<T> collection, FilterDefinition<T> filter, SortDefinition<T> sort, int pageSize, int pageNumber, FindOptions options = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
			=> (await collection.SelectAsync(null, filter, sort, pageSize, pageNumber, options, cancellationToken).ConfigureAwait(false))
				.Select(doc => doc["_id"].AsString)
				.ToList();

		/// <summary>
		/// Finds all the matched documents and return the collection of identity attributes
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="filter">The filter-by expression for filtering</param>
		/// <param name="sort">The order-by expression for ordering</param>
		/// <param name="pageSize">The size of one page</param>
		/// <param name="pageNumber">The number of page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="options">The options</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<List<string>> SelectIdentitiesAsync<T>(this RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessEntityID = null, bool autoAssociateWithMultipleParents = true, FindOptions options = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			var info = Extensions.PrepareNoSqlStatements(filter, sort, businessEntityID, autoAssociateWithMultipleParents);
			return context.GetCollection<T>(dataSource).SelectIdentitiesAsync(info.Item1, info.Item2, pageSize, pageNumber, options, cancellationToken);
		}
		#endregion

		#region Find
		/// <summary>
		/// Finds all the matched documents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="session"></param>
		/// <param name="filter">The filter-by expression for filtering</param>
		/// <param name="sort">The order-by expression for ordering</param>
		/// <param name="pageSize">The size of one page</param>
		/// <param name="pageNumber">The number of page</param>
		/// <param name="options">The options</param>
		/// <returns></returns>
		public static List<T> Find<T>(this IMongoCollection<T> collection, IClientSessionHandle session, FilterDefinition<T> filter, SortDefinition<T> sort, int pageSize, int pageNumber, FindOptions options = null) where T : class
		{
			var results = collection.Find(session ?? collection.StartSession(), filter ?? Builders<T>.Filter.Empty, options).Sort(sort ?? Builders<T>.Sort.Ascending("_id"));

			if (pageSize > 0)
			{
				if (pageNumber > 1)
					results = results.Skip((pageNumber - 1) * pageSize);
				results = results.Limit(pageSize);
			}

			return results.ToList();
		}

		/// <summary>
		/// Finds all the matched documents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="filter">The filter-by expression for filtering</param>
		/// <param name="sort">The order-by expression for ordering</param>
		/// <param name="pageSize">The size of one page</param>
		/// <param name="pageNumber">The number of page</param>
		/// <param name="options">The options</param>
		/// <returns></returns>
		public static List<T> Find<T>(this IMongoCollection<T> collection, FilterDefinition<T> filter, SortDefinition<T> sort, int pageSize, int pageNumber, FindOptions options = null) where T : class
			=> collection.Find(collection.StartSession(), filter, sort, pageSize, pageNumber, options);

		/// <summary>
		/// Finds all the matched documents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="filter">The filter-by expression for filtering</param>
		/// <param name="sort">The order-by expression for ordering</param>
		/// <param name="pageSize">The size of one page</param>
		/// <param name="pageNumber">The number of page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="options">The options</param>
		/// <returns></returns>
		public static List<T> Find<T>(this RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessEntityID = null, bool autoAssociateWithMultipleParents = true, FindOptions options = null) where T : class
		{
			var info = Extensions.PrepareNoSqlStatements(filter, sort, businessEntityID, autoAssociateWithMultipleParents);
			return context.GetCollection<T>(dataSource).Find(context.NoSqlSession, info.Item1, info.Item2, pageSize, pageNumber, options);
		}

		/// <summary>
		/// Finds all the matched documents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="session"></param>
		/// <param name="filter">The filter-by expression for filtering</param>
		/// <param name="sort">The order-by expression for ordering</param>
		/// <param name="pageSize">The size of one page</param>
		/// <param name="pageNumber">The number of page</param>
		/// <param name="options">The options</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<List<T>> FindAsync<T>(this IMongoCollection<T> collection, IClientSessionHandle session, FilterDefinition<T> filter, SortDefinition<T> sort, int pageSize, int pageNumber, FindOptions options = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			var results = collection.Find(session ?? collection.StartSession(), filter ?? Builders<T>.Filter.Empty, options).Sort(sort ?? Builders<T>.Sort.Ascending("_id"));

			if (pageSize > 0)
			{
				if (pageNumber > 1)
					results = results.Skip((pageNumber - 1) * pageSize);
				results = results.Limit(pageSize);
			}

			return results.ToListAsync(cancellationToken);
		}

		/// <summary>
		/// Finds all the matched documents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="filter">The filter-by expression for filtering</param>
		/// <param name="sort">The order-by expression for ordering</param>
		/// <param name="pageSize">The size of one page</param>
		/// <param name="pageNumber">The number of page</param>
		/// <param name="options">The options</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<List<T>> FindAsync<T>(this IMongoCollection<T> collection, FilterDefinition<T> filter, SortDefinition<T> sort, int pageSize, int pageNumber, FindOptions options = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
			=> await collection.FindAsync(await collection.StartSessionAsync(cancellationToken).ConfigureAwait(false), filter, sort, pageSize, pageNumber, options, cancellationToken).ConfigureAwait(false);

		/// <summary>
		/// Finds all the matched documents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="filter">The filter-by expression for filtering</param>
		/// <param name="sort">The order-by expression for ordering</param>
		/// <param name="pageSize">The size of one page</param>
		/// <param name="pageNumber">The number of page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="options">The options</param>
		/// <param name="cancellationToken">The options</param>
		/// <returns></returns>
		public static Task<List<T>> FindAsync<T>(this RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessEntityID = null, bool autoAssociateWithMultipleParents = true, FindOptions options = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			var info = Extensions.PrepareNoSqlStatements(filter, sort, businessEntityID, autoAssociateWithMultipleParents);
			return context.GetCollection<T>(dataSource).FindAsync(context.NoSqlSession, info.Item1, info.Item2, pageSize, pageNumber, options, cancellationToken);
		}
		#endregion

		#region Find (by identities)
		/// <summary>
		/// Finds all the documents that specified by identity
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="identities">The collection of identities for finding</param>
		/// <param name="sort">The order-by expression for ordering</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="options">The options</param>
		/// <returns></returns>
		public static List<T> Find<T>(this RepositoryContext context, DataSource dataSource, List<string> identities, SortBy<T> sort = null, string businessEntityID = null, FindOptions options = null) where T : class
		{
			if (identities == null || identities.Count < 1)
				return new List<T>();
			var info = Extensions.PrepareNoSqlStatements(Filters<T>.Or(identities.Select(id => Filters<T>.Equals("ID", id))), sort, businessEntityID, false);
			return context.GetCollection<T>(dataSource).Find(context.NoSqlSession, info.Item1, info.Item2, 0, 1, options);
		}

		/// <summary>
		/// Finds all the documents that specified by identity
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="identities">The collection of identities for finding</param>
		/// <param name="sort">The order-by expression for ordering</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="options">The options</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<List<T>> FindAsync<T>(this RepositoryContext context, DataSource dataSource, List<string> identities, SortBy<T> sort = null, string businessEntityID = null, FindOptions options = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			if (identities == null || identities.Count < 1)
				return Task.FromResult(new List<T>());
			var info = Extensions.PrepareNoSqlStatements(Filters<T>.Or(identities.Select(id => Filters<T>.Equals("ID", id))), sort, businessEntityID, false);
			return context.GetCollection<T>(dataSource).FindAsync(context.NoSqlSession, info.Item1, info.Item2, 0, 1, options, cancellationToken);
		}
		#endregion

		#region Count
		/// <summary>
		/// Counts the number of all the matched documents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source for counting</param>
		/// <param name="filter">The filter-by expression for counting</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="options">The options</param>
		/// <returns></returns>
		public static long Count<T>(this RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, string businessEntityID = null, bool autoAssociateWithMultipleParents = true, CountOptions options = null) where T : class
		{
			var info = Extensions.PrepareNoSqlStatements(filter, null, businessEntityID, autoAssociateWithMultipleParents);
			var collection = context.GetCollection<T>(dataSource);
			return collection.CountDocuments(context.NoSqlSession ?? collection.StartSession(), info.Item1 ?? Builders<T>.Filter.Empty, options);
		}

		/// <summary>
		/// Counts the number of all the matched documents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source for counting</param>
		/// <param name="filter">The filter-by expression for counting</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="options">The options</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<long> CountAsync<T>(this RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, string businessEntityID = null, bool autoAssociateWithMultipleParents = true, CountOptions options = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			var info = Extensions.PrepareNoSqlStatements(filter, null, businessEntityID, autoAssociateWithMultipleParents);
			var collection = context.GetCollection<T>(dataSource);
			return await collection.CountDocumentsAsync(context.NoSqlSession ?? await collection.StartSessionAsync(null, cancellationToken).ConfigureAwait(false), info.Item1 ?? Builders<T>.Filter.Empty, options, cancellationToken).ConfigureAwait(false);
		}
		#endregion

		#region Search
		/// <summary>
		/// Creates a text-search filter
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="query">The expression for searching documents</param>
		/// <returns></returns>
		public static FilterDefinition<T> CreateSearchFilter<T>(this string query) where T : class
		{
			var searchQuery = new SearchQuery(query);

			var searchBy = "";
			searchQuery.AndWords.ForEach(word => searchBy += (!searchBy.Equals("") ? " " : "") + word);
			searchQuery.OrWords.ForEach(word => searchBy += (!searchBy.Equals("") ? " " : "") + word);
			searchQuery.NotWords.ForEach(word => searchBy += (!searchBy.Equals("") ? " " : "") + "-" + word);
			searchQuery.AndPhrases.ForEach(phrase => searchBy += (!searchBy.Equals("") ? " " : "") + "\"" + phrase + "\"");
			searchQuery.OrPhrases.ForEach(phrase => searchBy += (!searchBy.Equals("") ? " " : "") + "\"" + phrase + "\"");
			searchQuery.NotPhrases.ForEach(phrase => searchBy += (!searchBy.Equals("") ? " " : "") + "-" + "\"" + phrase + "\"");

			return Builders<T>.Filter.Text(searchBy, new TextSearchOptions { CaseSensitive = false });
		}

		static FilterDefinition<T> CreateSearchFilter<T>(this RepositoryContext context, IFilterBy<T> filter, string businessEntityID = null) where T : class
		{
			var info = RepositoryMediator.GetProperties<T>(businessEntityID);

			var filterBy = filter != null
				? filter is FilterBys<T>
					? (filter as FilterBys<T>).GetNoSqlStatement(standardProperties: info.Item1, extendedProperties: info.Item2)
					: (filter as FilterBy<T>).GetNoSqlStatement(standardProperties: info.Item1, extendedProperties: info.Item2)
				: null;

			if (!string.IsNullOrWhiteSpace(businessEntityID) && info.Item2 != null)
				filterBy = filterBy == null
					? Builders<T>.Filter.Eq("EntityID", businessEntityID)
					: filterBy & Builders<T>.Filter.Eq("EntityID", businessEntityID);

			return filterBy;
		}

		static IFindFluent<T, T> CreateFindFluent<T>(this IMongoCollection<T> collection, IClientSessionHandle session, string scoreProperty, string query, FilterDefinition<T> filter, int pageSize, int pageNumber) where T : class
		{
			var filterBy = query.CreateSearchFilter<T>();
			if (filter != null && !filter.Equals(Builders<T>.Filter.Empty))
				filterBy = filterBy & filter;

			var fluent = collection.Find(session, filterBy).Sort(Builders<T>.Sort.MetaTextScore(scoreProperty ?? "SearchScore"));

			if (pageSize > 0)
			{
				if (pageNumber > 1)
					fluent = fluent.Skip((pageNumber - 1) * pageSize);
				fluent = fluent.Limit(pageSize);
			}
			return fluent;
		}

		static IFindFluent<T, T> CreateSearchFluent<T>(this IMongoCollection<T> collection, IClientSessionHandle session, string scoreProperty, string query, FilterDefinition<T> filter, int pageSize, int pageNumber) where T : class
			=> collection.CreateFindFluent(session, scoreProperty, query, filter, pageSize, pageNumber).Project<T>(Builders<T>.Projection.MetaTextScore(scoreProperty ?? "SearchScore"));

		/// <summary>
		/// Searchs all the matched documents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="session"></param>
		/// <param name="scoreProperty"></param>
		/// <param name="query"></param>
		/// <param name="filter"></param>
		/// <param name="pageSize"></param>
		/// <param name="pageNumber"></param>
		/// <returns></returns>
		public static List<T> Search<T>(this IMongoCollection<T> collection, IClientSessionHandle session, string scoreProperty, string query, FilterDefinition<T> filter, int pageSize, int pageNumber) where T : class
			=> collection.CreateSearchFluent(session ?? collection.StartSession(), scoreProperty, query, filter, pageSize, pageNumber).ToList();

		/// <summary>
		/// Searchs all the matched documents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="scoreProperty"></param>
		/// <param name="query"></param>
		/// <param name="filter"></param>
		/// <param name="pageSize"></param>
		/// <param name="pageNumber"></param>
		/// <returns></returns>
		public static List<T> Search<T>(this IMongoCollection<T> collection, string scoreProperty, string query, FilterDefinition<T> filter, int pageSize, int pageNumber) where T : class
			=> collection.Search(collection.StartSession(), scoreProperty, query, filter, pageSize, pageNumber);

		/// <summary>
		/// Searchs all the matched documents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="query"></param>
		/// <param name="filter"></param>
		/// <param name="pageSize"></param>
		/// <param name="pageNumber"></param>
		/// <returns></returns>
		public static List<T> Search<T>(this IMongoCollection<T> collection, string query, FilterDefinition<T> filter, int pageSize, int pageNumber) where T : class
			=> collection.Search("SearchScore", query, filter, pageSize, pageNumber);

		/// <summary>
		/// Searchs all the matched documents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source for searching</param>
		/// <param name="query">The text query for searching</param>
		/// <param name="filter">The additional filter</param>
		/// <param name="pageSize">The size of one page</param>
		/// <param name="pageNumber">The number of the page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="options">The options</param>
		/// <returns></returns>
		public static List<T> Search<T>(this RepositoryContext context, DataSource dataSource, string query, IFilterBy<T> filter, int pageSize, int pageNumber, string businessEntityID = null, FindOptions options = null) where T : class
			=> context.GetCollection<T>(dataSource).Search(context.NoSqlSession, "SearchScore", query, context.CreateSearchFilter(filter, businessEntityID), pageSize, pageNumber);

		/// <summary>
		/// Searchs all the matched documents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="scoreProperty"></param>
		/// <param name="query"></param>
		/// <param name="filter"></param>
		/// <param name="pageSize"></param>
		/// <param name="pageNumber"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public static async Task<List<T>> SearchAsync<T>(this IMongoCollection<T> collection, IClientSessionHandle session, string scoreProperty, string query, FilterDefinition<T> filter, int pageSize, int pageNumber, CancellationToken cancellationToken = default(CancellationToken)) where T : class
			=> await collection.CreateSearchFluent(session ?? await collection.StartSessionAsync(cancellationToken).ConfigureAwait(false), scoreProperty, query, filter, pageSize, pageNumber).ToListAsync(cancellationToken).ConfigureAwait(false);

		/// <summary>
		/// Searchs all the matched documents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="scoreProperty"></param>
		/// <param name="query"></param>
		/// <param name="filter"></param>
		/// <param name="pageSize"></param>
		/// <param name="pageNumber"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public static async Task<List<T>> SearchAsync<T>(this IMongoCollection<T> collection, string scoreProperty, string query, FilterDefinition<T> filter, int pageSize, int pageNumber, CancellationToken cancellationToken = default(CancellationToken)) where T : class
			=> await collection.SearchAsync(await collection.StartSessionAsync(cancellationToken).ConfigureAwait(false), scoreProperty, query, filter, pageSize, pageNumber, cancellationToken).ConfigureAwait(false);

		/// <summary>
		/// Searchs all the matched documents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="scoreProperty"></param>
		/// <param name="query"></param>
		/// <param name="pageSize"></param>
		/// <param name="pageNumber"></param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public static Task<List<T>> SearchAsync<T>(this IMongoCollection<T> collection, string scoreProperty, string query, int pageSize, int pageNumber, string businessEntityID = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
			=> collection.SearchAsync(scoreProperty, query, null, pageSize, pageNumber, cancellationToken);

		/// <summary>
		/// Searchs all the matched documents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="query"></param>
		/// <param name="filter"></param>
		/// <param name="pageSize"></param>
		/// <param name="pageNumber"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public static Task<List<T>> SearchAsync<T>(this IMongoCollection<T> collection, string query, FilterDefinition<T> filter, int pageSize, int pageNumber, CancellationToken cancellationToken = default(CancellationToken)) where T : class
			=> collection.SearchAsync("SearchScore", query, filter, pageSize, pageNumber, cancellationToken);

		/// <summary>
		/// Searchs all the matched documents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source for searching</param>
		/// <param name="query">The text query for searching</param>
		/// <param name="filter">The additional filter</param>
		/// <param name="pageSize">The size of one page</param>
		/// <param name="pageNumber">The number of the page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="options">The options</param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public static Task<List<T>> SearchAsync<T>(this RepositoryContext context, DataSource dataSource, string query, IFilterBy<T> filter, int pageSize, int pageNumber, string businessEntityID = null, FindOptions options = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
			=> context.GetCollection<T>(dataSource).SearchAsync(context.NoSqlSession, "SearchScore", query, context.CreateSearchFilter(filter, businessEntityID), pageSize, pageNumber, cancellationToken);
		#endregion

		#region Search (identities)
		/// <summary>
		/// Searchs all the matched documents and return the collection of identities
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="session"></param>
		/// <param name="scoreProperty"></param>
		/// <param name="query"></param>
		/// <param name="filter"></param>
		/// <param name="pageSize"></param>
		/// <param name="pageNumber"></param>
		/// <returns></returns>
		public static List<string> SearchIdentities<T>(this IMongoCollection<T> collection, IClientSessionHandle session, string scoreProperty, string query, FilterDefinition<T> filter, int pageSize, int pageNumber) where T : class
			=> collection.CreateFindFluent(session ?? collection.StartSession(), scoreProperty, query, filter, pageSize, pageNumber).Project(Builders<T>.Projection.MetaTextScore(scoreProperty ?? "SearchScore")).ToList().Select(doc => doc["_id"].AsString).ToList();

		/// <summary>
		/// Searchs all the matched documents and return the collection of identities
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="query"></param>
		/// <param name="filter"></param>
		/// <param name="pageSize"></param>
		/// <param name="pageNumber"></param>
		/// <returns></returns>
		public static List<string> SearchIdentities<T>(this IMongoCollection<T> collection, string query, FilterDefinition<T> filter, int pageSize, int pageNumber) where T : class
			=> collection.SearchIdentities(collection.StartSession(), "SearchScore", query, filter, pageSize, pageNumber);

		/// <summary>
		/// Searchs all the matched documents and return the collection of identities
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source for searching</param>
		/// <param name="query">The text query for searching</param>
		/// <param name="filter">The additional filter</param>
		/// <param name="pageSize">The size of one page</param>
		/// <param name="pageNumber">The number of the page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="options">The options</param>
		/// <returns></returns>
		public static List<string> SearchIdentities<T>(this RepositoryContext context, DataSource dataSource, string query, IFilterBy<T> filter, int pageSize, int pageNumber, string businessEntityID = null, FindOptions options = null) where T : class
			=> context.GetCollection<T>(dataSource).SearchIdentities(context.NoSqlSession, "SearchScore", query, context.CreateSearchFilter(filter, businessEntityID), pageSize, pageNumber);

		/// <summary>
		/// Searchs all the matched documents and return the collection of identities
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="session"></param>
		/// <param name="scoreProperty"></param>
		/// <param name="query"></param>
		/// <param name="filter"></param>
		/// <param name="pageSize"></param>
		/// <param name="pageNumber"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public static async Task<List<string>> SearchIdentitiesAsync<T>(this IMongoCollection<T> collection, IClientSessionHandle session, string scoreProperty, string query, FilterDefinition<T> filter, int pageSize, int pageNumber, CancellationToken cancellationToken = default(CancellationToken)) where T : class
			=> (await collection.CreateFindFluent(session ?? await collection.StartSessionAsync(cancellationToken).ConfigureAwait(false), scoreProperty, query, filter, pageSize, pageNumber).Project(Builders<T>.Projection.MetaTextScore(scoreProperty ?? "SearchScore")).ToListAsync(cancellationToken).ConfigureAwait(false)).Select(doc => doc["_id"].AsString).ToList();

		/// <summary>
		/// Searchs all the matched documents and return the collection of identities
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="query"></param>
		/// <param name="filter"></param>
		/// <param name="pageSize"></param>
		/// <param name="pageNumber"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public static async Task<List<string>> SearchIdentitiesAsync<T>(this IMongoCollection<T> collection, string query, FilterDefinition<T> filter, int pageSize, int pageNumber, CancellationToken cancellationToken = default(CancellationToken)) where T : class
			=> await collection.SearchIdentitiesAsync(await collection.StartSessionAsync(cancellationToken).ConfigureAwait(false), "SearchScore", query, filter, pageSize, pageNumber, cancellationToken).ConfigureAwait(false);

		/// <summary>
		/// Searchs all the matched documents and return the collection of identities
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source for searching</param>
		/// <param name="query">The text query for searching</param>
		/// <param name="filter">The additional filter</param>
		/// <param name="pageSize">The size of one page</param>
		/// <param name="pageNumber">The number of the page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="options">The options</param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public static Task<List<string>> SearchIdentitiesAsync<T>(this RepositoryContext context, DataSource dataSource, string query, IFilterBy<T> filter, int pageSize, int pageNumber, string businessEntityID = null, FindOptions options = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
			=> context.GetCollection<T>(dataSource).SearchIdentitiesAsync(context.NoSqlSession, "SearchScore", query, context.CreateSearchFilter(filter, businessEntityID), pageSize, pageNumber, cancellationToken);
		#endregion

		#region Count (search by query)
		/// <summary>
		/// Counts the number of all the matched documents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="session"></param>
		/// <param name="query">The text query for counting</param>
		/// <param name="filter">The additional filter-by expression for counting</param>
		/// <returns></returns>
		public static long Count<T>(this IMongoCollection<T> collection, IClientSessionHandle session, string query, FilterDefinition<T> filter) where T : class
		{
			var filterBy = query.CreateSearchFilter<T>();
			if (filter != null && !filter.Equals(Builders<T>.Filter.Empty))
				filterBy = filterBy & filter;
			return collection.CountDocuments(session ?? collection.StartSession(), filterBy);
		}

		/// <summary>
		/// Counts the number of all the matched documents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="query">The text query for counting</param>
		/// <param name="filter">The additional filter-by expression for counting</param>
		/// <returns></returns>
		public static long Count<T>(this IMongoCollection<T> collection, string query, FilterDefinition<T> filter) where T : class
			=> collection.Count(collection.StartSession(), query, filter);

		/// <summary>
		/// Counts the number of all the matched documents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source for counting</param>
		/// <param name="query">The text query for counting</param>
		/// <param name="filter">The additional filter-by expression for counting</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="options">The options</param>
		/// <returns></returns>
		public static long Count<T>(this RepositoryContext context, DataSource dataSource, string query, IFilterBy<T> filter = null, string businessEntityID = null, CountOptions options = null) where T : class
			=> context.GetCollection<T>(dataSource).Count(context.NoSqlSession, query, context.CreateSearchFilter(filter, businessEntityID));

		/// <summary>
		/// Counts the number of all the matched documents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="session"></param>
		/// <param name="query">The text query for counting</param>
		/// <param name="filter">The additional filter-by expression for counting</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<long> CountAsync<T>(this IMongoCollection<T> collection, IClientSessionHandle session, string query, FilterDefinition<T> filter, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			var filterBy = query.CreateSearchFilter<T>();
			if (filter != null && !filter.Equals(Builders<T>.Filter.Empty))
				filterBy = filterBy & filter;
			return await collection.CountDocumentsAsync(session ?? await collection.StartSessionAsync(cancellationToken).ConfigureAwait(false), filterBy, null, cancellationToken).ConfigureAwait(false);
		}

		/// <summary>
		/// Counts the number of all the matched documents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="query">The text query for counting</param>
		/// <param name="filter">The additional filter-by expression for counting</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<long> CountAsync<T>(this IMongoCollection<T> collection, string query, FilterDefinition<T> filter, CancellationToken cancellationToken = default(CancellationToken)) where T : class
			=> await collection.CountAsync(await collection.StartSessionAsync(cancellationToken).ConfigureAwait(false), query, filter, cancellationToken).ConfigureAwait(false);

		/// <summary>
		/// Counts the number of all the matched documents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source for counting</param>
		/// <param name="query">The text query for counting</param>
		/// <param name="filter">The additional filter-by expression for counting</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="options">The options</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<long> CountAsync<T>(this RepositoryContext context, DataSource dataSource, string query, IFilterBy<T> filter = null, string businessEntityID = null, CountOptions options = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
			=> context.GetCollection<T>(dataSource).CountAsync(context.NoSqlSession, query, context.CreateSearchFilter(filter, businessEntityID), cancellationToken);
		#endregion

		#region Schemas & Indexes
		internal static async Task EnsureIndexesAsync(this EntityDefinition definition, DataSource dataSource, Action<string, Exception> tracker = null, CancellationToken cancellationToken = default(CancellationToken))
		{
			// prepare indexes
			var prefix = "IDX_" + definition.CollectionName;
			var indexes = new Dictionary<string, List<AttributeInfo>>
			{
				{ prefix, new List<AttributeInfo>() }
			};
			var uniqueIndexes = new Dictionary<string, List<AttributeInfo>>();

			definition.Attributes.ForEach(attribute =>
			{
				var attributes = attribute.Info.GetCustomAttributes(typeof(SortableAttribute), true);
				if (attributes.Length > 0)
				{
					var attr = attributes[0] as SortableAttribute;
					if (!string.IsNullOrWhiteSpace(attr.UniqueIndexName))
					{
						var name = prefix + "_" + attr.UniqueIndexName;
						if (!uniqueIndexes.ContainsKey(name))
							uniqueIndexes.Add(name, new List<AttributeInfo>());
						uniqueIndexes[name].Add(attribute);

						if (!string.IsNullOrWhiteSpace(attr.IndexName))
						{
							name = prefix + "_" + attr.IndexName;
							if (!indexes.ContainsKey(name))
								indexes.Add(name, new List<AttributeInfo>());
							indexes[name].Add(attribute);
						}
					}
					else
					{
						var name = prefix + (string.IsNullOrWhiteSpace(attr.IndexName) ? "" : "_" + attr.IndexName);
						if (!indexes.ContainsKey(name))
							indexes.Add(name, new List<AttributeInfo>());
						indexes[name].Add(attribute);
					}
				}
			});

			var textIndexes = definition.Searchable
				? definition.Attributes
					.Where(attribute => attribute.Info.GetCustomAttributes(typeof(SearchableAttribute), true).Length > 0)
					.Select(attribute => string.IsNullOrWhiteSpace(attribute.Column) ? attribute.Name : attribute.Column)
					.ToList()
				: new List<string>();

			// get the collection
			var collection = NoSqlHelper.GetCollection<BsonDocument>(RepositoryMediator.GetConnectionString(dataSource), dataSource.DatabaseName, definition.CollectionName, true);

			// create indexes
			await indexes.ForEachAsync(async (info, token) =>
			{
				if (info.Value.Count > 0)
				{
					IndexKeysDefinition<BsonDocument> index = null;
					info.Value.ForEach(attribute =>
					{
						index = index == null
							? Builders<BsonDocument>.IndexKeys.Ascending(attribute.Name)
							: index.Ascending(attribute.Name);
					});
					tracker?.Invoke($"Create index of No SQL: {info.Key}", null);
					if (RepositoryMediator.IsDebugEnabled)
						RepositoryMediator.WriteLogs($"Create index of No SQL: {info.Key}", null);
					await collection.Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(index, new CreateIndexOptions { Name = info.Key, Background = true }), null, token).ConfigureAwait(false);
				}
			}, cancellationToken, true, false).ConfigureAwait(false);

			await uniqueIndexes.ForEachAsync(async (info, token) =>
			{
				if (info.Value.Count > 0)
				{
					IndexKeysDefinition<BsonDocument> index = null;
					info.Value.ForEach(attribute =>
					{
						index = index == null
							? Builders<BsonDocument>.IndexKeys.Ascending(attribute.Name)
							: index.Ascending(attribute.Name);
					});
					tracker?.Invoke($"Create unique index of No SQL: {info.Key}", null);
					if (RepositoryMediator.IsDebugEnabled)
						RepositoryMediator.WriteLogs($"Create unique index of No SQL: {info.Key}", null);
					await collection.Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(index, new CreateIndexOptions { Name = info.Key, Background = true, Unique = true }), null, token).ConfigureAwait(false);
				}
			}, cancellationToken, true, false).ConfigureAwait(false);

			if (textIndexes.Count > 0)
			{
				IndexKeysDefinition<BsonDocument> index = null;
				textIndexes.ForEach(attribute =>
				{
					index = index == null
						? Builders<BsonDocument>.IndexKeys.Text(attribute)
						: index.Text(attribute);
				});
				tracker?.Invoke($"Create text index of No SQL: {prefix + "_Text_Search"}", null);
				if (RepositoryMediator.IsDebugEnabled)
					RepositoryMediator.WriteLogs($"Create text index of No SQL: {prefix + "_Text_Search"}", null);
				await collection.Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(index, new CreateIndexOptions() { Name = prefix + "_Text_Search", Background = true }), null, cancellationToken).ConfigureAwait(false);
			}

			// create the blank document for ensuring the collection is created
			if (await collection.CountDocumentsAsync(Builders<BsonDocument>.Filter.Empty).ConfigureAwait(false) < 1)
				try
				{
					var @object = definition.Type.CreateInstance() as RepositoryBase;
					@object.ID = UtilityService.BlankUUID;
					await collection.InsertOneAsync(@object.ToBsonDocument(), null, cancellationToken)
						.ContinueWith(async (t) =>
						{
							await Task.Delay(456, cancellationToken).ConfigureAwait(false);
							await collection.DeleteOneAsync(Builders<BsonDocument>.Filter.Eq("_id", UtilityService.BlankUUID), null, cancellationToken).ConfigureAwait(false);
						}, cancellationToken, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Current)
						.ConfigureAwait(false);
				}
				catch { }
		}
		#endregion

	}
}