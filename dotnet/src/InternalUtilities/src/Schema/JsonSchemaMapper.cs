﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace JsonSchemaMapper;

/// <summary>
/// Maps .NET types to JSON schema objects using contract metadata from <see cref="JsonTypeInfo"/> instances.
/// </summary>
#if EXPOSE_JSON_SCHEMA_MAPPER
    public
#else
[ExcludeFromCodeCoverage]
internal
#endif
static partial class JsonSchemaMapper
{
    /// <summary>
    /// The JSON schema draft version used by the generated schemas.
    /// </summary>
    public const string SchemaVersion = "https://json-schema.org/draft/2020-12/schema";

    /// <summary>
    /// Generates a JSON schema corresponding to the contract metadata of the specified type.
    /// </summary>
    /// <param name="options">The options instance from which to resolve the contract metadata.</param>
    /// <param name="type">The root type for which to generate the JSON schema.</param>
    /// <param name="configuration">The configuration object controlling the schema generation.</param>
    /// <returns>A new <see cref="JsonObject"/> instance defining the JSON schema for <paramref name="type"/>.</returns>
    /// <exception cref="ArgumentNullException">One of the specified parameters is <see langword="null" />.</exception>
    /// <exception cref="NotSupportedException">The <paramref name="options"/> parameter contains unsupported configuration.</exception>
    public static JsonObject GetJsonSchema(this JsonSerializerOptions options, Type type, JsonSchemaMapperConfiguration? configuration = null)
    {
        if (options is null)
        {
            ThrowHelpers.ThrowArgumentNullException(nameof(options));
        }

        if (type is null)
        {
            ThrowHelpers.ThrowArgumentNullException(nameof(type));
        }

        ValidateOptions(options);
        configuration ??= JsonSchemaMapperConfiguration.Default;

        JsonTypeInfo typeInfo = options.GetTypeInfo(type);
        var state = new GenerationState(configuration);
        return MapJsonSchemaCore(typeInfo, ref state);
    }

    /// <summary>
    /// Generates a JSON object schema with properties corresponding to the specified method parameters.
    /// </summary>
    /// <param name="options">The options instance from which to resolve the contract metadata.</param>
    /// <param name="method">The method from whose parameters to generate the JSON schema.</param>
    /// <param name="configuration">The configuration object controlling the schema generation.</param>
    /// <returns>A new <see cref="JsonObject"/> instance defining the JSON schema for <paramref name="method"/>.</returns>
    /// <exception cref="ArgumentNullException">One of the specified parameters is <see langword="null" />.</exception>
    /// <exception cref="NotSupportedException">The <paramref name="options"/> parameter contains unsupported configuration.</exception>
    public static JsonObject GetJsonSchema(this JsonSerializerOptions options, MethodBase method, JsonSchemaMapperConfiguration? configuration = null)
    {
        if (options is null)
        {
            ThrowHelpers.ThrowArgumentNullException(nameof(options));
        }

        if (method is null)
        {
            ThrowHelpers.ThrowArgumentNullException(nameof(method));
        }

        ValidateOptions(options);
        configuration ??= JsonSchemaMapperConfiguration.Default;

        var state = new GenerationState(configuration);
        string title = method.Name;
        string? description = configuration.ResolveDescriptionAttributes
            ? method.GetCustomAttribute<DescriptionAttribute>()?.Description
            : null;

        JsonSchemaType type = JsonSchemaType.Object;
        JsonObject? paramSchemas = null;
        JsonArray? requiredParams = null;

        foreach (ParameterInfo parameter in method.GetParameters())
        {
            if (parameter.Name is null)
            {
                ThrowHelpers.ThrowInvalidOperationException_TrimmedMethodParameters(method);
            }

            JsonTypeInfo parameterInfo = options.GetTypeInfo(parameter.ParameterType);
            bool isNullableReferenceType = false;
            string? parameterDescription = null;
            bool hasDefaultValue = false;
            JsonNode? defaultValue = null;
            bool isRequired = false;

            ResolveParameterInfo(parameter, parameterInfo, ref state, ref parameterDescription, ref hasDefaultValue, ref defaultValue, ref isNullableReferenceType, ref isRequired);

            state.Push(parameter.Name);
            JsonObject paramSchema = MapJsonSchemaCore(
                parameterInfo,
                ref state,
                title: null,
                parameterDescription,
                isNullableReferenceType,
                hasDefaultValue: hasDefaultValue,
                defaultValue: defaultValue);

            state.Pop();

            (paramSchemas ??= []).Add(parameter.Name, paramSchema);
            if (isRequired)
            {
                (requiredParams ??= []).Add((JsonNode)parameter.Name);
            }
        }

        return CreateSchemaDocument(ref state, title: title, description: description, schemaType: type, properties: paramSchemas, requiredProperties: requiredParams);
    }

