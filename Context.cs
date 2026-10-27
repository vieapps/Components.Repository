#region Related components
using System;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Transactions;
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
		public string ID { get; internal set; } = UtilityService.NewUUID;

		/// <summary>
		/// Gets the operation of the context
		/// </summary>
		public RepositoryOperation Operation
		{
			get;
#if DEBUG
			set;
#else
			internal set;
#endif
		}

		/// <summary>
		/// Gets the entity definition of the context
		/// </summary>
		public EntityDefinition EntityDefinition
		{
			get;
#if DEBUG
			set;
#else
			internal set;
#endif
		}

		/// <summary>
		/// Gets the alias type name of the context (if got alias type name, means working with diffirent data source because the module is alias of other module)
		/// </summary>
		public string AliasTypeName
		{
			get;
#if DEBUG
			set;
#else
			internal set;
#endif
		}

		/// <summary>
		/// Gets the state that determines to use transaction or don't use
		/// </summary>
		public bool UseTransaction { get; internal set; } = false;

		/// <summary>
		/// Gets the exception that got while processing
		/// </summary>
		public Exception Exception { get; internal set; }

		internal ConcurrentDictionary<string, Dictionary<string, object>> PreviousStateData { get; set; }

		internal ConcurrentDictionary<string, Dictionary<string, object>> CurrentStateData { get; set; }

		internal TransactionScope SqlTransaction { get; set; }

		internal MongoDB.Driver.IClientSessionHandle NoSqlSession { get; set; }
		#endregion

		#region Prepare
		internal void Prepare(RepositoryOperation operation = RepositoryOperation.Query, EntityDefinition entityDefinition = null, string aliasTypeName = null, MongoDB.Driver.IClientSessionHandle nosqlSession = null)
		{
			this.ID = this.ID ?? UtilityService.NewUUID;
			this.PreviousStateData = this.PreviousStateData ?? new ConcurrentDictionary<string, Dictionary<string, object>>(StringComparer.OrdinalIgnoreCase);
			this.CurrentStateData = this.CurrentStateData ?? new ConcurrentDictionary<string, Dictionary<string, object>>(StringComparer.OrdinalIgnoreCase);
			this.Operation = operation;

			if (entityDefinition != null)
				this.EntityDefinition = entityDefinition;

			if (!string.IsNullOrWhiteSpace(aliasTypeName))
				this.AliasTypeName = aliasTypeName;

			if (this.UseTransaction)
			{
				this.NoSqlSession = this.NoSqlSession ?? nosqlSession;
				this.StartTransaction();
			}
		}

		internal void Prepare<T>(RepositoryOperation operation = RepositoryOperation.Query, MongoDB.Driver.IClientSessionHandle nosqlSession = null) where T : class
			=> this.Prepare(operation, RepositoryMediator.GetEntityDefinition<T>(), null, nosqlSession);
		#endregion

		#region Constructors & Destructors
		/// <summary>
		/// Creates new context for working with repositories
		/// </summary>
		/// <param name="useTransaction">true to use transaction with default settings (async flow is enabled); false to not</param>
		/// <param name="nosqlSession">The client session of NoSQL database</param>
		public RepositoryContext(bool useTransaction = true, MongoDB.Driver.IClientSessionHandle nosqlSession = null) : this(null, useTransaction, nosqlSession) { }

		/// <summary>
		/// Creates new context for working with repositories
		/// </summary>
		/// <param name="useTransaction">true to use transaction with default settings (async flow is enabled); false to not</param>
		/// <param name="nosqlSession">The client session of NoSQL database</param>
		public RepositoryContext(EntityDefinition entityDefinition, bool useTransaction = true, MongoDB.Driver.IClientSessionHandle nosqlSession = null)
		{
			if (entityDefinition != null)
				this.EntityDefinition = entityDefinition;
			this.UseTransaction = useTransaction;
			this.NoSqlSession = nosqlSession;
			this.Prepare();
		}

		public void Dispose()
		{
			this.EndTransaction();
			GC.SuppressFinalize(this);
		}

		~RepositoryContext()
			=> this.Dispose();
		#endregion

		#region Start/Commit/Abort Transaction
		/// <summary>
		/// Starts the transaction
		/// </summary>
		/// <param name="transactionOptions"></param>
		public void StartTransaction(MongoDB.Driver.TransactionOptions transactionOptions = null)
		{
			if (this.UseTransaction)
			{
				this.SqlTransaction = this.SqlTransaction ?? SqlHelper.CreateTransaction();
				this.NoSqlSession?.StartTransaction(transactionOptions);
			}
		}

		/// <summary>
		/// Commits the transaction (indicates that all operations within the context are completed successfully)
		/// </summary>
		public void CommitTransaction()
		{
			if (this.UseTransaction)
			{
				this.NoSqlSession?.CommitTransaction();
				this.SqlTransaction?.Complete();
			}
		}

		/// <summary>
		/// Aborts the transaction
		/// </summary>
		public void AbortTransaction()
		{
			if (this.UseTransaction)
				this.NoSqlSession?.AbortTransaction();
		}

		/// <summary>
		/// Ends the transaction and marks this context is completed
		/// </summary>
		public void EndTransaction()
		{
			try
			{
				if (this.Exception == null)
					this.CommitTransaction();
				else
					this.AbortTransaction();
			}
			catch (ObjectDisposedException) { }
			catch (TransactionAbortedException)
			{
				if (this.Exception != null)
					throw;
			}
			catch (Exception)
			{
				throw;
			}
			finally
			{
				this.SqlTransaction?.Dispose();
				this.SqlTransaction = null;
				this.NoSqlSession?.Dispose();
				this.NoSqlSession = null;
			}
		}
		#endregion

		#region State data
		internal Dictionary<string, object> SetPreviousState<T>(T @object, Dictionary<string, object> stateData = null) where T : class
		{
			stateData = stateData ?? @object.GetStateData();
			this.PreviousStateData[@object.GetCacheKey(true)] = stateData;
			return stateData;
		}

		/// <summary>
		/// Gets the previous state of the object
		/// </summary>
		/// <param name="object">The object that need to get previous state</param>
		/// <returns></returns>
		public Dictionary<string, object> GetPreviousState<T>(T @object) where T : class
			=> @object != null && this.PreviousStateData.TryGetValue(@object.GetCacheKey(true), out var stateData) ? stateData : null;

		internal Dictionary<string, object> SetCurrentState<T>(T @object, Dictionary<string, object> stateData = null) where T : class
		{
			stateData = stateData ?? @object.GetStateData();
			this.CurrentStateData[@object.GetCacheKey(true)] = stateData;
			return stateData;
		}

		/// <summary>
		/// Gets the current state of the object
		/// </summary>
		/// <param name="object">The object that need to get current state</param>
		/// <returns></returns>
		public Dictionary<string, object> GetCurrentState<T>(T @object) where T : class
			=> @object != null
				? this.CurrentStateData.TryGetValue(@object.GetCacheKey(true), out var stateData)
					? stateData
					: this.SetCurrentState(@object)
				: null;

		/// <summary>
		/// Finds the dirty attributes (means the changed attributes)
		/// </summary>
		/// <param name="previousStateData">The previous state</param>
		/// <param name="currentStateData">The current state</param>
		/// <returns>The collection of dirty attributes (means the collection of changed attributes)</returns>
		internal HashSet<string> FindDirty(Dictionary<string, object> previousStateData, Dictionary<string, object> currentStateData)
			=> RepositoryMediator.FindDirty(previousStateData, currentStateData);
		#endregion

	}
}