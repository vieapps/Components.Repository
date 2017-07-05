using System;
using net.vieapps.Components.Caching;

namespace net.vieapps.Components.Repository
{
	/// <summary>
	/// Specifies this class is a repository
	/// </summary>
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
	public class RepositoryAttribute : Attribute
	{
		public RepositoryAttribute() {}

		/// <summary>
		/// Gets or sets the identity of this module definition
		/// </summary>
		public string ID { get; set; }
		/// <summary>
		/// Gets or sets the title of this module definition
		/// </summary>
		public string Title { get; set; }
		/// <summary>
		/// Gets or sets the description of this module definition
		/// </summary>
		public string Description { get; set; }
	}

	/// <summary>
	/// Specifies this class is an entity of a repository
	/// </summary>
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
	public class EntityAttribute : Attribute
	{
		public EntityAttribute() {}

		/// <summary>
		/// Gets or sets the name of the SQL table 
		/// </summary>
		public string TableName { get; set; }
		/// <summary>
		/// Gets or sets the name of the NoSQL collection
		/// </summary>
		public string CollectionName { get; set; }
		/// <summary>
		/// Gets or sets the identity of this content-type definition
		/// </summary>
		public string ID { get; set; }
		/// <summary>
		/// Gets or sets the title (means name) of this content-type definition
		/// </summary>
		public string Title { get; set; }
		/// <summary>
		/// Gets or sets the description of this content-type definition
		/// </summary>
		public string Description { get; set; }
		/// <summary>
		/// Gets or Sets the type of a static class that contains information of the cache storage for processing caching data
		/// </summary>
		public Type CacheStorageType { get; set; }
		/// <summary>
		/// Gets or Sets the name of the object in the static class that contains information of the cache storage for processing caching data
		/// </summary>
		public string CacheStorageName { get; set; }
	}

	/// <summary>
	/// Specifies this property is primary-key
	/// </summary>
	[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
	public class PrimaryKeyAttribute : Attribute
	{
		public PrimaryKeyAttribute()
		{
			this.MaxLength = 0;
		}

		/// <summary>
		/// Gets or sets the name of the column in SQL table 
		/// </summary>
		public string Column { get; set; }
		/// <summary>
		/// Gets or sets max-length (of the string property)
		/// </summary>
		public int MaxLength { get; set; }
	}

	/// <summary>
	/// Specifies this property is map to a column in SQL table with special settings
	/// </summary>
	[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
	public class PropertyAttribute : Attribute
	{
		public PropertyAttribute()
		{
			this.NotNull = false;
			this.MaxLength = 0;
			this.IsCLOB = false;
		}

		/// <summary>
		/// Gets or sets the name of the column in SQL table
		/// </summary>
		public string Column { get; set; }
		/// <summary>
		/// Gets or sets state that specified this property is required (not allow nullable value - default is false)
		/// </summary>
		public bool NotNull { get; set; }
		/// <summary>
		/// Gets or sets max-length (of the string property)
		/// </summary>
		public int MaxLength { get; set; }
		/// <summary>
		/// Gets or sets state that specified this string property is use CLOB (character of large object) - default is false
		/// </summary>
		public bool IsCLOB { get; set; }
	}

	/// <summary>
	/// Specifies this field is map to a column in SQL table
	/// </summary>
	[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
	public class FieldAttribute : Attribute
	{
		public FieldAttribute()
		{
			this.NotNull = false;
			this.MaxLength = 0;
			this.IsCLOB = false;
		}

		/// <summary>
		/// Gets or sets the name of the column in SQL table
		/// </summary>
		public string Column { get; set; }
		/// <summary>
		/// Gets or sets state that specified this property is required (not allow nullable value - default is false)
		/// </summary>
		public bool NotNull { get; set; }
		/// <summary>
		/// Gets or sets max-length (of the string field)
		/// </summary>
		public int MaxLength { get; set; }
		/// <summary>
		/// Gets or sets state that specified this string property is use CLOB (character of large object) - default is false
		/// </summary>
		public bool IsCLOB { get; set; }
	}

	/// <summary>
	/// Specifies this property is not map to a column in SQL table
	/// </summary>
	[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
	public class IgnoreWhenSqlAttribute : Attribute
	{
		public IgnoreWhenSqlAttribute() { }
	}

	/// <summary>
	/// Specifies this property is not map to an attribute in NoSQL collection
	/// </summary>
	[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
	public class IgnoreWhenNoSqlAttribute : Attribute
	{
		public IgnoreWhenNoSqlAttribute() { }
	}

	/// <summary>
	/// Specifies this property is not map to a column in SQL table and not map to an attribute in NoSQL collection
	/// </summary>
	[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
	public class IgnoreAttribute : Attribute
	{
		public IgnoreAttribute() { }
	}

	/// <summary>
	/// Specifies this property is able for sorting (means got pre-defined index)
	/// </summary>
	[AttributeUsage(AttributeTargets.Property)]
	public class SortableAttribute : Attribute
	{
		public SortableAttribute() { }
	}

	/// <summary>
	/// Specifies this date-time property will be stored in SQL as string with format 'yyyy/MM/dd HH:mm:ss'
	/// </summary>
	[AttributeUsage(AttributeTargets.Property)]
	public class DateTimeStringAttribute : Attribute
	{
		public DateTimeStringAttribute() { }
	}

	/// <summary>
	/// Specifies this property is storing-bag of extended properties (means the object is got custom properties while running)
	/// </summary>
	[AttributeUsage(AttributeTargets.Property)]
	public class ExtendedAttribute : Attribute
	{
		public ExtendedAttribute() { }

		/// <summary>
		/// Gets or sets the name of the SQL table that stores value of extended properties (default is T_Data_Custom)
		/// </summary>
		public string TableName { get; set; }
	}

}