    /// <summary>
    /// Generates a JSON schema corresponding to the specified contract metadata.
    /// </summary>
    /// <param name="typeInfo">The contract metadata for which to generate the schema.</param>
    /// <param name="configuration">The configuration object controlling the schema generation.</param>
    /// <returns>A new <see cref="JsonObject"/> instance defining the JSON schema for <paramref name="typeInfo"/>.</returns>
    /// <exception cref="ArgumentNullException">One of the specified parameters is <see langword="null" />.</exception>
    /// <exception cref="NotSupportedException">The <paramref name="typeInfo"/> parameter contains unsupported configuration.</exception>
    public static JsonObject GetJsonSchema(this JsonTypeInfo typeInfo, JsonSchemaMapperConfiguration? configuration = null)
    {
        if (typeInfo is null)
        {
            ThrowHelpers.ThrowArgumentNullException(nameof(typeInfo));
        }

        ValidateOptions(typeInfo.Options);
        typeInfo.MakeReadOnly();

        var state = new GenerationState(configuration ?? JsonSchemaMapperConfiguration.Default);
        return MapJsonSchemaCore(typeInfo, ref state);
    }

    /// <summary>
    /// Renders the specified <see cref="JsonNode"/> instance as a JSON string.
    /// </summary>
    /// <param name="node">The node to serialize.</param>
    /// <param name="writeIndented">Whether to indent the resultant JSON text.</param>
    /// <returns>The JSON node rendered as a JSON string.</returns>
    public static string ToJsonString(this JsonNode? node, bool writeIndented = false)
    {
        return node is null
            ? "null"
            : node.ToJsonString(writeIndented ? new JsonSerializerOptions { WriteIndented = true } : null);
    }

