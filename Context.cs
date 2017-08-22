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
	/// Working context for holding the transaction and state while processing
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
		public RepositoryOperation Operation { get; set; }
#else
		public RepositoryOperation Operation { get; internal set; }
#endif

		/// <summary>
		/// Gets the entity definition of the context
		/// </summary>
#if DEBUG
		public EntityDefinition EntityDefinition { get; set; }
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

			// standard properties
			@object.GetProperties().ForEach(attribute =>
			{
				if (!attribute.IsIgnored() && attribute.CanRead && attribute.CanWrite)
					stateData.Add(attribute.Name, @object.GetAttributeValue(attribute.Name));
			});

			// standard fields
			@object.GetFields().ForEach(attribute =>
			{
				stateData.Add(attribute.Name, @object.GetAttributeValue(attribute.Name));
			});

			// extended properties
			if (@object is IBusinessEntity && (@object as IBusinessEntity).ExtendedProperties != null)
				(@object as IBusinessEntity).ExtendedProperties.ForEach(info =>
				{
					stateData.Add("ExtendedProperties." + info.Key, info.Value);
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

		#region Clone the context (for working with event handlers)
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

		#region Helper for working with database
		/// <summary>
		/// Gets the connection of SQL database of a specified data-source
		/// </summary>
		/// <param name="dataSource">The object that presents related information of a data source of SQL database</param>
		/// <returns></returns>
		public DbConnection GetSqlConnection(DataSource dataSource)
		{
			return dataSource != null
				? SqlHelper.GetProviderFactory(dataSource).CreateConnection(dataSource)
				: null;
		}

		/// <summary>
		/// Gets the No SQL collection of a specified data-source
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="dataSource">The object that presents related information of a data source of No SQL database</param>
		/// <returns></returns>
		public IMongoCollection<T> GetNoSqlCollection<T>(DataSource dataSource) where T : class
		{
			return dataSource != null && dataSource.Mode.Equals(RepositoryMode.NoSQL)
				? this.GetCollection<T>(dataSource)
				: null;
		}

		/// <summary>
		/// Gets the No SQL collection of the primary data-source
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <returns></returns>
		public IMongoCollection<T> GetNoSqlCollection<T>() where T : class
		{
			return this.GetNoSqlCollection<T>(RepositoryMediator.GetPrimaryDataSource(RepositoryMediator.GetEntityDefinition<T>()));
		}
		#endregion

	}
}