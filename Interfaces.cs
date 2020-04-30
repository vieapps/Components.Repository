#region Related components
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Xml.Serialization;
using System.Xml.Linq;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using MongoDB.Driver;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

using net.vieapps.Components.Utility;
using net.vieapps.Components.Security;
#endregion

namespace net.vieapps.Components.Repository
{
	/// <summary>
	/// Presents a business repository (means a business module at the run-time)
	/// </summary>
	public interface IBusinessRepository
	{
		/// <summary>
		/// Gets the identity
		/// </summary>
		string ID { get; }

		/// <summary>
		/// Gets the title
		/// </summary>
		string Title { get; }

		/// <summary>
		/// Gets the description
		/// </summary>
		string Description { get; }

		/// <summary>
		/// Gets the identity of a system that the business repository is belong to
		/// </summary>
		string SystemID { get; }

		/// <summary>
		/// Gets the definition of the business repository (means the module definition)
		/// </summary>
		RepositoryDefinition RepositoryDefinition { get; }

		/// <summary>
		/// Gets the collection of business repository entities (means the collection of business content-types)
		/// </summary>
		List<IBusinessRepositoryEntity> BusinessRepositoryEntities { get; }
	}

	//  --------------------------------------------------------------------------------------------

	/// <summary>
	/// Presents a business repository entity (means a business content-type at the run-time)
	/// </summary>
	public interface IBusinessRepositoryEntity
	{
		/// <summary>
		/// Gets the identity
		/// </summary>
		string ID { get; }

		/// <summary>
		/// Gets the title
		/// </summary>
		string Title { get; }

		/// <summary>
		/// Gets the description
		/// </summary>
		string Description { get; }

		/// <summary>
		/// Gets the state to create new version when a run-time entity object was updated
		/// </summary>
		bool CreateNewVersionWhenUpdated { get; }

		/// <summary>
		/// Gets the collection of extended property definition (means custom properties)
		/// </summary>
		List<ExtendedPropertyDefinition> ExtendedPropertyDefinitions { get; }

		/// <summary>
		/// Gets the identity of a system that the run-time entity is belong to
		/// </summary>
		string SystemID { get; }

		/// <summary>
		/// Gets the identity of a run-time repository that the run-time entity is belong to
		/// </summary>
		string RepositoryID { get; }

		/// <summary>
		/// Gets the entity definition of the business repository entity (means content-type definition)
		/// </summary>
		EntityDefinition EntityDefinition { get; }

		/// <summary>
		/// Gets the business repository that the business repository entity is belong to (means the business module)
		/// </summary>
		IBusinessRepository BusinessRepository { get; }
	}

	//  --------------------------------------------------------------------------------------------

	/// <summary>
	/// Presents a business entity (means a business object of a business content-type at the run-time)
	/// </summary>
	public interface IBusinessEntity
	{
		/// <summary>
		/// Gets the identity
		/// </summary>
		string ID { get; }

		/// <summary>
		/// Gets the title
		/// </summary>
		string Title { get; }

		/// <summary>
		/// Gets the identity of a system (at run-time) that the object is belong to
		/// </summary>
		string SystemID { get; }

		/// <summary>
		/// Gets the identity of the repository that the object is belong to (means the business module)
		/// </summary>
		string RepositoryID { get; }

		/// <summary>
		/// Gets the identity of a business repository entity that the object is belong to (means the business content-type)
		/// </summary>
		string RepositoryEntityID { get; }

		/// <summary>
		/// Gets or sets the collection of extended properties
		/// </summary>
		Dictionary<string, object> ExtendedProperties { get; set; }

		/// <summary>
		/// Gets the business entity that marks as parent of this object
		/// </summary>
		IBusinessEntity Parent { get; }

		/// <summary>
		/// Gets or sets the original privileges (means original working permissions) of this object
		/// </summary>
		Privileges OriginalPrivileges { get; set; }

		/// <summary>
		/// Gets the actual privileges (mean the combined privileges) of this object
		/// </summary>
		Privileges WorkingPrivileges { get; }

		/// <summary>
		/// Gets the time when object is created
		/// </summary>
		DateTime Created { get; }

		/// <summary>
		/// Gets the identity of an user who creates this object at the first-time
		/// </summary>
		string CreatedID { get; }

		/// <summary>
		/// Gets the time when object is modified at the last-time
		/// </summary>
		DateTime LastModified { get; }

