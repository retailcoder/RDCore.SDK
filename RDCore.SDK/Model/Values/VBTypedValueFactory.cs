using RDCore.SDK.Model.Symbols.Abstract;
using RDCore.SDK.Model.Types;
using RDCore.SDK.Model.Types.Abstract;
using RDCore.SDK.Model.Values.Abstract;
using RDCore.SDK.Model.Values.Runtime;
using RDCore.SDK.Model.Values.Intrinsic;
using RDCore.SDK.Model.Values.Meta;
using System.Data;

namespace RDCore.SDK.Model.Values;

/// <summary>
/// A utility factory for creating new <c>VBTypedValue</c> instances.
/// </summary>
public static class VBTypedValueFactory
{
    /// <summary>
    /// Creates a new <see cref="VBTypeDescValue"/>, which is a meta-<see cref="VBTypedValue"/> representing a <see cref="VBType"/>.
    /// </summary>
    /// <param name="type">The <see cref="VBType"/> to describe.</param>
    public static VBTypeDescValue DescribeType(VBType type) => new(type);

    /// <summary>
    /// Creates a new <c>VBDateValue</c> with the specified value for the specified symbol.
    /// </summary>
    /// <param name="symbol">The symbol to be associated with the new value.</param>
    /// <param name="dateTimeValue">The underying (managed) <c>DateTime</c> value being wrapped.</param>
    public static VBDateValue CreateValue(DateTime dateTimeValue) => new(dateTimeValue.ToOADate());

    /// <summary>
    /// Creates a new <c>VBBooleanValue</c> with the specified value for the specified symbol.
    /// </summary>
    /// <param name="boolValue">The underying (managed) <c>bool</c> value being wrapped.</param>
    public static VBBooleanValue CreateValue(bool boolValue) => new(boolValue);
    /// <summary>
    /// Creates a new <c>VBBooleanValue</c> with the specified value for the specified symbol.
    /// </summary>
    public static VBBooleanValue CreateBooleanValue(double numericValue) => new(numericValue != 0);
    /// <summary>
    /// Creates a new <c>VBBooleanValue</c> with the specified value for the specified symbol.
    /// </summary>
    /// <param name="symbol">The symbol to be associated with the new value.</param>
    public static VBBooleanValue CreateBooleanValue(bool value) => new(value);


    /// <summary>
    /// Creates a new <c>VBNumericValue</c> of the specified type, with the specified value, for the specified symbol.
    /// </summary>
    /// <param name="type">The target numeric <c>VBType</c>.</param>
    /// <param name="symbol">The symbol to be associated with the new value.</param>
    /// <param name="numericValue">The underlying (managed) numeric value being wrapped.</param>
    public static VBTypedValue CreateValue(VBType type, double numericValue)
        => (VBNumericTypedValue)CreateValue(type, numericValue);

    /// <summary>
    /// Creates a new <see cref="VBNullValue"/> for the specified <see cref="Symbol"/>.
    /// </summary>
    /// <param name="symbol">The symbol to be associated with the new value.</param>
    public static VBNullValue CreateNullValue() => VBNullValue.Null;

    /// <summary>
    /// Creates a new <c>VBNumericValue</c> of the specified described type, with the specified value, for the specified symbol.
    /// </summary>
    /// <param name="typeDesc">A <c>VBTypeDescValue</c> describing the target numeric data type.</param>
    /// <param name="numericValue">The underlying (managed) numeric value being wrapped.</param>
    /// <remarks>
    /// 👉 Overloads taking a <see cref="VBTypeDescValue"/> <em>type descriptor value</em> parameter are 
    /// intended for let-coercion semantics and may eventually need to be moved.
    /// </remarks>
    public static VBTypedValue CreateValue(VBTypeDescValue typeDesc, double numericValue)
        => CreateValue(typeDesc.Target, (numericValue));

