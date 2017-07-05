#region Related components
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

using MongoDB.Driver;
using MongoDB.Bson;

using net.vieapps.Components.Utility;
#endregion

namespace net.vieapps.Components.Repository
{
	/// <summary>
	/// Collection of methods for working with NoSQL database (MongoDB)
	/// </summary>
	public static class NoSqlHelper
	{

		#region Client & Database
		/// <summary>
		/// Gets a client for working with MongoDB
		/// </summary>
		/// <param name="connectionString"></param>
		/// <returns></returns>
		public static IMongoClient GetClient(string connectionString)
		{
			return !string.IsNullOrWhiteSpace(connectionString)
				? new MongoClient(connectionString)
				: null;
		}

		/// <summary>
		/// Gets a database of MongoDB
		/// </summary>
		/// <param name="mongoClient"></param>
		/// <param name="databaseName"></param>
		/// <param name="databaseSettings"></param>
		/// <returns></returns>
		public static IMongoDatabase GetDatabase(IMongoClient mongoClient, string databaseName, MongoDatabaseSettings databaseSettings)
		{
			return !string.IsNullOrWhiteSpace(databaseName) && mongoClient != null
				? mongoClient.GetDatabase(databaseName, databaseSettings)
				: null;
		}

		/// <summary>
		/// Gets a database of MongoDB
		/// </summary>
		/// <param name="mongoClient"></param>
		/// <param name="databaseName"></param>
		/// <returns></returns>
		public static IMongoDatabase GetDatabase(IMongoClient mongoClient, string databaseName)
		{
			return NoSqlHelper.GetDatabase(mongoClient, databaseName, null);
		}

		/// <summary>
		/// Gets a database of MongoDB
		/// </summary>
		/// <param name="connectionString"></param>
		/// <param name="databaseName"></param>
		/// <param name="databaseSettings"></param>
		/// <returns></returns>
		public static IMongoDatabase GetDatabase(string connectionString, string databaseName, MongoDatabaseSettings databaseSettings)
		{
			return !string.IsNullOrWhiteSpace(databaseName) && !string.IsNullOrWhiteSpace(connectionString)
				? NoSqlHelper.GetDatabase(NoSqlHelper.GetClient(connectionString), databaseName, databaseSettings)
				: null;
		}

		/// <summary>
		/// Gets a database of MongoDB
		/// </summary>
		/// <param name="connectionString"></param>
		/// <param name="databaseName"></param>
		/// <returns></returns>
		public static IMongoDatabase GetDatabase(string connectionString, string databaseName)
		{
			return NoSqlHelper.GetDatabase(databaseName, connectionString, null);
		}
		#endregion

		#region Collection
		/// <summary>
		/// Gets a collection of MongoDB
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="mongoDatabase"></param>
		/// <param name="collectionName"></param>
		/// <param name="collectionSettings"></param>
		/// <returns></returns>
		public static IMongoCollection<T> GetCollection<T>(IMongoDatabase mongoDatabase, string collectionName, MongoCollectionSettings collectionSettings) where T : class
		{
			return mongoDatabase != null && !string.IsNullOrWhiteSpace(collectionName)
				? mongoDatabase.GetCollection<T>(collectionName, collectionSettings)
				: null;
		}

		/// <summary>
		/// Gets a collection of MongoDB
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="mongoDatabase"></param>
		/// <param name="collectionName"></param>
		/// <returns></returns>
		public static IMongoCollection<T> GetCollection<T>(IMongoDatabase mongoDatabase, string collectionName) where T : class
		{
			return NoSqlHelper.GetCollection<T>(mongoDatabase, collectionName, null);
		}

		/// <summary>
		/// Gets a collection of MongoDB
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="connectionString"></param>
		/// <param name="databaseName"></param>
		/// <param name="databaseSettings"></param>
		/// <param name="collectionName"></param>
		/// <param name="collectionSettings"></param>
		/// <returns></returns>
		public static IMongoCollection<T> GetCollection<T>(string connectionString, string databaseName, MongoDatabaseSettings databaseSettings, string collectionName, MongoCollectionSettings collectionSettings) where T : class
		{
			return !string.IsNullOrWhiteSpace(connectionString) && !string.IsNullOrWhiteSpace(databaseName) && !string.IsNullOrWhiteSpace(collectionName)
				? NoSqlHelper.GetCollection<T>(NoSqlHelper.GetDatabase(connectionString, databaseName, databaseSettings), collectionName, collectionSettings)
				: null;
		}

		/// <summary>
		/// Gets a collection of MongoDB
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="connectionString"></param>
		/// <param name="databaseName"></param>
		/// <param name="collectionName"></param>
		/// <returns></returns>
		public static IMongoCollection<T> GetCollection<T>(string connectionString, string databaseName, string collectionName) where T : class
		{
			return NoSqlHelper.GetCollection<T>(connectionString, databaseName, null, collectionName, null);
		}
		#endregion

		#region Create
		/// <summary>
		/// Creates new a document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="object"></param>
		/// <param name="options"></param>
		public static void Create<T>(this IMongoCollection<T> collection, T @object, InsertOneOptions options = null) where T : class
		{
			if (object.ReferenceEquals(@object, null))
				throw new NullReferenceException("Cannot create new because the object is null");

			collection.InsertOne(@object, options);
		}

