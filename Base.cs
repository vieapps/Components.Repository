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
#endregion

namespace net.vieapps.Components.Repository
{
	/// <summary>
	/// Base class of an entity in the a repository
	/// </summary>
	[Serializable]
	public abstract class RepositoryBase
	{
		/// <summary>
		/// Gets or sets the object identity (primary-key)
		/// </summary>
		[BsonId(IdGenerator = typeof(IdentityGenerator))]
		[PrimaryKey(MaxLength = 32)]
		public virtual string ID { get; set; }

		/// <summary>
		/// Gets the score while searching
		/// </summary>
		[Ignore, JsonIgnore, XmlIgnore, BsonIgnoreIfNull]
		public virtual double? SearchScore { get; set; }

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
		/// Serializes this object to JSON object
		/// </summary>
		/// <param name="addTypeOfExtendedProperties">true to add type of all extended properties (named with surfix '$Type')</param>
		/// <returns></returns>
		public abstract JObject ToJson(bool addTypeOfExtendedProperties);

		/// <summary>
		/// Serializes this object to JSON object
		/// </summary>
		/// <returns></returns>
		public virtual JObject ToJson()
		{
			return this.ToJson(false);
		}

		/// <summary>
		/// Parses the JSON object and copy values into this object
		/// </summary>
		/// <param name="json">The JSON object that contains information</param>
		public abstract void ParseJson(JObject json);

		/// <summary>
		/// Serializes this object to XML object
		/// </summary>
		/// <param name="addTypeOfExtendedProperties">true to add type of all extended properties (attribute named '$type')</param>
		/// <returns></returns>
		public abstract XElement ToXml(bool addTypeOfExtendedProperties);

		/// <summary>
		/// Serializes this object to XML object
		/// </summary>
		/// <returns></returns>
		public virtual XElement ToXml()
		{
			return this.ToXml(false);
		}

		/// <summary>
		/// Parses the XML object and copy values into this object
		/// </summary>
		/// <param name="xml">The XML object that contains information</param>
		public abstract void ParseXml(XContainer xml);
		#endregion

	}

	//  --------------------------------------------------------------------------------------------

