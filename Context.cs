#region Related components
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;

using System.Data;
using System.Data.Common;
using System.Transactions;

using MongoDB.Driver;

using net.vieapps.Components.Utility;
#endregion

namespace net.vieapps.Components.Repository
{
	/// <summary>
	/// Repository working context for holding the transaction and state data while processing
	/// </summary>
	[DebuggerDisplay("Operation = {Operation}, Type = {EntityDefinition.Type.FullName}")]
	public class RepositoryContext : IDisposable
	{

		#region Properties
		/// <summary>
		/// Gets the identity of the context
		/// </summary>
		public string ID { get; internal set; }

		/// <summary>
		/// Gets the operation of the context
		/// </summary>
#if DEBUG
		public RepositoryOperations Operation { get; set; }
#else
		public RepositoryOperations Operation { get; internal set; }
#endif

		/// <summary>
		/// Gets the entity definition of the context
		/// </summary>
#if DEBUG
		public RepositoryEntityDefinition EntityDefinition { get; set; }
#else
		public EntityDefinition EntityDefinition { get; internal set; }
#endif

		/// <summary>
		/// Gets the alias type name of the context (if got alias type name, means working with diffirent data source because the module is alias of other module)
		/// </summary>
#if DEBUG
		public string AliasTypeName { get; set; }
#else
		public string AliasTypeName { get; internal set; }
#endif

		/// <summary>
		/// Gets the exception that got while processing
		/// </summary>
		public Exception Exception { get; internal set; }

		Dictionary<string, Dictionary<string, object>> PreviousStateData { get; set; }
		Dictionary<string, Dictionary<string, object>> CurrentStateData { get; set; }
		TransactionScope Transaction { get; set; }
		#endregion

		#region Constructors
		/// <summary>
		/// Creates new context for working with repositories (with default transaction support - async flow is enabled)
		/// </summary>
		public RepositoryContext() : this(true) { }

		/// <summary>
		/// Creates new context for working with repositories
		/// </summary>
		/// <param name="openTransaction">true to open transaction with default settings (async flow is enabled); false to not</param>
		public RepositoryContext(bool openTransaction)
		{
			this.Initialize();
			if (openTransaction)
				this.Transaction = new TransactionScope(TransactionScopeOption.Required, TransactionScopeAsyncFlowOption.Enabled);
		}

		/// <summary>
		/// Creates new context for working with repositories (with transaction support)
		/// </summary>
		/// <param name="scopeOption"></param>
		public RepositoryContext(TransactionScopeOption scopeOption)
		{
			this.Initialize();
			this.Transaction = new TransactionScope(scopeOption);
		}

		/// <summary>
		/// Creates new working context for working with repositories (with transaction support)
		/// </summary>
		/// <param name="scopeAsyncFlowOption"></param>
		public RepositoryContext(TransactionScopeAsyncFlowOption scopeAsyncFlowOption)
		{
			this.Initialize();
			this.Transaction = new TransactionScope(scopeAsyncFlowOption);
		}

		/// <summary>
		/// Creates new context for working with repositories (with transaction support)
		/// </summary>
		/// <param name="scopeOption"></param>
		/// <param name="scopeAsyncFlowOption"></param>
		public RepositoryContext(TransactionScopeOption scopeOption, TransactionScopeAsyncFlowOption scopeAsyncFlowOption)
		{
			this.Initialize();
			this.Transaction = new TransactionScope(scopeOption, scopeAsyncFlowOption);
		}

		/// <summary>
		/// Creates new context for working with repositories (with transaction support)
		/// </summary>
		/// <param name="scopeOption"></param>
		/// <param name="transactionOptions"></param>
		/// <param name="scopeAsyncFlowOption"></param>
		public RepositoryContext(TransactionScopeOption scopeOption, TransactionOptions transactionOptions, TransactionScopeAsyncFlowOption scopeAsyncFlowOption)
		{
			this.Initialize();
			this.OpenTransaction(scopeOption, transactionOptions, scopeAsyncFlowOption);
		}

		void Initialize()
		{
			this.ID = Utility.Utility.GetUUID();
			this.Operation = RepositoryOperations.Query;
			this.PreviousStateData = new Dictionary<string, Dictionary<string, object>>();
			this.CurrentStateData = new Dictionary<string, Dictionary<string, object>>();
		}
		#endregion

		#region Open/Commit Transaction
		/// <summary>
		/// Opens the transaction (only work if the context is constructed with no transaction)
		/// </summary>
		/// <param name="scopeOption">Scope option</param>
		/// <param name="transactionOptions">Transaction options</param>
		/// <param name="scopeAsyncFlowOption">Async flow option</param>
		public void OpenTransaction(TransactionScopeOption scopeOption, TransactionOptions transactionOptions, TransactionScopeAsyncFlowOption scopeAsyncFlowOption)
		{
			if (this.Transaction == null)
				this.Transaction = new TransactionScope(scopeOption, transactionOptions, scopeAsyncFlowOption);
		}