		/// <summary>
		/// Creates new a document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="object">The object for creating new instance in storage</param>
		/// <param name="options"></param>
		public static void Create<T>(RepositoryContext context, DataSource dataSource, T @object, InsertOneOptions options = null) where T : class
		{
			RepositoryContext.GetNoSqlCollection<T>(context, dataSource).Create(@object, options);
		}

		/// <summary>
		/// Creates new a document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="object"></param>
		/// <param name="options"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public static Task CreateAsync<T>(this IMongoCollection<T> collection, T @object, InsertOneOptions options = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			if (object.ReferenceEquals(@object, null))
				throw new NullReferenceException("Cannot create new because the object is null");

			return collection.InsertOneAsync(@object, options, cancellationToken);
		}

		/// <summary>
		/// Creates new a document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="object">The object for creating new instance in storage</param>
		/// <param name="options"></param>
		/// <param name="cancellationToken"></param>
		public static Task CreateAsync<T>(RepositoryContext context, DataSource dataSource, T @object, InsertOneOptions options = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			return RepositoryContext.GetNoSqlCollection<T>(context, dataSource).CreateAsync(@object, options, cancellationToken);
		}
		#endregion

		#region Get
		/// <summary>
		/// Gets a document (the first matched) and construct an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="filter">The filter-by expression for filtering</param>
		/// <param name="sort">The order-by expression for ordering</param>
		/// <returns></returns>
		public static T Get<T>(this IMongoCollection<T> collection, FilterDefinition<T> filter, SortDefinition<T> sort = null) where T : class
		{
			var objects = collection.Find(filter, sort, 1, 1);
			return !object.ReferenceEquals(objects, null) && objects.Count > 0
				? objects[0]
				: null;
		}

		/// <summary>
		/// Gets a document (the first matched) and construct an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="filter">The filter-by expression for filtering</param>
		/// <param name="sort">The order-by expression for ordering</param>
		/// <returns></returns>
		public static T Get<T>(RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, SortBy<T> sort = null) where T : class
		{
			return RepositoryContext.GetNoSqlCollection<T>(context, dataSource).Get(filter.GetNoSqlStatement(), sort != null ? sort.GetNoSqlStatement() : null);
		}

		/// <summary>
		/// Gets a document and construct an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="id"></param>
		/// <returns></returns>
		public static T Get<T>(this IMongoCollection<T> collection, string id) where T : class
		{
			return !string.IsNullOrWhiteSpace(id)
				? collection.Get(Builders<T>.Filter.Eq("_id", id))
				: null;
		}

		/// <summary>
		/// Gets a document and construct an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="id">The string that presents identity</param>
		/// <returns></returns>
		public static T Get<T>(RepositoryContext context, DataSource dataSource, string id) where T : class
		{
			return !string.IsNullOrWhiteSpace(id)
				? RepositoryContext.GetNoSqlCollection<T>(context, dataSource).Get(id)
				: null;
		}