    private static JsonObject MapJsonSchemaCore(
        JsonTypeInfo typeInfo,
        ref GenerationState state,
        string? title = null,
        string? description = null,
        bool isNullableReferenceType = false,
        bool isNullableOfTElement = false,
        JsonConverter? customConverter = null,
        bool hasDefaultValue = false,
        JsonNode? defaultValue = null,
        JsonNumberHandling? customNumberHandling = null,
        KeyValuePair<string, JsonNode?>? derivedTypeDiscriminator = null,
        Type? parentNullableOfT = null)
    {
        Debug.Assert(typeInfo.IsReadOnly);

        Type type = typeInfo.Type;
        JsonConverter effectiveConverter = customConverter ?? typeInfo.Converter;
        JsonNumberHandling? effectiveNumberHandling = customNumberHandling ?? typeInfo.NumberHandling;
        bool emitsTypeDiscriminator = derivedTypeDiscriminator?.Value is not null;
        bool isCacheable = !emitsTypeDiscriminator && description is null && !hasDefaultValue && !isNullableOfTElement;

        if (!IsBuiltInConverter(effectiveConverter))
        {
            return []; // We can't make any schema determinations if a custom converter is used
        }

        if (isCacheable && state.TryGetGeneratedSchemaPath(type, parentNullableOfT, customConverter, isNullableReferenceType, customNumberHandling, out string? typePath))
        {
            // Schema for type has already been generated, return a reference to it.
            // For derived types using discriminators, the schema is generated inline.
            return new JsonObject { [RefPropertyName] = typePath };
        }

        if (state.Configuration.ResolveDescriptionAttributes)
        {
            description ??= type.GetCustomAttribute<DescriptionAttribute>()?.Description;
        }

        if (Nullable.GetUnderlyingType(type) is Type nullableElementType)
        {
            // Nullable<T> types must be handled separately
            JsonTypeInfo nullableElementTypeInfo = typeInfo.Options.GetTypeInfo(nullableElementType);
            customConverter = ExtractCustomNullableConverter(customConverter);

            return MapJsonSchemaCore(
                nullableElementTypeInfo,
                ref state,
                title,
                description,
                hasDefaultValue: hasDefaultValue,
                defaultValue: defaultValue,
                customNumberHandling: customNumberHandling,
                customConverter: customConverter,
                parentNullableOfT: type,
                isNullableOfTElement: true);
        }

        if (isCacheable && typeInfo.Kind != JsonTypeInfoKind.None)
        {
            // For complex types such objects, arrays, and dictionaries register the current path
            // so that it can be referenced by later occurrences in the type graph. Do not register
            // types in a polymorphic hierarchy using discriminators as they need to be inlined.
            state.RegisterTypePath(type, parentNullableOfT, customConverter, isNullableReferenceType, customNumberHandling);
        }

        JsonSchemaType schemaType = JsonSchemaType.Any;
        string? format = null;
        string? pattern = null;
        JsonObject? properties = null;
        JsonArray? requiredProperties = null;
        JsonObject? arrayItems = null;
        JsonNode? additionalProperties = null;
        JsonArray? enumValues = null;
        JsonArray? anyOfTypes = null;

        if (derivedTypeDiscriminator is null && typeInfo.PolymorphismOptions is { DerivedTypes.Count: > 0 } polyOptions)
        {
            // This is the base type of a polymorphic type hierarchy. The schema for this type
            // will include an "anyOf" property with the schemas for all derived types.

            string typeDiscriminatorKey = polyOptions.TypeDiscriminatorPropertyName;
            List<JsonDerivedType> derivedTypes = polyOptions.DerivedTypes.ToList();

            if (!type.IsAbstract && derivedTypes.Any(derived => derived.DerivedType == type))
            {
                // For non-abstract base types that haven't been explicitly configured,
                // add a trivial schema to the derived types since we should support it.
                derivedTypes.Add(new JsonDerivedType(type));
            }

            state.Push(AnyOfPropertyName);
            anyOfTypes = [];

            int i = 0;
            foreach (JsonDerivedType derivedType in derivedTypes)
            {
                Debug.Assert(derivedType.TypeDiscriminator is null or int or string);
                JsonNode? typeDiscriminatorPropertySchema = derivedType.TypeDiscriminator switch
                {
                    string stringId => new JsonObject { [ConstPropertyName] = (JsonNode)stringId },
                    int intId => new JsonObject { [ConstPropertyName] = (JsonNode)intId },
                    _ => null,
                };

                JsonTypeInfo derivedTypeInfo = typeInfo.Options.GetTypeInfo(derivedType.DerivedType);

                state.Push(i++.ToString(CultureInfo.InvariantCulture));
                JsonObject derivedSchema = MapJsonSchemaCore(
                    derivedTypeInfo,
                    ref state,
                    derivedTypeDiscriminator: new(typeDiscriminatorKey, typeDiscriminatorPropertySchema));
                state.Pop();

                anyOfTypes.Add((JsonNode)derivedSchema);
            }

            state.Pop();
            goto ConstructSchemaDocument;
        }

        switch (typeInfo.Kind)
        {
            case JsonTypeInfoKind.None:
                if (s_simpleTypeInfo.TryGetValue(type, out SimpleTypeJsonSchema simpleTypeInfo))
                {
                    schemaType = simpleTypeInfo.SchemaType;
                    format = simpleTypeInfo.Format;
                    pattern = simpleTypeInfo.Pattern;

                    if (effectiveNumberHandling is JsonNumberHandling numberHandling &&
                        schemaType is JsonSchemaType.Integer or JsonSchemaType.Number)
                    {
                        if ((numberHandling & (JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)) != 0)
                        {
                            schemaType |= JsonSchemaType.String;
                        }
                        else if (numberHandling is JsonNumberHandling.AllowNamedFloatingPointLiterals)
                        {
                            anyOfTypes =
                            [
                                (JsonNode)new JsonObject { [TypePropertyName] = MapSchemaType(schemaType) },
                                (JsonNode)new JsonObject
                                {
                                    [EnumPropertyName] = new JsonArray { (JsonNode)"NaN", (JsonNode)"Infinity", (JsonNode)"-Infinity" },
                                },
                            ];

                            schemaType = JsonSchemaType.Any; // reset the parent setting
                        }
                    }
                }
                else if (type.IsEnum)
                {
                    if (TryGetStringEnumConverterValues(typeInfo, effectiveConverter, out enumValues))
                    {
                        schemaType = JsonSchemaType.String;

                        if (enumValues != null && isNullableOfTElement)
                        {
                            // We're generating the schema for a nullable
                            // enum type. Append null to the "enum" array.
                            enumValues.Add(null);
                        }
                    }
                    else
                    {
                        schemaType = JsonSchemaType.Integer;
                    }
                }

                break;

            case JsonTypeInfoKind.Object:
                schemaType = JsonSchemaType.Object;

                if (typeInfo.UnmappedMemberHandling is JsonUnmappedMemberHandling.Disallow)
                {
                    // Disallow unspecified properties.
                    additionalProperties = false;
                }

                if (emitsTypeDiscriminator)
                {
                    Debug.Assert(derivedTypeDiscriminator?.Value is not null);
                    (properties ??= []).Add(derivedTypeDiscriminator!.Value);
                    (requiredProperties ??= []).Add((JsonNode)derivedTypeDiscriminator.Value.Key);
                }

                Func<JsonPropertyInfo, ParameterInfo?> parameterInfoMapper = ResolveJsonConstructorParameterMapper(typeInfo);

                state.Push(PropertiesPropertyName);
                foreach (JsonPropertyInfo property in typeInfo.Properties)
                {
                    if (property is { Get: null, Set: null })
                    {
                        continue; // Skip [JsonIgnore] property
                    }

                    if (property.IsExtensionData)
                    {
                        continue; // Extension data properties don't impact the schema.
                    }

                    JsonNumberHandling? propertyNumberHandling = property.NumberHandling ?? effectiveNumberHandling;
                    JsonTypeInfo propertyTypeInfo = typeInfo.Options.GetTypeInfo(property.PropertyType);

                    // Only resolve nullability metadata for reference types.
                    NullabilityInfoContext? nullabilityCtx = !property.PropertyType.IsValueType ? state.NullabilityInfoContext : null;

                    // Only resolve the attribute provider if needed.
                    ICustomAttributeProvider? attributeProvider = state.Configuration.ResolveDescriptionAttributes || nullabilityCtx is not null
                        ? ResolveAttributeProvider(typeInfo, property)
                        : null;

                    // Resolve property-level description attributes.
                    string? propertyDescription = state.Configuration.ResolveDescriptionAttributes
                        ? attributeProvider?.GetCustomAttributes(inherit: true).OfType<DescriptionAttribute>().FirstOrDefault()?.Description
                        : null;

                    // Declare the property as nullable if either getter or setter are nullable.
                    bool isPropertyNullableReferenceType = nullabilityCtx is not null && attributeProvider is MemberInfo memberInfo
                        ? nullabilityCtx.GetMemberNullability(memberInfo) is { WriteState: NullabilityState.Nullable } or { ReadState: NullabilityState.Nullable }
                        : false;

                    bool isRequired = property.IsRequired;
                    bool propertyHasDefaultValue = false;
                    JsonNode? propertyDefaultValue = null;

                    if (parameterInfoMapper(property) is ParameterInfo ctorParam)
                    {
                        ResolveParameterInfo(
                            ctorParam,
                            propertyTypeInfo,
                            ref state,
                            ref propertyDescription,
                            ref propertyHasDefaultValue,
                            ref propertyDefaultValue,
                            ref isPropertyNullableReferenceType,
                            ref isRequired);
                    }

                    state.Push(property.Name);
                    JsonObject propertySchema = MapJsonSchemaCore(
                        typeInfo: propertyTypeInfo,
                        state: ref state,
                        title: null,
                        description: propertyDescription,
                        isNullableReferenceType: isPropertyNullableReferenceType,
                        customConverter: property.CustomConverter,
                        hasDefaultValue: propertyHasDefaultValue,
                        defaultValue: propertyDefaultValue,
                        customNumberHandling: propertyNumberHandling);

                    state.Pop();

                    (properties ??= []).Add(property.Name, propertySchema);

                    if (isRequired)
                    {
                        (requiredProperties ??= []).Add((JsonNode)property.Name);
                    }
                }

                state.Pop();
                break;

            case JsonTypeInfoKind.Enumerable:
                Type elementType = GetElementType(typeInfo);
                JsonTypeInfo elementTypeInfo = typeInfo.Options.GetTypeInfo(elementType);

                if (emitsTypeDiscriminator)
                {
                    Debug.Assert(derivedTypeDiscriminator is not null);

                    // Polymorphic enumerable types are represented using a wrapping object:
                    // { "$type" : "discriminator", "$values" : [element1, element2, ...] }
                    // Which corresponds to the schema
                    // { "properties" : { "$type" : { "const" : "discriminator" }, "$values" : { "type" : "array", "items" : { ... } } } }

                    schemaType = JsonSchemaType.Object;
                    (properties ??= []).Add(derivedTypeDiscriminator!.Value);
                    (requiredProperties ??= []).Add((JsonNode)derivedTypeDiscriminator.Value.Key);

                    state.Push(PropertiesPropertyName);
                    state.Push(StjValuesMetadataProperty);
                    state.Push(ItemsPropertyName);
                    JsonObject elementSchema = MapJsonSchemaCore(elementTypeInfo, ref state);
                    state.Pop();
                    state.Pop();
                    state.Pop();

                    properties.Add(
                        StjValuesMetadataProperty,
                        new JsonObject
                        {
                            [TypePropertyName] = MapSchemaType(JsonSchemaType.Array),
                            [ItemsPropertyName] = elementSchema,
                        });
                }
                else
                {
                    schemaType = JsonSchemaType.Array;

                    state.Push(ItemsPropertyName);
                    arrayItems = MapJsonSchemaCore(elementTypeInfo, ref state);
                    state.Pop();
                }

                break;

            case JsonTypeInfoKind.Dictionary:
                schemaType = JsonSchemaType.Object;
                Type valueType = GetElementType(typeInfo);
                JsonTypeInfo valueTypeInfo = typeInfo.Options.GetTypeInfo(valueType);

                if (emitsTypeDiscriminator)
                {
                    Debug.Assert(derivedTypeDiscriminator?.Value is not null);
                    (properties ??= []).Add(derivedTypeDiscriminator!.Value);
                    (requiredProperties ??= []).Add((JsonNode)derivedTypeDiscriminator.Value.Key);
                }

                state.Push(AdditionalPropertiesPropertyName);
                additionalProperties = MapJsonSchemaCore(valueTypeInfo, ref state);
                state.Pop();
                break;

            default:
                Debug.Fail("Unreachable code");
                break;
        }

        if (schemaType != JsonSchemaType.Any &&
            (type.IsValueType
             ? parentNullableOfT is not null
             : (isNullableReferenceType || state.Configuration.ReferenceTypeNullability is ReferenceTypeNullability.AlwaysNullable)))
        {
            // Append "null" to the type array in the following cases:
            // 1. The type is a nullable value type or
            // 2. The type has been inferred to be a nullable reference type annotation or
            // 3. The schema generator has been configured to always emit null for reference types (default STJ semantics).
            schemaType |= JsonSchemaType.Null;
        }

ConstructSchemaDocument:
        return CreateSchemaDocument(
            ref state,
            title,
            description,
            schemaType,
            format,
            pattern,
            properties,
            requiredProperties,
            arrayItems,
            additionalProperties,
            enumValues,
            anyOfTypes,
            hasDefaultValue,
            defaultValue);
    }