    /// <summary>
    /// Creates a new <c>VBNumericValue</c> of the specified described type, with the specified value, for the specified symbol.
    /// </summary>
    /// <param name="typeDesc">A <c>VBTypeDescValue</c> describing the target numeric data type.</param>
    /// <param name="managedValue">The underlying (managed) numeric value being wrapped.</param>
    /// <remarks>
    /// 👉 Overloads taking a <see cref="VBTypeDescValue"/> <em>type descriptor value</em> parameter are 
    /// intended for let-coercion semantics and may eventually need to be moved.
    /// </remarks>
    public static VBTypedValue CreateValue(VBTypeDescValue typeDesc, IRuntimeValue managedValue)
        => typeDesc.Target.DefaultValue.WithValue(new(managedValue));

    /// <summary>
    /// Creates a new <c>VBNumericValue</c> of the specified described type, with the specified value, for the specified symbol.
    /// </summary>
    /// <param name="typeDesc">A <c>VBTypeDescValue</c> describing the target numeric data type.</param>
    /// <param name="source">A <see cref="VBNumericTypedValue"/> source value.</param>
    /// <remarks>
    /// 👉 Overloads taking a <see cref="VBTypeDescValue"/> <em>type descriptor value</em> parameter are 
    /// intended for let-coercion semantics and may eventually need to be moved.
    /// </remarks>
    public static VBTypedValue CreateValue(VBTypeDescValue typeDesc, VBNumericTypedValue source)
        => typeDesc.Target.DefaultValue.WithValue(new(source.UnderlyingValue.RuntimeValue!));

    /// <summary>
    /// Creates a new <c>VBNumericValue</c> of the specified described type, with the specified value, for the specified symbol.
    /// </summary>
    /// <param name="typeDesc">A <c>VBTypeDescValue</c> describing the target numeric data type.</param>
    /// <param name="source">A <see cref="VBDateValue"/> source value.</param>
    /// <remarks>
    /// 👉 Overloads taking a <see cref="VBTypeDescValue"/> <em>type descriptor value</em> parameter are 
    /// intended for let-coercion semantics and may eventually need to be moved.
    /// </remarks>
    public static VBTypedValue CreateValue(VBTypeDescValue typeDesc, VBDateValue source)
        => typeDesc.Target.DefaultValue.WithValue(new(source.UnderlyingValue.RuntimeValue!));


    /// <summary>
    /// Creates a new <c>VBStringValue</c> with the specified value, for the specified symbol.
    /// </summary>
    /// <param name="symbol">The symbol to be associated with the new value.</param>
    /// <param name="stringValue">The underlying (managed) string value being wrapped.</param>
    public static VBStringValue CreateStringValue(string stringValue) => new(stringValue);

    /// <summary>
    /// Creates a new <c>VBTypedValue</c> of the specified <c>VType</c> 
    /// </summary>
    /// <param name="type">The <c>VBType</c> of the value to create.</param>
    /// <param name="symbol">The <c>Symbol</c> to be associated with the new value.</param>
    public static VBTypedValue? CreateValue(VBType type) =>
        type switch
        {
            VBStringType => new VBStringValue(),
            VBBooleanType => new VBBooleanValue(false),
            VBByteType => new VBByteValue(),
            VBIntegerType => new VBIntegerValue(),
            VBLongType => new VBLongValue(),
            VBLongLongType => new VBLongLongValue(),
            VBSingleType => new VBSingleValue(),
            VBDoubleType => new VBDoubleValue(),
            VBCurrencyType => new VBCurrencyValue(),
            VBDecimalType => new VBDecimalValue(),
            VBDateType => new VBDateValue(),
            VBNullType => new VBNullValue(),
            VBEmptyType => new VBEmptyValue(),
            VBObjectType => new VBObjectValue((object)null!),
        
            VBVariantType => CreateVariant(type.DefaultValue),
            _ => null
        };

    /// <summary>
    /// Creates a new <c>Variant</c> value wrapping the specified <c>VBTypedValue</c>.
    /// </summary>
    /// <param name="wrapped">The <c>VBTypedValue</c> to be wrapped.into a <c>Variant</c>.</param>
    /// <returns>A <c>VBVariantValue</c> with a <c>SubType</c> matching the data type of the <c>wrapped</c> value.</returns>
    public static VBVariantValue CreateVariant(VBTypedValue wrapped) => new(wrapped);
}