		/// <summary>
		/// Gets the identity of an user who modifies this object at the last-time
		/// </summary>
		string LastModifiedID { get; }
	}

	//  --------------------------------------------------------------------------------------------

	/// <summary>
	/// Presents an aliased business entity (means a business object of a business content-type at the run-time that got alias)
	/// </summary>
	public interface IAliasEntity : IBusinessEntity
	{
		/// <summary>
		/// Gets the alias of this business entity
		/// </summary>
		string Alias { get; }

		/// <summary>
		/// Gets a business entity by an alias
		/// </summary>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity (means a business content-type at run-time)</param>
		/// <param name="alias">The alias</param>
		/// <param name="parentIdentity">The identity (alias or UUID) of parent object (if has)</param>
		/// <returns></returns>
		IBusinessEntity GetByAlias(string businessRepositoryEntityID, string alias, string parentIdentity = null);

		/// <summary>
		/// Gets a business entity by an alias
		/// </summary>
		/// <param name="businessRepositoryEntityID">The identity of a business repository entity (means a business content-type at run-time)</param>
		/// <param name="alias">The alias</param>
		/// <param name="parentIdentity">The identity (alias or UUID) of parent object (if has)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		Task<IBusinessEntity> GetByAliasAsync(string businessRepositoryEntityID, string alias, string parentIdentity = null, CancellationToken cancellationToken = default);
	}

	//  --------------------------------------------------------------------------------------------

	/// <summary>
	/// Presents a filtering expression
	/// </summary>
	public interface IFilterBy
	{
		/// <summary>
		/// Parses the expression from JSON
		/// </summary>
		/// <param name="json">The JSON object that contains the expression</param>
		void Parse(JObject json);

		/// <summary>
		/// Converts the expression to JSON
		/// </summary>
		/// <returns></returns>
		JToken ToJson();
	}

	//  --------------------------------------------------------------------------------------------

	/// <summary>
	/// Presents a filtering expression with a specified class
	/// </summary>
	public interface IFilterBy<T> : IFilterBy where T : class
	{
		/// <summary>
		/// Gets the statement of SQL
		/// </summary>
		/// <returns></returns>
		Tuple<string, Dictionary<string, object>> GetSqlStatement();

		/// <summary>
		/// Gets the statement of No SQL
		/// </summary>
		/// <returns></returns>
		FilterDefinition<T> GetNoSqlStatement();
	}

	//  --------------------------------------------------------------------------------------------

	/// <summary>
	/// Presents a sorting expression
	/// </summary>
	public interface ISortBy
	{
		/// <summary>
		/// Gets or sets the attribute for sorting
		/// </summary>
		string Attribute { get; set; }

		/// <summary>
		/// Gets or sets the mode for sorting
		/// </summary>
		SortMode Mode { get; set; }

		/// <summary>
		/// Gets or sets the next-sibling
		/// </summary>
		ISortBy ThenBy { get; set; }

		/// <summary>
		/// Parses the expression from JSON
		/// </summary>
		/// <param name="json">The JSON object that contains the expression</param>
		void Parse(JObject json);

		/// <summary>
		/// Converts the expression to JSON
		/// </summary>
		/// <returns></returns>
		JToken ToJson();
	}

	//  --------------------------------------------------------------------------------------------

	/// <summary>
	/// Presents a sorting expression with a specified class
	/// </summary>
	public interface ISortBy<T> : ISortBy where T : class
	{

		/// <summary>
		/// Gets or sets the next-sibling
		/// </summary>
		new ISortBy<T> ThenBy { get; set; }

		/// <summary>
		/// Gets the statement of SQL
		/// </summary>
		/// <returns></returns>
		string GetSqlStatement();

		/// <summary>
		/// Gets the statement of No SQL
		/// </summary>
		/// <returns></returns>
		SortDefinition<T> GetNoSqlStatement();
	}

	//  --------------------------------------------------------------------------------------------

	/// <summary>
	/// Presents a handler that be called before creating new instance of an object
	/// </summary>
	public interface IPreCreateHandler
	{
		/// <summary>
		/// Fires before creating new instance of object (called by <see cref="RepositoryMediator">RepositoryMediator</see> when process by synchronous way)
		/// </summary>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="object">The object to create new instance</param>
		/// <param name="isRestore">Specified this object is created new when restore from trash</param>
		/// <returns>true if the operation should be vetoed (means the operation will be cancelled when return value is true)</returns>
		bool OnPreCreate<T>(RepositoryContext context, T @object, bool isRestore) where T : class;