    private static void ResolveParameterInfo(
        ParameterInfo parameter,
        JsonTypeInfo parameterTypeInfo,
        ref GenerationState state,
        ref string? description,
        ref bool hasDefaultValue,
        ref JsonNode? defaultValue,
        ref bool isNullableReferenceType,
        ref bool isRequired)
    {
        Debug.Assert(parameterTypeInfo.Type == parameter.ParameterType);

        if (state.Configuration.ResolveDescriptionAttributes)
        {
            // Resolve parameter-level description attributes.
            description ??= parameter.GetCustomAttribute<DescriptionAttribute>()?.Description;
        }

        if (!isNullableReferenceType && state.NullabilityInfoContext is { } ctx)
        {
            // Consult the nullability annotation of the constructor parameter if available.
            isNullableReferenceType = ctx.GetParameterNullability(parameter) is NullabilityState.Nullable;
        }

        if (parameter.HasDefaultValue)
        {
            // Append the default value to the description.
            object? defaultVal = parameter.GetNormalizedDefaultValue();
            defaultValue = JsonSerializer.SerializeToNode(defaultVal, parameterTypeInfo);
            hasDefaultValue = true;
        }
        else if (state.Configuration.RequireConstructorParameters)
        {
            // Parameter is not optional, mark as required.
            isRequired = true;
        }
    }

