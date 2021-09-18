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
	[AttributeUsage(AttributeTargets.Class)]
	public class RepositoryAttribute : Attribute
	{
		public RepositoryAttribute() { }

		/// <summary>
		/// Gets or Sets the name of the name of the service that associates with the repository (when this object is defined as a module definition)
		/// </summary>
		public string ServiceName { get; set; }

		/// <summary>
		/// Gets or Sets the identity (when this object is defined as a module definition)
		/// </summary>
		public string ID { get; set; }

		/// <summary>
		/// Gets or Sets the name of the directory that contains all files for working with user interfaces (when this object is defined as a module definition - will be placed in directory named '/themes/modules/', the value of 'ServiceName' will be used if no value was provided)
		/// </summary>
		public string Directory { get; set; }

		/// <summary>
		/// Gets or Sets the title (when this object is defined as a module definition)
		/// </summary>
		public string Title { get; set; }

		/// <summary>
		/// Gets or Sets the description (when this object is defined as a module definition)
		/// </summary>
		public string Description { get; set; }

		/// <summary>
		/// Gets or Sets the name of the icon for working with user interfaces (when this object is defined as a module definition)
		/// </summary>
		public string Icon { get; set; }

		/// <summary>
		/// Gets or Sets the name of the data table (in SQL databases) for storing extended properties, default is 'T_Data_Extended_Properties' (when this object is defined as a module definition)
		/// </summary>
		public string ExtendedPropertiesTableName { get; set; }
	}

	// ------------------------------------------

	/// <summary>
	/// Specifies this class is an entity of a repository
	/// </summary>
	[AttributeUsage(AttributeTargets.Class)]
	public class EntityAttribute : Attribute
	{
		public EntityAttribute() { }

		/// <summary>
		/// Gets or Sets the name of the SQL table 
		/// </summary>
		public string TableName { get; set; }

		/// <summary>
		/// Gets or Sets the name of the NoSQL collection
		/// </summary>
		public string CollectionName { get; set; }

		/// <summary>
		/// Gets or Sets the type of a static class that contains information of the caching storage for processing caching data
		/// </summary>
		public Type CacheClass { get; set; }

		/// <summary>
		/// Gets or Sets the name of the object in the static class that contains information of the caching storage for processing caching data
		/// </summary>
		public string CacheName { get; set; }

		/// <summary>
		/// Gets or Sets the state that specifies this entity is able to search using full-text method (default is false)
		/// </summary>
		public bool Searchable { get; set; } = false;

		/// <summary>
		/// Gets or Sets the name of the service's object that associates with the entity (when this object is defined as a content-type definition)
		/// </summary>
		public string ObjectName { get; set; }

		/// <summary>
		/// Gets or Sets the name prefix of the service's object that associates with the entity (when this object is defined as a content-type definition)
		/// </summary>
		public string ObjectNamePrefix { get; set; }

		/// <summary>
		/// Gets or Sets the name suffix of the service's object that associates with the entity (when this object is defined as a content-type definition)
		/// </summary>
		public string ObjectNameSuffix { get; set; }

		/// <summary>
		/// Gets or Sets the identity (when this object is defined as a content-type definition)
		/// </summary>
		public string ID { get; set; }

		/// <summary>
		/// Gets or Sets the title (when this object is defined as a content-type definition)
		/// </summary>
		public string Title { get; set; }

		/// <summary>
		/// Gets or Sets the description (when this object is defined as a content-type definition)
		/// </summary>
		public string Description { get; set; }

		/// <summary>
		/// Gets or Sets the name of the icon for working with user interfaces (when this object is defined as a content-type definition)
		/// </summary>
		public string Icon { get; set; }

		/// <summary>
		/// Gets or Sets the state to create new version when an entity object is updated (default is true)
		/// </summary>
		public bool CreateNewVersionWhenUpdated { get; set; } = true;

		/// <summary>
		/// Gets or Sets the state that allow to use multiple instances, default is false (when this object is defined as a content-type definition)
		/// </summary>
		public bool MultipleIntances { get; set; } = false;

		/// <summary>
		/// Gets or Sets the state that specifies this entity is able to index with global search module, default is false (when this object is defined as a content-type definition)
		/// </summary>
		public bool Indexable { get; set; } = false;

		/// <summary>
		/// Gets or Sets the state that allow to extend this entity by extended properties, default is false (when this object is defined as a content-type definition)
		/// </summary>
		public bool Extendable { get; set; } = false;

		/// <summary>
		/// Gets or Sets the state to specify that content-type got some instances of portlet (default is true)
		/// </summary>
		public bool Portlets { get; set; } = true;
	}

	// ------------------------------------------

	/// <summary>
	/// Specifies this class is a handler of a repository event (CRUD event)
	/// </summary>
	[AttributeUsage(AttributeTargets.Class)]
	public class EventHandlersAttribute : Attribute
	{
		public EventHandlersAttribute() { }
	}

	// ------------------------------------------

	/// <summary>
	/// Specifies this object attribute is map to a column in SQL table with special settings
	/// </summary>
	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
	public class ColumnMapAttribute : Attribute
	{
		public ColumnMapAttribute() { }

		/// <summary>
		/// Gets or Sets the name of the column in SQL table
		/// </summary>
		public string Column { get; set; }

		/// <summary>
		/// Gets or Sets state that nullable value is not allowed (default is false)
		/// </summary>
		public bool NotNull { get; set; } = false;

		/// <summary>
		/// Gets or Sets state that specified this string is required (not allow empty or null value - default is false)
		/// </summary>
		public bool NotEmpty { get; set; } = false;

		/// <summary>
		/// Gets or Sets state that specified this string is use CLOB (character of large object - default is false)
		/// </summary>
		public bool IsCLOB { get; set; } = false;

		/// <summary>
		/// Gets or Sets minimum length (for string only)
		/// </summary>
		public int MinLength { get; set; } = 0;

		/// <summary>
		/// Gets or Sets maximum length (for string only)
		/// </summary>
		public int MaxLength { get; set; } = 0;

		/// <summary>
		/// Gets or Sets minimum value
		/// </summary>
		public string MinValue { get; set; }

		/// <summary>
		/// Gets or Sets maximum value
		/// </summary>
		public string MaxValue { get; set; }
	}

	// ------------------------------------------

	/// <summary>
	/// Specifies this property is map to a column in SQL table with special settings
	/// </summary>
	[AttributeUsage(AttributeTargets.Property)]
	public class PropertyAttribute : ColumnMapAttribute
	{
		public PropertyAttribute() { }
	}

	// ------------------------------------------

	/// <summary>
	/// Specifies this field is map to a column in SQL table
	/// </summary>
	[AttributeUsage(AttributeTargets.Field)]
	public class FieldAttribute : ColumnMapAttribute
	{
		public FieldAttribute() { }
	}

	// ------------------------------------------

	/// <summary>
	/// Specifies this property is primary key
	/// </summary>
	[AttributeUsage(AttributeTargets.Property)]
	public class PrimaryKeyAttribute : ColumnMapAttribute
	{
		public PrimaryKeyAttribute()
		{
			this.MaxLength = 32;
			this.NotNull = true;
			this.NotEmpty = true;
		}
	}

	// ------------------------------------------

	/// <summary>
	/// Specifies this property is be ignored (means not be stored in the database)
	/// </summary>
	[AttributeUsage(AttributeTargets.Property)]
	public class IgnoreAttribute : Attribute
	{
		public IgnoreAttribute() { }
	}

	// ------------------------------------------

	/// <summary>
	/// Specifies this property is be ignored (means not be stored in the database) if value is null
	/// </summary>
	[AttributeUsage(AttributeTargets.Property)]
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
		/// Gets or Sets the name of the index (for ensuring schemas)
		/// </summary>
		public string IndexName { get; set; }

		/// <summary>
		/// Gets or Sets the name of the unique index (for ensuring schemas)
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
	/// Specifies this property will be stored as the alias (unique value)
	/// </summary>
	[AttributeUsage(AttributeTargets.Property)]
	public class AliasAttribute : Attribute
	{
		public AliasAttribute() { }

		/// <summary>
		/// Gets or Sets the names of other attributes to make the unique index of alias (means combination of repository entity identity, alias and these properties)
		/// </summary>
		public string Properties { get; set; }
	}

	// ------------------------------------------

	/// <summary>
	/// Specifies this property will be stored as the identity of a parent entity
	/// </summary>
	[AttributeUsage(AttributeTargets.Property)]
	public class ParentMappingAttribute : Attribute
	{
		public ParentMappingAttribute() { }

		/// <summary>
		/// Gets or Sets the type of parent entity definition
		/// </summary>
		public Type Type { get; set; }
	}

	// ------------------------------------------

	/// <summary>
	/// Specifies this property is a collection of identities and will be stored as an external table in SQL
	/// </summary>
	/// <remarks>The property must be generic list or hash-set of string</remarks>
	[AttributeUsage(AttributeTargets.Property)]
	public class MappingsAttribute : Attribute
	{
		public MappingsAttribute() { }

		/// <summary>
		/// Gets or Sets the name of the SQL table to store the collection of identities
		/// </summary>
		public string TableName { get; set; }

		/// <summary>
		/// Gets or Sets the column name of the SQL table to store the link value (master)
		/// </summary>
		public string LinkColumn { get; set; }

		/// <summary>
		/// Gets or Sets the column name of the SQL table to store the map value (slave)
		/// </summary>
		public string MapColumn { get; set; }
	}

	// ------------------------------------------

	/// <summary>
	/// Specifies this property is a collection of parent identities and will be stored as an external table in SQL
	/// </summary>
	/// <remarks>The property must be generic list or hash-set of string</remarks>
	[AttributeUsage(AttributeTargets.Property)]
	public class MultipleParentMappingsAttribute : MappingsAttribute
	{
		public MultipleParentMappingsAttribute() { }
	}

	// ------------------------------------------

	/// <summary>
	/// Specifies this property is a collection of children identities and will be stored as an external table in SQL
	/// </summary>
	/// <remarks>The property must be generic list or hash-set of string</remarks>
	[AttributeUsage(AttributeTargets.Property)]
	public class ChildrenMappingsAttribute : MappingsAttribute
	{
		public ChildrenMappingsAttribute() { }

		/// <summary>
		/// Gets or Sets the type of child entity definition
		/// </summary>
		public Type Type { get; set; }
	}

	// ------------------------------------------

	/// <summary>
	/// Specifies this date-time property will be stored as a string with format 'yyyy/MM/dd HH:mm:ss' in SQL table
	/// </summary>
	[AttributeUsage(AttributeTargets.Property)]
	public class AsStringAttribute : Attribute
	{
		public AsStringAttribute() { }

		/// <summary>
		/// Gets or Sets the state to allow time - false to store date only
		/// </summary>
		public bool AllowTime { get; set; } = true;
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
	/// Presents a form control
	/// </summary>
	[AttributeUsage(AttributeTargets.Property)]
	public class FormControlAttribute : Attribute
	{
		public FormControlAttribute() { }

		/// <summary>
		/// Gets or Sets the type of the control (TextBox, TextArea, Select, DatePicker, FilePicker, YesNo, Range, Completer)
		/// </summary>
		public string ControlType { get; set; }

		/// <summary>
		/// Gets or Sets the segment (means a tab that contains group of controls)
		/// </summary>
		public string Segment { get; set; }

		/// <summary>
		/// Gets or Sets the data-type (text, date, number, tel, url, ... - follow the HTML5 input data-type)
		/// </summary>
		public string DataType { get; set; }

		/// <summary>
		/// Gets or Sets the excluded state
		/// </summary>
		public bool Excluded { get; set; } = false;

		/// <summary>
		/// Gets or Sets the hidden state
		/// </summary>
		public bool Hidden { get; set; } = false;

		/// <summary>
		/// Gets or Sets the require state
		/// </summary>
		public bool Required { get; set; } = false;

		/// <summary>
		/// Gets or Sets the label - use doube braces to specified code of a language resource - ex: {{common.buttons.ok}}
		/// </summary>
		public string Label { get; set; }

		/// <summary>
		/// Gets or Sets the place-holder - use doube braces to specified code of a language resource - ex: {{common.buttons.ok}}
		/// </summary>
		public string PlaceHolder { get; set; }

		/// <summary>
		/// Gets or Sets the description - use doube braces to specified code of a language resource - ex: {{common.buttons.ok}}
		/// </summary>
		public string Description { get; set; }

		/// <summary>
		/// Gets or Sets the RegEx pattern for data validation
		/// </summary>
		public string ValidatePattern { get; set; }

		/// <summary>
		/// Gets or Sets the order number
		/// </summary>
		public int Order { get; set; } = -1;

		/// <summary>
		/// Gets or Sets the disable state
		/// </summary>
		public bool Disabled { get; set; } = false;

		/// <summary>
		/// Gets or Sets the read-only state
		/// </summary>
		public bool ReadOnly { get; set; } = false;

		/// <summary>
		/// Gets or Sets the auto-focus state
		/// </summary>
		public bool AutoFocus { get; set; } = false;

		/// <summary>
		/// Gets or Sets the minimum value
		/// </summary>
		public string MinValue { get; set; }

		/// <summary>
		/// Gets or Sets the maximum value
		/// </summary>
		public string MaxValue { get; set; }

		/// <summary>
		/// Gets or Sets the minimum length
		/// </summary>
		public int MinLength { get; set; } = 0;

		/// <summary>
		/// Gets or Sets the maximum length
		/// </summary>
		public int MaxLength { get; set; } = 0;

		/// <summary>
		/// Gets or Sets the width
		/// </summary>
		public string Width { get; set; }

		/// <summary>
		/// Gets or Sets the height
		/// </summary>
		public string Height { get; set; }

		/// <summary>
		/// Gets or Sets the state to act as text/html editor
		/// </summary>
		public bool AsTextEditor { get; set; } = false;

		/// <summary>
		/// Gets or Sets the date-picker with times
		/// </summary>
		public bool DatePickerWithTimes { get; set; } = false;

		/// <summary>
		/// Gets or Sets the multiple of select/lookup control
		/// </summary>
		public bool Multiple { get; set; } = false;

		/// <summary>
		/// Gets or Sets the values of select control (seperated by comma [,])
		/// </summary>
		public string SelectValues { get; set; }

		/// <summary>
		/// Gets or Sets the remote URI of the values of select control
		/// </summary>
		public string SelectValuesRemoteURI { get; set; }

		/// <summary>
		/// Gets or Sets the 'as-boxes' of select control
		/// </summary>
		public bool SelectAsBoxes { get; set; } = false;

		/// <summary>
		/// Gets or Sets the interface mode of select control (alert, popover, actionsheet)
		/// </summary>
		public string SelectInterface { get; set; }

		/// <summary>
		/// Gets or Sets the type for looking-up (Address, User or type-name of business object)
		/// </summary>
		public string LookupType { get; set; }

		/// <summary>
		/// Gets or Sets the nested state for looking-up
		/// </summary>
		public bool LookupObjectIsNested { get; set; } = false;

		/// <summary>
		/// Gets or Sets the state that determines the sub-controls as array of controls
		/// </summary>
		public bool AsArray { get; set; } = false;

		/// <summary>
		/// Gets or Sets the state that determines the control can be used as a view control
		/// </summary>
		public bool AsViewControl { get; set; } = true;
	}

	// ------------------------------------------

	/// <summary>
	/// Extension methods for working with repository objects
	/// </summary>
	public static partial class RepositoryExtensions
	{
		/// <summary>
		/// Gets the state that determines this attribute is be ignored or not
		/// </summary>
		/// <param name="attribute"></param>
		/// <returns></returns>
		public static bool IsIgnored(this ObjectService.AttributeInfo attribute)
			=> attribute?.GetCustomAttribute<IgnoreAttribute>(false) != null || attribute?.GetCustomAttribute<MongoDB.Bson.Serialization.Attributes.BsonIgnoreAttribute>(false) != null;

		/// <summary>
		/// Gets the state that determines this attribute is be ignored if value is null or not
		/// </summary>
		/// <param name="attribute"></param>
		/// <returns></returns>
		public static bool IsIgnoredIfNull(this ObjectService.AttributeInfo attribute)
			=> attribute?.GetCustomAttribute<IgnoreIfNullAttribute>(false) != null;

		/// <summary>
		/// Gets the state that determines this property is mark as alias
		/// </summary>
		/// <param name="attribute"></param>
		/// <returns></returns>
		public static bool IsAlias(this ObjectService.AttributeInfo attribute)
			=> attribute?.GetCustomAttribute<AliasAttribute>() != null;

		/// <summary>
		/// Gets the state that determines this property is mapping identity of a parent object
		/// </summary>
		/// <param name="attribute"></param>
		/// <returns></returns>
		public static bool IsParentMapping(this ObjectService.AttributeInfo attribute)
			=> attribute?.GetCustomAttribute<ParentMappingAttribute>() != null;

		/// <summary>
		/// Gets the state that determines this property is mapping values and will be stored as an external SQL table
		/// </summary>
		/// <param name="attribute"></param>
		/// <returns></returns>
		public static bool IsMappings(this ObjectService.AttributeInfo attribute)
			=> attribute?.GetCustomAttribute<MappingsAttribute>() != null;

		/// <summary>
		/// Gets the state that determines this property is mapping identities of a parent objects
		/// </summary>
		/// <param name="attribute"></param>
		/// <returns></returns>
		public static bool IsMultipleParentMappings(this ObjectService.AttributeInfo attribute)
			=> attribute?.GetCustomAttribute<MultipleParentMappingsAttribute>() != null;

		/// <summary>
		/// Gets the state that determines this property is mapping identities of a children objects
		/// </summary>
		/// <param name="attribute"></param>
		/// <returns></returns>
		public static bool IsChildrenMappings(this ObjectService.AttributeInfo attribute)
			=> attribute?.GetCustomAttribute<ChildrenMappingsAttribute>() != null;

		/// <summary>
		/// Gets the state that determines this attribute is sortable or not
		/// </summary>
		/// <param name="attribute"></param>
		/// <returns></returns>
		public static bool IsSortable(this ObjectService.AttributeInfo attribute)
			=> attribute?.GetCustomAttribute<SortableAttribute>() != null;

		/// <summary>
		/// Gets the state that determines this attribute is 'pre-defined' full-text index and able to search or not
		/// </summary>
		/// <param name="attribute"></param>
		/// <returns></returns>
		public static bool IsSearchable(this ObjectService.AttributeInfo attribute)
			=> attribute != null && attribute.IsStringType() && attribute.GetCustomAttribute<SearchableAttribute>() != null;

		/// <summary>
		/// Gets the state that determines this attribute is form control or not
		/// </summary>
		/// <param name="attribute"></param>
		/// <returns></returns>
		public static bool IsFormControl(this ObjectService.AttributeInfo attribute)
			=> attribute?.GetCustomAttribute<FormControlAttribute>() != null;

		/// <summary>
		/// Gets the state that determines this attribute is view control or not
		/// </summary>
		/// <param name="attribute"></param>
		/// <returns></returns>
		public static bool IsViewControl(this ObjectService.AttributeInfo attribute)
			=> attribute != null && attribute.IsFormControl() && attribute.GetCustomAttribute<FormControlAttribute>().AsViewControl;

		/// <summary>
		/// Gets the state that determines this attribute is large string (CLOB) or not
		/// </summary>
		/// <param name="attribute"></param>
		/// <returns></returns>
		public static bool IsLargeString(this AttributeInfo attribute)
			=> attribute != null && attribute.IsCLOB != null && attribute.IsCLOB.Value;

		/// <summary>
		/// Gets the state that determines this attribute is large string (CLOB) or not
		/// </summary>
		/// <param name="definition"></param>
		/// <returns></returns>
		public static bool IsLargeString(this ExtendedPropertyDefinition definition)
			=> definition != null && definition.Type != null && definition.Type.IsStringType() && definition.Mode.Equals(ExtendedPropertyMode.LargeText);

		/// <summary>
		/// Gets the state that determines this date-time attribute is be stored as string or not
		/// </summary>
		/// <param name="attribute"></param>
		/// <returns></returns>
		public static bool IsStoredAsString(this ObjectService.AttributeInfo attribute)
			=> attribute != null && attribute.IsDateTimeType() && attribute.GetCustomAttribute<AsStringAttribute>() != null;

		/// <summary>
		/// Gets the state that determines this date-time attribute is be stored as string with date-only or not
		/// </summary>
		/// <param name="attribute"></param>
		/// <returns></returns>
		public static bool IsStoredAsDateOnlyString(this ObjectService.AttributeInfo attribute)
			=> attribute != null && attribute.IsStoredAsString() && !attribute.GetCustomAttribute<AsStringAttribute>().AllowTime;

		/// <summary>
		/// Gets the state that determines this date-time attribute is be stored as string with date and time or not
		/// </summary>
		/// <param name="attribute"></param>
		/// <returns></returns>
		public static bool IsStoredAsDateTimeString(this ObjectService.AttributeInfo attribute)
			=> attribute != null && attribute.IsStoredAsString() && attribute.GetCustomAttribute<AsStringAttribute>().AllowTime;

		/// <summary>
		/// Gets the state that determines this object attribute is be stored as JSON or not
		/// </summary>
		/// <param name="attribute"></param>
		/// <returns></returns>
		public static bool IsStoredAsJson(this ObjectService.AttributeInfo attribute)
			=> attribute != null && attribute.IsClassType() && attribute.GetCustomAttribute<AsJsonAttribute>() != null;
	}
}