		/// <summary>
		/// Fires before creating new instance of object (called by <see cref="RepositoryMediator">RepositoryMediator</see> when process by asynchronous way)
		/// </summary>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="object">The object to create new instance</param>
		/// <param name="isRestore">Specified this object is created new when restore from trash</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>true if the operation should be vetoed (means the operation will be cancelled when return value is true)</returns>
		Task<bool> OnPreCreateAsync<T>(RepositoryContext context, T @object, bool isRestore, CancellationToken cancellationToken) where T : class;
	}

	//  --------------------------------------------------------------------------------------------

	/// <summary>
	/// Presents a handler that be called after creating new instance of an object successful
	/// </summary>
	public interface IPostCreateHandler
	{
		/// <summary>
		/// Fires after creating new instance of object successful (called by <see cref="RepositoryMediator">RepositoryMediator</see> when process by synchronous way)
		/// </summary>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="object">The object that instance is created successful</param>
		/// <param name="isRestore">Specified this object is created new when restore from trash</param>
		/// <remarks>
		/// This method will be ran by the thread-pool thread the best performance
		/// </remarks>
		void OnPostCreate<T>(RepositoryContext context, T @object, bool isRestore) where T : class;

		/// <summary>
		/// Fires after creating new instance of object successful (called by <see cref="RepositoryMediator">RepositoryMediator</see> when process by asynchronous way)
		/// </summary>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="object">The object that instance is created successful</param>
		/// <param name="isRestore">Specified this object is created new when restore from trash</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <remarks>
		/// This method will be ran by the thread-pool thread the best performance
		/// </remarks>
		/// <returns></returns>
		Task OnPostCreateAsync<T>(RepositoryContext context, T @object, bool isRestore, CancellationToken cancellationToken) where T : class;
	}

	//  --------------------------------------------------------------------------------------------

	/// <summary>
	/// Presents a handler that be called before getting (loading) instance of an object
	/// </summary>
	public interface IPreGetHandler
	{
		/// <summary>
		/// Fires before getting instance of object (called by <see cref="RepositoryMediator">RepositoryMediator</see> when process by synchronous way)
		/// </summary>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="id">The string that presents object's identity to get instance</param>
		/// <returns>true if the operation should be vetoed (means the operation will be cancelled when return value is true)</returns>
		bool OnPreGet<T>(RepositoryContext context, string id) where T : class;

		/// <summary>
		/// Fires before getting instance of object (called by <see cref="RepositoryMediator">RepositoryMediator</see> when process by asynchronous way)
		/// </summary>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="id">The string that presents object's identity to get instance</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>true if the operation should be vetoed (means the operation will be cancelled when return value is true)</returns>
		Task<bool> OnPreGetAsync<T>(RepositoryContext context, string id, CancellationToken cancellationToken) where T : class;
	}

	//  --------------------------------------------------------------------------------------------

	/// <summary>
	/// Presents a handler that be called after getting (loading) instance of an object successful
	/// </summary>
	public interface IPostGetHandler
	{
		/// <summary>
		/// Fires after getting instance of object successful (called by <see cref="RepositoryMediator">RepositoryMediator</see> when process by synchronous way)
		/// </summary>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="object">The object that instance is getted successful</param>
		/// <remarks>
		/// This method will be ran by the thread-pool thread the best performance
		/// </remarks>
		void OnPostGet<T>(RepositoryContext context, T @object) where T : class;

		/// <summary>
		/// Fires after getting instance of object successful (called by <see cref="RepositoryMediator">RepositoryMediator</see> when process by asynchronous way)
		/// </summary>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="object">The object that instance is getted successful</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <remarks>
		/// This method will be ran by the thread-pool thread the best performance
		/// </remarks>
		/// <returns></returns>
		Task OnPostGetAsync<T>(RepositoryContext context, T @object, CancellationToken cancellationToken) where T : class;
	}

	//  --------------------------------------------------------------------------------------------

	/// <summary>
	/// Presents a handler that be called before updating instance of an object
	/// </summary>
	public interface IPreUpdateHandler
	{
		/// <summary>
		/// Fires before updating instance of object (called by <see cref="RepositoryMediator">RepositoryMediator</see> when process by synchronous way)
		/// </summary>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="object">The object to update instance</param>
		/// <param name="changed">The collection of changed attributes of the object</param>
		/// <param name="isRollback">Specified this object is updated when rollback from a version</param>
		/// <returns>true if the operation should be vetoed (means the operation will be cancelled when return value is true)</returns>
		bool OnPreUpdate<T>(RepositoryContext context, T @object, HashSet<string> changed, bool isRollback) where T : class;

