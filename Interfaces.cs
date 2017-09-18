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
	/// Presents a repository (means information of a business module object in the run-time)
	/// </summary>
	public interface IRepository
	{

		#region Properites
		/// <summary>
		/// Gets the identity (primary-key)
		/// </summary>
		string ID { get; }

		/// <summary>
		/// Gets the title
		/// </summary>
		string Title { get; }

		/// <summary>
		/// Gets the identity of a business system that the object is belong to
		/// </summary>
		string SystemID { get; }

		/// <summary>
		/// Gets the definition of the repository (means module definition)
		/// </summary>
		RepositoryDefinition Definition { get; }
		#endregion

		#region Methods
		#endregion

	}

	//  --------------------------------------------------------------------------------------------

	/// <summary>
	/// Presents an entity of a repository (means information of a business content-type object in the run-time)
	/// </summary>
	public interface IRepositoryEntity
	{

		#region Properites
		/// <summary>
		/// Gets the identity (primary-key)
		/// </summary>
		string ID { get; }

		/// <summary>
		/// Gets the title
		/// </summary>
		string Title { get; }

		/// <summary>
		/// Gets the identity of a business system that the object is belong to
		/// </summary>
		string SystemID { get; }

		/// <summary>
		/// Gets the identity of a business repository that the object is belong to
		/// </summary>
		string RepositoryID { get; }

		/// <summary>
		/// Gets the definition of custom properties
		/// </summary>
		List<ExtendedPropertyDefinition> ExtendedPropertyDefinitions { get; }

		/// <summary>
		/// Gets the definition for working with custom properties on UI
		/// </summary>
		ExtendedUIDefinition ExtendedUIDefinition { get; }

		/// <summary>
		/// Gets the definition of the entity (means content-type definition)
		/// </summary>
		EntityDefinition Definition { get; }
		#endregion

		#region Methods
		#endregion

	}

	//  --------------------------------------------------------------------------------------------

	/// <summary>
	/// Presents a business entity (means information of a business object in the run-time)
	/// </summary>
	public interface IBusinessEntity
	{

		#region Properites
		/// <summary>
		/// Gets the identity (primary-key)
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
		/// Gets the identity of a business repository (at run-time) that the object is belong to
		/// </summary>
		string RepositoryID { get; }

		/// <summary>
		/// Gets the identity of a business entity (at run-time) that the object is belong to
		/// </summary>
		string EntityID { get; }

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
		#endregion

		#region Methods
		#endregion

	}

	//  --------------------------------------------------------------------------------------------

	/// <summary>
	/// Presents a filtering expression
	/// </summary>
	public interface IFilterBy<T> where T : class
	{
		/// <summary>
		/// Parses the expression from JSON
		/// </summary>
		/// <param name="json"></param>
		void Parse(JObject json);

		/// <summary>
		/// Converts the expression to JSON
		/// </summary>
		/// <returns></returns>
		JObject ToJson();

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
	/// Called before creating new instance of object
	/// </summary>
	public interface IPreCreateHandler
	{
		/// <summary>
		/// Fires before creating new instance of object (called by <see cref="RepositoryMediator">RepositoryMediator</see> when process by synchronous way)
		/// </summary>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="object">The object to create new instance</param>
		/// <returns>true if the operation should be vetoed (means the operation will be cancelled when return value is true)</returns>
		bool OnPreCreate(RepositoryContext context, object @object);

		/// <summary>
		/// Fires before creating new instance of object (called by <see cref="RepositoryMediator">RepositoryMediator</see> when process by asynchronous way)
		/// </summary>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="object">The object to create new instance</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>true if the operation should be vetoed (means the operation will be cancelled when return value is true)</returns>
		Task<bool> OnPreCreateAsync(RepositoryContext context, object @object, CancellationToken cancellationToken);
	}

	//  --------------------------------------------------------------------------------------------

	/// <summary>
	/// Called after creating new instance of object successful
	/// </summary>
	public interface IPostCreateHandler
	{
		/// <summary>
		/// Fires after creating new instance of object successful (called by <see cref="RepositoryMediator">RepositoryMediator</see> when process by synchronous way)
		/// </summary>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="object">The object that instance is created successful</param>
		/// <remarks>
		/// This method will be called in a seperated task thread for the best performance
		/// </remarks>
		void OnPostCreate(RepositoryContext context, object @object);

		/// <summary>
		/// Fires after creating new instance of object successful (called by <see cref="RepositoryMediator">RepositoryMediator</see> when process by asynchronous way)
		/// </summary>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="object">The object that instance is created successful</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <remarks>
		/// This method will be called in a seperated task thread for the best performance
		/// </remarks>
		/// <returns></returns>
		Task OnPostCreateAsync(RepositoryContext context, object @object, CancellationToken cancellationToken);
	}

	//  --------------------------------------------------------------------------------------------

	/// <summary>
	/// Called before getting (loading) instance of object
	/// </summary>
	public interface IPreGetHandler
	{
		/// <summary>
		/// Fires before getting instance of object (called by <see cref="RepositoryMediator">RepositoryMediator</see> when process by synchronous way)
		/// </summary>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="id">The string that presents object's identity to get instance</param>
		/// <returns>true if the operation should be vetoed (means the operation will be cancelled when return value is true)</returns>
		bool OnPreGet(RepositoryContext context, string id);

		/// <summary>
		/// Fires before getting instance of object (called by <see cref="RepositoryMediator">RepositoryMediator</see> when process by asynchronous way)
		/// </summary>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="id">The string that presents object's identity to get instance</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>true if the operation should be vetoed (means the operation will be cancelled when return value is true)</returns>
		Task<bool> OnPreGetAsync(RepositoryContext context, string id, CancellationToken cancellationToken);
	}

	//  --------------------------------------------------------------------------------------------

	/// <summary>
	/// Called after getting instance of object successful
	/// </summary>
	public interface IPostGetHandler
	{
		/// <summary>
		/// Fires after getting instance of object successful (called by <see cref="RepositoryMediator">RepositoryMediator</see> when process by synchronous way)
		/// </summary>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="object">The object that instance is getted successful</param>
		/// <remarks>
		/// This method will be called in a seperated task thread for the best performance
		/// </remarks>
		void OnPostGet(RepositoryContext context, object @object);

		/// <summary>
		/// Fires after getting instance of object successful (called by <see cref="RepositoryMediator">RepositoryMediator</see> when process by asynchronous way)
		/// </summary>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="object">The object that instance is getted successful</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <remarks>
		/// This method will be called in a seperated task thread for the best performance
		/// </remarks>
		/// <returns></returns>
		Task OnPostGetAsync(RepositoryContext context, object @object, CancellationToken cancellationToken);
	}

	//  --------------------------------------------------------------------------------------------

	/// <summary>
	/// Called before updating instance of object
	/// </summary>
	public interface IPreUpdateHandler
	{
		/// <summary>
		/// Fires before updating instance of object (called by <see cref="RepositoryMediator">RepositoryMediator</see> when process by synchronous way)
		/// </summary>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="object">The object to update instance</param>
		/// <returns>true if the operation should be vetoed (means the operation will be cancelled when return value is true)</returns>
		bool OnPreUpdate(RepositoryContext context, object @object);

		/// <summary>
		/// Fires before updating instance of object (called by <see cref="RepositoryMediator">RepositoryMediator</see> when process by asynchronous way)
		/// </summary>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="object">The object to update instance</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>true if the operation should be vetoed (means the operation will be cancelled when return value is true)</returns>
		Task<bool> OnPreUpdateAsync(RepositoryContext context, object @object, CancellationToken cancellationToken);
	}

	//  --------------------------------------------------------------------------------------------

	/// <summary>
	/// Called after updating instance of object successful
	/// </summary>
	public interface IPostUpdateHandler
	{
		/// <summary>
		/// Fires after updating instance of object successful (called by <see cref="RepositoryMediator">RepositoryMediator</see> when process by synchronous way)
		/// </summary>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="object">The object that instance is getted successful</param>
		/// <remarks>
		/// This method will be called in a seperated task thread for the best performance
		/// </remarks>
		void OnPostUpdate(RepositoryContext context, object @object);

		/// <summary>
		/// Fires after updating instance of object successful (called by <see cref="RepositoryMediator">RepositoryMediator</see> when process by asynchronous way)
		/// </summary>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="object">The object that instance is getted successful</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <remarks>
		/// This method will be called in a seperated task thread for the best performance
		/// </remarks>
		/// <returns></returns>
		Task OnPostUpdateAsync(RepositoryContext context, object @object, CancellationToken cancellationToken);
	}

	//  --------------------------------------------------------------------------------------------

	/// <summary>
	/// Called before deleting instance of object
	/// </summary>
	public interface IPreDeleteHandler
	{
		/// <summary>
		/// Fires before delete instance of object (called by <see cref="RepositoryMediator">RepositoryMediator</see> when process by synchronous way)
		/// </summary>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="object">The object to delete instance</param>
		/// <returns>true if the operation should be vetoed (means the operation will be cancelled when return value is true)</returns>
		bool OnPreDelete(RepositoryContext context, object @object);

		/// <summary>
		/// Fires before deleting instance of object (called by <see cref="RepositoryMediator">RepositoryMediator</see> when process by asynchronous way)
		/// </summary>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="object">The object to delete instance</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>true if the operation should be vetoed (means the operation will be cancelled when return value is true)</returns>
		Task<bool> OnPreDeleteAsync(RepositoryContext context, object @object, CancellationToken cancellationToken);
	}

	//  --------------------------------------------------------------------------------------------

	/// <summary>
	/// Called after deleting instance of object successful
	/// </summary>
	public interface IPostDeleteHandler
	{
		/// <summary>
		/// Fires after deleting instance of object successful (called by <see cref="RepositoryMediator">RepositoryMediator</see> when process by synchronous way)
		/// </summary>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="object">The object that instance is deleted successful</param>
		/// <remarks>
		/// This method will be called in a seperated task thread for the best performance
		/// </remarks>
		void OnPostDelete(RepositoryContext context, object @object);

		/// <summary>
		/// Fires after deleting instance of object successful (called by <see cref="RepositoryMediator">RepositoryMediator</see> when process by asynchronous way)
		/// </summary>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="object">The object that instance is deleted successful</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <remarks>
		/// This method will be called in a seperated task thread for the best performance
		/// </remarks>
		/// <returns></returns>
		Task OnPostDeleteAsync(RepositoryContext context, object @object, CancellationToken cancellationToken);
	}

}