		/// <summary>
		/// Commits the transaction and marks this context is completed
		/// </summary>
		public void CommitTransaction()
		{
			if (this.Transaction != null)
			{
				if (this.Exception == null)
					this.Transaction.Complete();
				this.Transaction.Dispose();
				this.Transaction = null;
			}
		}
		#endregion

		#region Disposers
		public void Dispose()
		{
			this.CommitTransaction();
			if (this.SqlConnection != null)
			{
				if (this.SqlConnection.State != ConnectionState.Open)
					this.SqlConnection.Close();
				this.SqlConnection.Dispose();
			}
		}

		~RepositoryContext ()
		{
			this.Dispose();
		}
		#endregion

		#region State data
		internal Dictionary<string, object> GetStateData(object @object)
		{
			if (@object == null)
				return new Dictionary<string, object>();

			var stateData = new Dictionary<string, object>();

			@object.GetProperties().ForEach(attribute =>
			{
				if (!attribute.IsIgnored() && attribute.CanRead && attribute.CanWrite)
					stateData.Add(attribute.Name, @object.GetAttributeValue(attribute.Name));
			});

			@object.GetFields().ForEach(attribute =>
			{
				stateData.Add(attribute.Name, @object.GetAttributeValue(attribute.Name));
			});

			return stateData;
		}

		internal Dictionary<string, object> SetPreviousState(object @object, Dictionary<string, object> stateData)
		{
			var key = @object.GetCacheKey(true);
			stateData = stateData != null
				? stateData
				: this.GetStateData(@object);

			if (this.PreviousStateData.ContainsKey(key))
				this.PreviousStateData[key] = stateData;
			else
				this.PreviousStateData.Add(key, stateData);

			return stateData;
		}

		internal Dictionary<string, object> SetPreviousState(object @object)
		{
			return this.SetPreviousState(@object, null);
		}

		/// <summary>
		/// Gets the previous state of the object
		/// </summary>
		/// <param name="object">The object that need to get previous state</param>
		/// <returns></returns>
		public Dictionary<string, object> GetPreviousState(object @object)
		{
			if (@object == null)
				return null;

			var key = @object.GetCacheKey(true);
			return this.PreviousStateData.ContainsKey(key)
				? this.PreviousStateData[key]
				: null;
		}

		internal Dictionary<string, object> SetCurrentState(object @object, Dictionary<string, object> stateData)
		{
			var key = @object.GetCacheKey(true);
			stateData = stateData != null
				? stateData
				: this.GetStateData(@object);

			if (this.CurrentStateData.ContainsKey(key))
				this.CurrentStateData[key] = stateData;
			else
				this.CurrentStateData.Add(key, stateData);

			return stateData;
		}

		internal Dictionary<string, object> SetCurrentState(object @object)
		{
			return this.SetCurrentState(@object, null);
		}

		/// <summary>
		/// Gets current state of the object
		/// </summary>
		/// <param name="object">The object that need to get current state</param>
		/// <returns></returns>
		public Dictionary<string, object> GetCurrentState(object @object)
		{
			if (@object == null)
				return null;

			var key = @object.GetCacheKey(true);
			return this.CurrentStateData.ContainsKey(key)
				? this.CurrentStateData[key]
				: this.SetCurrentState(@object);
		}

		/// <summary>
		/// Finds dirty attributes (means changed attributes)
		/// </summary>
		/// <param name="previousStateData">The previous state</param>
		/// <param name="currentStateData">The current state</param>
		/// <returns>Collection of attributes</returns>
		public HashSet<string> FindDirty(Dictionary<string, object> previousStateData, Dictionary<string, object> currentStateData)
		{
			if (currentStateData == null || currentStateData.Count < 0)
				return new HashSet<string>();

			else if (previousStateData == null || previousStateData.Count < 0)
				return currentStateData.Select(item => item.Key).ToHashSet(false);

			var dirtyAttributes = new HashSet<string>();
			foreach(var currentState in currentStateData)
			{
				var previousState = previousStateData[currentState.Key];
				if (currentState.Value == null)
				{
					if (previousState != null)
						dirtyAttributes.Add(currentState.Key);
				}
				else
				{
					if (previousState == null)
						dirtyAttributes.Add(currentState.Key);
					else if (!currentState.Value.Equals(previousState))
						dirtyAttributes.Add(currentState.Key);
				}
			}

			return dirtyAttributes;
		}
		#endregion

		#region Clone the context for working with event handler
		internal RepositoryContext Clone()
		{
			return new RepositoryContext()
			{
				ID = this.ID,
				AliasTypeName = this.AliasTypeName,
				EntityDefinition = this.EntityDefinition,
				Operation = this.Operation,
				PreviousStateData = this.PreviousStateData,
				CurrentStateData = this.CurrentStateData,
				Exception = this.Exception
			};
		}
		#endregion