    private ref struct GenerationState
    {
        private readonly JsonSchemaMapperConfiguration _configuration;
        private readonly NullabilityInfoContext? _nullabilityInfoContext;
        private readonly Dictionary<(Type, JsonConverter? CustomConverter, bool IsNullableReferenceType, JsonNumberHandling? CustomNumberHandling), string>? _generatedTypePaths;
        private readonly List<string>? _currentPath;
        private int _currentDepth;

        public GenerationState(JsonSchemaMapperConfiguration configuration)
        {
            this._configuration = configuration;
            this._nullabilityInfoContext = configuration.ReferenceTypeNullability is ReferenceTypeNullability.Annotated ? new() : null;
            this._generatedTypePaths = configuration.AllowSchemaReferences ? new() : null;
            this._currentPath = configuration.AllowSchemaReferences ? new() : null;
            this._currentDepth = 0;
        }

        public readonly JsonSchemaMapperConfiguration Configuration => this._configuration;
        public readonly NullabilityInfoContext? NullabilityInfoContext => this._nullabilityInfoContext;
        public readonly int CurrentDepth => this._currentDepth;

        public void Push(string nodeId)
        {
            if (this._currentDepth == this.Configuration.MaxDepth)
            {
                ThrowHelpers.ThrowInvalidOperationException_MaxDepthReached();
            }

            this._currentDepth++;

            if (this.Configuration.AllowSchemaReferences)
            {
                Debug.Assert(this._currentPath is not null);
                this._currentPath!.Add(nodeId);
            }
        }

        public void Pop()
        {
            Debug.Assert(this._currentDepth > 0);
            this._currentDepth--;

            if (this.Configuration.AllowSchemaReferences)
            {
                Debug.Assert(this._currentPath is not null);
                this._currentPath!.RemoveAt(this._currentPath.Count - 1);
            }
        }

        /// <summary>
        /// Associates the specified type configuration with the current path in the schema.
        /// </summary>
        public readonly void RegisterTypePath(Type type, Type? parentNullableOfT, JsonConverter? customConverter, bool isNullableReferenceType, JsonNumberHandling? customNumberHandling)
        {
            if (this.Configuration.AllowSchemaReferences)
            {
                Debug.Assert(this._currentPath is not null);
                Debug.Assert(this._generatedTypePaths is not null);

                string pointer = this._currentDepth == 0 ? "#" : "#/" + string.Join("/", this._currentPath);
                this._generatedTypePaths!.Add((parentNullableOfT ?? type, customConverter, isNullableReferenceType, customNumberHandling), pointer);
            }
        }

        /// <summary>
        /// Looks up the schema path for the specified type configuration.
        /// </summary>
        public readonly bool TryGetGeneratedSchemaPath(Type type, Type? parentNullableOfT, JsonConverter? customConverter, bool isNullableReferenceType, JsonNumberHandling? customNumberHandling, [NotNullWhen(true)] out string? value)
        {
            if (this.Configuration.AllowSchemaReferences)
            {
                Debug.Assert(this._generatedTypePaths is not null);
                return this._generatedTypePaths!.TryGetValue((parentNullableOfT ?? type, customConverter, isNullableReferenceType, customNumberHandling), out value);
            }

            value = null;
            return false;
        }
    }

