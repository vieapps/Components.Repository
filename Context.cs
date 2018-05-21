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
	/// Context for holding the transaction and state while processing
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
#if DEBUG || PROCESSLOGS
		public RepositoryOperation Operation { get; set; }
#else
		public RepositoryOperation Operation { get; internal set; }
#endif

		/// <summary>
		/// Gets the entity definition of the context
		/// </summary>
#if DEBUG || PROCESSLOGS
		public EntityDefinition EntityDefinition { get; set; }
#else
		public EntityDefinition EntityDefinition { get; internal set; }
#endif

		/// <summary>
		/// Gets the alias type name of the context (if got alias type name, means working with diffirent data source because the module is alias of other module)
		/// </summary>
#if DEBUG || PROCESSLOGS
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
		/// Creates new context for working with repositories
		/// </summary>
		/// <param name="openTransaction">true to open transaction with default settings (async flow is enabled); false to not</param>
		public RepositoryContext(bool openTransaction = true)
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
			this.ID = Utility.UtilityService.GetUUID();
			this.Operation = RepositoryOperation.Query;
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
					try
					{
						this.Transaction.Complete();
					}
					catch (ObjectDisposedException) { }
					catch (Exception)
					{
						throw;
					}
				this.Transaction.Dispose();
				this.Transaction = null;
			}
		}
		#endregion

		#region State data
		internal Dictionary<string, object> GetStateData(object @object)
		{
			// initialize
			var stateData = new Dictionary<string, object>();

			// standard properties
			@object?.GetProperties()
				.Where(attribute => !attribute.IsIgnored())
				.ForEach(attribute =>
				{
					try
					{
						stateData[attribute.Name] = @object.GetAttributeValue(attribute.Name);
					}
					catch { }
				});

			// standard fields
			@object?.GetFields()
				.Where(attribute => !attribute.IsIgnored())
				.ForEach(attribute =>
				{
					try
					{
						stateData[attribute.Name] = @object.GetAttributeValue(attribute.Name);
					}
					catch { }
				});

			// extended properties
			(@object as IBusinessEntity)?.ExtendedProperties?.ForEach(kvp =>
			{
				try
				{
					stateData[$"ExtendedProperties.{kvp.Key}"] = kvp.Value;
				}
				catch { }
			});

			return stateData;
		}

		internal Dictionary<string, object> SetPreviousState(object @object, Dictionary<string, object> stateData = null)
		{
			stateData = stateData ?? this.GetStateData(@object);
			this.PreviousStateData[@object.GetCacheKey(true)] = stateData;
			return stateData;
		}

		/// <summary>
		/// Gets the previous state of the object
		/// </summary>
		/// <param name="object">The object that need to get previous state</param>
		/// <returns></returns>
		public Dictionary<string, object> GetPreviousState(object @object)
			=> @object != null
				? this.PreviousStateData.TryGetValue(@object.GetCacheKey(true), out Dictionary<string, object> stateData)
					? stateData
					: null
				: null;

		internal Dictionary<string, object> SetCurrentState(object @object, Dictionary<string, object> stateData = null)
		{
			stateData = stateData ?? this.GetStateData(@object);
			this.CurrentStateData[@object.GetCacheKey(true)] = stateData;
			return stateData;
		}

		/// <summary>
		/// Gets current state of the object
		/// </summary>
		/// <param name="object">The object that need to get current state</param>
		/// <returns></returns>
		public Dictionary<string, object> GetCurrentState(object @object)
		{
			var key = @object?.GetCacheKey(true);
			return @object != null
				? this.CurrentStateData.ContainsKey(key)
					? this.CurrentStateData[key]
					: this.SetCurrentState(@object)
				: null;
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

		#region Helper for working with database
		/// <summary>
		/// Gets the connection of SQL database of a specified data-source
		/// </summary>
		/// <param name="dataSource">The object that presents related information of a data source of SQL database</param>
		/// <returns></returns>
		public DbConnection GetSqlConnection(DataSource dataSource)
			=> dataSource?.GetProviderFactory().CreateConnection(dataSource, false);

		/// <summary>
		/// Gets the No SQL collection of a specified data-source
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="dataSource">The object that presents related information of a data source of No SQL database</param>
		/// <returns></returns>
		public IMongoCollection<T> GetNoSqlCollection<T>(DataSource dataSource) where T : class
			=> dataSource != null && dataSource.Mode.Equals(RepositoryMode.NoSQL)
				? this.GetCollection<T>(dataSource)
				: null;

		/// <summary>
		/// Gets the No SQL collection of the primary data-source
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <returns></returns>
		public IMongoCollection<T> GetNoSqlCollection<T>() where T : class
			=> this.GetNoSqlCollection<T>(RepositoryMediator.GetPrimaryDataSource(RepositoryMediator.GetEntityDefinition<T>()));
		#endregion

		#region Disposal
		public void Dispose()
		{
			this.CommitTransaction();
			GC.SuppressFinalize(this);
		}

		~RepositoryContext()
		{
			this.Dispose();
		}
		#endregion

	}
}