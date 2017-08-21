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
	/// Base class of an entity in the a repository
	/// </summary>
	[Serializable]
	public abstract class RepositoryBase
	{

		#region Properties
		/// <summary>
		/// Gets or sets the object identity (primary-key)
		/// </summary>
		[BsonId(IdGenerator = typeof(IdentityGenerator)), PrimaryKey(MaxLength = 32)]
		public virtual string ID { get; set; }

		/// <summary>
		/// Gets the score while searching
		/// </summary>
		[JsonIgnore, XmlIgnore, BsonIgnoreIfNull, Ignore]
		public virtual double? SearchScore { get; set; }
		#endregion

		#region Methods
		/// <summary>
		/// Gest or sets value of a property by name
		/// </summary>
		/// <param name="name">The string that presents the name of a property</param>
		/// <returns></returns>
		protected virtual object this[string name]
		{
			get { return this.GetProperty(name); }
			set { this.SetProperty(name, value); }
		}

		/// <summary>
		/// Gets the value of a specified property
		/// </summary>
		/// <param name="name">The name of the property</param>
		/// <returns></returns>
		public abstract object GetProperty(string name);

		/// <summary>
		/// Sets the value of a specified property
		/// </summary>
		/// <param name="name">The name of the property</param>
		/// <param name="value">The value of the property</param>
		public abstract void SetProperty(string name, object value);

		/// <summary>
		/// Serializes this object to JSON
		/// </summary>
		/// <param name="addTypeOfExtendedProperties">true to add type of all extended properties (named with surfix '$Type')</param>
		/// <returns></returns>
		public abstract JObject ToJson(bool addTypeOfExtendedProperties);

		/// <summary>
		/// Serializes this object to JSON
		/// </summary>
		/// <returns></returns>
		public virtual JObject ToJson()
		{
			return this.ToJson(false);
		}

		/// <summary>
		/// Parses the JSON and copy values into this object
		/// </summary>
		/// <param name="json">The JSON object that contains information</param>
		public abstract void ParseJson(JObject json);

		/// <summary>
		/// Serializes this object to XML
		/// </summary>
		/// <param name="addTypeOfExtendedProperties">true to add type of all extended properties (attribute named '$type')</param>
		/// <returns></returns>
		public abstract XElement ToXml(bool addTypeOfExtendedProperties);

		/// <summary>
		/// Serializes this object to XML
		/// </summary>
		/// <returns></returns>
		public virtual XElement ToXml()
		{
			return this.ToXml(false);
		}

		/// <summary>
		/// Parses the XML and copy values into this object
		/// </summary>
		/// <param name="xml">The XML object that contains information</param>
		public abstract void ParseXml(XContainer xml);
		#endregion

	}

	//  --------------------------------------------------------------------------------------------

	/// <summary>
	/// Base class of an entity of a repository with helper methods to perform CRUD operations, count, find, and query (full-text search)
	/// </summary>
	[Serializable, DebuggerDisplay("ID = {ID}, Type = {typeof(T).FullName}")]
	public abstract class RepositoryBase<T> : RepositoryBase where T : class
	{

		#region Create
		/// <summary>
		/// Creates new an object
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object to create new instance in the repository</param>
		public static void Create<TEntity>(RepositoryContext context, string aliasTypeName, TEntity @object) where TEntity : class
		{
			try
			{
				RepositoryMediator.Create<TEntity>(context, aliasTypeName, @object);
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				throw ex;
			}
		}

		/// <summary>
		/// Creates new an object
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object to create new instance in the repository</param>
		public static void Create<TEntity>(string aliasTypeName, TEntity @object) where TEntity : class
		{
			RepositoryMediator.Create<TEntity>(aliasTypeName, @object);
		}

		/// <summary>
		/// Creates new an object
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="object">The object to create new instance in the repository</param>
		public static void Create<TEntity>(TEntity @object) where TEntity : class
		{
			RepositoryBase<T>.Create<TEntity>(null, @object);
		}

		/// <summary>
		/// Creates new the instance of this object
		/// </summary>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		protected virtual void Create(RepositoryContext context, string aliasTypeName)
		{
			RepositoryBase<T>.Create<T>(context, aliasTypeName, this as T);
		}

		/// <summary>
		/// Creates new the instance of this object
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		protected virtual void Create(string aliasTypeName = null)
		{
			RepositoryBase<T>.Create<T>(aliasTypeName, this as T);
		}

		/// <summary>
		/// Creates new an object
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object to create new instance in the repository</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task CreateAsync<TEntity>(RepositoryContext context, string aliasTypeName, TEntity @object, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
		{
			try
			{
				return RepositoryMediator.CreateAsync<TEntity>(context, aliasTypeName, @object, cancellationToken);
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				return Task.FromException(ex);
			}
		}

		/// <summary>
		/// Creates new an object
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object to create new instance in the repository</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task CreateAsync<TEntity>(string aliasTypeName, TEntity @object, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
		{
			return RepositoryMediator.CreateAsync<TEntity>(aliasTypeName, @object, cancellationToken);
		}

		/// <summary>
		/// Creates new an object
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="object">The object to create new instance in the repository</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task CreateAsync<TEntity>(TEntity @object, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
		{
			return RepositoryBase<T>.CreateAsync<TEntity>(null, @object);
		}

		/// <summary>
		/// Creates new the instance of this object
		/// </summary>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		protected virtual Task CreateAsync(RepositoryContext context, string aliasTypeName, CancellationToken cancellationToken = default(CancellationToken))
		{
			return RepositoryBase<T>.CreateAsync<T>(context, aliasTypeName, this as T);
		}

		/// <summary>
		/// Creates new the instance of this object
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		protected virtual Task CreateAsync(string aliasTypeName = null, CancellationToken cancellationToken = default(CancellationToken))
		{
			return RepositoryBase<T>.CreateAsync<T>(aliasTypeName, this as T, cancellationToken);
		}
		#endregion

		#region Get
		/// <summary>
		/// Gets an object
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="id">The string that present identity (primary-key)</param>
		/// <returns></returns>
		public static TEntity Get<TEntity>(RepositoryContext context, string aliasTypeName, string id) where TEntity : class
		{
			try
			{
				return !string.IsNullOrWhiteSpace(id)
					? RepositoryMediator.Get<TEntity>(context, aliasTypeName, id)
					: null;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				throw ex;
			}
		}

		/// <summary>
		/// Gets an object
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="id">The string that present identity (primary-key)</param>
		/// <returns></returns>
		public static TEntity Get<TEntity>(string aliasTypeName, string id) where TEntity : class
		{
			return !string.IsNullOrWhiteSpace(id)
				? RepositoryMediator.Get<TEntity>(aliasTypeName, id)
				: null;
		}

		/// <summary>
		/// Gets an object
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="id">The string that present identity (primary-key)</param>
		/// <returns></returns>
		public static TEntity Get<TEntity>(string id) where TEntity : class
		{
			return RepositoryBase<T>.Get<TEntity>(null, id);
		}

		/// <summary>
		/// Gets the instance of this object
		/// </summary>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		protected virtual void Get(RepositoryContext context, string aliasTypeName)
		{
			if (!string.IsNullOrWhiteSpace(this.ID))
			{
				var instance = RepositoryBase<T>.Get<T>(context, aliasTypeName, this.ID);
				if (instance != null)
					this.CopyFrom(instance);
			}
		}

		/// <summary>
		/// Gets the instance of this object
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		protected virtual void Get(string aliasTypeName = null)
		{
			if (!string.IsNullOrWhiteSpace(this.ID))
			{
				var instance = RepositoryBase<T>.Get<T>(aliasTypeName, this.ID);
				if (instance != null)
					this.CopyFrom(instance);
			}
		}

		/// <summary>
		/// Gets an object
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="id">The string that present identity (primary-key)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<TEntity> GetAsync<TEntity>(RepositoryContext context, string aliasTypeName, string id, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
		{
			try
			{
				return !string.IsNullOrWhiteSpace(id)
					? RepositoryMediator.GetAsync<TEntity>(context, aliasTypeName, id, true, cancellationToken)
					: null;
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				return Task.FromException<TEntity>(ex);
			}
		}

		/// <summary>
		/// Gets an object
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="id">The string that present identity (primary-key)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<TEntity> GetAsync<TEntity>(string aliasTypeName, string id, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
		{
			return !string.IsNullOrWhiteSpace(id)
				? RepositoryMediator.GetAsync<TEntity>(aliasTypeName, id, cancellationToken)
				: null;
		}

		/// <summary>
		/// Gets an object
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="id">The string that present identity (primary-key)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task<TEntity> GetAsync<TEntity>(string id, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
		{
			return RepositoryBase<T>.GetAsync<TEntity>(null, id, cancellationToken);
		}

		/// <summary>
		/// Gets the instance of this object
		/// </summary>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		protected virtual async Task GetAsync(RepositoryContext context, string aliasTypeName, CancellationToken cancellationToken = default(CancellationToken))
		{
			if (!string.IsNullOrWhiteSpace(this.ID))
				try
				{
					var instance = await RepositoryBase<T>.GetAsync<T>(context, aliasTypeName, this.ID, cancellationToken);
					if (instance != null)
						this.CopyFrom(instance);
				}
				catch (Exception ex)
				{
					context.Exception = ex;
					throw ex;
				}
		}

		/// <summary>
		/// Gets the instance of this object
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		protected virtual async Task GetAsync(string aliasTypeName = null, CancellationToken cancellationToken = default(CancellationToken))
		{
			if (!string.IsNullOrWhiteSpace(this.ID))
			{
				var instance = await RepositoryBase<T>.GetAsync<T>(aliasTypeName, this.ID, cancellationToken);
				if (instance != null)
					this.CopyFrom(instance);
			}
		}
		#endregion

		#region Get (first match)
		/// <summary>
		/// Gets an object (the first matched with the filter)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns>The first object that matched with the filter; otherwise null</returns>
		public static TEntity Get<TEntity>(RepositoryContext context, string aliasTypeName, IFilterBy<TEntity> filter, SortBy<TEntity> sort = null, string businessEntityID = null) where TEntity : class
		{
			try
			{
				return RepositoryMediator.Get<TEntity>(context, aliasTypeName, filter, sort, businessEntityID);
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				throw ex;
			}
		}

		/// <summary>
		/// Gets an object (the first matched with the filter)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns>The first object that matched with the filter; otherwise null</returns>
		public static TEntity Get<TEntity>(string aliasTypeName, IFilterBy<TEntity> filter, SortBy<TEntity> sort = null, string businessEntityID = null) where TEntity : class
		{
			return RepositoryMediator.Get<TEntity>(aliasTypeName, filter, sort, businessEntityID);
		}

		/// <summary>
		/// Gets an object (the first matched with the filter)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns>The first object that matched with the filter; otherwise null</returns>
		public static TEntity Get<TEntity>(IFilterBy<TEntity> filter, SortBy<TEntity> sort = null, string businessEntityID = null) where TEntity : class
		{
			return RepositoryBase<T>.Get<TEntity>(null, filter, sort, businessEntityID);
		}

		/// <summary>
		/// Gets an object (the first matched with the filter)
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns>The first object that matched with the filter; otherwise null</returns>
		public static T Get(string aliasTypeName, IFilterBy<T> filter, SortBy<T> sort = null, string businessEntityID = null)
		{
			return RepositoryBase<T>.Get<T>(aliasTypeName, filter, sort, businessEntityID);
		}

		/// <summary>
		/// Gets an object (the first matched with the filter)
		/// </summary>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns>The first object that matched with the filter; otherwise null</returns>
		public static T Get(IFilterBy<T> filter, SortBy<T> sort = null, string businessEntityID = null)
		{
			return RepositoryBase<T>.Get(null, filter, sort, businessEntityID);
		}

		/// <summary>
		/// Gets an object (the first matched with the filter)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The first object that matched with the filter; otherwise null</returns>
		public static Task<TEntity> GetAsync<TEntity>(RepositoryContext context, string aliasTypeName, IFilterBy<TEntity> filter, SortBy<TEntity> sort = null, string businessEntityID = null, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
		{
			try
			{
				return RepositoryMediator.GetAsync<TEntity>(context, aliasTypeName, filter, sort, businessEntityID, cancellationToken);
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				return Task.FromException<TEntity>(ex);
			}
		}

		/// <summary>
		/// Gets an object (the first matched with the filter)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The first object that matched with the filter; otherwise null</returns>
		public static Task<TEntity> GetAsync<TEntity>(string aliasTypeName, IFilterBy<TEntity> filter, SortBy<TEntity> sort = null, string businessEntityID = null, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
		{
			return RepositoryMediator.GetAsync<TEntity>(aliasTypeName, filter, sort, businessEntityID, cancellationToken);
		}

		/// <summary>
		/// Gets an object (the first matched with the filter)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The first object that matched with the filter; otherwise null</returns>
		public static Task<TEntity> GetAsync<TEntity>(IFilterBy<TEntity> filter, SortBy<TEntity> sort = null, string businessEntityID = null, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
		{
			return RepositoryBase<T>.GetAsync<TEntity>(null, filter, sort, businessEntityID, cancellationToken);
		}

		/// <summary>
		/// Gets an object (the first matched with the filter)
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The first object that matched with the filter; otherwise null</returns>
		public static Task<T> GetAsync(string aliasTypeName, IFilterBy<T> filter, SortBy<T> sort = null, string businessEntityID = null, CancellationToken cancellationToken = default(CancellationToken))
		{
			return RepositoryBase<T>.GetAsync<T>(aliasTypeName, filter, sort, businessEntityID, cancellationToken);
		}

		/// <summary>
		/// Gets an object (the first matched with the filter)
		/// </summary>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The first object that matched with the filter; otherwise null</returns>
		public static Task<T> GetAsync(IFilterBy<T> filter, SortBy<T> sort = null, string businessEntityID = null, CancellationToken cancellationToken = default(CancellationToken))
		{
			return RepositoryBase<T>.GetAsync(null, filter, sort, businessEntityID, cancellationToken);
		}
		#endregion

		#region Replace
		/// <summary>
		/// Updates an object (using replace method)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		public static void Replace<TEntity>(RepositoryContext context, string aliasTypeName, TEntity @object) where TEntity : class
		{
			try
			{
				RepositoryMediator.Replace<TEntity>(context, aliasTypeName, @object);
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				throw ex;
			}
		}

		/// <summary>
		/// Updates an object (using replace method)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		public static void Replace<TEntity>(string aliasTypeName, TEntity @object) where TEntity : class
		{
			RepositoryMediator.Replace<TEntity>(aliasTypeName, @object);
		}

		/// <summary>
		/// Updates an object (using replace method)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		public static void Replace<TEntity>(TEntity @object) where TEntity : class
		{
			RepositoryBase<T>.Replace<TEntity>(null, @object);
		}

		/// <summary>
		/// Updates the instance of this object (using replace method)
		/// </summary>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		protected virtual void Replace(RepositoryContext context, string aliasTypeName)
		{
			RepositoryBase<T>.Replace<T>(context, aliasTypeName, this as T);
		}

		/// <summary>
		/// Updates the instance of this object (using replace method)
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		protected virtual void Replace(string aliasTypeName = null)
		{
			RepositoryBase<T>.Replace<T>(aliasTypeName, this as T);
		}

		/// <summary>
		/// Updates an object (using replace method)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task ReplaceAsync<TEntity>(RepositoryContext context, string aliasTypeName, TEntity @object, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
		{
			try
			{
				return RepositoryMediator.ReplaceAsync<TEntity>(context, aliasTypeName, @object, cancellationToken);
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				return Task.FromException<TEntity>(ex);
			}
		}

		/// <summary>
		/// Updates an object (using replace method)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task ReplaceAsync<TEntity>(string aliasTypeName, TEntity @object, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
		{
			return RepositoryMediator.ReplaceAsync<TEntity>(aliasTypeName, @object, cancellationToken);
		}

		/// <summary>
		/// Updates an object (using replace method)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task ReplaceAsync<TEntity>(TEntity @object, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
		{
			return RepositoryBase<T>.ReplaceAsync<TEntity>(null, @object, cancellationToken);
		}

		/// <summary>
		/// Updates the instance of this object (using replace method)
		/// </summary>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		protected virtual Task ReplaceAsync(RepositoryContext context, string aliasTypeName, CancellationToken cancellationToken = default(CancellationToken))
		{
			return RepositoryBase<T>.ReplaceAsync<T>(context, aliasTypeName, this as T, cancellationToken);
		}

		/// <summary>
		/// Updates the instance of this object (using replace method)
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		protected virtual Task ReplaceAsync(string aliasTypeName = null, CancellationToken cancellationToken = default(CancellationToken))
		{
			return RepositoryBase<T>.ReplaceAsync<T>(aliasTypeName, this as T, cancellationToken);
		}
		#endregion

		#region Update
		/// <summary>
		/// Updates an object (only update changed attributes)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		public static void Update<TEntity>(RepositoryContext context, string aliasTypeName, TEntity @object) where TEntity : class
		{
			try
			{
				RepositoryMediator.Update<TEntity>(context, aliasTypeName, @object);
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				throw ex;
			}
		}

		/// <summary>
		/// Updates an object (only update changed attributes)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		public static void Update<TEntity>(string aliasTypeName, TEntity @object) where TEntity : class
		{
			RepositoryMediator.Update<TEntity>(aliasTypeName, @object);
		}

		/// <summary>
		/// Updates an object (only update changed attributes)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		public static void Update<TEntity>(TEntity @object) where TEntity : class
		{
			RepositoryBase<T>.Update<TEntity>(null, @object);
		}

		/// <summary>
		/// Updates the instance of this object (only update changed attributes)
		/// </summary>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		protected virtual void Update(RepositoryContext context, string aliasTypeName)
		{
			RepositoryBase<T>.Update<T>(context, aliasTypeName, this as T);
		}

		/// <summary>
		/// Updates the instance of this object (only update changed attributes)
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		protected virtual void Update(string aliasTypeName = null)
		{
			RepositoryBase<T>.Update<T>(aliasTypeName, this as T);
		}

		/// <summary>
		/// Updates an object (only update changed attributes)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task UpdateAsync<TEntity>(RepositoryContext context, string aliasTypeName, TEntity @object, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
		{
			try
			{
				return RepositoryMediator.UpdateAsync<TEntity>(context, aliasTypeName, @object, cancellationToken);
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				return Task.FromException<TEntity>(ex);
			}
		}

		/// <summary>
		/// Updates an object (only update changed attributes)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task UpdateAsync<TEntity>(string aliasTypeName, TEntity @object, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
		{
			return RepositoryMediator.UpdateAsync<TEntity>(aliasTypeName, @object, cancellationToken);
		}

		/// <summary>
		/// Updates an object (only update changed attributes)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task UpdateAsync<TEntity>(TEntity @object, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
		{
			return RepositoryBase<T>.UpdateAsync<TEntity>(null, @object, cancellationToken);
		}

		/// <summary>
		/// Updates the instance of this object (only update changed attributes)
		/// </summary>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		protected virtual Task UpdateAsync(RepositoryContext context, string aliasTypeName, CancellationToken cancellationToken = default(CancellationToken))
		{
			return RepositoryBase<T>.UpdateAsync<T>(context, aliasTypeName, this as T, cancellationToken);
		}

		/// <summary>
		/// Updates the instance of this object (only update changed attributes)
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		protected virtual Task UpdateAsync(string aliasTypeName = null, CancellationToken cancellationToken = default(CancellationToken))
		{
			return RepositoryBase<T>.UpdateAsync<T>(aliasTypeName, this as T, cancellationToken);
		}
		#endregion

		#region Delete
		/// <summary>
		/// Deletes an object
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="id">The string that presents object identity that want to delete instance from repository</param>
		public static void Delete<TEntity>(RepositoryContext context, string aliasTypeName, string id) where TEntity : class
		{
			try
			{
				RepositoryMediator.Delete<TEntity>(context, aliasTypeName, id);
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				throw ex;
			}
		}

		/// <summary>
		/// Deletes an object
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="id">The string that presents object identity that want to delete instance from repository</param>
		public static void Delete<TEntity>(string aliasTypeName, string id) where TEntity : class
		{
			RepositoryMediator.Delete<TEntity>(aliasTypeName, id);
		}

		/// <summary>
		/// Deletes an object
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="id">The string that presents object identity that want to delete instance from repository</param>
		public static void Delete<TEntity>(string id) where TEntity : class
		{
			RepositoryBase<T>.Delete<TEntity>(null, id);
		}

		/// <summary>
		/// Deletes the instance of this object
		/// </summary>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		protected virtual void Delete(RepositoryContext context, string aliasTypeName)
		{
			if (!string.IsNullOrWhiteSpace(this.ID))
				RepositoryBase<T>.Delete<T>(context, aliasTypeName, this.ID);
		}

		/// <summary>
		/// Deletes the instance of this object
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		protected virtual void Delete(string aliasTypeName = null)
		{
			if (!string.IsNullOrWhiteSpace(this.ID))
				RepositoryBase<T>.Delete<T>(aliasTypeName, this.ID);
		}

		/// <summary>
		/// Deletes an object
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="id">The string that presents object identity that want to delete instance from repository</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task DeleteAsync<TEntity>(RepositoryContext context, string aliasTypeName, string id, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
		{
			try
			{
				return RepositoryMediator.DeleteAsync<TEntity>(context, aliasTypeName, id, cancellationToken);
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				return Task.FromException(ex);
			}
		}

		/// <summary>
		/// Deletes an object
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="id">The string that presents object identity that want to delete instance from repository</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task DeleteAsync<TEntity>(string aliasTypeName, string id, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
		{
			return RepositoryMediator.DeleteAsync<TEntity>(aliasTypeName, id, cancellationToken);
		}

		/// <summary>
		/// Deletes an object
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="id">The string that presents object identity that want to delete instance from repository</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task DeleteAsync<TEntity>(string id, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
		{
			return RepositoryBase<T>.DeleteAsync<TEntity>(null, id, cancellationToken);
		}

		/// <summary>
		/// Deletes the instance of this object
		/// </summary>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		protected virtual Task DeleteAsync(RepositoryContext context, string aliasTypeName, CancellationToken cancellationToken = default(CancellationToken))
		{
			return !string.IsNullOrWhiteSpace(this.ID)
				? RepositoryBase<T>.DeleteAsync<T>(context, aliasTypeName, this.ID, cancellationToken)
				: Task.FromException(new ArgumentNullException("ID", "The identity of the object is null or empty"));
		}

		/// <summary>
		/// Deletes the instance of this object
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		protected virtual Task DeleteAsync(string aliasTypeName = null, CancellationToken cancellationToken = default(CancellationToken))
		{
			return !string.IsNullOrWhiteSpace(this.ID)
				? RepositoryBase<T>.DeleteAsync<T>(aliasTypeName, this.ID, cancellationToken)
				: Task.FromException(new ArgumentNullException("ID", "The identity of the object is null or empty"));
		}
		#endregion

		#region Delete (many)
		/// <summary>
		/// Deletes many objects that matched with the filter
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		public static void DeleteMany<TEntity>(RepositoryContext context, string aliasTypeName, IFilterBy<TEntity> filter, string businessEntityID = null) where TEntity : class
		{
			try
			{
				RepositoryMediator.DeleteMany<TEntity>(context, aliasTypeName, filter, businessEntityID);
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				throw ex;
			}
		}

		/// <summary>
		/// Deletes many objects that matched with the filter
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		public static void DeleteMany<TEntity>(string aliasTypeName, IFilterBy<TEntity> filter, string businessEntityID = null) where TEntity : class
		{
			RepositoryMediator.DeleteMany<TEntity>(aliasTypeName, filter, businessEntityID);
		}

		/// <summary>
		/// Deletes many objects that matched with the filter
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		public static void DeleteMany<TEntity>(IFilterBy<TEntity> filter, string businessEntityID = null) where TEntity : class
		{
			RepositoryBase<T>.DeleteMany<TEntity>(null, filter, businessEntityID);
		}

		/// <summary>
		/// Deletes many objects that matched with the filter
		/// </summary>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		protected static void DeleteMany(RepositoryContext context, string aliasTypeName, IFilterBy<T> filter, string businessEntityID = null)
		{
			RepositoryBase<T>.DeleteMany<T>(context, aliasTypeName, filter, businessEntityID);
		}

		/// <summary>
		/// Deletes many objects that matched with the filter
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		protected static void DeleteMany(string aliasTypeName, IFilterBy<T> filter, string businessEntityID = null)
		{
			RepositoryBase<T>.DeleteMany<T>(aliasTypeName, filter, businessEntityID);
		}

		/// <summary>
		/// Deletes many objects that matched with the filter
		/// </summary>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		protected static void DeleteMany(IFilterBy<T> filter, string businessEntityID = null)
		{
			RepositoryBase<T>.DeleteMany(null, filter, businessEntityID);
		}

		/// <summary>
		/// Deletes many objects that matched with the filter
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task DeleteManyAsync<TEntity>(RepositoryContext context, string aliasTypeName, IFilterBy<TEntity> filter, string businessEntityID = null, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
		{
			try
			{
				return RepositoryMediator.DeleteManyAsync<TEntity>(context, aliasTypeName, filter, businessEntityID, cancellationToken);
			}
			catch (Exception ex)
			{
				context.Exception = ex;
				return Task.FromException(ex);
			}
		}

		/// <summary>
		/// Deletes many objects that matched with the filter
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task DeleteManyAsync<TEntity>(string aliasTypeName, IFilterBy<TEntity> filter, string businessEntityID = null, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
		{
			return RepositoryMediator.DeleteManyAsync<TEntity>(aliasTypeName, filter, businessEntityID, cancellationToken);
		}

		/// <summary>
		/// Deletes many objects that matched with the filter
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		public static Task DeleteManyAsync<TEntity>(IFilterBy<TEntity> filter, string businessEntityID = null, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
		{
			return RepositoryBase<T>.DeleteManyAsync<TEntity>(null, filter, businessEntityID, cancellationToken);
		}

		/// <summary>
		/// Deletes many objects that matched with the filter
		/// </summary>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		protected static Task DeleteManyAsync(RepositoryContext context, string aliasTypeName, IFilterBy<T> filter, string businessEntityID = null, CancellationToken cancellationToken = default(CancellationToken))
		{
			return RepositoryBase<T>.DeleteManyAsync<T>(context, aliasTypeName, filter, businessEntityID, cancellationToken);
		}

		/// <summary>
		/// Deletes many objects that matched with the filter
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		protected static Task DeleteManyAsync(string aliasTypeName, IFilterBy<T> filter, string businessEntityID = null, CancellationToken cancellationToken = default(CancellationToken))
		{
			return RepositoryBase<T>.DeleteManyAsync<T>(aliasTypeName, filter, businessEntityID, cancellationToken);
		}

		/// <summary>
		/// Deletes many objects that matched with the filter
		/// </summary>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		protected static Task DeleteManyAsync(IFilterBy<T> filter, string businessEntityID = null, CancellationToken cancellationToken = default(CancellationToken))
		{
			return RepositoryBase<T>.DeleteManyAsync(null, filter, businessEntityID, cancellationToken);
		}
		#endregion

		#region Find
		/// <summary>
		/// Finds all the matched objects
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <param name="cacheExpirationType">The string that presents the expiration type (Sliding | Absolute)</param>
		/// <param name="cacheExpirationTime">The number that presents the expiration time (in minutes)</param>
		/// <returns>The collection of objects</returns>
		public static List<TEntity> Find<TEntity>(string aliasTypeName, IFilterBy<TEntity> filter, SortBy<TEntity> sort, int pageSize, int pageNumber, string businessEntityID, bool autoAssociateWithMultipleParents, string cacheKey = null, string cacheExpirationType = null, int cacheExpirationTime = 0) where TEntity : class
		{
			return RepositoryMediator.Find<TEntity>(aliasTypeName, filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents, cacheKey, cacheExpirationType, cacheExpirationTime);
		}

		/// <summary>
		/// Finds all the matched objects
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <param name="cacheExpirationType">The string that presents the expiration type (Sliding | Absolute)</param>
		/// <param name="cacheExpirationTime">The number that presents the expiration time (in minutes)</param>
		/// <returns>The collection of objects</returns>
		public static List<TEntity> Find<TEntity>(IFilterBy<TEntity> filter, SortBy<TEntity> sort, int pageSize, int pageNumber, string businessEntityID, bool autoAssociateWithMultipleParents, string cacheKey = null, string cacheExpirationType = null, int cacheExpirationTime = 0) where TEntity : class
		{
			return RepositoryBase<T>.Find<TEntity>(null, filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents, cacheKey, cacheExpirationType, cacheExpirationTime);
		}

		/// <summary>
		/// Finds all the matched objects
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <param name="cacheExpirationType">The string that presents the expiration type (Sliding | Absolute)</param>
		/// <param name="cacheExpirationTime">The number that presents the expiration time (in minutes)</param>
		/// <returns>The collection of objects</returns>
		public static List<T> Find(string aliasTypeName, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessEntityID = null, bool autoAssociateWithMultipleParents = true, string cacheKey = null, string cacheExpirationType = null, int cacheExpirationTime = 0)
		{
			return RepositoryBase<T>.Find<T>(aliasTypeName, filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents, cacheKey, cacheExpirationType, cacheExpirationTime);
		}

		/// <summary>
		/// Finds all the matched objects
		/// </summary>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <param name="cacheExpirationType">The string that presents the expiration type (Sliding | Absolute)</param>
		/// <param name="cacheExpirationTime">The number that presents the expiration time (in minutes)</param>
		/// <returns>The collection of objects</returns>
		public static List<T> Find(IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessEntityID = null, bool autoAssociateWithMultipleParents = true, string cacheKey = null, string cacheExpirationType = null, int cacheExpirationTime = 0)
		{
			return RepositoryBase<T>.Find(null, filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents, cacheKey, cacheExpirationType, cacheExpirationTime);
		}

		/// <summary>
		/// Finds all the matched objects
		/// </summary>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <returns>The collection of objects</returns>
		public static List<T> Find(IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessEntityID, string cacheKey)
		{
			return RepositoryBase<T>.Find(filter, sort, pageSize, pageNumber, businessEntityID, true, cacheKey, null, 0);
		}

		/// <summary>
		/// Finds all the matched objects
		/// </summary>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <returns>The collection of objects</returns>
		public static List<T> Find(IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string cacheKey)
		{
			return RepositoryBase<T>.Find(filter, sort, pageSize, pageNumber, null, cacheKey);
		}

		/// <summary>
		/// Finds all the matched objects
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <param name="cacheExpirationType">The string that presents the expiration type (Sliding | Absolute)</param>
		/// <param name="cacheExpirationTime">The number that presents the expiration time (in minutes)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The collection of objects</returns>
		public static Task<List<TEntity>> FindAsync<TEntity>(string aliasTypeName, IFilterBy<TEntity> filter, SortBy<TEntity> sort, int pageSize, int pageNumber, string businessEntityID, bool autoAssociateWithMultipleParents, string cacheKey = null, string cacheExpirationType = null, int cacheExpirationTime = 0, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
		{
			return RepositoryMediator.FindAsync<TEntity>(aliasTypeName, filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents, cacheKey, cacheExpirationType, cacheExpirationTime, cancellationToken);
		}

		/// <summary>
		/// Finds all the matched objects
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <param name="cacheExpirationType">The string that presents the expiration type (Sliding | Absolute)</param>
		/// <param name="cacheExpirationTime">The number that presents the expiration time (in minutes)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The collection of objects</returns>
		public static Task<List<TEntity>> FindAsync<TEntity>(IFilterBy<TEntity> filter, SortBy<TEntity> sort, int pageSize, int pageNumber, string businessEntityID, bool autoAssociateWithMultipleParents, string cacheKey = null, string cacheExpirationType = null, int cacheExpirationTime = 0, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
		{
			return RepositoryBase<T>.FindAsync<TEntity>(null, filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents, cacheKey, cacheExpirationType, cacheExpirationTime, cancellationToken);
		}

		/// <summary>
		/// Finds all the matched objects
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <param name="cacheExpirationType">The string that presents the expiration type (Sliding | Absolute)</param>
		/// <param name="cacheExpirationTime">The number that presents the expiration time (in minutes)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The collection of objects</returns>
		public static Task<List<T>> FindAsync(string aliasTypeName, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessEntityID = null, bool autoAssociateWithMultipleParents = true, string cacheKey = null, string cacheExpirationType = null, int cacheExpirationTime = 0, CancellationToken cancellationToken = default(CancellationToken))
		{
			return RepositoryBase<T>.FindAsync<T>(aliasTypeName, filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents, cacheKey, cacheExpirationType, cacheExpirationTime, cancellationToken);
		}

		/// <summary>
		/// Finds all the matched objects
		/// </summary>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <param name="cacheExpirationType">The string that presents the expiration type (Sliding | Absolute)</param>
		/// <param name="cacheExpirationTime">The number that presents the expiration time (in minutes)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The collection of objects</returns>
		public static Task<List<T>> FindAsync(IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessEntityID = null, bool autoAssociateWithMultipleParents = true, string cacheKey = null, string cacheExpirationType = null, int cacheExpirationTime = 0, CancellationToken cancellationToken = default(CancellationToken))
		{
			return RepositoryBase<T>.FindAsync(null, filter, sort, pageSize, pageNumber, businessEntityID, autoAssociateWithMultipleParents, cacheKey, cacheExpirationType, cacheExpirationTime, cancellationToken);
		}

		/// <summary>
		/// Finds all the matched objects
		/// </summary>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The collection of objects</returns>
		public static Task<List<T>> FindAsync(IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string businessEntityID, string cacheKey, CancellationToken cancellationToken = default(CancellationToken))
		{
			return RepositoryBase<T>.FindAsync(filter, sort, pageSize, pageNumber, businessEntityID, true, cacheKey, null, 0, cancellationToken);
		}

		/// <summary>
		/// Finds all the matched objects
		/// </summary>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of identities</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The collection of objects</returns>
		public static Task<List<T>> FindAsync(IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber, string cacheKey, CancellationToken cancellationToken = default(CancellationToken))
		{
			return RepositoryBase<T>.FindAsync(filter, sort, pageSize, pageNumber, null, cacheKey, cancellationToken);
		}
		#endregion

		#region Count
		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for counting objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of total number of objects</param>
		/// <param name="cacheExpirationType">The string that presents the expiration type (Sliding | Absolute)</param>
		/// <param name="cacheExpirationTime">The number that presents the expiration time (in minutes)</param>
		/// <returns>The number of all matched objects</returns>
		public static long Count<TEntity>(string aliasTypeName, IFilterBy<TEntity> filter, string businessEntityID, bool autoAssociateWithMultipleParents, string cacheKey = null, string cacheExpirationType = null, int cacheExpirationTime = 0) where TEntity : class
		{
			return RepositoryMediator.Count<TEntity>(aliasTypeName, filter, businessEntityID, autoAssociateWithMultipleParents, cacheKey, cacheExpirationType, cacheExpirationTime);
		}

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="filter">The expression for counting objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of total number of objects</param>
		/// <param name="cacheExpirationType">The string that presents the expiration type (Sliding | Absolute)</param>
		/// <param name="cacheExpirationTime">The number that presents the expiration time (in minutes)</param>
		/// <returns>The number of all matched objects</returns>
		public static long Count<TEntity>(IFilterBy<TEntity> filter, string businessEntityID, bool autoAssociateWithMultipleParents, string cacheKey = null, string cacheExpirationType = null, int cacheExpirationTime = 0) where TEntity : class
		{
			return RepositoryBase<T>.Count<TEntity>(null, filter, businessEntityID, autoAssociateWithMultipleParents, cacheKey, cacheExpirationType, cacheExpirationTime);
		}

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="filter">The expression for counting objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of total number of objects</param>
		/// <returns>The number of all matched objects</returns>
		public static long Count<TEntity>(IFilterBy<TEntity> filter, string businessEntityID, string cacheKey = null) where TEntity : class
		{
			return RepositoryBase<T>.Count<TEntity>(filter, businessEntityID, true, cacheKey, null, 0);
		}

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for counting objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of total number of objects</param>
		/// <param name="cacheExpirationType">The string that presents the expiration type (Sliding | Absolute)</param>
		/// <param name="cacheExpirationTime">The number that presents the expiration time (in minutes)</param>
		/// <returns>The number of all matched objects</returns>
		public static long Count(string aliasTypeName, IFilterBy<T> filter, string businessEntityID, bool autoAssociateWithMultipleParents, string cacheKey = null, string cacheExpirationType = null, int cacheExpirationTime = 0)
		{
			return RepositoryBase<T>.Count<T>(aliasTypeName, filter, businessEntityID, autoAssociateWithMultipleParents, cacheKey, cacheExpirationType, cacheExpirationTime);
		}

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="filter">The expression for counting objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of total number of objects</param>
		/// <param name="cacheExpirationType">The string that presents the expiration type (Sliding | Absolute)</param>
		/// <param name="cacheExpirationTime">The number that presents the expiration time (in minutes)</param>
		/// <returns>The number of all matched objects</returns>
		public static long Count(IFilterBy<T> filter, string businessEntityID, bool autoAssociateWithMultipleParents, string cacheKey = null, string cacheExpirationType = null, int cacheExpirationTime = 0)
		{
			return RepositoryBase<T>.Count<T>(filter, businessEntityID, autoAssociateWithMultipleParents, cacheKey, cacheExpirationType, cacheExpirationTime);
		}

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="filter">The expression for counting objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of total number of objects</param>
		/// <returns>The number of all matched objects</returns>
		public static long Count(IFilterBy<T> filter = null, string businessEntityID = null, string cacheKey = null)
		{
			return RepositoryBase<T>.Count(filter, businessEntityID, true, cacheKey);
		}

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for counting objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of total number of objects</param>
		/// <param name="cacheExpirationType">The string that presents the expiration type (Sliding | Absolute)</param>
		/// <param name="cacheExpirationTime">The number that presents the expiration time (in minutes)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The number of all matched objects</returns>
		public static Task<long> CountAsync<TEntity>(string aliasTypeName, IFilterBy<TEntity> filter, string businessEntityID, bool autoAssociateWithMultipleParents, string cacheKey = null, string cacheExpirationType = null, int cacheExpirationTime = 0, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
		{
			return RepositoryMediator.CountAsync<TEntity>(aliasTypeName, filter, businessEntityID, autoAssociateWithMultipleParents, cacheKey, cacheExpirationType, cacheExpirationTime, cancellationToken);
		}

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="filter">The expression for counting objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of total number of objects</param>
		/// <param name="cacheExpirationType">The string that presents the expiration type (Sliding | Absolute)</param>
		/// <param name="cacheExpirationTime">The number that presents the expiration time (in minutes)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The number of all matched objects</returns>
		public static Task<long> CountAsync<TEntity>(IFilterBy<TEntity> filter, string businessEntityID, bool autoAssociateWithMultipleParents, string cacheKey = null, string cacheExpirationType = null, int cacheExpirationTime = 0, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
		{
			return RepositoryBase<T>.CountAsync<TEntity>(null, filter, businessEntityID, autoAssociateWithMultipleParents, cacheKey, cacheExpirationType, cacheExpirationTime, cancellationToken);
		}

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="filter">The expression for counting objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of total number of objects</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The number of all matched objects</returns>
		public static Task<long> CountAsync<TEntity>(IFilterBy<TEntity> filter, string businessEntityID, string cacheKey = null, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
		{
			return RepositoryBase<T>.CountAsync<TEntity>(filter, businessEntityID, true, cacheKey, null, 0, cancellationToken);
		}

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for counting objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of total number of objects</param>
		/// <param name="cacheExpirationType">The string that presents the expiration type (Sliding | Absolute)</param>
		/// <param name="cacheExpirationTime">The number that presents the expiration time (in minutes)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The number of all matched objects</returns>
		public static Task<long> CountAsync(string aliasTypeName, IFilterBy<T> filter, string businessEntityID, bool autoAssociateWithMultipleParents, string cacheKey = null, string cacheExpirationType = null, int cacheExpirationTime = 0, CancellationToken cancellationToken = default(CancellationToken))
		{
			return RepositoryBase<T>.CountAsync<T>(aliasTypeName, filter, businessEntityID, autoAssociateWithMultipleParents, cacheKey, cacheExpirationType, cacheExpirationTime, cancellationToken);
		}

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="filter">The expression for counting objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="autoAssociateWithMultipleParents">true to auto associate with multiple parents (if has - default is true)</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of total number of objects</param>
		/// <param name="cacheExpirationType">The string that presents the expiration type (Sliding | Absolute)</param>
		/// <param name="cacheExpirationTime">The number that presents the expiration time (in minutes)</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The number of all matched objects</returns>
		public static Task<long> CountAsync(IFilterBy<T> filter, string businessEntityID, bool autoAssociateWithMultipleParents, string cacheKey = null, string cacheExpirationType = null, int cacheExpirationTime = 0, CancellationToken cancellationToken = default(CancellationToken))
		{
			return RepositoryBase<T>.CountAsync(null, filter, businessEntityID, autoAssociateWithMultipleParents, cacheKey, cacheExpirationType, cacheExpirationTime, cancellationToken);
		}

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="filter">The expression for counting objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of total number of objects</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The number of all matched objects</returns>
		public static Task<long> CountAsync(IFilterBy<T> filter, string businessEntityID, string cacheKey = null, CancellationToken cancellationToken = default(CancellationToken))
		{
			return RepositoryBase<T>.CountAsync(filter, businessEntityID, true, cacheKey, null, 0, cancellationToken);
		}

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="filter">The expression for counting objects</param>
		/// <param name="cacheKey">The string that presents key for fetching/storing cache of total number of objects</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The number of all matched objects</returns>
		public static Task<long> CountAsync(IFilterBy<T> filter = null, string cacheKey = null, CancellationToken cancellationToken = default(CancellationToken))
		{
			return RepositoryBase<T>.CountAsync(filter, null, cacheKey, cancellationToken);
		}
		#endregion

		#region Search (by query)
		/// <summary>
		/// Searchs all the matched objects (using full-text search)
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="query">The expression for searching objects</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns>The collection of objects</returns>
		public static List<TEntity> Search<TEntity>(string aliasTypeName, string query, IFilterBy<TEntity> filter, int pageSize, int pageNumber, string businessEntityID) where TEntity : class
		{
			return RepositoryMediator.Search<TEntity>(aliasTypeName, query, filter, pageSize, pageNumber, businessEntityID);
		}

		/// <summary>
		/// Searchs all the matched objects (using full-text search)
		/// </summary>
		/// <param name="query">The expression for searching objects</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns>The collection of objects</returns>
		public static List<TEntity> Search<TEntity>(string query, IFilterBy<TEntity> filter, int pageSize, int pageNumber, string businessEntityID) where TEntity : class
		{
			return RepositoryBase<T>.Search<TEntity>(null, query, filter, pageSize, pageNumber, businessEntityID);
		}

		/// <summary>
		/// Searchs all the matched objects (using full-text search)
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="query">The expression for searching objects</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns>The collection of objects</returns>
		public static List<T> Search(string aliasTypeName, string query, IFilterBy<T> filter, int pageSize, int pageNumber, string businessEntityID)
		{
			return RepositoryBase<T>.Search<T>(aliasTypeName, query, filter, pageSize, pageNumber, businessEntityID);
		}

		/// <summary>
		/// Searchs all the matched objects (using full-text search)
		/// </summary>
		/// <param name="query">The expression for searching objects</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns>The collection of objects</returns>
		public static List<T> Search(string query, IFilterBy<T> filter, int pageSize, int pageNumber, string businessEntityID = null)
		{
			return RepositoryBase<T>.Search(null, query, filter, pageSize, pageNumber, businessEntityID);
		}

		/// <summary>
		/// Searchs all the matched objects (using full-text search)
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="query">The expression for searching objects</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The collection of objects</returns>
		public static Task<List<TEntity>> SearchAsync<TEntity>(string aliasTypeName, string query, IFilterBy<TEntity> filter, int pageSize, int pageNumber, string businessEntityID, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
		{
			return RepositoryMediator.SearchAsync<TEntity>(aliasTypeName, query, filter, pageSize, pageNumber, businessEntityID, cancellationToken);
		}

		/// <summary>
		/// Searchs all the matched objects (using full-text search)
		/// </summary>
		/// <param name="query">The expression for searching objects</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The collection of objects</returns>
		public static Task<List<TEntity>> SearchAsync<TEntity>(string query, IFilterBy<TEntity> filter, int pageSize, int pageNumber, string businessEntityID, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
		{
			return RepositoryBase<T>.SearchAsync<TEntity>(null, query, filter, pageSize, pageNumber, businessEntityID, cancellationToken);
		}

		/// <summary>
		/// Searchs all the matched objects (using full-text search)
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="query">The expression for searching objects</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The collection of objects</returns>
		public static Task<List<T>> SearchAsync(string aliasTypeName, string query, IFilterBy<T> filter, int pageSize, int pageNumber, string businessEntityID, CancellationToken cancellationToken = default(CancellationToken))
		{
			return RepositoryBase<T>.SearchAsync<T>(aliasTypeName, query, filter, pageSize, pageNumber, businessEntityID, cancellationToken);
		}

		/// <summary>
		/// Searchs all the matched objects (using full-text search)
		/// </summary>
		/// <param name="query">The expression for searching objects</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The collection of objects</returns>
		public static Task<List<T>> SearchAsync(string query, IFilterBy<T> filter, int pageSize, int pageNumber, string businessEntityID, CancellationToken cancellationToken = default(CancellationToken))
		{
			return RepositoryBase<T>.SearchAsync(null, query, filter, pageSize, pageNumber, businessEntityID, cancellationToken);
		}

		/// <summary>
		/// Searchs all the matched objects (using full-text search)
		/// </summary>
		/// <param name="query">The expression for searching objects</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The collection of objects</returns>
		public static Task<List<T>> SearchAsync(string query, IFilterBy<T> filter, int pageSize, int pageNumber, CancellationToken cancellationToken = default(CancellationToken))
		{
			return RepositoryBase<T>.SearchAsync(null, query, filter, pageSize, pageNumber, null, cancellationToken);
		}
		#endregion

		#region Count (by query)
		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="query">The expression (full-text search) for counting objects</param>
		/// <param name="filter">The expression (additional filter) for counting objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns>The number of all matched objects</returns>
		public static long CountByQuery<TEntity>(string aliasTypeName, string query, IFilterBy<TEntity> filter, string businessEntityID) where TEntity : class
		{
			return RepositoryMediator.Count<TEntity>(aliasTypeName, query, filter, businessEntityID);
		}

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="query">The expression (full-text search) for counting objects</param>
		/// <param name="filter">The expression (additional filter) for counting objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns>The number of all matched objects</returns>
		public static long CountByQuery<TEntity>(string query, IFilterBy<TEntity> filter = null, string businessEntityID = null) where TEntity : class
		{
			return RepositoryBase<T>.CountByQuery<TEntity>(null, query, filter, businessEntityID);
		}

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="query">The expression (full-text search) for counting objects</param>
		/// <param name="filter">The expression (additional filter) for counting objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns>The number of all matched objects</returns>
		public static long CountByQuery(string aliasTypeName, string query, IFilterBy<T> filter, string businessEntityID)
		{
			return RepositoryBase<T>.CountByQuery<T>(aliasTypeName, query, filter, businessEntityID);
		}

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="query">The expression (full-text search) for counting objects</param>
		/// <param name="filter">The expression (additional filter) for counting objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <returns>The number of all matched objects</returns>
		public static long CountByQuery(string query, IFilterBy<T> filter = null, string businessEntityID = null)
		{
			return RepositoryBase<T>.CountByQuery(null, query, filter, businessEntityID);
		}

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="query">The expression (full-text search) for counting objects</param>
		/// <param name="filter">The expression (additional filter) for counting objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The number of all matched objects</returns>
		public static Task<long> CountByQueryAsync<TEntity>(string aliasTypeName, string query, IFilterBy<TEntity> filter, string businessEntityID, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
		{
			return RepositoryMediator.CountAsync<TEntity>(aliasTypeName, query, filter, businessEntityID, cancellationToken);
		}

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="query">The expression (full-text search) for counting objects</param>
		/// <param name="filter">The expression (additional filter) for counting objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The number of all matched objects</returns>
		public static Task<long> CountByQueryAsync<TEntity>(string query, IFilterBy<TEntity> filter, string businessEntityID, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
		{
			return RepositoryBase<T>.CountByQueryAsync<TEntity>(null, query, filter, businessEntityID, cancellationToken);
		}

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="query">The expression (full-text search) for counting objects</param>
		/// <param name="filter">The expression (additional filter) for counting objects</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The number of all matched objects</returns>
		public static Task<long> CountByQueryAsync<TEntity>(string query, IFilterBy<TEntity> filter = null, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
		{
			return RepositoryBase<T>.CountByQueryAsync<TEntity>(null, query, filter, null, cancellationToken);
		}

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="query">The expression (full-text search) for counting objects</param>
		/// <param name="filter">The expression (additional filter) for counting objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The number of all matched objects</returns>
		public static Task<long> CountByQueryAsync(string aliasTypeName, string query, IFilterBy<T> filter, string businessEntityID, CancellationToken cancellationToken = default(CancellationToken))
		{
			return RepositoryBase<T>.CountByQueryAsync<T>(aliasTypeName, query, filter, businessEntityID, cancellationToken);
		}

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="query">The expression (full-text search) for counting objects</param>
		/// <param name="filter">The expression (additional filter) for counting objects</param>
		/// <param name="businessEntityID">The identity of a business entity for working with extended properties/seperated data of a business content-type</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The number of all matched objects</returns>
		public static Task<long> CountByQueryAsync(string query, IFilterBy<T> filter, string businessEntityID, CancellationToken cancellationToken = default(CancellationToken))
		{
			return RepositoryBase<T>.CountByQueryAsync(null, query, filter, businessEntityID, cancellationToken);
		}

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="query">The expression (full-text search) for counting objects</param>
		/// <param name="filter">The expression (additional filter) for counting objects</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The number of all matched objects</returns>
		public static Task<long> CountByQueryAsync(string query, IFilterBy<T> filter = null, CancellationToken cancellationToken = default(CancellationToken))
		{
			return RepositoryBase<T>.CountByQueryAsync(query, filter, null, cancellationToken);
		}
		#endregion

		#region Get/Set properties
		/// <summary>
		/// Gets the value of a specified property
		/// </summary>
		/// <param name="name">The name of the property</param>
		/// <param name="value">The value of the property</param>
		/// <returns>true if the property is getted; otherwise false;</returns>
		public virtual bool TryGetProperty(string name, out object value)
		{
			value = null;
			var attributes = this.GetProperties().ToDictionary(attribute => attribute.Name);
			try
			{
				if (attributes.ContainsKey(name))
				{
					value = (this as T).GetAttributeValue(name);
					return true;
				}
				else if (this is IBusinessEntity && (this as IBusinessEntity).ExtendedProperties != null && (this as IBusinessEntity).ExtendedProperties.ContainsKey(name))
				{
					value = (this as IBusinessEntity).ExtendedProperties[name];
					return true;
				}
				else
					return false;		
			}
			catch
			{
				return false;
			}
		}

		/// <summary>
		/// Gets the value of a specified property
		/// </summary>
		/// <param name="name">The name of the property</param>
		/// <returns></returns>
		public override object GetProperty(string name)
		{
			object value;
			return this.TryGetProperty(name, out value)
				? value
				: null;
		}

		/// <summary>
		/// Gets the value of a specified property
		/// </summary>
		/// <typeparam name="TValue"></typeparam>
		/// <param name="name">The name of the property</param>
		/// <param name="value">The value of the property</param>
		/// <returns>true if the property is getted; otherwise false;</returns>
		public virtual bool TryGetProperty<TValue>(string name, out TValue value)
		{
			// assign default
			value = default(TValue);

			// get value & cast
			object theValue = null;
			if (this.TryGetProperty(name, out theValue))
			{
				value = theValue != null ? theValue.CastAs<TValue>() : default(TValue);
				return true;
			}

			// return the default state
			return false;
		}

		/// <summary>
		/// Gets the value of a specified property
		/// </summary>
		/// <param name="name">The name of the property</param>
		/// <returns></returns>
		public virtual TValue GetProperty<TValue>(string name)
		{
			TValue value;
			return this.TryGetProperty<TValue>(name, out value)
				? value
				: default(TValue);
		}

		/// <summary>
		/// Gets the value of a specified property
		/// </summary>
		/// <param name="name">The name of the property</param>
		/// <returns>true if the property is setted; otherwise false;</returns>
		public virtual bool TrySetProperty(string name, object value)
		{
			value = null;
			var attributes = this.GetProperties().ToDictionary(attribute => attribute.Name);
			try
			{
				if (attributes.ContainsKey(name))
				{
					this.SetAttributeValue(attributes[name], value, true);
					return true;
				}
				else if (this is IBusinessEntity && (this as IBusinessEntity).ExtendedProperties != null)
				{
					if ((this as IBusinessEntity).ExtendedProperties.ContainsKey(name))
						(this as IBusinessEntity).ExtendedProperties[name] = value;
					else
						(this as IBusinessEntity).ExtendedProperties.Add(name, value);
					return true;
				}
				else
					return false;
			}
			catch
			{
				return false;
			}
		}

		/// <summary>
		/// Sets the value of a specified property
		/// </summary>
		/// <param name="name">The name of the property</param>
		/// <param name="value">The value of the property</param>
		public override void SetProperty(string name, object value)
		{
			this.TrySetProperty(name, value);
		}
		#endregion

		#region JSON/XML conversions
		/// <summary>
		/// Serializes this object to JSON object
		/// </summary>
		/// <param name="addTypeOfExtendedProperties">true to add type of all extended properties (named with surfix '$Type')</param>
		/// <returns></returns>
		public override JObject ToJson(bool addTypeOfExtendedProperties = false)
		{
			var json = (this as T).ToJson() as JObject;
			if (this is IBusinessEntity && (this as IBusinessEntity).ExtendedProperties != null && (this as IBusinessEntity).ExtendedProperties.Count > 0)
				(this as IBusinessEntity).ExtendedProperties.ForEach(property =>
				{
					Type type = property.Value != null
						? property.Value.GetType()
						: null;

					if (addTypeOfExtendedProperties && type != null)
						json.Add(new JProperty(property.Key + "$Type", type.IsPrimitiveType() ? type.ToString() : type.GetTypeName()));

					json.Add(new JProperty(property.Key, type == null || type.IsPrimitiveType() ? property.Value : property.Value is RepositoryBase ? (property.Value as RepositoryBase).ToJson(addTypeOfExtendedProperties) : property.Value.ToJson()));
				});
			return json;
		}

		/// <summary>
		/// Parses the JSON object and copy values into this object
		/// </summary>
		/// <param name="json">The JSON object that contains information</param>
		public override void ParseJson(JObject json)
		{
			if (json != null)
				this.CopyFrom(json);
		}

		/// <summary>
		/// Serializes this object to XML object
		/// </summary>
		/// <param name="addTypeOfExtendedProperties">true to add type of all extended properties (attribute named '$type')</param>
		/// <returns></returns>
		public override XElement ToXml(bool addTypeOfExtendedProperties)
		{
			var xml = (this as T).ToXml();
			if (this is IBusinessEntity && (this as IBusinessEntity).ExtendedProperties != null && (this as IBusinessEntity).ExtendedProperties.Count > 0)
				(this as IBusinessEntity).ExtendedProperties.ForEach(property =>
				{
					Type type = property.Value != null
						? property.Value.GetType()
						: null;

					var value = type == null || type.IsPrimitiveType()
						? property.Value
						: property.Value is RepositoryBase
							? (property.Value as RepositoryBase).ToXml(addTypeOfExtendedProperties)
							: property.Value.ToXml();

					var element = addTypeOfExtendedProperties && type != null
						? new XElement(XName.Get(property.Key), new XAttribute("$type", type.IsPrimitiveType() ? type.ToString() : type.GetTypeName()), value)
						: new XElement(XName.Get(property.Key), value);
					xml.Add(element);
				});
			return xml;
		}

		/// <summary>
		/// Parses the XML object and copy values into this object
		/// </summary>
		/// <param name="xml">The XML object that contains information</param>
		public override void ParseXml(XContainer xml)
		{
			if (xml != null)
				this.CopyFrom(xml.FromXml<T>());
		}
		#endregion

		#region Properties & Methods of IBussineEntity
		/// <summary>
		/// Gets or sets the title
		/// </summary>
		[BsonIgnoreIfNull, Property(MaxLength = 250), IgnoreIfNull, Sortable, Searchable]
		public virtual string Title { get; set; }

		/// <summary>
		/// Gets or sets the identity of the business system that the object is belong to (means the run-time system)
		/// </summary>
		[JsonIgnore, XmlIgnore, BsonIgnoreIfNull, Property(MaxLength = 32), IgnoreIfNull, Sortable(IndexName = "System")]
		public virtual string SystemID { get; set; }

		/// <summary>
		/// Gets or sets the identity of the business repository that the object is belong to (means the run-time business module)
		/// </summary>
		[JsonIgnore, XmlIgnore, BsonIgnoreIfNull, Property(MaxLength = 32), IgnoreIfNull, Sortable(IndexName = "System")]
		public virtual string RepositoryID { get; set; }

		/// <summary>
		/// Gets or sets the identity of the business entity that the object is belong to (means the run-time business content-type)
		/// </summary>
		[JsonIgnore, XmlIgnore, BsonIgnoreIfNull, Property(MaxLength = 32), IgnoreIfNull, Sortable(IndexName = "System")]
		public virtual string EntityID { get; set; }

		/// <summary>
		/// Gets or sets the collection of extended properties
		/// </summary>
		[JsonIgnore, XmlIgnore, BsonIgnoreIfNull, Ignore]
		public virtual Dictionary<string, object> ExtendedProperties { get; set; }

		/// <summary>
		/// Gets the business entity that marks as parent of this object
		/// </summary>
		[JsonIgnore, XmlIgnore, BsonIgnore, Ignore]
		public virtual IBusinessEntity Parent { get; }

		/// <summary>
		/// Gets or sets the original privileges (means original working permissions)
		/// </summary>
		[JsonIgnore, XmlIgnore, BsonIgnoreIfNull, AsJson]
		public virtual Privileges OriginalPrivileges { get; set; }

		/// <summary>
		/// The privileges that are combined from original privileges and parent privileges
		/// </summary>
		[NonSerialized]
		protected Privileges Privileges = null;

		/// <summary>
		/// Gets the actual privileges (mean the combined privileges)
		/// </summary>
		[JsonIgnore, XmlIgnore, BsonIgnore, Ignore]
		public virtual Privileges WorkingPrivileges
		{
			get
			{
				if (this.Privileges == null)
					this.Privileges = User.CombinePrivileges(this.OriginalPrivileges, this.Parent != null ? this.Parent.WorkingPrivileges : null);
				return this.Privileges;
			}
		}
		#endregion

	}

	//  --------------------------------------------------------------------------------------------

	#region Identity generator (for working with MongoDB)
	/// <summary>
	/// Generates identity as UUID (128 bits) for MongoDB documents
	/// </summary>
	public class IdentityGenerator : MongoDB.Bson.Serialization.IIdGenerator
	{
		public object GenerateId(object container, object document)
		{
			return Guid.NewGuid().ToString("N").ToLower();
		}

		public bool IsEmpty(object id)
		{
			return id == null || id.Equals(string.Empty);
		}
	}
	#endregion

}