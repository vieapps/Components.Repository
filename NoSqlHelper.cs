#region Related components
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Attributes;
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
			if (NoSqlHelper.ReplicaSetClients.TryGetValue(mongoClient.GetHashCode(), out var status))
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
		public static async Task<bool> IsReplicaSetAsync(this IMongoClient mongoClient, CancellationToken cancellationToken = default)
		{
			if (NoSqlHelper.ReplicaSetClients.TryGetValue(mongoClient.GetHashCode(), out var status))
				return status;

			try
			{
				var statusDoc = await mongoClient.GetDatabase("admin").RunCommandAsync<BsonDocument>("{ replSetGetStatus: 1 }", null, cancellationToken).ConfigureAwait(false);
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
			if (!NoSqlHelper.Clients.TryGetValue(key, out var client))
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
			if (!NoSqlHelper.Databases.TryGetValue(key, out var database))
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
		public static Task<IClientSessionHandle> StartSessionAsync(this IMongoDatabase database, ClientSessionOptions options = null, CancellationToken cancellationToken = default)
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
		public static Task<bool> IsReplicaSetAsync(this IMongoDatabase database, CancellationToken cancellationToken = default)
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
			if (!NoSqlHelper.Collections.TryGetValue(key, out var collection))
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
		public static Task<IClientSessionHandle> StartSessionAsync<T>(this IMongoCollection<T> collection, ClientSessionOptions options = null, CancellationToken cancellationToken = default) where T : class
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
		public static Task<bool> IsReplicaSetAsync<T>(this IMongoCollection<T> collection, CancellationToken cancellationToken = default)
			=> collection.Database.IsReplicaSetAsync(cancellationToken);
		#endregion

		#region Helpers (filter, sort, projection, find fluent)
		static FilterDefinition<T> CreateFilterDefinition<T>(this string query) where T : class
		{
			var searchTerms = "";
			var searchQuery = new SearchQuery(query);
			searchQuery.AndWords.ForEach(word => searchTerms += (!searchTerms.Equals("") ? " " : "") + word);
			searchQuery.OrWords.ForEach(word => searchTerms += (!searchTerms.Equals("") ? " " : "") + word);
			searchQuery.NotWords.ForEach(word => searchTerms += (!searchTerms.Equals("") ? " " : "") + "-" + word);
			searchQuery.AndPhrases.ForEach(phrase => searchTerms += (!searchTerms.Equals("") ? " " : "") + $"\"{phrase}\"");
			searchQuery.OrPhrases.ForEach(phrase => searchTerms += (!searchTerms.Equals("") ? " " : "") + $"\"{phrase}\"");
			searchQuery.NotPhrases.ForEach(phrase => searchTerms += (!searchTerms.Equals("") ? " " : "") + "-" + $"\"{phrase}\"");
			return Builders<T>.Filter.Text(searchTerms, new TextSearchOptions { CaseSensitive = false });
		}

		static FilterDefinition<T> CreateFilterDefinition<T>(this IFilterBy<T> filter, string businessRepositoryEntityID = null) where T : class
		{
			var propertiesInfo = RepositoryMediator.GetProperties<T>(businessRepositoryEntityID);
			var standardProperties = propertiesInfo.Item1;
			var extendedProperties = propertiesInfo.Item2;
			var filterDefinition = filter != null
				? filter is FilterBys<T>
					? (filter as FilterBys<T>).GetNoSqlStatement(standardProperties, extendedProperties)
					: (filter as FilterBy<T>).GetNoSqlStatement(standardProperties, extendedProperties)
				: null;
			if (!string.IsNullOrWhiteSpace(businessRepositoryEntityID) && extendedProperties != null)
				filterDefinition = filterDefinition == null
					? Builders<T>.Filter.Eq("RepositoryEntityID", businessRepositoryEntityID)
					: filterDefinition & Builders<T>.Filter.Eq("RepositoryEntityID", businessRepositoryEntityID);
			return filterDefinition;
		}

		static SortDefinition<T> CreateSortDefinition<T>(this SortBy<T> sort, string businessRepositoryEntityID = null) where T : class
		{
			if (sort == null)
				return null;
			var propertiesInfo = RepositoryMediator.GetProperties<T>(businessRepositoryEntityID);
			var standardProperties = propertiesInfo.Item1;
			var extendedProperties = propertiesInfo.Item2;
			return sort.GetNoSqlStatement(null, standardProperties, extendedProperties);
		}

		static SortDefinition<T> CreateSortDefinition<T>(this string scoreProperty) where T : class
			=> Builders<T>.Sort.MetaTextScore(scoreProperty ?? "SearchScore");

		static SortDefinition<T> CreateSortDefinition<T>(this SortDefinition<T> sort, string scoreProperty) where T : class
			=> sort != null
				? sort.MetaTextScore(scoreProperty ?? "SearchScore")
				: NoSqlHelper.CreateSortDefinition<T>(scoreProperty);

		static ProjectionDefinition<T> CreateProjectionDefinition<T>(this IEnumerable<string> attributes) where T : class
		{
			ProjectionDefinition<T> projection = null;
			if (attributes == null || !attributes.Any())
				projection = Builders<T>.Projection.Include("_id");
			else
				attributes.ForEach(attribute => projection = projection == null ? Builders<T>.Projection.Include(attribute) : projection.Include(attribute));
			return projection;
		}

		static ProjectionDefinition<T> CreateProjectionDefinition<T>(this string scoreProperty) where T : class
			=> Builders<T>.Projection.MetaTextScore(scoreProperty ?? "SearchScore");

		static ProjectionDefinition<T> CreateProjectionDefinition<T>(this IEnumerable<string> attributes, string scoreProperty) where T : class
			=> attributes != null && attributes.Any()
				? NoSqlHelper.CreateProjectionDefinition<T>(attributes).MetaTextScore(scoreProperty ?? "SearchScore")
				: NoSqlHelper.CreateProjectionDefinition<T>(scoreProperty);

		static IFindFluent<T, T> CreateFindFluent<T>(this IMongoCollection<T> collection, IClientSessionHandle session, FilterDefinition<T> filter, SortDefinition<T> sort, int pageSize, int pageNumber, FindOptions options = null) where T : class
		{
			var findFluent = collection.Find(session, filter ?? Builders<T>.Filter.Empty, options).Sort(sort ?? Builders<T>.Sort.Ascending("_id"));
			if (pageSize > 0)
			{
				if (pageNumber > 1)
					findFluent = findFluent.Skip((pageNumber - 1) * pageSize);
				findFluent = findFluent.Limit(pageSize);
			}
			return findFluent;
		}

		static IFindFluent<T, BsonDocument> CreateSelectFluent<T>(this IMongoCollection<T> collection, IClientSessionHandle session, IEnumerable<string> attributes, FilterDefinition<T> filter, SortDefinition<T> sort, int pageSize, int pageNumber, FindOptions options = null) where T : class
			=> collection.CreateFindFluent(session, filter, sort, pageSize, pageNumber, options).Project(NoSqlHelper.CreateProjectionDefinition<T>(attributes));

		static IFindFluent<T, T> CreateSearchFluent<T>(this IMongoCollection<T> collection, IClientSessionHandle session, string query, FilterDefinition<T> filter, SortDefinition<T> sort, string scoreProperty, IEnumerable<string> attributes, int pageSize, int pageNumber, FindOptions options = null) where T : class
		{
			var filterBy = filter != null && !filter.Equals(Builders<T>.Filter.Empty)
				? query.CreateFilterDefinition<T>() & filter
				: query.CreateFilterDefinition<T>();
			var sortBy = NoSqlHelper.CreateSortDefinition<T>(sort, scoreProperty);
			var projection = NoSqlHelper.CreateProjectionDefinition<T>(attributes, scoreProperty);
			return collection.CreateFindFluent(session, filterBy, sortBy, pageSize, pageNumber, options).Project<T>(projection);
		}

		static IFindFluent<T, T> CreateSearchFluent<T>(this IMongoCollection<T> collection, IClientSessionHandle session, string scoreProperty, string query, FilterDefinition<T> filter, int pageSize, int pageNumber, FindOptions options = null) where T : class
			=> collection.CreateSearchFluent(session, query, filter, null, scoreProperty, null, pageSize, pageNumber, options);
		#endregion

		#region Create
		/// <summary>
		/// Creates new document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="session">The working session</param>
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
				RepositoryMediator.WriteLogs(new[]
				{
					$"NoSQL: Perform CREATE command successful [{typeof(T)}#{@object?.GetEntityID()}] @ {collection.CollectionNamespace.CollectionName}",
					$"{(@object != null ? "Objects' data:\r\n\t" + @object.GetPublicProperties(attribute => !attribute.IsIgnored()).Select(attribute => $"+ @{attribute.Name} ({attribute.Type.GetTypeName(true)}) => [{@object.GetAttributeValue(attribute) ?? "(null)"}]").ToString("\r\n\t") + "\r\n" : "")}Execution times: {stopwatch.GetElapsedTimes()}"
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
			=> collection.Create(null, @object, options);

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
		/// <param name="session">The working session</param>
		/// <param name="object"></param>
		/// <param name="options"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public static async Task CreateAsync<T>(this IMongoCollection<T> collection, IClientSessionHandle session, T @object, InsertOneOptions options = null, CancellationToken cancellationToken = default) where T : class
		{
			if (@object == null)
				throw new ArgumentNullException(nameof(@object), "The object is null");

			var stopwatch = Stopwatch.StartNew();
			await collection.InsertOneAsync(session ?? await collection.StartSessionAsync(cancellationToken).ConfigureAwait(false), @object, options, cancellationToken).ConfigureAwait(false);
			stopwatch.Stop();
			if (RepositoryMediator.IsDebugEnabled)
				RepositoryMediator.WriteLogs(new[]
				{
					$"NoSQL: Perform CREATE command successful [{typeof(T)}#{@object?.GetEntityID()}] @ {collection.CollectionNamespace.CollectionName}",
					$"{(@object != null ? "Objects' data:\r\n\t" + @object.GetPublicProperties(attribute => !attribute.IsIgnored()).Select(attribute => $"+ @{attribute.Name} ({attribute.Type.GetTypeName(true)}) => [{@object.GetAttributeValue(attribute) ?? "(null)"}]").ToString("\r\n\t") + "\r\n" : "")}Execution times: {stopwatch.GetElapsedTimes()}"
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
		public static Task CreateAsync<T>(this IMongoCollection<T> collection, T @object, InsertOneOptions options = null, CancellationToken cancellationToken = default) where T : class
			=> collection.CreateAsync(null, @object, options, cancellationToken);

		/// <summary>
		/// Creates new document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="dataSource">The data source</param>
		/// <param name="object">The object for creating new instance in storage</param>
		/// <param name="options"></param>
		public static Task CreateAsync<T>(DataSource dataSource, T @object, InsertOneOptions options = null, CancellationToken cancellationToken = default) where T : class
			=> NoSqlHelper.GetCollection<T>(dataSource, RepositoryMediator.GetEntityDefinition<T>()).CreateAsync(@object, options, cancellationToken);

		/// <summary>
		/// Creates new document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="object">The object for creating new instance in storage</param>
		/// <param name="options"></param>
		public static Task CreateAsync<T>(T @object, InsertOneOptions options = null, CancellationToken cancellationToken = default) where T : class
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
		public static Task CreateAsync<T>(this RepositoryContext context, DataSource dataSource, T @object, InsertOneOptions options = null, CancellationToken cancellationToken = default) where T : class
			=> context.GetCollection<T>(dataSource).CreateAsync(context.NoSqlSession, @object, options, cancellationToken);
		#endregion

		#region Get
		/// <summary>
		/// Gets document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="session">The working session</param>
		/// <param name="id"></param>
		/// <param name="options">The options</param>
		/// <returns></returns>
		public static T Get<T>(this IMongoCollection<T> collection, IClientSessionHandle session, string id, FindOptions options = null) where T : class
			=> !string.IsNullOrWhiteSpace(id)
				? collection.Get(session, Builders<T>.Filter.Eq("_id", id), null, options)
				: default;

		/// <summary>
		/// Gets document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="id"></param>
		/// <param name="options">The options</param>
		/// <returns></returns>
		public static T Get<T>(this IMongoCollection<T> collection, string id, FindOptions options = null) where T : class
			=> collection.Get(null, id, options);

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
				? context.GetCollection<T>(dataSource).Get(context.NoSqlSession, id, options)
				: default;

		/// <summary>
		/// Gets document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="session">The working session</param>
		/// <param name="id">The string that presents identity of the object that need to get</param>
		/// <param name="options">The options</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<T> GetAsync<T>(this IMongoCollection<T> collection, IClientSessionHandle session, string id, FindOptions options = null, CancellationToken cancellationToken = default) where T : class
			=> !string.IsNullOrWhiteSpace(id)
				? collection.GetAsync(session, Builders<T>.Filter.Eq("_id", id), null, options, cancellationToken)
				: Task.FromResult(default(T));

		/// <summary>
		/// Gets document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="id">The string that presents identity of the object that need to get</param>
		/// <param name="options">The options</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<T> GetAsync<T>(this IMongoCollection<T> collection, string id, FindOptions options = null, CancellationToken cancellationToken = default) where T : class
			=> collection.GetAsync(null, id, options, cancellationToken);

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
		public static Task<T> GetAsync<T>(this RepositoryContext context, DataSource dataSource, string id, FindOptions options = null, CancellationToken cancellationToken = default) where T : class
			=> context.GetCollection<T>(dataSource).GetAsync(context.NoSqlSession, id, options, cancellationToken);
		#endregion

		#region Get (first match)
		/// <summary>
		/// Gets document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="session">The working session</param>
		/// <param name="filter">The filter-by expression for filtering</param>
		/// <param name="sort">The order-by expression for ordering</param>
		/// <param name="options">The options</param>
		/// <returns></returns>
		public static T Get<T>(this IMongoCollection<T> collection, IClientSessionHandle session, FilterDefinition<T> filter, SortDefinition<T> sort = null, FindOptions options = null) where T : class
			=> collection.Find(session, filter, sort, 1, 1, options).First();

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
			=> collection.Get(null, filter, sort, options);

		/// <summary>
		/// Gets document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="filter">The filter-by expression for filtering</param>
		/// <param name="sort">The order-by expression for ordering</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="options">The options</param>
		/// <returns></returns>
		public static T Get<T>(this RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, SortBy<T> sort = null, string businessRepositoryEntityID = null, FindOptions options = null) where T : class
			=> context.GetCollection<T>(dataSource).Get(context.NoSqlSession, NoSqlHelper.CreateFilterDefinition<T>(filter, businessRepositoryEntityID), NoSqlHelper.CreateSortDefinition<T>(sort, businessRepositoryEntityID), options);

		/// <summary>
		/// Gets document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="session">The working session</param>
		/// <param name="filter">The filter-by expression for filtering</param>
		/// <param name="sort">The order-by expression for ordering</param>
		/// <param name="options">The options</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<T> GetAsync<T>(this IMongoCollection<T> collection, IClientSessionHandle session, FilterDefinition<T> filter, SortDefinition<T> sort = null, FindOptions options = null, CancellationToken cancellationToken = default) where T : class
			=> (await collection.FindAsync(session, filter, sort, 1, 1, options, cancellationToken).ConfigureAwait(false)).First();

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
		public static Task<T> GetAsync<T>(this IMongoCollection<T> collection, FilterDefinition<T> filter, SortDefinition<T> sort = null, FindOptions options = null, CancellationToken cancellationToken = default) where T : class
			=> collection.GetAsync(null, filter, sort, options, cancellationToken);

		/// <summary>
		/// Gets document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="filter">The filter-by expression for filtering</param>
		/// <param name="sort">The order-by expression for ordering</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="options">The options</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<T> GetAsync<T>(this RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, SortBy<T> sort = null, string businessRepositoryEntityID = null, FindOptions options = null, CancellationToken cancellationToken = default) where T : class
			=> context.GetCollection<T>(dataSource).GetAsync(context.NoSqlSession, NoSqlHelper.CreateFilterDefinition<T>(filter, businessRepositoryEntityID), NoSqlHelper.CreateSortDefinition<T>(sort, businessRepositoryEntityID), options, cancellationToken);
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
			=> NoSqlHelper.Get(definition?.GetPrimaryDataSource(), definition, id, options);

		/// <summary>
		/// Gets document of an object
		/// </summary>
		/// <param name="dataSource">The data source</param>
		/// <param name="definition">The definition</param>
		/// <param name="id">The identity</param>
		/// <param name="options">The options</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<object> GetAsync(DataSource dataSource, EntityDefinition definition, string id, FindOptions options = null, CancellationToken cancellationToken = default)
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
		public static Task<object> GetAsync(EntityDefinition definition, string id, FindOptions options = null, CancellationToken cancellationToken = default)
			=> NoSqlHelper.GetAsync(definition?.GetPrimaryDataSource(), definition, id, options, cancellationToken);
		#endregion

		#region Replace
		/// <summary>
		/// Replaces document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="session">The working session</param>
		/// <param name="object">The object for updating</param>
		/// <param name="options">The options for updating</param>
		/// <returns></returns>
		public static ReplaceOneResult Replace<T>(this IMongoCollection<T> collection, IClientSessionHandle session, T @object, ReplaceOptions options = null) where T : class
		{
			var stopwatch = Stopwatch.StartNew();
			try
			{
				return @object != null
					? collection.ReplaceOne(session ?? collection.StartSession(), Builders<T>.Filter.Eq("_id", @object.GetEntityID()), @object, options ?? new ReplaceOptions { IsUpsert = true })
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
					RepositoryMediator.WriteLogs(new[]
					{
						$"NoSQL: {(@object != null ? "Perform REPLACE command successful" : "No valid object to perform replace")} [{typeof(T)}#{@object?.GetEntityID()}] @ {collection.CollectionNamespace.CollectionName}",
						$"{(@object != null ? "Objects' data:\r\n\t" + @object.GetPublicProperties(attribute => !attribute.IsIgnored()).Select(attribute => $"+ @{attribute.Name} ({attribute.Type.GetTypeName(true)}) => [{@object.GetAttributeValue(attribute) ?? "(null)"}]").ToString("\r\n\t") + "\r\n" : "")}Execution times: {stopwatch.GetElapsedTimes()}"
					});
			}
		}

		/// <summary>
		/// Replaces document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="object">The object for updating</param>
		/// <param name="options">The options for updating</param>
		/// <returns></returns>
		public static ReplaceOneResult Replace<T>(this IMongoCollection<T> collection, T @object, ReplaceOptions options = null) where T : class
			=> collection.Replace(null, @object, options);

		/// <summary>
		/// Replaces document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="object">The object for updating</param>
		/// <param name="options">The options for updating</param>
		/// <returns></returns>
		public static ReplaceOneResult Replace<T>(this RepositoryContext context, DataSource dataSource, T @object, ReplaceOptions options = null) where T : class
			=> context.GetCollection<T>(dataSource).Replace(context.NoSqlSession, @object, options);

		/// <summary>
		/// Replaces document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="session">The working session</param>
		/// <param name="object">The object for updating</param>
		/// <param name="options">The options for updating</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<ReplaceOneResult> ReplaceAsync<T>(this IMongoCollection<T> collection, IClientSessionHandle session, T @object, ReplaceOptions options = null, CancellationToken cancellationToken = default) where T : class
		{
			var stopwatch = Stopwatch.StartNew();
			try
			{
				return @object != null
					? await collection.ReplaceOneAsync(session ?? await collection.StartSessionAsync(cancellationToken).ConfigureAwait(false), Builders<T>.Filter.Eq("_id", @object.GetEntityID()), @object, options ?? new ReplaceOptions { IsUpsert = true }, cancellationToken).ConfigureAwait(false)
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
					RepositoryMediator.WriteLogs(new[]
					{
						$"NoSQL: {(@object != null ? "Perform REPLACE command successful" : "No valid object to perform replace")} [{typeof(T)}#{@object?.GetEntityID()}] @ {collection.CollectionNamespace.CollectionName}",
						$"{(@object != null ? "Objects' data:\r\n\t" + @object.GetPublicProperties(attribute => !attribute.IsIgnored()).Select(attribute => $"+ @{attribute.Name} ({attribute.Type.GetTypeName(true)}) => [{@object.GetAttributeValue(attribute) ?? "(null)"}]").ToString("\r\n\t") + "\r\n" : "")}Execution times: {stopwatch.GetElapsedTimes()}"
					});
			}
		}

		/// <summary>
		/// Replaces document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="object">The object for updating</param>
		/// <param name="options">The options for updating</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<ReplaceOneResult> ReplaceAsync<T>(this IMongoCollection<T> collection, T @object, ReplaceOptions options = null, CancellationToken cancellationToken = default) where T : class
			=> collection.ReplaceAsync(null, @object, options, cancellationToken);

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
		public static Task<ReplaceOneResult> ReplaceAsync<T>(this RepositoryContext context, DataSource dataSource, T @object, ReplaceOptions options = null, CancellationToken cancellationToken = default) where T : class
				=> context.GetCollection<T>(dataSource).ReplaceAsync(context.NoSqlSession, @object, options, cancellationToken);
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

		static BsonValue ToBsonValue(this ObjectService.AttributeInfo attribute, object value)
		{
			if (value == null)
				return null;

			var bsonRepresentation = attribute.IsEnum() ? attribute.GetCustomAttribute<BsonRepresentationAttribute>() : null;
			return bsonRepresentation != null && bsonRepresentation.Representation.Equals(BsonType.String)
				? BsonValue.Create(value.ToString())
				: BsonValue.Create(value);
		}

		/// <summary>
		/// Updates document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="session">The working session</param>
		/// <param name="object">The object for updating</param>
		/// <param name="attributes">The collection of attributes for updating individually</param>
		/// <param name="options">The options for updating</param>
		/// <returns>ReplaceOneResult or UpdateResult object</returns>
		public static void Update<T>(this IMongoCollection<T> collection, IClientSessionHandle session, T @object, List<string> attributes, UpdateOptions options = null) where T : class
		{
			// check
			if (@object == null)
				throw new ArgumentNullException(nameof(@object), "The object is null");
			else if (attributes == null || attributes.Count < 1)
				throw new ArgumentException("No attribute to update");

			var stopwatch = Stopwatch.StartNew();

			// get collection of all attributes
			var objAttributes = @object.GetAttributes();

			// check to use replace (when got generic of primitive or class type member - workaround)
			var useReplace = false;
			foreach (var name in attributes)
			{
				if (name.StartsWith("ExtendedProperties."))
				{
					useReplace = true;
					break;
				}

				var attribute = objAttributes.FirstOrDefault(a => a.Name.IsEquals(name));
				if (attribute != null)
				{
					useReplace = attribute.Type.IsClassType() || (attribute.Type.IsGenericListOrHashSet() && attribute.Type.GenericTypeArguments[0].IsClassType()) || (attribute.Type.IsArray && attribute.Type.GetElementType().IsClassType());
					if (useReplace)
						break;
				}
			}

			// replace whole document (when got generic of primitive or class type member - workaround)
			if (useReplace)
			{
				collection.ReplaceOne(session ?? collection.StartSession(), Builders<T>.Filter.Eq("_id", @object.GetEntityID()), @object, new ReplaceOptions { IsUpsert = options == null || options.IsUpsert });

				stopwatch.Stop();
				if (RepositoryMediator.IsDebugEnabled)
					RepositoryMediator.WriteLogs(new[]
					{
						$"NoSQL: updated attributes got a generic primitive or a class type, then switch to use Replace instead of Update",
						$"- Execution times: {stopwatch.GetElapsedTimes()}",
						$"- Updated attributes: [{attributes.ToString(", ")}]",
						$"- Objects' data for replacing:\r\n\t" + objAttributes.Select(attribute => $"+ @{attribute.Name} ({attribute.Type.GetTypeName(true)}) => [{@object.GetAttributeValue(attribute) ?? "(null)"}]").ToString("\r\n\t") + "\r\n\t+ ExtendedProperties: " + @object.GetAttributeValue("ExtendedProperties")?.ToJson(null).ToString(Newtonsoft.Json.Formatting.None)
					});

				return;
			}

			// update individually
			UpdateDefinition<T> updater = null;
			var updated = "";

			attributes.ForEach(name =>
			{
				var attribute = objAttributes.FirstOrDefault(a => a.Name.IsEquals(name));
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
						? Builders<T>.Update.Set(attribute.Name, isList ? (value as IEnumerable).ToBsonArray(type) : attribute.ToBsonValue(value))
						: updater.Set(attribute.Name, isList ? (value as IEnumerable).ToBsonArray(type) : attribute.ToBsonValue(value));

					if (RepositoryMediator.IsDebugEnabled)
						updated += $"\r\n\t+ @{attribute.Name} ({attribute.Type}) ==> [{value ?? "(null)"}]";
				}
			});

			if (updater != null)
				collection.UpdateOne(session ?? collection.StartSession(), Builders<T>.Filter.Eq("_id", @object.GetEntityID()), updater, options ?? new UpdateOptions { IsUpsert = true });

			stopwatch.Stop();
			if (RepositoryMediator.IsDebugEnabled)
				RepositoryMediator.WriteLogs(new[]
				{
					$"NoSQL: {(updater != null ? "Perform UPDATE command successful" : "No valid update to perform")} [{typeof(T)}#{@object?.GetEntityID()}] @ {collection.CollectionNamespace.CollectionName}",
					$"{(updater != null ? $"Updated attributes:{updated}\r\n" : "")}Execution times: {stopwatch.GetElapsedTimes()}"
				});
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
			=> collection.Update(null, @object, attributes, options);

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
			=> context.GetCollection<T>(dataSource).Update(context.NoSqlSession, @object, attributes, options);

		/// <summary>
		/// Updates document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="session">The working session</param>
		/// <param name="object">The object for updating</param>
		/// <param name="update">The definition for updating</param>
		/// <param name="options">The options for updating</param>
		public static UpdateResult Update<T>(this IMongoCollection<T> collection, IClientSessionHandle session, T @object, UpdateDefinition<T> update, UpdateOptions options = null) where T : class
			=> @object != null
				? update != null
					? collection.UpdateOne(session ?? collection.StartSession(), Builders<T>.Filter.Eq("_id", @object.GetEntityID()), update, options)
					: throw new ArgumentNullException(nameof(update), "The update definition is null")
				: throw new ArgumentNullException(nameof(@object), "The object is null");

		/// <summary>
		/// Updates document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="object">The object for updating</param>
		/// <param name="update">The definition for updating</param>
		/// <param name="options">The options for updating</param>
		public static UpdateResult Update<T>(this IMongoCollection<T> collection, T @object, UpdateDefinition<T> update, UpdateOptions options = null) where T : class
			=> collection.Update(@object, update, options);

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
			=> context.GetCollection<T>(dataSource).Update(context.NoSqlSession, @object, update, options);

		/// <summary>
		/// Updates document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="session">The working session</param>
		/// <param name="object">The object for updating</param>
		/// <param name="attributes">The collection of attributes for updating individually</param>
		/// <param name="options">The options for updating</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task UpdateAsync<T>(this IMongoCollection<T> collection, IClientSessionHandle session, T @object, List<string> attributes, UpdateOptions options = null, CancellationToken cancellationToken = default) where T : class
		{
			if (@object == null)
				throw new ArgumentNullException(nameof(@object), "The object is null");
			else if (attributes == null || attributes.Count < 1)
				throw new ArgumentException("No attribute to update");

			var stopwatch = Stopwatch.StartNew();

			// get collection of all attributes
			var objAttributes = @object.GetAttributes();

			// check to use replace (when got generic of primitive or class type member - workaround)
			var useReplace = false;
			foreach (var name in attributes)
			{
				if (name.StartsWith("ExtendedProperties."))
				{
					useReplace = true;
					break;
				}

				var attribute = objAttributes.FirstOrDefault(a => a.Name.IsEquals(name));
				if (attribute != null)
				{
					useReplace = attribute.Type.IsClassType() || (attribute.Type.IsGenericListOrHashSet() && attribute.Type.GenericTypeArguments[0].IsClassType()) || (attribute.Type.IsArray && attribute.Type.GetElementType().IsClassType());
					if (useReplace)
						break;
				}
			}

			// replace whole document (when got generic of primitive or class type member - workaround)
			if (useReplace)
			{
				await collection.ReplaceOneAsync(session ?? await collection.StartSessionAsync(cancellationToken).ConfigureAwait(false), Builders<T>.Filter.Eq("_id", @object.GetEntityID()), @object, new ReplaceOptions { IsUpsert = options == null || options.IsUpsert }, cancellationToken).ConfigureAwait(false);

				stopwatch.Stop();
				if (RepositoryMediator.IsDebugEnabled)
					RepositoryMediator.WriteLogs(new[]
					{
						$"NoSQL: updated attributes got a generic primitive or a class type, then switch to use Replace instead of Update",
						$"- Execution times: {stopwatch.GetElapsedTimes()}",
						$"- Updated attributes: [{attributes.ToString(", ")}]",
						$"- Objects' data for replacing:\r\n\t" + objAttributes.Select(attribute => $"+ @{attribute.Name} ({attribute.Type.GetTypeName(true)}) => [{@object.GetAttributeValue(attribute) ?? "(null)"}]").ToString("\r\n\t") + "\r\n\t+ ExtendedProperties: " + @object.GetAttributeValue("ExtendedProperties")?.ToJson(null).ToString(Newtonsoft.Json.Formatting.None)
					});

				return;
			}

			// update individually
			UpdateDefinition<T> updater = null;
			var updated = "";

			attributes.ForEach(name =>
			{
				var attribute = objAttributes.FirstOrDefault(a => a.Name.IsEquals(name));
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
						? Builders<T>.Update.Set(attribute.Name, isList ? (value as IEnumerable).ToBsonArray(type) : attribute.ToBsonValue(value))
						: updater.Set(attribute.Name, isList ? (value as IEnumerable).ToBsonArray(type) : attribute.ToBsonValue(value));

					if (RepositoryMediator.IsDebugEnabled)
						updated += $"\r\n\t+ @{attribute.Name} ({attribute.Type}) ==> [{value ?? "(null)"}]";
				}
			});

			if (updater != null)
				await collection.UpdateOneAsync(session ?? await collection.StartSessionAsync(cancellationToken).ConfigureAwait(false), Builders<T>.Filter.Eq("_id", @object.GetEntityID()), updater, options ?? new UpdateOptions { IsUpsert = true }, cancellationToken).ConfigureAwait(false);

			stopwatch.Stop();
			if (RepositoryMediator.IsDebugEnabled)
				RepositoryMediator.WriteLogs(new[]
				{
					$"NoSQL: {(updater != null ? "Perform UPDATE command successful" : "No valid update to perform")} [{typeof(T)}#{@object?.GetEntityID()}] @ {collection.CollectionNamespace.CollectionName}",
					$"{(updater != null ? $"Updated attributes:{updated}\r\n" : "")}Execution times: {stopwatch.GetElapsedTimes()}"
				});
		}

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
		public static Task UpdateAsync<T>(this IMongoCollection<T> collection, T @object, List<string> attributes, UpdateOptions options = null, CancellationToken cancellationToken = default) where T : class
			=> collection.UpdateAsync(null, @object, attributes, options, cancellationToken);

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
		public static Task UpdateAsync<T>(this RepositoryContext context, DataSource dataSource, T @object, List<string> attributes, UpdateOptions options = null, CancellationToken cancellationToken = default) where T : class
			=> context.GetCollection<T>(dataSource).UpdateAsync(context.NoSqlSession, @object, attributes, options, cancellationToken);

		/// <summary>
		/// Updates document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="session">The working session</param>
		/// <param name="object">The object for updating</param>
		/// <param name="update">The definition for updating</param>
		/// <param name="options">The options for updating</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<UpdateResult> UpdateAsync<T>(this IMongoCollection<T> collection, IClientSessionHandle session, T @object, UpdateDefinition<T> update, UpdateOptions options = null, CancellationToken cancellationToken = default) where T : class
			=> @object != null
				? update != null
					? await collection.UpdateOneAsync(session ?? await collection.StartSessionAsync(cancellationToken).ConfigureAwait(false), Builders<T>.Filter.Eq("_id", @object.GetEntityID()), update, options, cancellationToken).ConfigureAwait(false)
					: throw new ArgumentNullException(nameof(update), "The update definition is null")
				: throw new ArgumentNullException(nameof(@object), "The object is null");

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
		public static Task<UpdateResult> UpdateAsync<T>(this IMongoCollection<T> collection, T @object, UpdateDefinition<T> update, UpdateOptions options = null, CancellationToken cancellationToken = default) where T : class
			=> collection.UpdateAsync(null, @object, update, options, cancellationToken);

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
		public static Task<UpdateResult> UpdateAsync<T>(this RepositoryContext context, DataSource dataSource, T @object, UpdateDefinition<T> update, UpdateOptions options = null, CancellationToken cancellationToken = default) where T : class
			=> context.GetCollection<T>(dataSource).UpdateAsync(context.NoSqlSession, @object, update, options, cancellationToken);
		#endregion

		#region Delete
		/// <summary>
		/// Deletes the document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="session">The working session</param>
		/// <param name="id"></param>
		/// <param name="options"></param>
		/// <returns></returns>
		public static DeleteResult Delete<T>(this IMongoCollection<T> collection, IClientSessionHandle session, string id, DeleteOptions options = null) where T : class
			=> !string.IsNullOrWhiteSpace(id)
				? collection.DeleteOne(session ?? collection.StartSession(), Builders<T>.Filter.Eq("_id", id), options)
				: null;

		/// <summary>
		/// Deletes the document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="id"></param>
		/// <param name="options"></param>
		/// <returns></returns>
		public static DeleteResult Delete<T>(this IMongoCollection<T> collection, string id, DeleteOptions options = null) where T : class
			=> collection.Delete(null, id, options);

		/// <summary>
		/// Deletes the document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="id">The identity of the document of an object for deleting</param>
		/// <returns></returns>
		public static DeleteResult Delete<T>(this RepositoryContext context, DataSource dataSource, string id, DeleteOptions options = null) where T : class
			=> context.GetCollection<T>(dataSource).Delete(context.NoSqlSession, id, options);

		/// <summary>
		/// Deletes the document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="session">The working session</param>
		/// <param name="object"></param>
		/// <param name="options"></param>
		/// <returns></returns>
		public static DeleteResult Delete<T>(this IMongoCollection<T> collection, IClientSessionHandle session, T @object, DeleteOptions options = null) where T : class
			=> @object != null
				? collection.Delete(session, @object.GetEntityID(), options)
				: throw new ArgumentNullException(nameof(@object), "The object is null");

		/// <summary>
		/// Deletes the document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="object"></param>
		/// <param name="options"></param>
		/// <returns></returns>
		public static DeleteResult Delete<T>(this IMongoCollection<T> collection, T @object, DeleteOptions options = null) where T : class
			=> collection.Delete(null, @object, options);

		/// <summary>
		/// Deletes the document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="object"></param>
		/// <returns></returns>
		public static DeleteResult Delete<T>(this RepositoryContext context, DataSource dataSource, T @object, DeleteOptions options = null) where T : class
			=> context.GetCollection<T>(dataSource).Delete(context.NoSqlSession, @object, options);

		/// <summary>
		/// Deletes the document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="session">The working session</param>
		/// <param name="id"></param>
		/// <param name="options"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public static async Task<DeleteResult> DeleteAsync<T>(this IMongoCollection<T> collection, IClientSessionHandle session, string id, DeleteOptions options = null, CancellationToken cancellationToken = default) where T : class
			=> !string.IsNullOrWhiteSpace(id)
				? await collection.DeleteOneAsync(session ?? await collection.StartSessionAsync(cancellationToken).ConfigureAwait(false), Builders<T>.Filter.Eq("_id", id), options, cancellationToken).ConfigureAwait(false)
				: null;

		/// <summary>
		/// Deletes the document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="id"></param>
		/// <param name="options"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public static Task<DeleteResult> DeleteAsync<T>(this IMongoCollection<T> collection, string id, DeleteOptions options = null, CancellationToken cancellationToken = default) where T : class
			=> collection.DeleteAsync(id, options, cancellationToken);

		/// <summary>
		/// Deletes the document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="id">The identity of the document of an object for deleting</param>
		/// <returns></returns>
		public static Task<DeleteResult> DeleteAsync<T>(this RepositoryContext context, DataSource dataSource, string id, DeleteOptions options = null, CancellationToken cancellationToken = default) where T : class
			=> context.GetCollection<T>(dataSource).DeleteAsync(context.NoSqlSession, id, options, cancellationToken);

		/// <summary>
		/// Deletes the document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="session">The working session</param>
		/// <param name="object"></param>
		/// <param name="options"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public static Task<DeleteResult> DeleteAsync<T>(this IMongoCollection<T> collection, IClientSessionHandle session, T @object, DeleteOptions options = null, CancellationToken cancellationToken = default) where T : class
			=> @object != null
				? collection.DeleteAsync(session, @object.GetEntityID(), options, cancellationToken)
				: Task.FromException<DeleteResult>(new ArgumentNullException(nameof(@object), "The object is null"));

		/// <summary>
		/// Deletes the document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="object"></param>
		/// <param name="options"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public static Task<DeleteResult> DeleteAsync<T>(this IMongoCollection<T> collection, T @object, DeleteOptions options = null, CancellationToken cancellationToken = default) where T : class
			=> collection.DeleteAsync(@object, options, cancellationToken);

		/// <summary>
		/// Deletes the document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="object"></param>
		/// <returns></returns>
		public static Task<DeleteResult> DeleteAsync<T>(this RepositoryContext context, DataSource dataSource, T @object, DeleteOptions options = null, CancellationToken cancellationToken = default) where T : class
			=> context.GetCollection<T>(dataSource).DeleteAsync(context.NoSqlSession, @object, options, cancellationToken);
		#endregion

		#region Delete (many)
		/// <summary>
		/// Deletes document of multiple objects
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="filter">The filter for deleting</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="options">The options for deleting</param>
		/// <returns></returns>
		public static DeleteResult DeleteMany<T>(this RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, string businessRepositoryEntityID = null, DeleteOptions options = null) where T : class
		{
			var collection = context.GetCollection<T>(dataSource);
			return collection.DeleteMany(context.NoSqlSession ?? collection.StartSession(), NoSqlHelper.CreateFilterDefinition<T>(filter, businessRepositoryEntityID) ?? Builders<T>.Filter.Empty, options);
		}

		/// <summary>
		/// Deletes document of multiple objects
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="filter">The filter for deleting</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="options">The options for deleting</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<DeleteResult> DeleteManyAsync<T>(this RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, string businessRepositoryEntityID = null, DeleteOptions options = null, CancellationToken cancellationToken = default) where T : class
		{
			var collection = context.GetCollection<T>(dataSource);
			return await collection.DeleteManyAsync(context.NoSqlSession ?? await collection.StartSessionAsync(cancellationToken).ConfigureAwait(false), NoSqlHelper.CreateFilterDefinition<T>(filter, businessRepositoryEntityID) ?? Builders<T>.Filter.Empty, options, cancellationToken).ConfigureAwait(false);
		}
		#endregion

		#region Select
		/// <summary>
		/// Finds all the matched documents and return the collection of <see cref="BsonDocument">BsonDocument</see> objects with limited attributes
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="session">The working session</param>
		/// <param name="attributes">The collection of attributes to included in the results (set to null to include identity attribute only)</param>
		/// <param name="filter">The filter-by expression for filtering</param>
		/// <param name="sort">The order-by expression for ordering</param>
		/// <param name="pageSize">The size of one page</param>
		/// <param name="pageNumber">The number of page</param>
		/// <param name="options">The options</param>
		/// <returns></returns>
		public static List<BsonDocument> Select<T>(this IMongoCollection<T> collection, IClientSessionHandle session, IEnumerable<string> attributes, FilterDefinition<T> filter, SortDefinition<T> sort, int pageSize, int pageNumber, FindOptions options = null) where T : class
		{
			var selectFluent = collection.CreateSelectFluent(session ?? collection.StartSession(), attributes, filter, sort, pageSize, pageNumber, options);
			if (RepositoryMediator.IsTraceEnabled)
				RepositoryMediator.WriteLogs($"Select [{typeof(T).GetTypeName()}]\r\n{selectFluent}");
			return selectFluent.ToList();
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
		/// <returns></returns>
		public static List<BsonDocument> Select<T>(this IMongoCollection<T> collection, IEnumerable<string> attributes, FilterDefinition<T> filter, SortDefinition<T> sort, int pageSize, int pageNumber, FindOptions options = null) where T : class
			=> collection.Select(null, attributes, filter, sort, pageSize, pageNumber, options);

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
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="options">The options</param>
		/// <returns></returns>
		public static List<BsonDocument> Select<T>(this RepositoryContext context, DataSource dataSource, IEnumerable<string> attributes, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessRepositoryEntityID = null, bool autoAssociateWithMultipleParents = true, FindOptions options = null) where T : class
		{
			var info = RepositoryExtensions.PrepareNoSqlStatements(filter, sort, businessRepositoryEntityID, autoAssociateWithMultipleParents);
			return context.GetCollection<T>(dataSource).Select(context.NoSqlSession, attributes, info.Item1, info.Item2, pageSize, pageNumber, options);
		}

		/// <summary>
		/// Finds all the matched documents and return the collection of <see cref="BsonDocument">BsonDocument</see> objects with limited attributes
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="session">The working session</param>
		/// <param name="attributes">The collection of attributes to included in the results (set to null to include identity attribute only)</param>
		/// <param name="filter">The filter-by expression for filtering</param>
		/// <param name="sort">The order-by expression for ordering</param>
		/// <param name="pageSize">The size of one page</param>
		/// <param name="pageNumber">The number of page</param>
		/// <param name="options">The options</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<List<BsonDocument>> SelectAsync<T>(this IMongoCollection<T> collection, IClientSessionHandle session, IEnumerable<string> attributes, FilterDefinition<T> filter, SortDefinition<T> sort, int pageSize, int pageNumber, FindOptions options = null, CancellationToken cancellationToken = default) where T : class
		{
			var selectFluent = collection.CreateSelectFluent(session ?? await collection.StartSessionAsync(cancellationToken).ConfigureAwait(false), attributes, filter, sort, pageSize, pageNumber, options);
			if (RepositoryMediator.IsTraceEnabled)
				RepositoryMediator.WriteLogs($"Select [{typeof(T).GetTypeName()}]\r\n{selectFluent}");
			return await selectFluent.ToListAsync(cancellationToken).ConfigureAwait(false);
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
		public static Task<List<BsonDocument>> SelectAsync<T>(this IMongoCollection<T> collection, IEnumerable<string> attributes, FilterDefinition<T> filter, SortDefinition<T> sort, int pageSize, int pageNumber, FindOptions options = null, CancellationToken cancellationToken = default) where T : class
			=> collection.SelectAsync(null, attributes, filter, sort, pageSize, pageNumber, options, cancellationToken);

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
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="options">The options</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<List<BsonDocument>> SelectAsync<T>(this RepositoryContext context, DataSource dataSource, IEnumerable<string> attributes, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessRepositoryEntityID = null, bool autoAssociateWithMultipleParents = true, FindOptions options = null, CancellationToken cancellationToken = default) where T : class
		{
			var info = RepositoryExtensions.PrepareNoSqlStatements(filter, sort, businessRepositoryEntityID, autoAssociateWithMultipleParents);
			return context.GetCollection<T>(dataSource).SelectAsync(context.NoSqlSession, attributes, info.Item1, info.Item2, pageSize, pageNumber, options, cancellationToken);
		}
		#endregion

		#region Select (identities)
		/// <summary>
		/// Finds all the matched documents and return the collection of identity attributes
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="session">The working session</param>
		/// <param name="filter">The filter-by expression for filtering</param>
		/// <param name="sort">The order-by expression for ordering</param>
		/// <param name="pageSize">The size of one page</param>
		/// <param name="pageNumber">The number of page</param>
		/// <param name="options">The options</param>
		/// <returns></returns>
		public static List<string> SelectIdentities<T>(this IMongoCollection<T> collection, IClientSessionHandle session, FilterDefinition<T> filter, SortDefinition<T> sort, int pageSize, int pageNumber, FindOptions options = null) where T : class
			=> collection.Select(session, null, filter, sort, pageSize, pageNumber, options)
				.Select(doc => doc["_id"].AsString)
				.ToList();

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
			=> collection.SelectIdentities(null, filter, sort, pageSize, pageNumber, options);

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
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="options">The options</param>
		/// <returns></returns>
		public static List<string> SelectIdentities<T>(this RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessRepositoryEntityID = null, bool autoAssociateWithMultipleParents = true, FindOptions options = null) where T : class
		{
			var info = RepositoryExtensions.PrepareNoSqlStatements(filter, sort, businessRepositoryEntityID, autoAssociateWithMultipleParents);
			return context.GetCollection<T>(dataSource).SelectIdentities(context.NoSqlSession, info.Item1, info.Item2, pageSize, pageNumber, options);
		}

		/// <summary>
		/// Finds all the matched documents and return the collection of identity attributes
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="session">The working session</param>
		/// <param name="filter">The filter-by expression for filtering</param>
		/// <param name="sort">The order-by expression for ordering</param>
		/// <param name="pageSize">The size of one page</param>
		/// <param name="pageNumber">The number of page</param>
		/// <param name="options">The options</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<List<string>> SelectIdentitiesAsync<T>(this IMongoCollection<T> collection, IClientSessionHandle session, FilterDefinition<T> filter, SortDefinition<T> sort, int pageSize, int pageNumber, FindOptions options = null, CancellationToken cancellationToken = default) where T : class
			=> (await collection.SelectAsync(session, null, filter, sort, pageSize, pageNumber, options, cancellationToken).ConfigureAwait(false)).Select(doc => doc["_id"].AsString).ToList();

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
		public static Task<List<string>> SelectIdentitiesAsync<T>(this IMongoCollection<T> collection, FilterDefinition<T> filter, SortDefinition<T> sort, int pageSize, int pageNumber, FindOptions options = null, CancellationToken cancellationToken = default) where T : class
			=> collection.SelectIdentitiesAsync(null, filter, sort, pageSize, pageNumber, options, cancellationToken);

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
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="options">The options</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<List<string>> SelectIdentitiesAsync<T>(this RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessRepositoryEntityID = null, bool autoAssociateWithMultipleParents = true, FindOptions options = null, CancellationToken cancellationToken = default) where T : class
		{
			var info = RepositoryExtensions.PrepareNoSqlStatements(filter, sort, businessRepositoryEntityID, autoAssociateWithMultipleParents);
			return context.GetCollection<T>(dataSource).SelectIdentitiesAsync(context.NoSqlSession, info.Item1, info.Item2, pageSize, pageNumber, options, cancellationToken);
		}
		#endregion

		#region Find
		/// <summary>
		/// Finds all the matched documents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="session">The working session</param>
		/// <param name="filter">The filter-by expression for filtering</param>
		/// <param name="sort">The order-by expression for ordering</param>
		/// <param name="pageSize">The size of one page</param>
		/// <param name="pageNumber">The number of page</param>
		/// <param name="options">The options</param>
		/// <returns></returns>
		public static List<T> Find<T>(this IMongoCollection<T> collection, IClientSessionHandle session, FilterDefinition<T> filter, SortDefinition<T> sort, int pageSize, int pageNumber, FindOptions options = null) where T : class
		{
			var findFluent = collection.CreateFindFluent(session ?? collection.StartSession(), filter, sort, pageSize, pageNumber, options);
			if (RepositoryMediator.IsTraceEnabled)
				RepositoryMediator.WriteLogs($"Find [{typeof(T).GetTypeName()}]\r\n{findFluent}");
			return findFluent.ToList();
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
			=> collection.Find(null, filter, sort, pageSize, pageNumber, options);

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
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="options">The options</param>
		/// <returns></returns>
		public static List<T> Find<T>(this RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessRepositoryEntityID = null, bool autoAssociateWithMultipleParents = true, FindOptions options = null) where T : class
		{
			var info = RepositoryExtensions.PrepareNoSqlStatements(filter, sort, businessRepositoryEntityID, autoAssociateWithMultipleParents);
			return context.GetCollection<T>(dataSource).Find(context.NoSqlSession, info.Item1, info.Item2, pageSize, pageNumber, options);
		}

		/// <summary>
		/// Finds all the matched documents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="session">The working session</param>
		/// <param name="filter">The filter-by expression for filtering</param>
		/// <param name="sort">The order-by expression for ordering</param>
		/// <param name="pageSize">The size of one page</param>
		/// <param name="pageNumber">The number of page</param>
		/// <param name="options">The options</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<List<T>> FindAsync<T>(this IMongoCollection<T> collection, IClientSessionHandle session, FilterDefinition<T> filter, SortDefinition<T> sort, int pageSize, int pageNumber, FindOptions options = null, CancellationToken cancellationToken = default) where T : class
		{
			var findFluent = collection.CreateFindFluent(session ?? await collection.StartSessionAsync(cancellationToken).ConfigureAwait(false), filter, sort, pageSize, pageNumber, options);
			if (RepositoryMediator.IsTraceEnabled)
				RepositoryMediator.WriteLogs($"Find [{typeof(T).GetTypeName()}]\r\n{findFluent}");
			return await findFluent.ToListAsync(cancellationToken).ConfigureAwait(false);
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
		public static Task<List<T>> FindAsync<T>(this IMongoCollection<T> collection, FilterDefinition<T> filter, SortDefinition<T> sort, int pageSize, int pageNumber, FindOptions options = null, CancellationToken cancellationToken = default) where T : class
			=> collection.FindAsync(null, filter, sort, pageSize, pageNumber, options, cancellationToken);

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
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="options">The options</param>
		/// <param name="cancellationToken">The options</param>
		/// <returns></returns>
		public static Task<List<T>> FindAsync<T>(this RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessRepositoryEntityID = null, bool autoAssociateWithMultipleParents = true, FindOptions options = null, CancellationToken cancellationToken = default) where T : class
		{
			var info = RepositoryExtensions.PrepareNoSqlStatements(filter, sort, businessRepositoryEntityID, autoAssociateWithMultipleParents);
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
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="options">The options</param>
		/// <returns></returns>
		public static List<T> Find<T>(this RepositoryContext context, DataSource dataSource, List<string> identities, SortBy<T> sort = null, string businessRepositoryEntityID = null, FindOptions options = null) where T : class
		{
			if (identities == null || identities.Count < 1)
				return new List<T>();
			var info = RepositoryExtensions.PrepareNoSqlStatements(Filters<T>.Or(identities.Select(id => Filters<T>.Equals("ID", id))), sort, businessRepositoryEntityID, false);
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
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="options">The options</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<List<T>> FindAsync<T>(this RepositoryContext context, DataSource dataSource, List<string> identities, SortBy<T> sort = null, string businessRepositoryEntityID = null, FindOptions options = null, CancellationToken cancellationToken = default) where T : class
		{
			if (identities == null || identities.Count < 1)
				return Task.FromResult(new List<T>());
			var info = RepositoryExtensions.PrepareNoSqlStatements(Filters<T>.Or(identities.Select(id => Filters<T>.Equals("ID", id))), sort, businessRepositoryEntityID, false);
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
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="options">The options</param>
		/// <returns></returns>
		public static long Count<T>(this RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, string businessRepositoryEntityID = null, bool autoAssociateWithMultipleParents = true, CountOptions options = null) where T : class
		{
			var info = RepositoryExtensions.PrepareNoSqlStatements(filter, null, businessRepositoryEntityID, autoAssociateWithMultipleParents);
			var collection = context.GetCollection<T>(dataSource);
			if (RepositoryMediator.IsTraceEnabled)
				RepositoryMediator.WriteLogs($"Count [{typeof(T).GetTypeName()}]\r\n{(info.Item1 ?? Builders<T>.Filter.Empty).Render(collection.DocumentSerializer, collection.Settings.SerializerRegistry)}");
			return collection.CountDocuments(context.NoSqlSession ?? collection.StartSession(), info.Item1 ?? Builders<T>.Filter.Empty, options);
		}

		/// <summary>
		/// Counts the number of all the matched documents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source for counting</param>
		/// <param name="filter">The filter-by expression for counting</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="options">The options</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<long> CountAsync<T>(this RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, string businessRepositoryEntityID = null, bool autoAssociateWithMultipleParents = true, CountOptions options = null, CancellationToken cancellationToken = default) where T : class
		{
			var info = RepositoryExtensions.PrepareNoSqlStatements(filter, null, businessRepositoryEntityID, autoAssociateWithMultipleParents);
			var collection = context.GetCollection<T>(dataSource);
			if (RepositoryMediator.IsTraceEnabled)
				RepositoryMediator.WriteLogs($"Count [{typeof(T).GetTypeName()}]\r\n{(info.Item1 ?? Builders<T>.Filter.Empty).Render(collection.DocumentSerializer, collection.Settings.SerializerRegistry)}");
			return await collection.CountDocumentsAsync(context.NoSqlSession ?? await collection.StartSessionAsync(cancellationToken).ConfigureAwait(false), info.Item1 ?? Builders<T>.Filter.Empty, options, cancellationToken).ConfigureAwait(false);
		}
		#endregion

		#region Search
		/// <summary>
		/// Searchs all the matched documents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="session">The working session</param>
		/// <param name="query"></param>
		/// <param name="filter"></param>
		/// <param name="sort"></param>
		/// <param name="scoreProperty"></param>
		/// <param name="pageSize"></param>
		/// <param name="pageNumber"></param>
		/// <param name="options"></param>
		/// <returns></returns>
		public static List<T> Search<T>(this IMongoCollection<T> collection, IClientSessionHandle session, string query, FilterDefinition<T> filter, SortDefinition<T> sort, string scoreProperty, int pageSize, int pageNumber, FindOptions options = null) where T : class
		{
			var searchFluent = collection.CreateSearchFluent(session ?? collection.StartSession(), query, filter, sort, scoreProperty, null, pageSize, pageNumber, options);
			if (RepositoryMediator.IsTraceEnabled)
				RepositoryMediator.WriteLogs($"Search [{typeof(T).GetTypeName()}]\r\n{searchFluent}");
			return searchFluent.ToList();
		}

		/// <summary>
		/// Searchs all the matched documents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="query"></param>
		/// <param name="filter"></param>
		/// <param name="sort"></param>
		/// <param name="scoreProperty"></param>
		/// <param name="pageSize"></param>
		/// <param name="pageNumber"></param>
		/// <param name="options"></param>
		/// <returns></returns>
		public static List<T> Search<T>(this IMongoCollection<T> collection, string query, FilterDefinition<T> filter, SortDefinition<T> sort, string scoreProperty, int pageSize, int pageNumber, FindOptions options = null) where T : class
			=> collection.Search(null, query, filter, sort, scoreProperty, pageSize, pageNumber, options);

		/// <summary>
		/// Searchs all the matched documents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="query"></param>
		/// <param name="filter"></param>
		/// <param name="sort"></param>
		/// <param name="pageSize"></param>
		/// <param name="pageNumber"></param>
		/// <param name="options"></param>
		/// <returns></returns>
		public static List<T> Search<T>(this IMongoCollection<T> collection, string query, FilterDefinition<T> filter, SortDefinition<T> sort, int pageSize, int pageNumber, FindOptions options = null) where T : class
			=> collection.Search(query, filter, sort, null, pageSize, pageNumber, options);

		/// <summary>
		/// Searchs all the matched documents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="query"></param>
		/// <param name="sort"></param>
		/// <param name="scoreProperty"></param>
		/// <param name="pageSize"></param>
		/// <param name="pageNumber"></param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="options">The options</param>
		/// <returns></returns>
		public static List<T> Search<T>(this IMongoCollection<T> collection, string query, SortDefinition<T> sort, string scoreProperty, int pageSize, int pageNumber, string businessRepositoryEntityID = null, FindOptions options = null) where T : class
			=> collection.Search(query, NoSqlHelper.CreateFilterDefinition<T>(null, businessRepositoryEntityID), sort, scoreProperty, pageSize, pageNumber, options);

		/// <summary>
		/// Searchs all the matched documents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source for searching</param>
		/// <param name="query">The text query for searching</param>
		/// <param name="filter">The additional filtering expression</param>
		/// <param name="sort">The additional sorting expression</param>
		/// <param name="pageSize">The size of one page</param>
		/// <param name="pageNumber">The number of the page</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="options">The options</param>
		/// <returns></returns>
		public static List<T> Search<T>(this RepositoryContext context, DataSource dataSource, string query, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessRepositoryEntityID = null, FindOptions options = null) where T : class
			=> context.GetCollection<T>(dataSource).Search(context.NoSqlSession, query, NoSqlHelper.CreateFilterDefinition(filter, businessRepositoryEntityID), sort?.GetNoSqlStatement(), null, pageSize, pageNumber, options);

		/// <summary>
		/// Searchs all the matched documents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="query"></param>
		/// <param name="filter"></param>
		/// <param name="sort"></param>
		/// <param name="scoreProperty"></param>
		/// <param name="pageSize"></param>
		/// <param name="pageNumber"></param>
		/// <param name="options">The options</param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public static async Task<List<T>> SearchAsync<T>(this IMongoCollection<T> collection, IClientSessionHandle session, string query, FilterDefinition<T> filter, SortDefinition<T> sort, string scoreProperty, int pageSize, int pageNumber, FindOptions options = null, CancellationToken cancellationToken = default) where T : class
		{
			var searchFluent = collection.CreateSearchFluent(session ?? await collection.StartSessionAsync(cancellationToken).ConfigureAwait(false), query, filter, sort, scoreProperty, null, pageSize, pageNumber, options);
			if (RepositoryMediator.IsTraceEnabled)
				RepositoryMediator.WriteLogs($"Search [{typeof(T).GetTypeName()}]\r\n{searchFluent}");
			return await searchFluent.ToListAsync(cancellationToken).ConfigureAwait(false);
		}

		/// <summary>
		/// Searchs all the matched documents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="query"></param>
		/// <param name="filter"></param>
		/// <param name="sort"></param>
		/// <param name="scoreProperty"></param>
		/// <param name="pageSize"></param>
		/// <param name="pageNumber"></param>
		/// <param name="options">The options</param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public static Task<List<T>> SearchAsync<T>(this IMongoCollection<T> collection, string query, FilterDefinition<T> filter, SortDefinition<T> sort, string scoreProperty, int pageSize, int pageNumber, FindOptions options = null, CancellationToken cancellationToken = default) where T : class
			=> collection.SearchAsync(null, query, filter, sort, scoreProperty, pageSize, pageNumber, options, cancellationToken);

		/// <summary>
		/// Searchs all the matched documents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="query"></param>
		/// <param name="filter"></param>
		/// <param name="sort"></param>
		/// <param name="pageSize"></param>
		/// <param name="pageNumber"></param>
		/// <param name="options">The options</param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public static Task<List<T>> SearchAsync<T>(this IMongoCollection<T> collection, string query, FilterDefinition<T> filter, SortDefinition<T> sort, int pageSize, int pageNumber, FindOptions options = null, CancellationToken cancellationToken = default) where T : class
			=> collection.SearchAsync(query, filter, sort, null, pageSize, pageNumber, options, cancellationToken);

		/// <summary>
		/// Searchs all the matched documents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="query"></param>
		/// <param name="sort"></param>
		/// <param name="scoreProperty"></param>
		/// <param name="pageSize"></param>
		/// <param name="pageNumber"></param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="options">The options</param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public static Task<List<T>> SearchAsync<T>(this IMongoCollection<T> collection, string query, SortDefinition<T> sort, string scoreProperty, int pageSize, int pageNumber, string businessRepositoryEntityID = null, FindOptions options = null, CancellationToken cancellationToken = default) where T : class
			=> collection.SearchAsync(query, NoSqlHelper.CreateFilterDefinition<T>(null, businessRepositoryEntityID), sort, scoreProperty, pageSize, pageNumber, options, cancellationToken);

		/// <summary>
		/// Searchs all the matched documents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source for searching</param>
		/// <param name="query">The text query for searching</param>
		/// <param name="filter">The additional filtering expression</param>
		/// <param name="sort">The additional sorting expression</param>
		/// <param name="pageSize">The size of one page</param>
		/// <param name="pageNumber">The number of the page</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="options">The options</param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public static Task<List<T>> SearchAsync<T>(this RepositoryContext context, DataSource dataSource, string query, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessRepositoryEntityID = null, FindOptions options = null, CancellationToken cancellationToken = default) where T : class
			=> context.GetCollection<T>(dataSource).SearchAsync(context.NoSqlSession, query, NoSqlHelper.CreateFilterDefinition(filter, businessRepositoryEntityID), sort?.GetNoSqlStatement(), null, pageSize, pageNumber, options, cancellationToken);
		#endregion

		#region Search (identities)
		/// <summary>
		/// Searchs all the matched documents and return the collection of identities
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="session">The working session</param>
		/// <param name="query"></param>
		/// <param name="filter"></param>
		/// <param name="sort"></param>
		/// <param name="scoreProperty"></param>
		/// <param name="pageSize"></param>
		/// <param name="pageNumber"></param>
		/// <param name="options">The options</param>
		/// <returns></returns>
		public static List<string> SearchIdentities<T>(this IMongoCollection<T> collection, IClientSessionHandle session, string query, FilterDefinition<T> filter, SortDefinition<T> sort, string scoreProperty, int pageSize, int pageNumber, FindOptions options = null) where T : class
		{
			var searchFluent = collection.CreateSearchFluent(session ?? collection.StartSession(), query, filter, sort, scoreProperty, null, pageSize, pageNumber, options).Project(NoSqlHelper.CreateProjectionDefinition<T>(scoreProperty));
			if (RepositoryMediator.IsTraceEnabled)
				RepositoryMediator.WriteLogs($"Search identities [{typeof(T).GetTypeName()}]\r\n{searchFluent}");
			return searchFluent.ToList().Select(doc => doc["_id"].AsString).ToList();
		}

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
			=> collection.SearchIdentities(null, query, filter, null, null, pageSize, pageNumber);

		/// <summary>
		/// Searchs all the matched documents and return the collection of identities
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source for searching</param>
		/// <param name="query">The text query for searching</param>
		/// <param name="filter">The additional filtering expression</param>
		/// <param name="sort">The additional sorting expression</param>
		/// <param name="pageSize">The size of one page</param>
		/// <param name="pageNumber">The number of the page</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="options">The options</param>
		/// <returns></returns>
		public static List<string> SearchIdentities<T>(this RepositoryContext context, DataSource dataSource, string query, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessRepositoryEntityID = null, FindOptions options = null) where T : class
			=> context.GetCollection<T>(dataSource).SearchIdentities(context.NoSqlSession, query, NoSqlHelper.CreateFilterDefinition(filter, businessRepositoryEntityID), sort?.GetNoSqlStatement(), null, pageSize, pageNumber, options);

		/// <summary>
		/// Searchs all the matched documents and return the collection of identities
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="session">The working session</param>
		/// <param name="scoreProperty"></param>
		/// <param name="query"></param>
		/// <param name="filter"></param>
		/// <param name="pageSize"></param>
		/// <param name="pageNumber"></param>
		/// <param name="options">The options</param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public static async Task<List<string>> SearchIdentitiesAsync<T>(this IMongoCollection<T> collection, IClientSessionHandle session, string query, FilterDefinition<T> filter, SortDefinition<T> sort, string scoreProperty, int pageSize, int pageNumber, FindOptions options = null, CancellationToken cancellationToken = default) where T : class
		{
			var searchFluent = collection.CreateSearchFluent(session ?? await collection.StartSessionAsync(cancellationToken).ConfigureAwait(false), query, filter, sort, scoreProperty, null, pageSize, pageNumber, options).Project(NoSqlHelper.CreateProjectionDefinition<T>(scoreProperty));
			if (RepositoryMediator.IsTraceEnabled)
				RepositoryMediator.WriteLogs($"Search identities [{typeof(T).GetTypeName()}]\r\n{searchFluent}");
			return (await searchFluent.ToListAsync(cancellationToken).ConfigureAwait(false)).Select(doc => doc["_id"].AsString).ToList();
		}

		/// <summary>
		/// Searchs all the matched documents and return the collection of identities
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="query"></param>
		/// <param name="filter"></param>
		/// <param name="pageSize"></param>
		/// <param name="pageNumber"></param>
		/// <param name="options">The options</param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public static Task<List<string>> SearchIdentitiesAsync<T>(this IMongoCollection<T> collection, string query, FilterDefinition<T> filter, int pageSize, int pageNumber, FindOptions options = null, CancellationToken cancellationToken = default) where T : class
			=> collection.SearchIdentitiesAsync(null, query, filter, null, null, pageSize, pageNumber, options, cancellationToken);

		/// <summary>
		/// Searchs all the matched documents and return the collection of identities
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source for searching</param>
		/// <param name="query">The text query for searching</param>
		/// <param name="filter">The additional filtering expression</param>
		/// <param name="sort">The additional sorting expression</param>
		/// <param name="pageSize">The size of one page</param>
		/// <param name="pageNumber">The number of the page</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="options">The options</param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public static Task<List<string>> SearchIdentitiesAsync<T>(this RepositoryContext context, DataSource dataSource, string query, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessRepositoryEntityID = null, FindOptions options = null, CancellationToken cancellationToken = default) where T : class
			=> context.GetCollection<T>(dataSource).SearchIdentitiesAsync(context.NoSqlSession, query, NoSqlHelper.CreateFilterDefinition(filter, businessRepositoryEntityID), sort?.GetNoSqlStatement(), null, pageSize, pageNumber, options, cancellationToken);
		#endregion

		#region Count (searching)
		/// <summary>
		/// Counts the number of all the matched documents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="session">The working session</param>
		/// <param name="query">The text query for counting</param>
		/// <param name="filter">The additional filter-by expression for counting</param>
		/// <param name="options">The options</param>
		/// <returns></returns>
		public static long Count<T>(this IMongoCollection<T> collection, IClientSessionHandle session, string query, FilterDefinition<T> filter, CountOptions options = null) where T : class
		{
			var filterDefinition = filter != null && !filter.Equals(Builders<T>.Filter.Empty) ? query.CreateFilterDefinition<T>() & filter : query.CreateFilterDefinition<T>();
			if (RepositoryMediator.IsTraceEnabled)
				RepositoryMediator.WriteLogs($"Count [{typeof(T).GetTypeName()}]\r\n{filterDefinition.Render(collection.DocumentSerializer, collection.Settings.SerializerRegistry)}");
			return collection.CountDocuments(session ?? collection.StartSession(), filterDefinition, options);
		}

		/// <summary>
		/// Counts the number of all the matched documents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="query">The text query for counting</param>
		/// <param name="filter">The additional filter-by expression for counting</param>
		/// <param name="options">The options</param>
		/// <returns></returns>
		public static long Count<T>(this IMongoCollection<T> collection, string query, FilterDefinition<T> filter, CountOptions options = null) where T : class
			=> collection.Count(null, query, filter, options);

		/// <summary>
		/// Counts the number of all the matched documents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source for counting</param>
		/// <param name="query">The text query for counting</param>
		/// <param name="filter">The additional filter-by expression for counting</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="options">The options</param>
		/// <returns></returns>
		public static long Count<T>(this RepositoryContext context, DataSource dataSource, string query, IFilterBy<T> filter = null, string businessRepositoryEntityID = null, CountOptions options = null) where T : class
			=> context.GetCollection<T>(dataSource).Count(context.NoSqlSession, query, NoSqlHelper.CreateFilterDefinition(filter, businessRepositoryEntityID), options);

		/// <summary>
		/// Counts the number of all the matched documents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="session">The working session</param>
		/// <param name="query">The text query for counting</param>
		/// <param name="filter">The additional filter-by expression for counting</param>
		/// <param name="options">The options</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<long> CountAsync<T>(this IMongoCollection<T> collection, IClientSessionHandle session, string query, FilterDefinition<T> filter, CountOptions options = null, CancellationToken cancellationToken = default) where T : class
		{
			var filterDefinition = filter != null && !filter.Equals(Builders<T>.Filter.Empty) ? query.CreateFilterDefinition<T>() & filter : query.CreateFilterDefinition<T>();
			if (RepositoryMediator.IsTraceEnabled)
				RepositoryMediator.WriteLogs($"Count [{typeof(T).GetTypeName()}]\r\n{filterDefinition.Render(collection.DocumentSerializer, collection.Settings.SerializerRegistry)}");
			return await collection.CountDocumentsAsync(session ?? await collection.StartSessionAsync(cancellationToken).ConfigureAwait(false), filterDefinition, options, cancellationToken).ConfigureAwait(false);
		}

		/// <summary>
		/// Counts the number of all the matched documents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="query">The text query for counting</param>
		/// <param name="filter">The additional filter-by expression for counting</param>
		/// <param name="options">The options</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<long> CountAsync<T>(this IMongoCollection<T> collection, string query, FilterDefinition<T> filter, CountOptions options = null, CancellationToken cancellationToken = default) where T : class
			=> collection.CountAsync(null, query, filter, options, cancellationToken);

		/// <summary>
		/// Counts the number of all the matched documents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source for counting</param>
		/// <param name="query">The text query for counting</param>
		/// <param name="filter">The additional filter-by expression for counting</param>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="options">The options</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<long> CountAsync<T>(this RepositoryContext context, DataSource dataSource, string query, IFilterBy<T> filter = null, string businessRepositoryEntityID = null, CountOptions options = null, CancellationToken cancellationToken = default) where T : class
			=> context.GetCollection<T>(dataSource).CountAsync(context.NoSqlSession, query, NoSqlHelper.CreateFilterDefinition(filter, businessRepositoryEntityID), options, cancellationToken);
		#endregion

		#region Schemas & Indexes
		internal static async Task EnsureIndexesAsync(this EntityDefinition definition, DataSource dataSource, Action<string, Exception> tracker = null, CancellationToken cancellationToken = default)
		{
			// prepare
			var prefix = "IDX_" + definition.CollectionName;
			var normalIndexes = new Dictionary<string, List<AttributeInfo>>(StringComparer.OrdinalIgnoreCase)
			{
				{ prefix, new List<AttributeInfo>() }
			};
			var uniqueIndexes = new Dictionary<string, List<AttributeInfo>>(StringComparer.OrdinalIgnoreCase);

			// sortables
			definition.Attributes.Where(attribute => attribute.IsSortable()).ForEach(attribute =>
			{
				var sortInfo = attribute.GetCustomAttribute<SortableAttribute>();
				if (!string.IsNullOrWhiteSpace(sortInfo.UniqueIndexName))
				{
					var name = $"{prefix}_{sortInfo.UniqueIndexName}";
					if (uniqueIndexes.TryGetValue(name, out var indexes))
						indexes.Add(attribute);
					else
						uniqueIndexes.Add(name, new List<AttributeInfo> { attribute });

					if (!string.IsNullOrWhiteSpace(sortInfo.IndexName))
					{
						name = $"{prefix}_{sortInfo.IndexName}";
						if (normalIndexes.TryGetValue(name, out indexes))
							indexes.Add(attribute);
						else
							normalIndexes.Add(name, new List<AttributeInfo> { attribute });
					}
				}
				else
				{
					var name = prefix + (string.IsNullOrWhiteSpace(sortInfo.IndexName) ? "" : $"_{sortInfo.IndexName}");
					if (normalIndexes.TryGetValue(name, out var indexes))
						indexes.Add(attribute);
					else
						normalIndexes.Add(name, new List<AttributeInfo> { attribute });
				}
			});

			// mappings
			definition.Attributes.Where(attribute => !attribute.IsSortable() && (attribute.IsMappings() || attribute.IsParentMapping())).ForEach(attribute =>
			{
				var name = $"{prefix}_{attribute.Name}";
				if (normalIndexes.TryGetValue(name, out var indexes))
					indexes.Add(attribute);
				else
					normalIndexes.Add(name, new List<AttributeInfo> { attribute });
			});

			// alias
			definition.Attributes.Where(attribute => attribute.IsAlias()).ForEach(attribute =>
			{
				var aliasProps = attribute.GetCustomAttribute<AliasAttribute>().Properties.ToHashSet(",", true);
				var index = new[] { attribute, definition.Attributes.FirstOrDefault(attr => attr.Name.IsEquals("RepositoryEntityID")) }.Concat(definition.Attributes.Where(attr => aliasProps.Contains(attr.Name))).ToList();
				var name = $"{prefix}_Alias";
				if (uniqueIndexes.TryGetValue(name, out var indexes))
					indexes = indexes.Concat(index).ToList();
				else
					uniqueIndexes.Add(name, index);
			});

			// text indexes
			var textIndexes = definition.Searchable
				? definition.Attributes.Where(attribute => attribute.IsSearchable()).Select(attribute => string.IsNullOrWhiteSpace(attribute.Column) ? attribute.Name : attribute.Column).ToList()
				: new List<string>();

			// get the collection
			var collection = NoSqlHelper.GetCollection<BsonDocument>(RepositoryMediator.GetConnectionString(dataSource), dataSource.DatabaseName, definition.CollectionName, true);

			// create indexes
			await normalIndexes.Where(kvp => kvp.Value.Count > 0).ForEachAsync(async (kvp, token) =>
			{
				IndexKeysDefinition<BsonDocument> index = null;
				kvp.Value.ForEach(attribute =>
				{
					index = index == null
						? Builders<BsonDocument>.IndexKeys.Ascending(attribute.Name)
						: index.Ascending(attribute.Name);
				});
				try
				{
					await collection.Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(index, new CreateIndexOptions { Name = kvp.Key, Background = true }), null, token).ConfigureAwait(false);
					tracker?.Invoke($"Create index of No SQL successful => {kvp.Key}", null);
					if (tracker == null && RepositoryMediator.IsDebugEnabled)
						RepositoryMediator.WriteLogs($"Create index of No SQL successful => {kvp.Key}", null);
				}
				catch (Exception ex)
				{
					tracker?.Invoke($"Error occurred while creating index of No SQL => {ex.Message}", ex);
					RepositoryMediator.WriteLogs($"Error occurred while creating index of No SQL => {ex.Message}", ex, LogLevel.Error);
				}
			}, cancellationToken, true, false).ConfigureAwait(false);

			await uniqueIndexes.Where(kvp => kvp.Value.Count > 0).ForEachAsync(async (kvp, token) =>
			{
				IndexKeysDefinition<BsonDocument> index = null;
				kvp.Value.ForEach(attribute =>
				{
					index = index == null
						? Builders<BsonDocument>.IndexKeys.Ascending(attribute.Name)
						: index.Ascending(attribute.Name);
				});
				try
				{
					await collection.Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(index, new CreateIndexOptions { Name = kvp.Key, Background = true, Unique = true }), null, token).ConfigureAwait(false);
					tracker?.Invoke($"Create unique index of No SQL successful => {kvp.Key}", null);
					if (tracker == null && RepositoryMediator.IsDebugEnabled)
						RepositoryMediator.WriteLogs($"Create unique index of No SQL successful => {kvp.Key}", null);
				}
				catch (Exception ex)
				{
					tracker?.Invoke($"Error occurred while creating unique index of No SQL => {ex.Message}", ex);
					RepositoryMediator.WriteLogs($"Error occurred while creating unique index of No SQL => {ex.Message}", ex, LogLevel.Error);
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
				try
				{
					await collection.Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(index, new CreateIndexOptions { Name = $"{prefix}_Text_Search", Background = true }), null, cancellationToken).ConfigureAwait(false);
					tracker?.Invoke($"Create text index of No SQL successful => {prefix}_Text_Search", null);
					if (tracker == null && RepositoryMediator.IsDebugEnabled)
						RepositoryMediator.WriteLogs($"Create text index of No SQL successful => {prefix}_Text_Search", null);
				}
				catch (Exception ex)
				{
					tracker?.Invoke($"Error occurred while creating text index of No SQL => {ex.Message}", ex);
					RepositoryMediator.WriteLogs($"Error occurred while creating text index of No SQL => {ex.Message}", ex, LogLevel.Error);
				}
			}

			// create the blank document for ensuring the collection is created
			if (await collection.CountDocumentsAsync(Builders<BsonDocument>.Filter.Empty).ConfigureAwait(false) < 1)
				try
				{
					var @object = definition.Type.CreateInstance() as RepositoryBase;
					@object.ID = UtilityService.BlankUUID;
					await collection.InsertOneAsync(@object.ToBsonDocument(), null, cancellationToken).ContinueWith(async _ =>
					{
						await Task.Delay(456, cancellationToken).ConfigureAwait(false);
						await collection.DeleteOneAsync(Builders<BsonDocument>.Filter.Eq("_id", UtilityService.BlankUUID), null, cancellationToken).ConfigureAwait(false);
					}, cancellationToken, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Current).ConfigureAwait(false);
				}
				catch { }
		}
		#endregion

	}
}