		/// <summary>
		/// Gets a document (the first matched) and construct an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="filter">The filter-by expression for filtering</param>
		/// <param name="sort">The order-by expression for ordering</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<T> GetAsync<T>(this IMongoCollection<T> collection, FilterDefinition<T> filter, SortDefinition<T> sort = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			var objects = await collection.FindAsync(filter, sort, 1, 1, null, cancellationToken);
			return !object.ReferenceEquals(objects, null) && objects.Count > 0
				? objects[0]
				: null;
		}

		/// <summary>
		/// Gets a document (the first matched) and construct an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="filter">The filter-by expression for filtering</param>
		/// <param name="sort">The order-by expression for ordering</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<T> GetAsync<T>(RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, SortBy<T> sort = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			return RepositoryContext.GetNoSqlCollection<T>(context, dataSource).GetAsync(filter.GetNoSqlStatement(), sort != null ? sort.GetNoSqlStatement() : null, cancellationToken);
		}

		/// <summary>
		/// Gets a document and construct an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="id">The string that presents identity of the object that need to get</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<T> GetAsync<T>(this IMongoCollection<T> collection, string id, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			return !string.IsNullOrWhiteSpace(id)
				? collection.GetAsync(Builders<T>.Filter.Eq("_id", id), null, cancellationToken)
				: Task.FromResult<T>(null);
		}

		/// <summary>
		/// Gets a document and construct an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="id">The string that presents identity of the object that need to get</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<T> GetAsync<T>(RepositoryContext context, DataSource dataSource, string id, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			return RepositoryContext.GetNoSqlCollection<T>(context, dataSource).GetAsync(id, cancellationToken);
		}
		#endregion

		#region Replace
		/// <summary>
		/// Updates the document of an object (using replace method)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="object">The object for updating</param>
		/// <param name="options">The options for updating</param>
		/// <returns></returns>
		public static ReplaceOneResult Replace<T>(this IMongoCollection<T> collection, T @object, UpdateOptions options = null) where T : class
		{
			if (object.ReferenceEquals(@object, null))
				throw new NullReferenceException("Cannot update because the object is null");

			return collection.ReplaceOne(Builders<T>.Filter.Eq("_id", @object.GetEntityID()), @object, options != null ? options : new UpdateOptions() { IsUpsert = true });
		}

		/// <summary>
		/// Updates the document of an object (using replace method)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="object">The object for updating</param>
		/// <param name="options">The options for updating</param>
		/// <returns></returns>
		public static ReplaceOneResult Replace<T>(RepositoryContext context, DataSource dataSource, T @object, UpdateOptions options = null) where T : class
		{
			return RepositoryContext.GetNoSqlCollection<T>(context, dataSource).Replace(@object, options);
		}

		/// <summary>
		/// Updates the document of an object (using replace method)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="object">The object for updating</param>
		/// <param name="options">The options for updating</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<ReplaceOneResult> ReplaceAsync<T>(this IMongoCollection<T> collection, T @object, UpdateOptions options = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			if (object.ReferenceEquals(@object, null))
				throw new NullReferenceException("Cannot update because the object is null");

			return collection.ReplaceOneAsync(Builders<T>.Filter.Eq("_id", @object.GetEntityID()), @object, options != null ? options : new UpdateOptions() { IsUpsert = true }, cancellationToken);
		}

		/// <summary>
		/// Updates the document of an object (using replace method)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="object">The object for updating</param>
		/// <param name="options">The options for updating</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<ReplaceOneResult> ReplaceAsync<T>(RepositoryContext context, DataSource dataSource, T @object, UpdateOptions options = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			return RepositoryContext.GetNoSqlCollection<T>(context, dataSource).ReplaceAsync(@object, options, cancellationToken);
		}
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
		/// Updates the document of an object (update individual attributes instead of replace document)
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
			if (object.ReferenceEquals(@object, null))
				throw new NullReferenceException("Cannot update because the object is null");
			else if (attributes == null || attributes.Count < 1)
				throw new ArgumentException("No attribute to update");

			// get collection of all attributes
			var objAttributes = @object.GetAttributes();

			// check generic of primitive (workaround)
			bool gotGenericPrimitives = false;
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
				collection.ReplaceOne(Builders<T>.Filter.Eq("_id", @object.GetEntityID()), @object, options != null ? options : new UpdateOptions() { IsUpsert = true });
				return;
			}

			// update individually
			UpdateDefinition<T> updater = null;
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
				}
			});
			collection.UpdateOne(Builders<T>.Filter.Eq("_id", @object.GetEntityID()), updater, options);
		}

		/// <summary>
		/// Updates the document of an object (update individual attributes instead of replace document)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="object">The object for updating</param>
		/// <param name="attributes">The collection of attributes for updating individually</param>
		/// <param name="options">The options for updating</param>
		/// <returns></returns>
		public static void Update<T>(RepositoryContext context, DataSource dataSource, T @object, List<string> attributes, UpdateOptions options = null) where T : class
		{
			RepositoryContext.GetNoSqlCollection<T>(context, dataSource).Update(@object, attributes, options);
		}

		/// <summary>
		/// Updates the document of an object (update individual attributes instead of replace document)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="object">The object for updating</param>
		/// <param name="update">The definition for updating</param>
		/// <param name="options">The options for updating</param>
		public static UpdateResult Update<T>(this IMongoCollection<T> collection, T @object, UpdateDefinition<T> update, UpdateOptions options = null) where T : class
		{
			if (object.ReferenceEquals(@object, null))
				throw new NullReferenceException("Cannot update because the object is null");
			else if (update == null)
				throw new ArgumentException("No definition to update");
			return collection.UpdateOne(Builders<T>.Filter.Eq("_id", @object.GetEntityID()), update, options);
		}

		/// <summary>
		/// Updates the document of an object (update individual attributes instead of replace document)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="object">The object for updating</param>
		/// <param name="update">The definition for updating</param>
		/// <param name="options">The options for updating</param>
		/// <returns></returns>
		public static UpdateResult Update<T>(RepositoryContext context, DataSource dataSource, T @object, UpdateDefinition<T> update, UpdateOptions options = null) where T : class
		{
			return RepositoryContext.GetNoSqlCollection<T>(context, dataSource).Update(@object, update, options);
		}

		/// <summary>
		/// Updates the document of an object (update individual attributes instead of replace document)
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
			if (object.ReferenceEquals(@object, null))
				throw new NullReferenceException("Cannot update because the object is null");
			else if (attributes == null || attributes.Count < 1)
				throw new ArgumentException("No attribute to update");

			// get collection of all attributes
			var objAttributes = @object.GetAttributes();

			// check generic of primitive (workaround)
			bool gotGenericPrimitives = false;
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
				await collection.ReplaceOneAsync(Builders<T>.Filter.Eq("_id", @object.GetEntityID()), @object, options != null ? options : new UpdateOptions() { IsUpsert = true }, cancellationToken);
				return;
			}

			// update individually
			UpdateDefinition<T> updater = null;
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
				}
			});

			if (updater != null)
				await collection.UpdateOneAsync(Builders<T>.Filter.Eq("_id", @object.GetEntityID()), updater, options, cancellationToken);
		}

		/// <summary>
		/// Updates the document of an object (update individual attributes instead of replace document)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="object">The object for updating</param>
		/// <param name="attributes">The collection of attributes for updating individually</param>
		/// <param name="options">The options for updating</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task UpdateAsync<T>(RepositoryContext context, DataSource dataSource, T @object, List<string> attributes, UpdateOptions options = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			return RepositoryContext.GetNoSqlCollection<T>(context, dataSource).UpdateAsync(@object, attributes, options, cancellationToken);
		}

		/// <summary>
		/// Updates the document of an object (update individual attributes instead of replace document)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="object">The object for updating</param>
		/// <param name="update">The definition for updating</param>
		/// <param name="options">The options for updating</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<UpdateResult> UpdateAsync<T>(this IMongoCollection<T> collection, T @object, UpdateDefinition<T> update, UpdateOptions options = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			if (object.ReferenceEquals(@object, null))
				throw new NullReferenceException("Cannot update because the object is null");
			else if (update == null)
				throw new ArgumentException("No definition to update");
			return collection.UpdateOneAsync(Builders<T>.Filter.Eq("_id", @object.GetEntityID()), update, options, cancellationToken);
		}

		/// <summary>
		/// Updates the document of an object (update individual attributes instead of replace document)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="object">The object for updating</param>
		/// <param name="update">The definition for updating</param>
		/// <param name="options">The options for updating</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<UpdateResult> UpdateAsync<T>(RepositoryContext context, DataSource dataSource, T @object, UpdateDefinition<T> update, UpdateOptions options = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			return RepositoryContext.GetNoSqlCollection<T>(context, dataSource).UpdateAsync(@object, update, options, cancellationToken);
		}
		#endregion

		#region Delete
		/// <summary>
		/// Delets the document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="id"></param>
		/// <param name="options"></param>
		/// <returns></returns>
		public static DeleteResult Delete<T>(this IMongoCollection<T> collection, string id, DeleteOptions options = null) where T : class
		{
			return !string.IsNullOrWhiteSpace(id)
				? collection.DeleteOne(Builders<T>.Filter.Eq("_id", id), options)
				: null;
		}

		/// <summary>
		/// Delets the document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="id">The identity of the document of an object for deleting</param>
		/// <returns></returns>
		public static DeleteResult Delete<T>(RepositoryContext context, DataSource dataSource, string id, DeleteOptions options = null) where T : class
		{
			return RepositoryContext.GetNoSqlCollection<T>(context, dataSource).Delete(id, options);
		}

		/// <summary>
		/// Delets the document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="id"></param>
		/// <param name="options"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public static Task<DeleteResult> DeleteAsync<T>(this IMongoCollection<T> collection, string id, DeleteOptions options = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			return !string.IsNullOrWhiteSpace(id)
				? collection.DeleteOneAsync(Builders<T>.Filter.Eq("_id", id), options, cancellationToken)
				: Task.FromResult<DeleteResult>(null);
		}

		/// <summary>
		/// Delets the document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="id">The identity of the document of an object for deleting</param>
		/// <returns></returns>
		public static Task<DeleteResult> DeleteAsync<T>(RepositoryContext context, DataSource dataSource, string id, DeleteOptions options = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			return RepositoryContext.GetNoSqlCollection<T>(context, dataSource).DeleteAsync(id, options, cancellationToken);
		}

		/// <summary>
		/// Delets the document of an object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="object"></param>
		/// <param name="options"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public static Task<DeleteResult> DeleteAsync<T>(this IMongoCollection<T> collection, T @object, DeleteOptions options = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			if (object.ReferenceEquals(@object, null))
				throw new NullReferenceException("Cannot delete because the object is null");

			return collection.DeleteAsync(@object.GetEntityID(), options, cancellationToken);
		}

		/// <summary>
		/// Deletes the documents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="filter"></param>
		/// <param name="options"></param>
		/// <returns></returns>
		public static DeleteResult DeleteMany<T>(this IMongoCollection<T> collection, FilterDefinition<T> filter, DeleteOptions options = null) where T : class
		{
			return collection.DeleteMany(filter != null ? filter : Builders<T>.Filter.Empty, options);
		}

		internal static DeleteResult DeleteMany<T>(RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, DeleteOptions options = null) where T : class
		{
			return RepositoryContext.GetNoSqlCollection<T>(context, dataSource).DeleteMany(filter.GetNoSqlStatement(), options);
		}

		/// <summary>
		/// Deletes the documents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="filter"></param>
		/// <param name="options"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public static Task<DeleteResult> DeleteManyAsync<T>(this IMongoCollection<T> collection, FilterDefinition<T> filter, DeleteOptions options = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			return collection.DeleteManyAsync(filter != null ? filter : Builders<T>.Filter.Empty, options, cancellationToken);
		}

		internal static Task<DeleteResult> DeleteManyAsync<T>(RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, DeleteOptions options = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			return RepositoryContext.GetNoSqlCollection<T>(context, dataSource).DeleteManyAsync(filter.GetNoSqlStatement(), options, cancellationToken);
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
		/// <param name="options">The options for finding</param>
		/// <returns></returns>
		public static List<BsonDocument> Select<T>(this IMongoCollection<T> collection, IEnumerable<string> attributes, FilterDefinition<T> filter, SortDefinition<T> sort, int pageSize, int pageNumber, FindOptions options = null) where T : class
		{
			ProjectionDefinition<T> projection = null;
			if (attributes == null)
				projection = Builders<T>.Projection.Include("_id");
			else
				attributes.ForEach(attribute =>
				{
					projection = projection == null
						? Builders<T>.Projection.Include(attribute)
						: projection.Include(attribute);
				});

			var results = collection
				.Find(filter != null ? filter : Builders<T>.Filter.Empty, options)
				.Sort(sort != null ? sort : Builders<T>.Sort.Ascending("_id"));

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
		/// <param name="options">The options for finding</param>
		/// <returns></returns>
		public static List<BsonDocument> Select<T>(RepositoryContext context, DataSource dataSource, IEnumerable<string> attributes, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, FindOptions options = null) where T : class
		{
			return RepositoryContext.GetNoSqlCollection<T>(context, dataSource).Select(attributes, filter != null ? filter.GetNoSqlStatement() : null, sort != null ? sort.GetNoSqlStatement() : null, pageSize, pageNumber, options);
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
		/// <param name="options">The options for finding</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<List<BsonDocument>> SelectAsync<T>(this IMongoCollection<T> collection, IEnumerable<string> attributes, FilterDefinition<T> filter, SortDefinition<T> sort, int pageSize, int pageNumber, FindOptions options = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			ProjectionDefinition<T> projection = null;
			if (attributes == null)
				projection = Builders<T>.Projection.Include("_id");
			else
				attributes.ForEach(attribute =>
				{
					projection = projection == null
						? Builders<T>.Projection.Include(attribute)
						: projection.Include(attribute);
				});

			var results = collection
				.Find(filter != null ? filter : Builders<T>.Filter.Empty, options)
				.Sort(sort != null ? sort : Builders<T>.Sort.Ascending("_id"));

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
		/// <param name="options">The options for finding</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<List<BsonDocument>> SelectAsync<T>(RepositoryContext context, DataSource dataSource, IEnumerable<string> attributes, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, FindOptions options = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			return RepositoryContext.GetNoSqlCollection<T>(context, dataSource).SelectAsync(attributes, filter != null ? filter.GetNoSqlStatement() : null, sort != null ? sort.GetNoSqlStatement() : null, pageSize, pageNumber, options);
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
		/// <param name="options">The options for finding</param>
		/// <returns></returns>
		public static List<string> SelectIdentities<T>(this IMongoCollection<T> collection, FilterDefinition<T> filter, SortDefinition<T> sort, int pageSize, int pageNumber, FindOptions options = null) where T : class
		{
			return collection.Select(null, filter, sort, pageSize, pageNumber, options)
				.Select(doc => doc["_id"].AsString)
				.ToList();
		}

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
		/// <param name="options">The options for finding</param>
		/// <returns></returns>
		public static List<string> SelectIdentities<T>(RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, FindOptions options = null) where T : class
		{
			return RepositoryContext.GetNoSqlCollection<T>(context, dataSource).SelectIdentities(filter != null ? filter.GetNoSqlStatement() : null, sort != null ? sort.GetNoSqlStatement() : null, pageSize, pageNumber, options);
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
		/// <param name="options">The options for finding</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static async Task<List<string>> SelectIdentitiesAsync<T>(this IMongoCollection<T> collection, FilterDefinition<T> filter, SortDefinition<T> sort, int pageSize, int pageNumber, FindOptions options = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			return (await collection.SelectAsync(null, filter, sort, pageSize, pageNumber, options))
				.Select(doc => doc["_id"].AsString)
				.ToList();
		}

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
		/// <param name="options">The options for finding</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<List<string>> SelectIdentitiesAsync<T>(RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, FindOptions options = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			return RepositoryContext.GetNoSqlCollection<T>(context, dataSource).SelectIdentitiesAsync(filter != null ? filter.GetNoSqlStatement() : null, sort != null ? sort.GetNoSqlStatement() : null, pageSize, pageNumber, options);
		}
		#endregion

		#region Find
		/// <summary>
		/// Finds all the matched documents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="filter">The filter-by expression for filtering</param>
		/// <param name="sort">The order-by expression for ordering</param>
		/// <param name="pageSize">The size of one page</param>
		/// <param name="pageNumber">The number of page</param>
		/// <param name="options">The options for finding</param>
		/// <returns></returns>
		public static List<T> Find<T>(this IMongoCollection<T> collection, FilterDefinition<T> filter, SortDefinition<T> sort, int pageSize, int pageNumber, FindOptions options = null) where T : class
		{
			var results = collection.Find(filter != null ? filter : Builders<T>.Filter.Empty, options)
				.Sort(sort != null ? sort : Builders<T>.Sort.Ascending("_id"));

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
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="filter">The filter-by expression for filtering</param>
		/// <param name="sort">The order-by expression for ordering</param>
		/// <param name="pageSize">The size of one page</param>
		/// <param name="pageNumber">The number of page</param>
		/// <param name="options">The options for finding</param>
		/// <returns></returns>
		public static List<T> Find<T>(RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, FindOptions options = null) where T : class
		{
			return RepositoryContext.GetNoSqlCollection<T>(context, dataSource).Find(filter != null ? filter.GetNoSqlStatement() : null, sort != null ? sort.GetNoSqlStatement() : null, pageSize, pageNumber, options);
		}

		/// <summary>
		/// Finds all the documents that specified by identity
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="identities">The collection of identities for finding</param>
		/// <param name="sort">The order-by expression for ordering</param>
		/// <param name="options">The options for finding</param>
		/// <returns></returns>
		public static List<T> Find<T>(RepositoryContext context, DataSource dataSource, List<string> identities, SortBy<T> sort = null, FindOptions options = null) where T : class
		{
			if (identities == null || identities.Count < 1)
				return new List<T>();

			var filter = Filters.Or<T>();
			identities.ForEach(id =>
			{
				filter.Add(Filters.Equals<T>("ID", id));
			});

			return NoSqlHelper.Find(context, dataSource, filter, sort, 0, 1, options);
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
		/// <param name="options">The options for finding</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<List<T>> FindAsync<T>(this IMongoCollection<T> collection, FilterDefinition<T> filter, SortDefinition<T> sort, int pageSize, int pageNumber, FindOptions options = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			var results = collection.Find(filter != null ? filter : Builders<T>.Filter.Empty, options)
				.Sort(sort != null ? sort : Builders<T>.Sort.Ascending("_id"));

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
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="filter">The filter-by expression for filtering</param>
		/// <param name="sort">The order-by expression for ordering</param>
		/// <param name="pageSize">The size of one page</param>
		/// <param name="pageNumber">The number of page</param>
		/// <param name="options">The options for finding</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<List<T>> FindAsync<T>(RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, FindOptions options = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			return RepositoryContext.GetNoSqlCollection<T>(context, dataSource).FindAsync(filter != null ? filter.GetNoSqlStatement() : null, sort != null ? sort.GetNoSqlStatement() : null, pageSize, pageNumber, options, cancellationToken);
		}

		/// <summary>
		/// Finds all the documents that specified by identity
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <param name="identities">The collection of identities for finding</param>
		/// <param name="sort">The order-by expression for ordering</param>
		/// <param name="options">The options for finding</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<List<T>> FindAsync<T>(RepositoryContext context, DataSource dataSource, List<string> identities, SortBy<T> sort = null, FindOptions options = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			if (identities == null || identities.Count < 1)
				return Task.FromResult<List<T>>(new List<T>());

			var filter = Filters.Or<T>();
			identities.ForEach(id =>
			{
				filter.Add(Filters.Equals<T>("ID", id));
			});

			return NoSqlHelper.FindAsync(context, dataSource, filter, sort, 0, 1, options);
		}
		#endregion

		#region Search
		/// <summary>
		/// Creates a text-search filter
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="query">The expression for searching documents</param>
		/// <returns></returns>
		public static FilterDefinition<T> CreateTextSearchFilter<T>(string query) where T : class
		{
			var searchQuery = Utility.SearchQuery.Parse(query);
			var search = "";

			searchQuery.AndWords.ForEach(word =>
			{
				search += (!search.Equals("") ? " " : "") + word;
			});

			searchQuery.OrWords.ForEach(word =>
			{
				search += (!search.Equals("") ? " " : "") + word;
			});

			searchQuery.NotWords.ForEach(word =>
			{
				search += (!search.Equals("") ? " " : "") + "-" + word;
			});

			searchQuery.AndPhrases.ForEach(phrase =>
			{
				search += (!search.Equals("") ? " " : "") + "\"" + phrase + "\"";
			});

			searchQuery.OrPhrases.ForEach(phrase =>
			{
				search += (!search.Equals("") ? " " : "") + "\"" + phrase + "\"";
			});

			searchQuery.NotPhrases.ForEach(phrase =>
			{
				search += (!search.Equals("") ? " " : "") + "-" + "\"" + phrase + "\"";
			});

			return Builders<T>.Filter.Text(search, new TextSearchOptions() { CaseSensitive = false });
		}

		/// <summary>
		/// Searchs all the matched documents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="query"></param>
		/// <param name="scoreProperty"></param>
		/// <param name="otherFilters"></param>
		/// <param name="pageSize"></param>
		/// <param name="pageNumber"></param>
		/// <param name="options"></param>
		/// <returns></returns>
		public static List<T> Search<T>(this IMongoCollection<T> collection, string query, string scoreProperty, FilterDefinition<T> otherFilters, int pageSize, int pageNumber, FindOptions options = null) where T : class
		{
			var filter = NoSqlHelper.CreateTextSearchFilter<T>(query);
			if (otherFilters != null && !otherFilters.Equals(Builders<T>.Filter.Empty))
				filter = filter & otherFilters;

			var results = collection
				.Find(filter, options)
				.Sort(Builders<T>.Sort.MetaTextScore(scoreProperty));

			if (pageSize > 0)
			{
				if (pageNumber > 1)
					results = results.Skip((pageNumber - 1) * pageSize);
				results = results.Limit(pageSize);
			}

			return results.Project<T>(Builders<T>.Projection.MetaTextScore(scoreProperty)).ToList();
		}

		/// <summary>
		/// Searchs all the matched documents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="query"></param>
		/// <param name="otherFilters"></param>
		/// <param name="pageSize"></param>
		/// <param name="pageNumber"></param>
		/// <param name="options"></param>
		/// <returns></returns>
		public static List<T> Search<T>(this IMongoCollection<T> collection, string query, FilterDefinition<T> otherFilters, int pageSize, int pageNumber, FindOptions options = null) where T : class
		{
			return collection.Search(query, "SearchScore", otherFilters, pageSize, pageNumber, options);
		}

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
		/// <param name="options"></param>
		/// <returns></returns>
		public static List<T> Search<T>(RepositoryContext context, DataSource dataSource, string query, IFilterBy<T> filter, int pageSize, int pageNumber, FindOptions options = null) where T : class
		{
			return RepositoryContext.GetNoSqlCollection<T>(context, dataSource).Search(query, filter != null ? filter.GetNoSqlStatement() : null, pageSize, pageNumber, options);
		}

		/// <summary>
		/// Searchs all the matched documents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="query"></param>
		/// <param name="scoreProperty"></param>
		/// <param name="otherFilters"></param>
		/// <param name="pageSize"></param>
		/// <param name="pageNumber"></param>
		/// <param name="options"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public static Task<List<T>> SearchAsync<T>(this IMongoCollection<T> collection, string query, string scoreProperty, FilterDefinition<T> otherFilters, int pageSize, int pageNumber, FindOptions options = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			var filter = NoSqlHelper.CreateTextSearchFilter<T>(query);
			if (otherFilters != null && !otherFilters.Equals(Builders<T>.Filter.Empty))
				filter = filter & otherFilters;

			var results = collection
				.Find(filter, options)
				.Sort(Builders<T>.Sort.MetaTextScore(scoreProperty));

			if (pageSize > 0)
			{
				if (pageNumber > 1)
					results = results.Skip((pageNumber - 1) * pageSize);
				results = results.Limit(pageSize);
			}

			return results.Project<T>(Builders<T>.Projection.MetaTextScore(scoreProperty)).ToListAsync(cancellationToken);
		}

		/// <summary>
		/// Searchs all the matched documents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="query"></param>
		/// <param name="scoreProperty"></param>
		/// <param name="pageSize"></param>
		/// <param name="pageNumber"></param>
		/// <param name="options"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public static Task<List<T>> SearchAsync<T>(this IMongoCollection<T> collection, string query, string scoreProperty, int pageSize, int pageNumber, FindOptions options = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			return collection.SearchAsync<T>(query, scoreProperty, null, pageSize, pageNumber, options, cancellationToken);
		}

		/// <summary>
		/// Searchs all the matched documents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="query"></param>
		/// <param name="otherFilters"></param>
		/// <param name="pageSize"></param>
		/// <param name="pageNumber"></param>
		/// <param name="options"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public static Task<List<T>> SearchAsync<T>(this IMongoCollection<T> collection, string query, FilterDefinition<T> otherFilters, int pageSize, int pageNumber, FindOptions options = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			return collection.SearchAsync(query, "SearchScore", otherFilters, pageSize, pageNumber, options, cancellationToken);
		}

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
		/// <param name="options"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public static Task<List<T>> SearchAsync<T>(RepositoryContext context, DataSource dataSource, string query, IFilterBy<T> filter, int pageSize, int pageNumber, FindOptions options = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			return RepositoryContext.GetNoSqlCollection<T>(context, dataSource).SearchAsync(query, filter != null ? filter.GetNoSqlStatement() : null, pageSize, pageNumber, options, cancellationToken);
		}

		/// <summary>
		/// Searchs all the matched documents and return the collection of identities
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="query"></param>
		/// <param name="otherFilters"></param>
		/// <param name="pageSize"></param>
		/// <param name="pageNumber"></param>
		/// <param name="options"></param>
		/// <returns></returns>
		public static List<string> SearchIdentities<T>(this IMongoCollection<T> collection, string query, FilterDefinition<T> otherFilters, int pageSize, int pageNumber, FindOptions options = null) where T : class
		{
			var filter = NoSqlHelper.CreateTextSearchFilter<T>(query);
			if (otherFilters != null && !otherFilters.Equals(Builders<T>.Filter.Empty))
				filter = filter & otherFilters;

			var results = collection
				.Find(filter, options)
				.Sort(Builders<T>.Sort.MetaTextScore("SearchScore"));

			if (pageSize > 0)
			{
				if (pageNumber > 1)
					results = results.Skip((pageNumber - 1) * pageSize);
				results = results.Limit(pageSize);
			}

			return results.Project(Builders<T>.Projection.MetaTextScore("SearchScore"))
				.ToList()
				.Select(doc => doc["_id"].AsString)
				.ToList();
		}

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
		/// <param name="options"></param>
		/// <returns></returns>
		public static List<string> SearchIdentities<T>(RepositoryContext context, DataSource dataSource, string query, IFilterBy<T> filter, int pageSize, int pageNumber, FindOptions options = null) where T : class
		{
			return RepositoryContext.GetNoSqlCollection<T>(context, dataSource).SearchIdentities(query, filter != null ? filter.GetNoSqlStatement() : null, pageSize, pageNumber, options);
		}

		/// <summary>
		/// Searchs all the matched documents and return the collection of identities
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="query"></param>
		/// <param name="otherFilters"></param>
		/// <param name="pageSize"></param>
		/// <param name="pageNumber"></param>
		/// <param name="options"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public static async Task<List<string>> SearchIdentitiesAsync<T>(this IMongoCollection<T> collection, string query, FilterDefinition<T> otherFilters, int pageSize, int pageNumber, FindOptions options = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			var filter = NoSqlHelper.CreateTextSearchFilter<T>(query);
			if (otherFilters != null && !otherFilters.Equals(Builders<T>.Filter.Empty))
				filter = filter & otherFilters;

			var results = collection
				.Find(filter, options)
				.Sort(Builders<T>.Sort.MetaTextScore("SearchScore"));

			if (pageSize > 0)
			{
				if (pageNumber > 1)
					results = results.Skip((pageNumber - 1) * pageSize);
				results = results.Limit(pageSize);
			}

			return (await results.Project(Builders<T>.Projection.MetaTextScore("SearchScore")).ToListAsync(cancellationToken))
				.Select(doc => doc["_id"].AsString)
				.ToList();
		}

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
		/// <param name="options"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public static Task<List<string>> SearchIdentitiesAsync<T>(RepositoryContext context, DataSource dataSource, string query, IFilterBy<T> filter, int pageSize, int pageNumber, FindOptions options = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			return RepositoryContext.GetNoSqlCollection<T>(context, dataSource).SearchIdentitiesAsync(query, filter != null ? filter.GetNoSqlStatement() : null, pageSize, pageNumber, options, cancellationToken);
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
		/// <param name="options">The options for counting</param>
		/// <returns></returns>
		public static long Count<T>(RepositoryContext context, DataSource dataSource, IFilterBy<T> filter = null, CountOptions options = null) where T : class
		{
			return RepositoryContext.GetNoSqlCollection<T>(context, dataSource).Count(filter != null ? filter.GetNoSqlStatement() : Builders<T>.Filter.Empty, options);
		}

		/// <summary>
		/// Counts the number of all the matched documents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="query"></param>
		/// <param name="otherFilters"></param>
		/// <param name="options">The options for counting</param>
		/// <returns></returns>
		public static long Count<T>(this IMongoCollection<T> collection, string query, FilterDefinition<T> otherFilters, CountOptions options = null) where T : class
		{
			var filter = NoSqlHelper.CreateTextSearchFilter<T>(query);
			if (otherFilters != null && !otherFilters.Equals(Builders<T>.Filter.Empty))
				filter = filter & otherFilters;
			return collection.Count(filter, options);
		}

		/// <summary>
		/// Counts the number of all the matched documents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source for counting</param>
		/// <param name="query">The text query for counting</param>
		/// <param name="filter">The filter-by expression for counting</param>
		/// <param name="options">The options for counting</param>
		/// <returns></returns>
		public static long Count<T>(RepositoryContext context, DataSource dataSource, string query, IFilterBy<T> filter = null, CountOptions options = null) where T : class
		{
			return RepositoryContext.GetNoSqlCollection<T>(context, dataSource).Count(query, filter != null ? filter.GetNoSqlStatement() : null, options);
		}

		/// <summary>
		/// Counts the number of all the matched documents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source for counting</param>
		/// <param name="filter">The filter-by expression for counting</param>
		/// <param name="options">The options for counting</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<long> CountAsync<T>(RepositoryContext context, DataSource dataSource, IFilterBy<T> filter, CountOptions options = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			return RepositoryContext.GetNoSqlCollection<T>(context, dataSource).CountAsync(filter != null ? filter.GetNoSqlStatement() : Builders<T>.Filter.Empty, options, cancellationToken);
		}

		/// <summary>
		/// Counts the number of all the matched documents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="query"></param>
		/// <param name="otherFilters"></param>
		/// <param name="options">The options for counting</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<long> CountAsync<T>(this IMongoCollection<T> collection, string query, FilterDefinition<T> otherFilters, CountOptions options = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			var filter = NoSqlHelper.CreateTextSearchFilter<T>(query);
			if (otherFilters != null && !otherFilters.Equals(Builders<T>.Filter.Empty))
				filter = filter & otherFilters;
			return collection.CountAsync(filter, options, cancellationToken);
		}

		/// <summary>
		/// Counts the number of all the matched documents
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source for counting</param>
		/// <param name="query">The text query for counting</param>
		/// <param name="filter">The filter-by expression for counting</param>
		/// <param name="options">The options for counting</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<long> CountAsync<T>(RepositoryContext context, DataSource dataSource, string query, IFilterBy<T> filter = null, CountOptions options = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
		{
			return RepositoryContext.GetNoSqlCollection<T>(context, dataSource).CountAsync(query, filter != null ? filter.GetNoSqlStatement() : null, options, cancellationToken);
		}
		#endregion

	}
}