    private static JsonObject CreateSchemaDocument(
        ref GenerationState state,
        string? title = null,
        string? description = null,
        JsonSchemaType schemaType = JsonSchemaType.Any,
        string? format = null,
        string? pattern = null,
        JsonObject? properties = null,
        JsonArray? requiredProperties = null,
        JsonObject? arrayItems = null,
        JsonNode? additionalProperties = null,
        JsonArray? enumValues = null,
        JsonArray? anyOfSchema = null,
        bool hasDefaultValue = false,
        JsonNode? defaultValue = null)
    {
        var schema = new JsonObject();

        if (state.CurrentDepth == 0 && state.Configuration.IncludeSchemaVersion)
        {
            schema.Add(SchemaPropertyName, SchemaVersion);
        }

        if (title is not null)
        {
            schema.Add(TitlePropertyName, title);
        }

        if (description is not null)
        {
            schema.Add(DescriptionPropertyName, description);
        }

        if (MapSchemaType(schemaType) is JsonNode type)
        {
            schema.Add(TypePropertyName, type);
        }

        if (format is not null)
        {
            schema.Add(FormatPropertyName, format);
        }

        if (pattern is not null)
        {
            schema.Add(PatternPropertyName, pattern);
        }

        if (properties is not null)
        {
            schema.Add(PropertiesPropertyName, properties);
        }

        if (requiredProperties is not null)
        {
            schema.Add(RequiredPropertyName, requiredProperties);
        }

        if (arrayItems is not null)
        {
            schema.Add(ItemsPropertyName, arrayItems);
        }

        if (additionalProperties is not null)
        {
            schema.Add(AdditionalPropertiesPropertyName, additionalProperties);
        }

        if (enumValues is not null)
        {
            schema.Add(EnumPropertyName, enumValues);
        }

        if (anyOfSchema is not null)
        {
            schema.Add(AnyOfPropertyName, anyOfSchema);
        }

        if (hasDefaultValue)
        {
            schema.Add(DefaultPropertyName, defaultValue);
        }

        return schema;
    }

