#region Related components
using System;
using System.Linq;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Configuration;
using System.Collections.Generic;
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
		/// Gets the state that determines to use transaction or don't use
		/// </summary>
		public bool UseTransaction { get; internal set; } = false;

		/// <summary>
		/// Gets the exception that got while processing
		/// </summary>
		public Exception Exception { get; internal set; }

		internal Dictionary<string, Dictionary<string, object>> PreviousStateData { get; set; }

		internal Dictionary<string, Dictionary<string, object>> CurrentStateData { get; set; }

		internal TransactionScope SqlTransaction { get; set; }

		internal MongoDB.Driver.IClientSessionHandle NoSqlSession { get; set; }
		#endregion

		#region Prepare
		internal void Prepare(RepositoryOperation operation = RepositoryOperation.Query, EntityDefinition entityDefinition = null, string aliasTypeName = null, MongoDB.Driver.IClientSessionHandle nosqlSession = null)
		{
			this.ID = this.ID ?? UtilityService.GetUUID();
			this.PreviousStateData = this.PreviousStateData ?? new Dictionary<string, Dictionary<string, object>>(StringComparer.OrdinalIgnoreCase);
			this.CurrentStateData = this.CurrentStateData ?? new Dictionary<string, Dictionary<string, object>>(StringComparer.OrdinalIgnoreCase);
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
		public RepositoryContext(bool useTransaction = true, MongoDB.Driver.IClientSessionHandle nosqlSession = null)
		{
			this.UseTransaction = useTransaction;
			this.NoSqlSession = nosqlSession;
			this.Prepare();
		}

		public void Dispose()
		{
			this.EndTransaction();
			GC.SuppressFinalize(this);
		}

		~RepositoryContext() => this.Dispose();
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
		internal Dictionary<string, object> GetStateData(object @object)
		{
			// initialize
			var stateData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

			// standard properties
			@object?.GetProperties().Where(attribute => !attribute.IsIgnored()).ForEach(attribute =>
			{
				try
				{
					stateData[attribute.Name] = @object.GetAttributeValue(attribute.Name);
				}
				catch { }
			});

			// standard fields
			@object?.GetFields().Where(attribute => !attribute.IsIgnored()).ForEach(attribute =>
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
				? this.PreviousStateData.TryGetValue(@object.GetCacheKey(true), out var stateData)
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
				return currentStateData.Keys.ToHashSet(false);

			var dirtyAttributes = new HashSet<string>();
			currentStateData.ForEach(kvp =>
			{
				var previousState = previousStateData[kvp.Key];
				if (kvp.Value == null)
				{
					if (previousState != null)
						dirtyAttributes.Add(kvp.Key);
				}
				else
				{
					if (previousState == null)
						dirtyAttributes.Add(kvp.Key);
					else if (!kvp.Value.Equals(previousState))
						dirtyAttributes.Add(kvp.Key);
				}
			});

			return dirtyAttributes;
		}
		#endregion

	}
}