		#region Helper for working with NoSQL (MongoDB)
		internal static Dictionary<string, IMongoClient> NoSqlConnections = new Dictionary<string, IMongoClient>();
		internal static Dictionary<string, IMongoDatabase> NoSqlDatabases = new Dictionary<string, IMongoDatabase>();
		internal static Dictionary<string, object> NoSqlCollections = new Dictionary<string, object>();

		/// <summary>
		/// Gets a connection of NoSQL database (MongoDB client)
		/// </summary>
		/// <param name="connectionString">The string that presents the connection string</param>
		/// <returns></returns>
		public static IMongoClient GetNoSqlConnection(string connectionString)
		{
			var key = connectionString.Trim().ToLower().GetMD5();
			var connection = RepositoryContext.NoSqlConnections.ContainsKey(key) ? RepositoryContext.NoSqlConnections[key] : null;
			if (connection == null)
			{
				connection = NoSqlHelper.GetClient(connectionString);
				if (connection != null)
					try
					{
						if (RepositoryContext.NoSqlConnections.ContainsKey(key))
							RepositoryContext.NoSqlConnections[key] = connection;
						else
							RepositoryContext.NoSqlConnections.Add(key, connection);
					}
					catch { }
			}
			return connection;
		}

		/// <summary>
		/// Gets a NoSQL database (MongoDB database)
		/// </summary>
		/// <param name="connectionString">The string that presents the connection string</param>
		/// <param name="databaseName">The string that presents name of database</param>
		/// <returns></returns>
		public static IMongoDatabase GetNoSqlDatabase(string connectionString, string databaseName)
		{
			var key = (databaseName.Trim() + "@" + connectionString.Trim()).ToLower().GetMD5();
			var database = RepositoryContext.NoSqlDatabases.ContainsKey(key) ? RepositoryContext.NoSqlDatabases[key] : null;
			if (database == null)
			{
				database = NoSqlHelper.GetDatabase(RepositoryContext.GetNoSqlConnection(connectionString), databaseName);
				if (database != null)
					try
					{
						if (RepositoryContext.NoSqlDatabases.ContainsKey(key))
							RepositoryContext.NoSqlDatabases[key] = database;
						else
							RepositoryContext.NoSqlDatabases.Add(key, database);
					}
					catch { }
			}
			return database;
		}

		/// <summary>
		/// Gets a collection in NoSQL database (MongoDB collection)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="connectionString">The string that presents the connection string</param>
		/// <param name="databaseName">The string that presents name of database</param>
		/// <param name="collectionName">The string that presents name of collection</param>
		/// <returns></returns>
		public static IMongoCollection<T> GetNoSqlCollection<T>(string connectionString, string databaseName, string collectionName) where T : class
		{
			var key = (collectionName.Trim() + "@" + databaseName.Trim() + "#" + connectionString.Trim()).ToLower().GetMD5();
			var collection = RepositoryContext.NoSqlCollections.ContainsKey(key) ? RepositoryContext.NoSqlCollections[key] as IMongoCollection<T> : null;
			if (collection == null)
			{
				collection = NoSqlHelper.GetCollection<T>(RepositoryContext.GetNoSqlDatabase(connectionString, databaseName), collectionName);
				if (collection != null)
					try
					{
						if (RepositoryContext.NoSqlCollections.ContainsKey(key))
							RepositoryContext.NoSqlCollections[key] = collection;
						else
							RepositoryContext.NoSqlCollections.Add(key, collection);
					}
					catch { }
			}
			return collection;
		}

		/// <summary>
		/// Gets a collection in NoSQL database (MongoDB collection)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="context">The working context</param>
		/// <param name="dataSource">The data source</param>
		/// <returns></returns>
		public static IMongoCollection<T> GetNoSqlCollection<T>(RepositoryContext context, DataSource dataSource) where T : class
		{
			return RepositoryContext.GetNoSqlCollection<T>(RepositoryMediator.GetConnectionString(dataSource), dataSource.DatabaseName, context.EntityDefinition.CollectionName);
		}
		#endregion

		#region Helper for working with SQL
		internal string SqlConnectionStringName { get; set; }
		internal DbConnection SqlConnection { get; set; }

		/// <summary>
		/// Gets the connection of SQL database of a specified data-source
		/// </summary>
		/// <param name="dataSource">The object that presents related information of a data source in SQL database</param>
		/// <param name="providerFactory">The object that presents information of a database provider factory</param>
		/// <returns></returns>
		public DbConnection GetSqlConnection(DataSource dataSource, DbProviderFactory providerFactory = null)
		{
			if (dataSource == null)
				return null;

			else if (this.SqlConnection != null && this.SqlConnectionStringName != null && this.SqlConnectionStringName.Equals(dataSource.ConnectionStringName))
				return this.SqlConnection;

			this.SqlConnection = dataSource != null && dataSource.Mode.Equals(RepositoryModes.SQL)
				? SqlHelper.GetConnection(dataSource, providerFactory)
				: null;

			if (this.SqlConnection != null)
				this.SqlConnectionStringName = dataSource.ConnectionStringName;

			return this.SqlConnection;
		}
		#endregion

	}
}