		/// <summary>
		/// Fires before updating instance of object (called by <see cref="RepositoryMediator">RepositoryMediator</see> when process by asynchronous way)
		/// </summary>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="object">The object to update instance</param>
		/// <param name="changed">The collection of changed attributes of the object</param>
		/// <param name="isRollback">Specified this object is updated when rollback from a version</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>true if the operation should be vetoed (means the operation will be cancelled when return value is true)</returns>
		Task<bool> OnPreUpdateAsync<T>(RepositoryContext context, T @object, HashSet<string> changed, bool isRollback, CancellationToken cancellationToken) where T : class;
	}

	//  --------------------------------------------------------------------------------------------

	/// <summary>
	/// Presents a handler that be called after updating instance of an object successful
	/// </summary>
	public interface IPostUpdateHandler
	{
		/// <summary>
		/// Fires after updating instance of object successful (called by <see cref="RepositoryMediator">RepositoryMediator</see> when process by synchronous way)
		/// </summary>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="object">The object that instance is getted successful</param>
		/// <param name="changed">The collection of changed attributes of the object</param>
		/// <param name="isRollback">Specified this object is updated when rollback from a version</param>
		/// <remarks>
		/// This method will be ran by the thread-pool thread the best performance
		/// </remarks>
		void OnPostUpdate<T>(RepositoryContext context, T @object, HashSet<string> changed, bool isRollback) where T : class;

		/// <summary>
		/// Fires after updating instance of object successful (called by <see cref="RepositoryMediator">RepositoryMediator</see> when process by asynchronous way)
		/// </summary>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="object">The object that instance is getted successful</param>
		/// <param name="changed">The collection of changed attributes of the object</param>
		/// <param name="isRollback">Specified this object is updated when rollback from a version</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <remarks>
		/// This method will be ran by the thread-pool thread the best performance
		/// </remarks>
		/// <returns></returns>
		Task OnPostUpdateAsync<T>(RepositoryContext context, T @object, HashSet<string> changed, bool isRollback, CancellationToken cancellationToken) where T : class;
	}

	//  --------------------------------------------------------------------------------------------

	/// <summary>
	/// Presents a handler that be called before deleting instance of an object
	/// </summary>
	public interface IPreDeleteHandler
	{
		/// <summary>
		/// Fires before delete instance of object (called by <see cref="RepositoryMediator">RepositoryMediator</see> when process by synchronous way)
		/// </summary>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="object">The object to delete instance</param>
		/// <returns>true if the operation should be vetoed (means the operation will be cancelled when return value is true)</returns>
		bool OnPreDelete<T>(RepositoryContext context, T @object) where T : class;

		/// <summary>
		/// Fires before deleting instance of object (called by <see cref="RepositoryMediator">RepositoryMediator</see> when process by asynchronous way)
		/// </summary>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="object">The object to delete instance</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>true if the operation should be vetoed (means the operation will be cancelled when return value is true)</returns>
		Task<bool> OnPreDeleteAsync<T>(RepositoryContext context, T @object, CancellationToken cancellationToken) where T : class;
	}

	//  --------------------------------------------------------------------------------------------

	/// <summary>
	/// Presents a handler that be called after deleting instance of an object successful
	/// </summary>
	public interface IPostDeleteHandler
	{
		/// <summary>
		/// Fires after deleting instance of object successful (called by <see cref="RepositoryMediator">RepositoryMediator</see> when process by synchronous way)
		/// </summary>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="object">The object that instance is deleted successful</param>
		/// <remarks>
		/// This method will be ran by the thread-pool thread the best performance
		/// </remarks>
		void OnPostDelete<T>(RepositoryContext context, T @object) where T : class;

		/// <summary>
		/// Fires after deleting instance of object successful (called by <see cref="RepositoryMediator">RepositoryMediator</see> when process by asynchronous way)
		/// </summary>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="object">The object that instance is deleted successful</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <remarks>
		/// This method will be ran by the thread-pool thread the best performance
		/// </remarks>
		/// <returns></returns>
		Task OnPostDeleteAsync<T>(RepositoryContext context, T @object, CancellationToken cancellationToken) where T : class;
	}
}