    [Flags]
    private enum JsonSchemaType
    {
        Any = 0, // No type declared on the schema
        Null = 1,
        Boolean = 2,
        Integer = 4,
        Number = 8,
        String = 16,
        Array = 32,
        Object = 64,
    }

    private static readonly JsonSchemaType[] s_schemaValues =
    [
        // NB the order of these values influences order of types in the rendered schema
        JsonSchemaType.String,
        JsonSchemaType.Integer,
        JsonSchemaType.Number,
        JsonSchemaType.Boolean,
        JsonSchemaType.Array,
        JsonSchemaType.Object,
        JsonSchemaType.Null,
    ];

    private static JsonNode? MapSchemaType(JsonSchemaType schemaType)
    {
        return schemaType switch
        {
            JsonSchemaType.Any => null,
            JsonSchemaType.Null => "null",
            JsonSchemaType.Boolean => "boolean",
            JsonSchemaType.Integer => "integer",
            JsonSchemaType.Number => "number",
            JsonSchemaType.String => "string",
            JsonSchemaType.Array => "array",
            JsonSchemaType.Object => "object",
            _ => MapCompositeSchemaType(schemaType),
        };

        static JsonArray MapCompositeSchemaType(JsonSchemaType schemaType)
        {
            var array = new JsonArray();
            foreach (JsonSchemaType type in s_schemaValues)
            {
                if ((schemaType & type) != 0)
                {
                    array.Add(MapSchemaType(type));
                }
            }

            return array;
        }
    }

    private const string SchemaPropertyName = "$schema";
    private const string RefPropertyName = "$ref";
    private const string TitlePropertyName = "title";
    private const string DescriptionPropertyName = "description";
    private const string TypePropertyName = "type";
    private const string FormatPropertyName = "format";
    private const string PatternPropertyName = "pattern";
    private const string PropertiesPropertyName = "properties";
    private const string RequiredPropertyName = "required";
    private const string ItemsPropertyName = "items";
    private const string AdditionalPropertiesPropertyName = "additionalProperties";
    private const string EnumPropertyName = "enum";
    private const string AnyOfPropertyName = "anyOf";
    private const string ConstPropertyName = "const";
    private const string DefaultPropertyName = "default";
    private const string StjValuesMetadataProperty = "$values";

