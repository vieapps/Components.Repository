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
		public RepositoryAttribute() { }

		/// <summary>
		/// Gets or sets the type of a class that implements interfaces of event handlers
		/// </summary>
		public Type EventHandlers { get; set; }

		/// <summary>
		/// Gets or sets the identity (when this object is defined as a module definition)
		/// </summary>
		public string ID { get; set; }

		/// <summary>
		/// Gets or sets the path to folder that contains all UI files (when this object is defined as a module definition)
		/// </summary>
		public string Path { get; set; }

		/// <summary>
		/// Gets or sets the title (when this object is defined as a module definition)
		/// </summary>
		public string Title { get; set; }

		/// <summary>
		/// Gets or sets the description (when this object is defined as a module definition)
		/// </summary>
		public string Description { get; set; }

		/// <summary>
		/// Gets or sets the name of the SQL table for storing extended properties, default is 'T_Data_Extended_Properties' (when this object is defined as a module definition)
		/// </summary>
		public string ExtendedPropertiesTableName { get; set; }
	}

	/// <summary>
	/// Specifies this class is an entity of a repository
	/// </summary>
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
	public class EntityAttribute : Attribute
	{
		public EntityAttribute()
		{
			this.MultipleIntances = false;
			this.Extendable = false;
			this.Searchable = true;
			this.MultipleParentAssociates = false;
		}

		/// <summary>
		/// Gets or sets the name of the SQL table 
		/// </summary>
		public string TableName { get; set; }

		/// <summary>
		/// Gets or sets the name of the NoSQL collection
		/// </summary>
		public string CollectionName { get; set; }

		/// <summary>
		/// Gets or sets the type of a static class that contains information of the cache storage for processing caching data
		/// </summary>
		public Type CacheStorageType { get; set; }

		/// <summary>
		/// Gets or sets the name of the object in the static class that contains information of the cache storage for processing caching data
		/// </summary>
		public string CacheStorageName { get; set; }

		/// <summary>
		/// Gets or sets the identity (when this object is defined as a content-type definition)
		/// </summary>
		public string ID { get; set; }

		/// <summary>
		/// Gets or sets the title (when this object is defined as a content-type definition)
		/// </summary>
		public string Title { get; set; }

		/// <summary>
		/// Gets or sets the description (when this object is defined as a content-type definition)
		/// </summary>
		public string Description { get; set; }

		/// <summary>
		/// Gets or sets the state that allow to use multiple instances, default is false (when this object is defined as a content-type definition)
		/// </summary>
		public bool MultipleIntances { get; set; }

		/// <summary>
		/// Gets or sets the state that allow to extend this entity by extended properties, default is false (when this object is defined as a content-type definition)
		/// </summary>
		public bool Extendable { get; set; }

		/// <summary>
		/// Gets or sets the state that specifies this entity is able to search using global method, default is true (when this object is defined as a content-type definition)
		/// </summary>
		public bool Searchable { get; set; }

		/// <summary>
		/// Gets or sets the type of parent entity definition (when this object is defined as a content-type definition)
		/// </summary>
		public Type ParentType { get; set; }

		/// <summary>
		/// Gets or sets the name of the property that use to associate with parent object (when this object is defined as a content-type definition)
		/// </summary>
		public string ParentAssociatedProperty { get; set; }

		/// <summary>
		/// Gets or sets the state that specifies this entity had multiple associates with parent object, default is false (when this object is defined as a content-type definition)
		/// </summary>
		public bool MultipleParentAssociates { get; set; }

		/// <summary>
		/// Gets or sets the name of the property that use to store the information of multiple associates with parent, mus be List or HashSet (when this object is defined as a content-type definition)
		/// </summary>
		public string MultipleParentAssociatesProperty { get; set; }

		/// <summary>
		/// Gets or sets the name of the SQL table that use to store the information of multiple associates with parent (when this object is defined as a content-type definition)
		/// </summary>
		public string MultipleParentAssociatesTable { get; set; }

		/// <summary>
		/// Gets or sets the name of the column of SQL table that use to map the associate with parent (when this object is defined as a content-type definition)
		/// </summary>
		public string MultipleParentAssociatesMapColumn { get; set; }

		/// <summary>
		/// Gets or sets the name of the column of SQL table that use to link the associate with this entity (when this object is defined as a content-type definition)
		/// </summary>
		public string MultipleParentAssociatesLinkColumn { get; set; }

		/// <summary>
		/// Gets or sets the name of the property to use as short-name (when this object is defined as a content-type definition)
		/// </summary>
		public string ShortnameProperty { get; set; }

		/// <summary>
		/// Gets or sets the type of a class that use to generate navigator menu (when this object is defined as a content-type definition)
		/// </summary>
		public Type NavigatorType { get; set; }
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
	/// Specifies this property is ignore
	/// </summary>
	[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
	public class IgnoreAttribute : Attribute
	{
		public IgnoreAttribute() { }
	}

	/// <summary>
	/// Specifies this property is ignore if null
	/// </summary>
	[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
	public class IgnoreIfNullAttribute : Attribute
	{
		public IgnoreIfNullAttribute() { }
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
	/// Specifies this date-time property will be stored in SQL as a string with format 'yyyy/MM/dd HH:mm:ss'
	/// </summary>
	[AttributeUsage(AttributeTargets.Property)]
	public class AsStringAttribute : Attribute
	{
		public AsStringAttribute() { }
	}

	/// <summary>
	/// Specifies this property will be stored in SQL as a CLOB string in JSON format
	/// </summary>
	[AttributeUsage(AttributeTargets.Property)]
	public class AsJsonAttribute : Attribute
	{
		public AsJsonAttribute() { }
	}

}