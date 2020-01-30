#region Related components
using System;
using Newtonsoft.Json;
using net.vieapps.Components.Utility;
#endregion

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
		/// Gets or sets the identity (when this object is defined as a module definition)
		/// </summary>
		public string ID { get; set; }

		/// <summary>
		/// Gets or sets the name of the directory that contains all files for working with user interfaces (when this object is defined as a module definition - will be placed in directory named '/themes/modules/', the value of 'ServiceName' will be used if no value was provided)
		/// </summary>
		public string Directory { get; set; }

		/// <summary>
		/// Gets or sets the title (when this object is defined as a module definition)
		/// </summary>
		public string Title { get; set; }

		/// <summary>
		/// Gets or sets the description (when this object is defined as a module definition)
		/// </summary>
		public string Description { get; set; }

		/// <summary>
		/// Gets or sets the name of the icon for working with user interfaces (when this object is defined as a module definition)
		/// </summary>
		public string Icon { get; set; }

		/// <summary>
		/// Gets or sets the name of the data table (in SQL databases) for storing extended properties, default is 'T_Data_Extended_Properties' (when this object is defined as a module definition)
		/// </summary>
		public string ExtendedPropertiesTableName { get; set; }
	}

	// ------------------------------------------

	/// <summary>
	/// Specifies this class is an entity of a repository
	/// </summary>
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
	public class EntityAttribute : Attribute
	{
		public EntityAttribute() { }

		/// <summary>
		/// Gets or sets the name of the SQL table 
		/// </summary>
		public string TableName { get; set; }

		/// <summary>
		/// Gets or sets the name of the NoSQL collection
		/// </summary>
		public string CollectionName { get; set; }

		/// <summary>
		/// Gets or sets the type of a static class that contains information of the caching storage for processing caching data
		/// </summary>
		public Type CacheClass { get; set; }

		/// <summary>
		/// Gets or sets the name of the object in the static class that contains information of the caching storage for processing caching data
		/// </summary>
		public string CacheName { get; set; }

		/// <summary>
		/// Gets or sets the state that specifies this entity is able to search using full-text method
		/// </summary>
		public bool Searchable { get; set; } = true;

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
		/// Gets or sets the name of the icon for working with user interfaces (when this object is defined as a content-type definition)
		/// </summary>
		public string Icon { get; set; }

		/// <summary>
		/// Gets or sets the state that allow to use multiple instances, default is false (when this object is defined as a content-type definition)
		/// </summary>
		public bool MultipleIntances { get; set; } = false;

		/// <summary>
		/// Gets or sets the state that allow to extend this entity by extended properties, default is false (when this object is defined as a content-type definition)
		/// </summary>
		public bool Extendable { get; set; } = false;

		/// <summary>
		/// Gets or sets the state that specifies this entity is able to index with global search module, default is true (when this object is defined as a content-type definition)
		/// </summary>
		public bool Indexable { get; set; } = true;

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
		public bool MultipleParentAssociates { get; set; } = false;

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
		/// Gets or sets the state to create new version when an entity object is updated
		/// </summary>
		public bool CreateNewVersionWhenUpdated { get; set; } = true;

		/// <summary>
		/// Gets or sets the name of the property to use as alias (means short-name when this object is defined as a content-type definition)
		/// </summary>
		public string AliasProperty { get; set; }

		/// <summary>
		/// Gets or sets the type of a class that use to generate navigator menu (when this object is defined as a content-type definition)
		/// </summary>
		public Type NavigatorType { get; set; }
	}

	// ------------------------------------------

	/// <summary>
	/// Specifies this property is primary-key
	/// </summary>
	[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
	public class PrimaryKeyAttribute : Attribute
	{
		public PrimaryKeyAttribute() { }

		/// <summary>
		/// Gets or sets the name of the column in SQL table 
		/// </summary>
		public string Column { get; set; }

		/// <summary>
		/// Gets or sets max-length (of the string property)
		/// </summary>
		public int MaxLength { get; set; } = 32;
	}

	// ------------------------------------------

	/// <summary>
	/// Specifies this property is map to a column in SQL table with special settings
	/// </summary>
	[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
	public class PropertyAttribute : Attribute
	{
		public PropertyAttribute() { }

		/// <summary>
		/// Gets or sets the name of the column in SQL table
		/// </summary>
		public string Column { get; set; }

		/// <summary>
		/// Gets or sets state that specified this property is required (not allow nullable value - default is false)
		/// </summary>
		public bool NotNull { get; set; } = false;

		/// <summary>
		/// Gets or sets state that specified this string property is required (not allow empty value - default is false)
		/// </summary>
		public bool NotEmpty { get; set; } = false;

		/// <summary>
		/// Gets or sets state that specified this string property is use CLOB (character of large object) - default is false
		/// </summary>
		public bool IsCLOB { get; set; } = false;

		/// <summary>
		/// Gets or sets min-length (of the string property)
		/// </summary>
		public int MinLength { get; set; } = 0;

		/// <summary>
		/// Gets or sets max-length (of the string property)
		/// </summary>
		public int MaxLength { get; set; } = 0;

		/// <summary>
		/// Gets or sets min-value
		/// </summary>
		public string MinValue { get; set; }

		/// <summary>
		/// Gets or sets max-value
		/// </summary>
		public string MaxValue { get; set; }
	}

	// ------------------------------------------

	/// <summary>
	/// Specifies this field is map to a column in SQL table
	/// </summary>
	[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
	public class FieldAttribute : Attribute
	{
		public FieldAttribute() { }

		/// <summary>
		/// Gets or sets the name of the column in SQL table
		/// </summary>
		public string Column { get; set; }

		/// <summary>
		/// Gets or sets state that specified this field is required (not allow nullable value - default is false)
		/// </summary>
		public bool NotNull { get; set; } = false;

		/// <summary>
		/// Gets or sets state that specified this string field is required (not allow empty value - default is false)
		/// </summary>
		public bool NotEmpty { get; set; } = false;

		/// <summary>
		/// Gets or sets state that specified this string field is use CLOB (character of large object) - default is false
		/// </summary>
		public bool IsCLOB { get; set; } = false;

		/// <summary>
		/// Gets or sets min-length (of the string field)
		/// </summary>
		public int MinLength { get; set; } = 0;

		/// <summary>
		/// Gets or sets max-length (of the string field)
		/// </summary>
		public int MaxLength { get; set; } = 0;

		/// <summary>
		/// Gets or sets min-value
		/// </summary>
		public string MinValue { get; set; }

		/// <summary>
		/// Gets or sets max-value
		/// </summary>
		public string MaxValue { get; set; }
	}

	// ------------------------------------------

	/// <summary>
	/// Specifies this property is be ignored
	/// </summary>
	[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
	public class IgnoreAttribute : Attribute
	{
		public IgnoreAttribute() { }
	}

	// ------------------------------------------

	/// <summary>
	/// Specifies this property is be ignored if value is null
	/// </summary>
	[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
	public class IgnoreIfNullAttribute : Attribute
	{
		public IgnoreIfNullAttribute() { }
	}

	// ------------------------------------------

	/// <summary>
	/// Specifies this property is able for sorting (means got pre-defined index or able to create new index)
	/// </summary>
	[AttributeUsage(AttributeTargets.Property)]
	public class SortableAttribute : Attribute
	{
		public SortableAttribute() { }

		/// <summary>
		/// Gets or sets the name of the index (for ensuring schemas)
		/// </summary>
		public string IndexName { get; set; }

		/// <summary>
		/// Gets or sets the name of the unique index (for ensuring schemas)
		/// </summary>
		public string UniqueIndexName { get; set; }
	}

	// ------------------------------------------

	/// <summary>
	/// Specifies this property is able for searching by full-text search (means got pre-defined full-text index)
	/// </summary>
	[AttributeUsage(AttributeTargets.Property)]
	public class SearchableAttribute : Attribute
	{
		public SearchableAttribute() { }
	}

	// ------------------------------------------

	/// <summary>
	/// Specifies this date-time property will be stored as a string with format 'yyyy/MM/dd HH:mm:ss' in SQL table
	/// </summary>
	[AttributeUsage(AttributeTargets.Property)]
	public class AsStringAttribute : Attribute
	{
		public AsStringAttribute() { }
	}

	// ------------------------------------------

	/// <summary>
	/// Specifies this property will be stored as a CLOB string with JSON format in SQL table
	/// </summary>
	[AttributeUsage(AttributeTargets.Property)]
	public class AsJsonAttribute : Attribute
	{
		public AsJsonAttribute() { }
	}

	// ------------------------------------------

	/// <summary>
	/// Specifies this class is a handler of a repository event (CRUD event)
	/// </summary>
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
	public class EventHandlersAttribute : Attribute
	{
		public EventHandlersAttribute() { }
	}

	// ------------------------------------------

	/// <summary>
	/// Presents a form control
	/// </summary>
	[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
	public class FormControlAttribute : Attribute
	{
		public FormControlAttribute() { }

		/// <summary>
		/// Gets or sets the type of the control (TextBox, TextArea, Select, DatePicker, YesNo, Range, Completer)
		/// </summary>
		public string ControlType { get; set; }

		/// <summary>
		/// Gets or sets the data-type (text, date, number, tel, url, ... - follow the HTML5 input data-type)
		/// </summary>
		public string DataType { get; set; }

		/// <summary>
		/// Gets or sets the excluded state
		/// </summary>
		public bool Excluded { get; set; } = false;

		/// <summary>
		/// Gets or sets the hidden state
		/// </summary>
		public bool Hidden { get; set; } = false;

		/// <summary>
		/// Gets or sets the require state
		/// </summary>
		public bool Required { get; set; } = false;

		/// <summary>
		/// Gets or sets the label - use doube braces to specified code of a language resource - ex: {{common.buttons.ok}}
		/// </summary>
		public string Label { get; set; }

		/// <summary>
		/// Gets or sets the place-holder - use doube braces to specified code of a language resource - ex: {{common.buttons.ok}}
		/// </summary>
		public string PlaceHolder { get; set; }

		/// <summary>
		/// Gets or sets the description - use doube braces to specified code of a language resource - ex: {{common.buttons.ok}}
		/// </summary>
		public string Description { get; set; }

		/// <summary>
		/// Gets or sets the RegEx pattern for data validation
		/// </summary>
		public string ValidatePattern { get; set; }

		/// <summary>
		/// Gets or sets the order number
		/// </summary>
		public int Order { get; set; } = -1;

		/// <summary>
		/// Gets or sets the disable state
		/// </summary>
		public bool Disabled { get; set; } = false;

		/// <summary>
		/// Gets or sets the read-only state
		/// </summary>
		public bool ReadOnly { get; set; } = false;

		/// <summary>
		/// Gets or sets the auto-focus state
		/// </summary>
		public bool AutoFocus { get; set; } = false;

		/// <summary>
		/// Gets or sets the min value
		/// </summary>
		public string MinValue { get; set; }

		/// <summary>
		/// Gets or sets the max value
		/// </summary>
		public string MaxValue { get; set; }

		/// <summary>
		/// Gets or sets the min-length
		/// </summary>
		public int MinLength { get; set; } = 0;

		/// <summary>
		/// Gets or sets the max-length
		/// </summary>
		public int MaxLength { get; set; } = 0;

		/// <summary>
		/// Gets or sets the width
		/// </summary>
		public string Width { get; set; }

		/// <summary>
		/// Gets or sets the height
		/// </summary>
		public string Height { get; set; }

		/// <summary>
		/// Gets or sets the state to act as text/html editor
		/// </summary>
		public bool AsTextEditor { get; set; } = false;

		/// <summary>
		/// Gets or sets the date-picker with times
		/// </summary>
		public bool DatePickerWithTimes { get; set; } = false;

		/// <summary>
		/// Gets or sets the multiple of select/lookup control
		/// </summary>
		public bool Multiple { get; set; } = false;

		/// <summary>
		/// Gets or sets the values of select control (JSON string)
		/// </summary>
		public string SelectValues { get; set; }

		/// <summary>
		/// Gets or sets the remote URI of the values of select control
		/// </summary>
		public string SelectValuesRemoteURI { get; set; }

		/// <summary>
		/// Gets or sets the 'as-boxes' of select control
		/// </summary>
		public bool SelectAsBoxes { get; set; } = false;

		/// <summary>
		/// Gets or sets the interface mode of select control (alert, popover, actionsheet)
		/// </summary>
		public string SelectInterface { get; set; }

		/// <summary>
		/// Gets or sets the type for looking-up (Address, User or Business Object)
		/// </summary>
		public string LookupType { get; set; }

		/// <summary>
		/// Gets or sets the identity of the business repository for looking-up
		/// </summary>
		public string LookupRepositoryID { get; set; }

		/// <summary>
		/// Gets or sets the identity of the business entity for looking-up
		/// </summary>
		public string LookupEntityID { get; set; }

		/// <summary>
		/// Gets or sets the name of business entity's property for displaying while looking-up
		/// </summary>
		public string LookupProperty { get; set; }

		/// <summary>
		/// Gets or sets the state that determines the sub-controls as array of controls
		/// </summary>
		public bool AsArray { get; set; } = false;
	}

	// ------------------------------------------

	/// <summary>
	/// Extension methods for working with repository objects
	/// </summary>
	public static partial class Extensions
	{
		/// <summary>
		/// Gets the state that determines this attribute is be ignored or not
		/// </summary>
		/// <param name="attribute"></param>
		/// <returns></returns>
		public static bool IsIgnored(this ObjectService.AttributeInfo attribute)
			=> attribute.Info.GetCustomAttributes(typeof(IgnoreAttribute), true).Length > 0
				? true
				: attribute.Info.GetCustomAttributes(typeof(MongoDB.Bson.Serialization.Attributes.BsonIgnoreAttribute), true).Length > 0;

		/// <summary>
		/// Gets the state that determines this attribute is be ignored if value is null or not
		/// </summary>
		/// <param name="attribute"></param>
		/// <returns></returns>
		public static bool IsIgnoredIfNull(this ObjectService.AttributeInfo attribute)
			=> attribute.Info.GetCustomAttributes(typeof(IgnoreIfNullAttribute), true).Length > 0;

		/// <summary>
		/// Gets the state that determines this object attribute is be stored as JSON or not
		/// </summary>
		/// <param name="attribute"></param>
		/// <returns></returns>
		public static bool IsStoredAsJson(this ObjectService.AttributeInfo attribute)
			=> attribute.Type.IsClassType() && attribute.Info.GetCustomAttributes(typeof(AsJsonAttribute), true).Length > 0;

		/// <summary>
		/// Gets the state that determines this date-time attribute is be stored as string or not
		/// </summary>
		/// <param name="attribute"></param>
		/// <returns></returns>
		public static bool IsStoredAsString(this ObjectService.AttributeInfo attribute)
			=> attribute.Type.IsDateTimeType() && attribute.Info.GetCustomAttributes(typeof(AsStringAttribute), true).Length > 0;

		/// <summary>
		/// Gets the state that determines this attribute is enum-string or not
		/// </summary>
		/// <param name="attribute"></param>
		/// <returns></returns>
		public static bool IsEnumString(this ObjectService.AttributeInfo attribute)
		{
			var attributes = attribute.Type.IsEnum
				? attribute.Info.GetCustomAttributes(typeof(JsonConverterAttribute), true)
				: new object[] { };
			return attributes.Length > 0 && (attributes[0] as JsonConverterAttribute).ConverterType.Equals(typeof(Newtonsoft.Json.Converters.StringEnumConverter));
		}

		/// <summary>
		/// Gets the state that determines this attribute is sortable or not
		/// </summary>
		/// <param name="attribute"></param>
		/// <returns></returns>
		public static bool IsSortable(this ObjectService.AttributeInfo attribute)
			=> attribute.Info.GetCustomAttributes(typeof(SortableAttribute), true).Length > 0;

		/// <summary>
		/// Gets the state that determines this attribute is search or not
		/// </summary>
		/// <param name="attribute"></param>
		/// <returns></returns>
		public static bool IsSearchable(this ObjectService.AttributeInfo attribute)
			=> attribute.Type.IsStringType() && attribute.Info.GetCustomAttributes(typeof(SearchableAttribute), true).Length > 0;
	}
}