	/// <summary>
	/// Base class of an entity of a repository with helper methods to perform CRUD operations, count, find, and query (full-text search)
	/// </summary>
	[Serializable]
	[DebuggerDisplay("ID = {ID}, Type = {typeof(T).FullName}")]
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
		/// Gets an object (the first matched with the filter)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <returns>The first object that matched with the filter; otherwise null</returns>
		public static TEntity Get<TEntity>(RepositoryContext context, string aliasTypeName, IFilterBy<TEntity> filter, SortBy<TEntity> sort = null) where TEntity : class
		{
			try
			{
				return RepositoryMediator.Get<TEntity>(aliasTypeName, filter, sort);
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
		/// <returns>The first object that matched with the filter; otherwise null</returns>
		public static TEntity Get<TEntity>(string aliasTypeName, IFilterBy<TEntity> filter, SortBy<TEntity> sort = null) where TEntity : class
		{
			return RepositoryMediator.Get<TEntity>(aliasTypeName, filter, sort);
		}

		/// <summary>
		/// Gets an object (the first matched with the filter)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <returns>The first object that matched with the filter; otherwise null</returns>
		public static TEntity Get<TEntity>(IFilterBy<TEntity> filter, SortBy<TEntity> sort = null) where TEntity : class
		{
			return RepositoryBase<T>.Get<TEntity>(null, filter, sort);
		}

		/// <summary>
		/// Gets an object (the first matched with the filter)
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <returns>The first object that matched with the filter; otherwise null</returns>
		public static T Get(string aliasTypeName, IFilterBy<T> filter, SortBy<T> sort = null)
		{
			return RepositoryBase<T>.Get<T>(aliasTypeName, filter, sort);
		}

		/// <summary>
		/// Gets an object (the first matched with the filter)
		/// </summary>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <returns>The first object that matched with the filter; otherwise null</returns>
		public static T Get(IFilterBy<T> filter, SortBy<T> sort = null)
		{
			return RepositoryBase<T>.Get(null, filter, sort);
		}

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
		/// Gets an object (the first matched with the filter)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The first object that matched with the filter; otherwise null</returns>
		public static Task<TEntity> GetAsync<TEntity>(RepositoryContext context, string aliasTypeName, IFilterBy<TEntity> filter, SortBy<TEntity> sort = null, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
		{
			try
			{
				return RepositoryMediator.GetAsync<TEntity>(context, aliasTypeName, filter, sort, cancellationToken);
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
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The first object that matched with the filter; otherwise null</returns>
		public static Task<TEntity> GetAsync<TEntity>(string aliasTypeName, IFilterBy<TEntity> filter, SortBy<TEntity> sort = null, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
		{
			return RepositoryMediator.GetAsync<TEntity>(aliasTypeName, filter, sort, cancellationToken);
		}

		/// <summary>
		/// Gets an object (the first matched with the filter)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The first object that matched with the filter; otherwise null</returns>
		public static Task<TEntity> GetAsync<TEntity>(IFilterBy<TEntity> filter, SortBy<TEntity> sort = null, CancellationToken cancellationToken = default(CancellationToken)) where TEntity : class
		{
			return RepositoryBase<T>.GetAsync<TEntity>(null, filter, sort, cancellationToken);
		}

		/// <summary>
		/// Gets an object (the first matched with the filter)
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The first object that matched with the filter; otherwise null</returns>
		public static Task<T> GetAsync(string aliasTypeName, IFilterBy<T> filter, SortBy<T> sort = null, CancellationToken cancellationToken = default(CancellationToken))
		{
			return RepositoryBase<T>.GetAsync<T>(aliasTypeName, filter, sort, cancellationToken);
		}

		/// <summary>
		/// Gets an object (the first matched with the filter)
		/// </summary>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns>The first object that matched with the filter; otherwise null</returns>
		public static Task<T> GetAsync(IFilterBy<T> filter, SortBy<T> sort = null, CancellationToken cancellationToken = default(CancellationToken))
		{
			return RepositoryBase<T>.GetAsync(null, filter, sort, cancellationToken);
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
		/// <returns></returns>
		public static Task ReplaceAsync<TEntity>(RepositoryContext context, string aliasTypeName, TEntity @object) where TEntity : class
		{
			try
			{
				return RepositoryMediator.ReplaceAsync<TEntity>(context, aliasTypeName, @object);
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
		/// <returns></returns>
		public static Task ReplaceAsync<TEntity>(string aliasTypeName, TEntity @object) where TEntity : class
		{
			return RepositoryMediator.ReplaceAsync<TEntity>(aliasTypeName, @object);
		}

		/// <summary>
		/// Updates an object (using replace method)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <returns></returns>
		public static Task ReplaceAsync<TEntity>(TEntity @object) where TEntity : class
		{
			return RepositoryBase<T>.ReplaceAsync<TEntity>(null, @object);
		}

		/// <summary>
		/// Updates the instance of this object (using replace method)
		/// </summary>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		protected virtual Task ReplaceAsync(RepositoryContext context, string aliasTypeName)
		{
			return RepositoryBase<T>.ReplaceAsync<T>(context, aliasTypeName, this as T);
		}

		/// <summary>
		/// Updates the instance of this object (using replace method)
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <returns></returns>
		protected virtual Task ReplaceAsync(string aliasTypeName = null)
		{
			return RepositoryBase<T>.ReplaceAsync<T>(aliasTypeName, this as T);
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
		/// <returns></returns>
		public static Task UpdateAsync<TEntity>(RepositoryContext context, string aliasTypeName, TEntity @object) where TEntity : class
		{
			try
			{
				return RepositoryMediator.UpdateAsync<TEntity>(context, aliasTypeName, @object);
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
		/// <returns></returns>
		public static Task UpdateAsync<TEntity>(string aliasTypeName, TEntity @object) where TEntity : class
		{
			return RepositoryMediator.UpdateAsync<TEntity>(aliasTypeName, @object);
		}

		/// <summary>
		/// Updates an object (only update changed attributes)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="object">The object that presents the instance in the repository need to be updated</param>
		/// <returns></returns>
		public static Task UpdateAsync<TEntity>(TEntity @object) where TEntity : class
		{
			return RepositoryBase<T>.UpdateAsync<TEntity>(null, @object);
		}

		/// <summary>
		/// Updates the instance of this object (only update changed attributes)
		/// </summary>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		protected virtual Task UpdateAsync(RepositoryContext context, string aliasTypeName)
		{
			return RepositoryBase<T>.UpdateAsync<T>(context, aliasTypeName, this as T);
		}

		/// <summary>
		/// Updates the instance of this object (only update changed attributes)
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <returns></returns>
		protected virtual Task UpdateAsync(string aliasTypeName = null)
		{
			return RepositoryBase<T>.UpdateAsync<T>(aliasTypeName, this as T);
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
		/// <returns></returns>
		public static Task DeleteAsync<TEntity>(RepositoryContext context, string aliasTypeName, string id) where TEntity : class
		{
			try
			{
				return RepositoryMediator.DeleteAsync<TEntity>(context, aliasTypeName, id);
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
		/// <returns></returns>
		public static Task DeleteAsync<TEntity>(string aliasTypeName, string id) where TEntity : class
		{
			return RepositoryMediator.DeleteAsync<TEntity>(aliasTypeName, id);
		}

		/// <summary>
		/// Deletes an object
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="id">The string that presents object identity that want to delete instance from repository</param>
		/// <returns></returns>
		public static Task DeleteAsync<TEntity>(string id) where TEntity : class
		{
			return RepositoryBase<T>.DeleteAsync<TEntity>(null, id);
		}

		/// <summary>
		/// Deletes the instance of this object
		/// </summary>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		protected virtual Task DeleteAsync(RepositoryContext context, string aliasTypeName)
		{
			return !string.IsNullOrWhiteSpace(this.ID)
				? RepositoryBase<T>.DeleteAsync<T>(context, aliasTypeName, this.ID)
				: Task.FromException(new ArgumentNullException("ID", "The identity of the object is null or empty"));
		}

		/// <summary>
		/// Deletes the instance of this object
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		protected virtual Task DeleteAsync(string aliasTypeName = null)
		{
			return !string.IsNullOrWhiteSpace(this.ID)
				? RepositoryBase<T>.DeleteAsync<T>(aliasTypeName, this.ID)
				: Task.FromException(new ArgumentNullException("ID", "The identity of the object is null or empty"));
		}

		/// <summary>
		/// Deletes many objects that matched with the filter
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for filtering objects</param>
		public static void DeleteMany<TEntity>(RepositoryContext context, string aliasTypeName, IFilterBy<TEntity> filter) where TEntity : class
		{
			try
			{
				RepositoryMediator.DeleteMany<TEntity>(context, aliasTypeName, filter);
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
		public static void DeleteMany<TEntity>(string aliasTypeName, IFilterBy<TEntity> filter) where TEntity : class
		{
			RepositoryMediator.DeleteMany<TEntity>(aliasTypeName, filter);
		}

		/// <summary>
		/// Deletes many objects that matched with the filter
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="filter">The expression for filtering objects</param>
		public static void DeleteMany<TEntity>(IFilterBy<TEntity> filter) where TEntity : class
		{
			RepositoryBase<T>.DeleteMany<TEntity>(null, filter);
		}

		/// <summary>
		/// Deletes many objects that matched with the filter
		/// </summary>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for filtering objects</param>
		protected static void DeleteMany(RepositoryContext context, string aliasTypeName, IFilterBy<T> filter)
		{
			RepositoryBase<T>.DeleteMany<T>(context, aliasTypeName, filter);
		}

		/// <summary>
		/// Deletes many objects that matched with the filter
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for filtering objects</param>
		protected static void DeleteMany(string aliasTypeName, IFilterBy<T> filter)
		{
			RepositoryBase<T>.DeleteMany<T>(aliasTypeName, filter);
		}

		/// <summary>
		/// Deletes many objects that matched with the filter
		/// </summary>
		/// <param name="filter">The expression for filtering objects</param>
		protected static void DeleteMany(IFilterBy<T> filter)
		{
			RepositoryBase<T>.DeleteMany(null, filter);
		}

		/// <summary>
		/// Deletes many objects that matched with the filter
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <returns></returns>
		public static Task DeleteManyAsync<TEntity>(RepositoryContext context, string aliasTypeName, IFilterBy<TEntity> filter) where TEntity : class
		{
			try
			{
				return RepositoryMediator.DeleteManyAsync<TEntity>(context, aliasTypeName, filter);
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
		/// <returns></returns>
		public static Task DeleteManyAsync<TEntity>(string aliasTypeName, IFilterBy<TEntity> filter) where TEntity : class
		{
			return RepositoryMediator.DeleteManyAsync<TEntity>(aliasTypeName, filter);
		}

		/// <summary>
		/// Deletes many objects that matched with the filter
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="filter">The expression for filtering objects</param>
		/// <returns></returns>
		public static Task DeleteManyAsync<TEntity>(IFilterBy<TEntity> filter) where TEntity : class
		{
			return RepositoryBase<T>.DeleteManyAsync<TEntity>(null, filter);
		}

		/// <summary>
		/// Deletes many objects that matched with the filter
		/// </summary>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <returns></returns>
		protected static Task DeleteManyAsync(RepositoryContext context, string aliasTypeName, IFilterBy<T> filter)
		{
			return RepositoryBase<T>.DeleteManyAsync<T>(context, aliasTypeName, filter);
		}

		/// <summary>
		/// Deletes many objects that matched with the filter
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <returns></returns>
		protected static Task DeleteManyAsync(string aliasTypeName, IFilterBy<T> filter)
		{
			return RepositoryBase<T>.DeleteManyAsync<T>(aliasTypeName, filter);
		}

		/// <summary>
		/// Deletes many objects that matched with the filter
		/// </summary>
		/// <param name="filter">The expression for filtering objects</param>
		/// <returns></returns>
		protected static Task DeleteManyAsync(IFilterBy<T> filter)
		{
			return RepositoryBase<T>.DeleteManyAsync(null, filter);
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
		/// <returns>The collection of objects</returns>
		public static List<TEntity> Find<TEntity>(string aliasTypeName, IFilterBy<TEntity> filter, SortBy<TEntity> sort, int pageSize, int pageNumber) where TEntity : class
		{
			return RepositoryMediator.Find<TEntity>(aliasTypeName, filter, sort, pageSize, pageNumber);
		}

		/// <summary>
		/// Finds all the matched objects
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <returns>The collection of objects</returns>
		public static List<TEntity> Find<TEntity>(IFilterBy<TEntity> filter, SortBy<TEntity> sort, int pageSize, int pageNumber) where TEntity : class
		{
			return RepositoryBase<T>.Find<TEntity>(null, filter, sort, pageSize, pageNumber);
		}

		/// <summary>
		/// Finds all the matched objects
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <returns>The collection of objects</returns>
		public static List<T> Find(string aliasTypeName, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber)
		{
			return RepositoryBase<T>.Find<T>(aliasTypeName, filter, sort, pageSize, pageNumber);
		}

		/// <summary>
		/// Finds all the matched objects
		/// </summary>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <returns>The collection of objects</returns>
		public static List<T> Find(IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber)
		{
			return RepositoryBase<T>.Find(null, filter, sort, pageSize, pageNumber);
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
		/// <returns>The collection of objects</returns>
		public static Task<List<TEntity>> FindAsync<TEntity>(string aliasTypeName, IFilterBy<TEntity> filter, SortBy<TEntity> sort, int pageSize, int pageNumber) where TEntity : class
		{
			return RepositoryMediator.FindAsync<TEntity>(aliasTypeName, filter, sort, pageSize, pageNumber);
		}

		/// <summary>
		/// Finds all the matched objects
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <returns>The collection of objects</returns>
		public static Task<List<TEntity>> FindAsync<TEntity>(IFilterBy<TEntity> filter, SortBy<TEntity> sort, int pageSize, int pageNumber) where TEntity : class
		{
			return RepositoryBase<T>.FindAsync<TEntity>(null, filter, sort, pageSize, pageNumber);
		}

		/// <summary>
		/// Finds all the matched objects
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <returns>The collection of objects</returns>
		public static Task<List<T>> FindAsync(string aliasTypeName, IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber)
		{
			return RepositoryBase<T>.FindAsync<T>(aliasTypeName, filter, sort, pageSize, pageNumber);
		}

		/// <summary>
		/// Finds all the matched objects
		/// </summary>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <returns>The collection of objects</returns>
		public static Task<List<T>> FindAsync(IFilterBy<T> filter, SortBy<T> sort, int pageSize, int pageNumber)
		{
			return RepositoryBase<T>.FindAsync(null, filter, sort, pageSize, pageNumber);
		}
		#endregion

		#region Search
		/// <summary>
		/// Searchs all the matched objects (using full-text search)
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="query">The expression for searching objects</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <returns>The collection of objects</returns>
		public static List<TEntity> Search<TEntity>(string aliasTypeName, string query, IFilterBy<TEntity> filter, int pageSize, int pageNumber) where TEntity : class
		{
			return RepositoryMediator.Search<TEntity>(aliasTypeName, query, filter, pageSize, pageNumber);
		}

		/// <summary>
		/// Searchs all the matched objects (using full-text search)
		/// </summary>
		/// <param name="query">The expression for searching objects</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <returns>The collection of objects</returns>
		public static List<TEntity> Search<TEntity>(string query, IFilterBy<TEntity> filter, int pageSize, int pageNumber) where TEntity : class
		{
			return RepositoryBase<T>.Search<TEntity>(null, query, filter, pageSize, pageNumber);
		}

		/// <summary>
		/// Searchs all the matched objects (using full-text search)
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="query">The expression for searching objects</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <returns>The collection of objects</returns>
		public static List<T> Search(string aliasTypeName, string query, IFilterBy<T> filter, int pageSize, int pageNumber)
		{
			return RepositoryBase<T>.Search<T>(aliasTypeName, query, filter, pageSize, pageNumber);
		}

		/// <summary>
		/// Searchs all the matched objects (using full-text search)
		/// </summary>
		/// <param name="query">The expression for searching objects</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <returns>The collection of objects</returns>
		public static List<T> Search(string query, IFilterBy<T> filter, int pageSize, int pageNumber)
		{
			return RepositoryBase<T>.Search(null, query, filter, pageSize, pageNumber);
		}

		/// <summary>
		/// Searchs all the matched objects (using full-text search)
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="query">The expression for searching objects</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <returns>The collection of objects</returns>
		public static Task<List<TEntity>> SearchAsync<TEntity>(string aliasTypeName, string query, IFilterBy<TEntity> filter, int pageSize, int pageNumber) where TEntity : class
		{
			return RepositoryMediator.SearchAsync<TEntity>(aliasTypeName, query, filter, pageSize, pageNumber);
		}

		/// <summary>
		/// Searchs all the matched objects (using full-text search)
		/// </summary>
		/// <param name="query">The expression for searching objects</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <returns>The collection of objects</returns>
		public static Task<List<TEntity>> SearchAsync<TEntity>(string query, IFilterBy<TEntity> filter, int pageSize, int pageNumber) where TEntity : class
		{
			return RepositoryBase<T>.SearchAsync<TEntity>(null, query, filter, pageSize, pageNumber);
		}

		/// <summary>
		/// Searchs all the matched objects (using full-text search)
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="query">The expression for searching objects</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <returns>The collection of objects</returns>
		public static Task<List<T>> SearchAsync(string aliasTypeName, string query, IFilterBy<T> filter, int pageSize, int pageNumber)
		{
			return RepositoryBase<T>.SearchAsync<T>(aliasTypeName, query, filter, pageSize, pageNumber);
		}

		/// <summary>
		/// Searchs all the matched objects (using full-text search)
		/// </summary>
		/// <param name="query">The expression for searching objects</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="pageSize">The integer number that presents size of one page</param>
		/// <param name="pageNumber">The integer number that presents the number of page</param>
		/// <returns>The collection of objects</returns>
		public static Task<List<T>> SearchAsync(string query, IFilterBy<T> filter, int pageSize, int pageNumber)
		{
			return RepositoryBase<T>.SearchAsync(null, query, filter, pageSize, pageNumber);
		}
		#endregion

		#region Count
		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for counting objects</param>
		/// <returns>The number of all matched objects</returns>
		public static long Count<TEntity>(string aliasTypeName, IFilterBy<TEntity> filter) where TEntity : class
		{
			return RepositoryMediator.Count<TEntity>(aliasTypeName, filter);
		}

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="filter">The expression for counting objects</param>
		/// <returns>The number of all matched objects</returns>
		public static long Count<TEntity>(IFilterBy<TEntity> filter) where TEntity : class
		{
			return RepositoryBase<T>.Count<TEntity>(null, filter);
		}

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <returns>The number of all matched objects</returns>
		public static long Count<TEntity>() where TEntity : class
		{
			return RepositoryBase<T>.Count<TEntity>(null);
		}

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for counting objects</param>
		/// <returns>The number of all matched objects</returns>
		public static long Count(string aliasTypeName, IFilterBy<T> filter)
		{
			return RepositoryBase<T>.Count<T>(aliasTypeName, filter);
		}

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="filter">The expression for counting objects</param>
		/// <returns>The number of all matched objects</returns>
		public static long Count(IFilterBy<T> filter = null)
		{
			return RepositoryBase<T>.Count(null, filter);
		}

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for counting objects</param>
		/// <returns>The number of all matched objects</returns>
		public static Task<long> CountAsync<TEntity>(string aliasTypeName, IFilterBy<TEntity> filter) where TEntity : class
		{
			return RepositoryMediator.CountAsync<TEntity>(aliasTypeName, filter);
		}

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="filter">The expression for counting objects</param>
		/// <returns>The number of all matched objects</returns>
		public static Task<long> CountAsync<TEntity>(IFilterBy<TEntity> filter) where TEntity : class
		{
			return RepositoryBase<T>.CountAsync<TEntity>(null, filter);
		}

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <returns>The number of all matched objects</returns>
		public static Task<long> CountAsync<TEntity>() where TEntity : class
		{
			return RepositoryBase<T>.CountAsync<TEntity>(null);
		}

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for counting objects</param>
		/// <returns>The number of all matched objects</returns>
		public static Task<long> CountAsync(string aliasTypeName, IFilterBy<T> filter)
		{
			return RepositoryBase<T>.CountAsync<T>(aliasTypeName, filter);
		}

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="filter">The expression for counting objects</param>
		/// <returns>The number of all matched objects</returns>
		public static Task<long> CountAsync(IFilterBy<T> filter = null)
		{
			return RepositoryBase<T>.CountAsync(null, filter);
		}

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="query">The expression (full-text search) for counting objects</param>
		/// <param name="filter">The expression (additional filter) for counting objects</param>
		/// <returns>The number of all matched objects</returns>
		public static long CountQuery<TEntity>(string aliasTypeName, string query, IFilterBy<TEntity> filter) where TEntity : class
		{
			return RepositoryMediator.Count<TEntity>(aliasTypeName, query, filter);
		}

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="query">The expression (full-text search) for counting objects</param>
		/// <param name="filter">The expression (additional filter) for counting objects</param>
		/// <returns>The number of all matched objects</returns>
		public static long CountQuery<TEntity>(string query, IFilterBy<TEntity> filter) where TEntity : class
		{
			return RepositoryBase<T>.CountQuery<TEntity>(null, query, filter);
		}

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="query">The expression (full-text search) for counting objects</param>
		/// <param name="filter">The expression (additional filter) for counting objects</param>
		/// <returns>The number of all matched objects</returns>
		public static long CountQuery(string aliasTypeName, string query, IFilterBy<T> filter)
		{
			return RepositoryBase<T>.CountQuery<T>(aliasTypeName, query, filter);
		}

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="query">The expression (full-text search) for counting objects</param>
		/// <param name="filter">The expression (additional filter) for counting objects</param>
		/// <returns>The number of all matched objects</returns>
		public static long CountQuery(string query, IFilterBy<T> filter)
		{
			return RepositoryBase<T>.CountQuery(null, query, filter);
		}

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="query">The expression (full-text search) for counting objects</param>
		/// <param name="filter">The expression (additional filter) for counting objects</param>
		/// <returns>The number of all matched objects</returns>
		public static Task<long> CountQueryAsync<TEntity>(string aliasTypeName, string query, IFilterBy<TEntity> filter) where TEntity : class
		{
			return RepositoryMediator.CountAsync<TEntity>(aliasTypeName, query, filter);
		}

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="query">The expression (full-text search) for counting objects</param>
		/// <param name="filter">The expression (additional filter) for counting objects</param>
		/// <returns>The number of all matched objects</returns>
		public static Task<long> CountQueryAsync<TEntity>(string query, IFilterBy<TEntity> filter) where TEntity : class
		{
			return RepositoryBase<T>.CountQueryAsync<TEntity>(null, query, filter);
		}

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="query">The expression (full-text search) for counting objects</param>
		/// <param name="filter">The expression (additional filter) for counting objects</param>
		/// <returns>The number of all matched objects</returns>
		public static Task<long> CountQueryAsync(string aliasTypeName, string query, IFilterBy<T> filter)
		{
			return RepositoryBase<T>.CountQueryAsync<T>(aliasTypeName, query, filter);
		}

		/// <summary>
		/// Counts the number of all matched objects
		/// </summary>
		/// <param name="query">The expression (full-text search) for counting objects</param>
		/// <param name="filter">The expression (additional filter) for counting objects</param>
		/// <returns>The number of all matched objects</returns>
		public static Task<long> CountQueryAsync(string query, IFilterBy<T> filter)
		{
			return RepositoryBase<T>.CountQueryAsync(null, query, filter);
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
				value = theValue != null ? theValue.CastType<TValue>() : default(TValue);
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
					(this as T).SetAttributeValue(attributes[name], value, true);
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
			var json = (this as T).ToJson();
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

		#region Extended properties/methods [IBussineEntity]
		/// <summary>
		/// Gets or sets the title
		/// </summary>
		[Property(MaxLength = 250)]
		public virtual string Title { get; set; }

		/// <summary>
		/// Gets or sets the identity of the business system that the object is belong to (means the run-time system)
		/// </summary>
		[JsonIgnore, XmlIgnore, BsonIgnoreIfNull]
		[Property(MaxLength = 32)]
		public virtual string SystemID { get; set; }

		/// <summary>
		/// Gets or sets the identity of the business repository that the object is belong to (means the run-time business module)
		/// </summary>
		[JsonIgnore, XmlIgnore, BsonIgnoreIfNull]
		[Property(MaxLength = 32)]
		public virtual string RepositoryID { get; set; }

		/// <summary>
		/// Gets or sets the identity of the business entity that the object is belong to (means the run-time business content-type)
		/// </summary>
		[JsonIgnore, XmlIgnore, BsonIgnoreIfNull]
		[Property(MaxLength = 32)]
		public virtual string EntityID { get; set; }

		/// <summary>
		/// Gets or sets the collection of extended properties
		/// </summary>
		[JsonIgnore, XmlIgnore, BsonIgnoreIfNull]
		public virtual Dictionary<string, object> ExtendedProperties { get; set; }

		/// <summary>
		/// Gets the business entity that marks as parent of this object
		/// </summary>
		[Ignore, JsonIgnore, XmlIgnore, BsonIgnore]
		public virtual IBusinessEntity Parent { get; }

		/// <summary>
		/// Gets or sets the original working permissions
		/// </summary>
		[JsonIgnore, XmlIgnore, BsonIgnoreIfNull]
		public virtual AccessPermissions OriginalPermissions { get; set; }

		/// <summary>
		/// The combined permissions
		/// </summary>
		[NonSerialized]
		protected AccessPermissions Permissions = null;

		/// <summary>
		/// Gets the actual working permissions (mean the combined permissions)
		/// </summary>
		[Ignore, JsonIgnore, XmlIgnore, BsonIgnore]
		public virtual AccessPermissions WorkingPermissions
		{
			get
			{
				if (this.Permissions == null)
					this.Permissions = RepositoryMediator.Combine(this.OriginalPermissions, this.Parent != null ? this.Parent.WorkingPermissions : null);
				return this.Permissions;
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