    private readonly struct SimpleTypeJsonSchema
    {
        public SimpleTypeJsonSchema(JsonSchemaType schemaType, string? format = null, string? pattern = null)
        {
            this.SchemaType = schemaType;
            this.Format = format;
            this.Pattern = pattern;
        }

        public JsonSchemaType SchemaType { get; }
        public string? Format { get; }
        public string? Pattern { get; }
    }

    private static readonly Dictionary<Type, SimpleTypeJsonSchema> s_simpleTypeInfo = new()
    {
        [typeof(object)] = new(JsonSchemaType.Any),
        [typeof(bool)] = new(JsonSchemaType.Boolean),
        [typeof(byte)] = new(JsonSchemaType.Integer),
        [typeof(ushort)] = new(JsonSchemaType.Integer),
        [typeof(uint)] = new(JsonSchemaType.Integer),
        [typeof(ulong)] = new(JsonSchemaType.Integer),
        [typeof(sbyte)] = new(JsonSchemaType.Integer),
        [typeof(short)] = new(JsonSchemaType.Integer),
        [typeof(int)] = new(JsonSchemaType.Integer),
        [typeof(long)] = new(JsonSchemaType.Integer),
        [typeof(float)] = new(JsonSchemaType.Number),
        [typeof(double)] = new(JsonSchemaType.Number),
        [typeof(decimal)] = new(JsonSchemaType.Number),
#if NET6_0_OR_GREATER
        [typeof(Half)] = new(JsonSchemaType.Number),
#endif
#if NET7_0_OR_GREATER
        [typeof(UInt128)] = new(JsonSchemaType.Integer),
        [typeof(Int128)] = new(JsonSchemaType.Integer),
#endif
        [typeof(char)] = new(JsonSchemaType.String),
        [typeof(string)] = new(JsonSchemaType.String),
        [typeof(byte[])] = new(JsonSchemaType.String),
        [typeof(Memory<byte>)] = new(JsonSchemaType.String),
        [typeof(ReadOnlyMemory<byte>)] = new(JsonSchemaType.String),
        [typeof(DateTime)] = new(JsonSchemaType.String, format: "date-time"),
        [typeof(DateTimeOffset)] = new(JsonSchemaType.String, format: "date-time"),

        // TimeSpan is represented as a string in the format "[-][d.]hh:mm:ss[.fffffff]".
        [typeof(TimeSpan)] = new(JsonSchemaType.String, pattern: @"^-?(\d+\.)?\d{2}:\d{2}:\d{2}(\.\d{1,7})?$"),
#if NET6_0_OR_GREATER
        [typeof(DateOnly)] = new(JsonSchemaType.String, format: "date"),
        [typeof(TimeOnly)] = new(JsonSchemaType.String, format: "time"),
#endif
        [typeof(Guid)] = new(JsonSchemaType.String, format: "uuid"),
        [typeof(Uri)] = new(JsonSchemaType.String, format: "uri"),
        [typeof(Version)] = new(JsonSchemaType.String),
        [typeof(JsonDocument)] = new(JsonSchemaType.Any),
        [typeof(JsonElement)] = new(JsonSchemaType.Any),
        [typeof(JsonNode)] = new(JsonSchemaType.Any),
        [typeof(JsonValue)] = new(JsonSchemaType.Any),
        [typeof(JsonObject)] = new(JsonSchemaType.Object),
        [typeof(JsonArray)] = new(JsonSchemaType.Array),
    };

    private static void ValidateOptions(JsonSerializerOptions options)
    {
        if (options.ReferenceHandler == ReferenceHandler.Preserve)
        {
            ThrowHelpers.ThrowNotSupportedException_ReferenceHandlerPreserveNotSupported();
        }

        options.MakeReadOnly();
    }

    private static class ThrowHelpers
    {
        [DoesNotReturn]
        public static void ThrowArgumentNullException(string name) => throw new ArgumentNullException(name);

        [DoesNotReturn]
        public static void ThrowNotSupportedException_ReferenceHandlerPreserveNotSupported() =>
            throw new NotSupportedException("Schema generation not supported with ReferenceHandler.Preserve enabled.");

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_TrimmedMethodParameters(MethodBase method) =>
            throw new InvalidOperationException($"The parameters for method '{method}' have been trimmed away.");

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_MaxDepthReached() =>
            throw new InvalidOperationException("The maximum depth of the schema has been reached.");
    }
}
