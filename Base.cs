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
	/// Based-class for a repository
	/// </summary>
	[Serializable]
	public abstract class RepositoryBase
	{
		/// <summary>
		/// Gets or sets the object identity (primary-key)
		/// </summary>
		[BsonId(IdGenerator = typeof(IdentityGenerator))]
		[PrimaryKey(MaxLength = 32)]
		public abstract string ID { get; set; }

		/// <summary>
		/// Gets or sets the collection of custom properties
		/// </summary>
		[IgnoreWhenSql, JsonIgnore, XmlIgnore]
		public Dictionary<string, object> CustomProperties { get; set; }

		/// <summary>
		/// Gets the score while searching
		/// </summary>
		[Ignore, JsonIgnore, XmlIgnore, BsonIgnoreIfNull]
		public double? SearchScore { get; set; }

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
		/// <param name="addTypeOfCustomProperties">true to add type of all custom properties (named with surfix '$Type')</param>
		/// <returns></returns>
		public abstract JObject ToJson(bool addTypeOfCustomProperties);

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
		/// <param name="addTypeOfCustomProperties">true to add type of all custom properties (attribute named '$type')</param>
		/// <returns></returns>
		public abstract XElement ToXml(bool addTypeOfCustomProperties);

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

		/// <summary>
		/// Gest or sets value of a property by name
		/// </summary>
		/// <param name="name">The string that presents the name of a property</param>
		/// <returns></returns>
		internal protected virtual object this[string name]
		{
			get { return this.GetProperty(name); }
			set { this.SetProperty(name, value); }
		}
	}

	/// <summary>
	/// Based-class for a repository with helper methods to perform CRUD operations, count, find, and full-text query search
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
		internal protected virtual void Create(RepositoryContext context, string aliasTypeName)
		{
			RepositoryBase<T>.Create<T>(context, aliasTypeName, this as T);
		}

		/// <summary>
		/// Creates new the instance of this object
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		internal protected virtual void Create(string aliasTypeName = null)
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
		internal protected virtual Task CreateAsync(RepositoryContext context, string aliasTypeName, CancellationToken cancellationToken = default(CancellationToken))
		{
			return RepositoryBase<T>.CreateAsync<T>(context, aliasTypeName, this as T);
		}

		/// <summary>
		/// Creates new the instance of this object
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <returns></returns>
		internal protected virtual Task CreateAsync(string aliasTypeName = null, CancellationToken cancellationToken = default(CancellationToken))
		{
			return RepositoryBase<T>.CreateAsync<T>(aliasTypeName, this as T, cancellationToken);
		}
		#endregion

		#region Get
		/// <summary>
		/// Gets an object (the first matched with the filter)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <returns>The first object that matched with the filter or null</returns>
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
		/// <returns>The first object that matched with the filter or null</returns>
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
		/// <returns>The first object that matched with the filter or null</returns>
		public static T Get(string aliasTypeName, IFilterBy<T> filter, SortBy<T> sort = null)
		{
			return RepositoryBase<T>.Get<T>(aliasTypeName, filter, sort);
		}

		/// <summary>
		/// Gets an object (the first matched with the filter)
		/// </summary>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <returns>The first object that matched with the filter or null</returns>
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
		internal protected virtual void Get(RepositoryContext context, string aliasTypeName)
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
		internal protected virtual void Get(string aliasTypeName = null)
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
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <returns>The first object that matched with the filter or null</returns>
		public static Task<TEntity> GetAsync<TEntity>(string aliasTypeName, IFilterBy<TEntity> filter, SortBy<TEntity> sort = null) where TEntity : class
		{
			return RepositoryMediator.GetAsync<TEntity>(aliasTypeName, filter, sort);
		}

		/// <summary>
		/// Gets an object (the first matched with the filter)
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <returns>The first object that matched with the filter or null</returns>
		public static Task<TEntity> GetAsync<TEntity>(IFilterBy<TEntity> filter, SortBy<TEntity> sort = null) where TEntity : class
		{
			return RepositoryBase<T>.GetAsync<TEntity>(null, filter, sort);
		}

		/// <summary>
		/// Gets an object (the first matched with the filter)
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <returns>The first object that matched with the filter or null</returns>
		public static Task<T> GetAsync(string aliasTypeName, IFilterBy<T> filter, SortBy<T> sort = null)
		{
			return RepositoryBase<T>.GetAsync<T>(aliasTypeName, filter, sort);
		}

		/// <summary>
		/// Gets an object (the first matched with the filter)
		/// </summary>
		/// <param name="filter">The expression for filtering objects</param>
		/// <param name="sort">The expression for sorting objects</param>
		/// <returns>The first object that matched with the filter or null</returns>
		public static Task<T> GetAsync(IFilterBy<T> filter, SortBy<T> sort = null)
		{
			return RepositoryBase<T>.GetAsync(null, filter, sort);
		}

		/// <summary>
		/// Gets an object
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="id">The string that present identity (primary-key)</param>
		/// <returns></returns>
		public static Task<TEntity> GetAsync<TEntity>(RepositoryContext context, string aliasTypeName, string id) where TEntity : class
		{
			try
			{
				return !string.IsNullOrWhiteSpace(id)
					? RepositoryMediator.GetAsync<TEntity>(context, aliasTypeName, id)
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
		/// <returns></returns>
		public static Task<TEntity> GetAsync<TEntity>(string aliasTypeName, string id) where TEntity : class
		{
			return !string.IsNullOrWhiteSpace(id)
				? RepositoryMediator.GetAsync<TEntity>(aliasTypeName, id)
				: null;
		}

		/// <summary>
		/// Gets an object
		/// </summary>
		/// <typeparam name="TEntity"></typeparam>
		/// <param name="id">The string that present identity (primary-key)</param>
		/// <returns></returns>
		public static Task<TEntity> GetAsync<TEntity>(string id) where TEntity : class
		{
			return RepositoryBase<T>.GetAsync<TEntity>(null, id);
		}

		/// <summary>
		/// Gets the instance of this object
		/// </summary>
		/// <param name="context">Repository context that hold the transaction and state data</param>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		internal protected virtual async Task GetAsync(RepositoryContext context, string aliasTypeName)
		{
			if (!string.IsNullOrWhiteSpace(this.ID))
			{
				var instance = await RepositoryBase<T>.GetAsync<T>(context, aliasTypeName, this.ID);
				if (instance != null)
					this.CopyFrom(instance);
			}
		}

		/// <summary>
		/// Gets the instance of this object
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <returns></returns>
		internal protected virtual async Task GetAsync(string aliasTypeName = null)
		{
			if (!string.IsNullOrWhiteSpace(this.ID))
			{
				var instance = await RepositoryBase<T>.GetAsync<T>(aliasTypeName, this.ID);
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
		internal protected virtual void Replace(RepositoryContext context, string aliasTypeName)
		{
			RepositoryBase<T>.Replace<T>(context, aliasTypeName, this as T);
		}

		/// <summary>
		/// Updates the instance of this object (using replace method)
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		internal protected virtual void Replace(string aliasTypeName = null)
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
		internal protected virtual Task ReplaceAsync(RepositoryContext context, string aliasTypeName)
		{
			return RepositoryBase<T>.ReplaceAsync<T>(context, aliasTypeName, this as T);
		}

		/// <summary>
		/// Updates the instance of this object (using replace method)
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <returns></returns>
		internal protected virtual Task ReplaceAsync(string aliasTypeName = null)
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
		internal protected virtual void Update(RepositoryContext context, string aliasTypeName)
		{
			RepositoryBase<T>.Update<T>(context, aliasTypeName, this as T);
		}

		/// <summary>
		/// Updates the instance of this object (only update changed attributes)
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		internal protected virtual void Update(string aliasTypeName = null)
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
		internal protected virtual Task UpdateAsync(RepositoryContext context, string aliasTypeName)
		{
			return RepositoryBase<T>.UpdateAsync<T>(context, aliasTypeName, this as T);
		}

		/// <summary>
		/// Updates the instance of this object (only update changed attributes)
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <returns></returns>
		internal protected virtual Task UpdateAsync(string aliasTypeName = null)
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
		internal protected virtual void Delete(RepositoryContext context, string aliasTypeName)
		{
			if (!string.IsNullOrWhiteSpace(this.ID))
				RepositoryBase<T>.Delete<T>(context, aliasTypeName, this.ID);
		}

		/// <summary>
		/// Deletes the instance of this object
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		internal protected virtual void Delete(string aliasTypeName = null)
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
		internal protected virtual Task DeleteAsync(RepositoryContext context, string aliasTypeName)
		{
			return !string.IsNullOrWhiteSpace(this.ID)
				? RepositoryBase<T>.DeleteAsync<T>(context, aliasTypeName, this.ID)
				: Task.FromException(new ArgumentNullException("ID", "The identity of the object is null or empty"));
		}

		/// <summary>
		/// Deletes the instance of this object
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		internal protected virtual Task DeleteAsync(string aliasTypeName = null)
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
		internal protected static void DeleteMany(RepositoryContext context, string aliasTypeName, IFilterBy<T> filter)
		{
			RepositoryBase<T>.DeleteMany<T>(context, aliasTypeName, filter);
		}

		/// <summary>
		/// Deletes many objects that matched with the filter
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for filtering objects</param>
		internal protected static void DeleteMany(string aliasTypeName, IFilterBy<T> filter)
		{
			RepositoryBase<T>.DeleteMany<T>(aliasTypeName, filter);
		}

		/// <summary>
		/// Deletes many objects that matched with the filter
		/// </summary>
		/// <param name="filter">The expression for filtering objects</param>
		internal protected static void DeleteMany(IFilterBy<T> filter)
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
		internal protected static Task DeleteManyAsync(RepositoryContext context, string aliasTypeName, IFilterBy<T> filter)
		{
			return RepositoryBase<T>.DeleteManyAsync<T>(context, aliasTypeName, filter);
		}

		/// <summary>
		/// Deletes many objects that matched with the filter
		/// </summary>
		/// <param name="aliasTypeName">The string that presents type name of an alias</param>
		/// <param name="filter">The expression for filtering objects</param>
		/// <returns></returns>
		internal protected static Task DeleteManyAsync(string aliasTypeName, IFilterBy<T> filter)
		{
			return RepositoryBase<T>.DeleteManyAsync<T>(aliasTypeName, filter);
		}

		/// <summary>
		/// Deletes many objects that matched with the filter
		/// </summary>
		/// <param name="filter">The expression for filtering objects</param>
		/// <returns></returns>
		internal protected static Task DeleteManyAsync(IFilterBy<T> filter)
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
				else if (this.CustomProperties != null && this.CustomProperties.ContainsKey(name))
				{
					value = this.CustomProperties[name];
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

			// get value & normalize
			object theValue = null;
			if (this.TryGetProperty(name, out theValue))
			{
				// get type
				var type = typeof(TValue);

				// date-time
				if (type.Equals(typeof(DateTime)) && !(theValue is DateTime))
					theValue = Convert.ToDateTime(theValue);

				// long -> int
				else if (theValue is long && (type.Equals(typeof(int)) || type.Equals(typeof(int?))))
					theValue = Convert.ToInt32(theValue);

				// double -> decimal
				else if (theValue is double && (type.Equals(typeof(decimal)) || type.Equals(typeof(decimal?))))
					theValue = Convert.ToDecimal(theValue);

				// cast the value & return state
				value = (TValue)theValue;
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
				else if (this.CustomProperties != null)
				{
					if (this.CustomProperties.ContainsKey(name))
						this.CustomProperties[name] = value;
					else
						this.CustomProperties.Add(name, value);
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

		#region JSON conversions
		/// <summary>
		/// Serializes this object to JSON object
		/// </summary>
		/// <param name="addTypeOfCustomProperties">true to add type of all custom properties (named with surfix '$Type')</param>
		/// <returns></returns>
		public override JObject ToJson(bool addTypeOfCustomProperties)
		{
			var json = (this as T).ToJson();
			if (this.CustomProperties != null && this.CustomProperties.Count > 0)
				this.CustomProperties.ForEach(property =>
				{
					Type type = property.Value != null
						? property.Value.GetType()
						: null;
					if (addTypeOfCustomProperties && type != null)
						json.Add(new JProperty(property.Key + "$Type", type.IsPrimitiveType() ? type.ToString() : type.GetTypeName()));
					json.Add(new JProperty(property.Key, type == null || type.IsPrimitiveType() ? property.Value : property.Value is RepositoryBase ? (property.Value as RepositoryBase).ToJson(addTypeOfCustomProperties) : property.Value.ToJson()));
				});
			return json;
		}

		/// <summary>
		/// Parses the JSON object and copy values into this object
		/// </summary>
		/// <param name="json">The JSON object that contains information</param>
		public override void ParseJson(JObject json)
		{
			this.CopyFrom(RepositoryMediator.FromJson<T>(json));
		}
		#endregion

		#region XML conversions
		/// <summary>
		/// Serializes this object to XML object
		/// </summary>
		/// <param name="addTypeOfCustomProperties">true to add type of all custom properties (attribute named '$type')</param>
		/// <returns></returns>
		public override XElement ToXml(bool addTypeOfCustomProperties)
		{
			var xml = (this as T).ToXml();
			if (this.CustomProperties != null && this.CustomProperties.Count > 0)
				this.CustomProperties.ForEach(property =>
				{
					Type type = property.Value != null
						? property.Value.GetType()
						: null;
					var element = addTypeOfCustomProperties && type != null
						? new XElement(property.Key, new XAttribute("$type", type.IsPrimitiveType() ? type.ToString() : type.GetTypeName()))
						: new XElement(property.Key);
					element.SetValue(type == null || type.IsPrimitiveType() ? property.Value : property.Value is RepositoryBase ? (property.Value as RepositoryBase).ToJson(addTypeOfCustomProperties) : property.Value.ToJson());
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
			this.CopyFrom(RepositoryMediator.FromXml<T>(xml));
		}
		#endregion

	}

	#region Interfaces of event-handler
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
	#endregion

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