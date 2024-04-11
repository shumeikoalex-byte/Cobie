﻿using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Xbim.CobieExpress;
using Xbim.Common;
using Xbim.Common.Configuration;
using Xbim.Ifc4.DateTimeResource;
using Xbim.Ifc4.Interfaces;
using Xbim.Ifc4.MeasureResource;

namespace Xbim.CobieExpress.Exchanger
{
   
    /// <summary>
    /// 
    /// </summary>
    internal class XbimAttributedObject
    {
        private readonly IModel _targetModel;
        private readonly ILogger _log;
        private readonly Dictionary<string, IIfcProperty> _properties = new Dictionary<string, IIfcProperty>();
        private readonly Dictionary<string, IIfcPhysicalQuantity> _quantities = new Dictionary<string, IIfcPhysicalQuantity>();
        private readonly Dictionary<string, IIfcPropertySetDefinition> _propertySets=new Dictionary<string, IIfcPropertySetDefinition>();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="targetModel"></param>
        public XbimAttributedObject(IIfcObjectDefinition obj, IModel targetModel)
        {
            IfcObject = obj;
            _targetModel = targetModel;
            _log = XbimServices.Current.CreateLogger<XbimAttributedObject>();
        }

        /// <summary />
        public IIfcObjectDefinition IfcObject { get; }

        public void AddPropertySetDefinition(IIfcPropertySetDefinitionSelect pSetDefSelect)
        {
            foreach (var pSetDef in pSetDefSelect.PropertySetDefinitions)
            {
                AddPropertySetDefinition(pSetDef);
            }

        }
        /// <summary />
        /// <param name="pSetDef"></param>
        public void AddPropertySetDefinition(IIfcPropertySetDefinition pSetDef)
        {
            if (string.IsNullOrWhiteSpace(pSetDef.Name))
            {
                _log.LogWarning("Property Set Definition: #{0}, has no defined name. It has been ignored", pSetDef.EntityLabel);
                return;
            }
            if ( _propertySets.ContainsKey(pSetDef.Name))
            {
                _log.LogWarning("Property Set Definition: #{0}={1}, is duplicated in Entity #{2}={3}. Duplicate ignored", pSetDef.EntityLabel, pSetDef.Name, IfcObject.EntityLabel, IfcObject.GetType().Name);
                return;
            }
            _propertySets.Add(pSetDef.Name,pSetDef);
            var quantitySet = pSetDef as IIfcElementQuantity;
            if (pSetDef is IIfcPropertySet propertySet)
            {
                foreach (var prop in propertySet.HasProperties)
                {
                    var uniquePropertyName = pSetDef.Name + "." + prop.Name;
                    if (_properties.ContainsKey(uniquePropertyName))
                    {
                        _log.LogWarning("Property: #{0}={1}.{2}, is duplicated in Entity #{3}={4}. Duplicate ignored", prop.EntityLabel, pSetDef.Name, prop.Name, IfcObject.EntityLabel, IfcObject.GetType().Name);
                        continue;
                    }
                    _properties[uniquePropertyName] = prop;
                }
            }
            else if (quantitySet != null)
            {
                foreach (var quantity in quantitySet.Quantities)
                {
                    if (_quantities.ContainsKey(pSetDef.Name + "." + quantity.Name))
                    {
                        _log.LogWarning("Quantity: #{0}={1}.{2}, is duplicated in Entity #{3}={4}. Duplicate ignored", quantity.EntityLabel, pSetDef.Name, quantity.Name, IfcObject.EntityLabel, IfcObject.GetType().Name);
                        continue;
                    }
                    _quantities[pSetDef.Name + "." + quantity.Name] = quantity;
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public IDictionary<string, IIfcPhysicalQuantity> Quantities
        {
            get { return _quantities; }
        }

        /// <summary>
        /// 
        /// </summary>
        public IDictionary<string, IIfcProperty> Properties
        {
            get { return _properties; }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pSetName"></param>
        /// <returns></returns>
        public IIfcPropertySetDefinition GetPropertySetDefinition(string pSetName)
        {
            IIfcPropertySetDefinition pSetDef;
            return _propertySets.TryGetValue(pSetName, out pSetDef) ? 
                pSetDef : 
                null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="propertyName"></param>
        /// <param name="val"></param>
        /// <typeparam name="TValue"></typeparam>
        /// <returns></returns>
        public bool GetSimplePropertyValue<TValue>(string propertyName, out TValue val) where TValue : struct
        {
            IIfcProperty ifcProperty;
            val = default(TValue);
            if (!_properties.TryGetValue(propertyName, out ifcProperty)) 
                return false;

            val = ConvertToSimpleType<TValue>(ifcProperty);
            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="propertyName"></param>
        /// <param name="val"></param>
        /// <returns></returns>
        public bool GetSimplePropertyValue(string propertyName, out string val)
        {
            IIfcProperty ifcProperty;
            val = null;
            if (!_properties.TryGetValue(propertyName, out ifcProperty)) 
                return false;
            
            val = ConvertToString(ifcProperty);
            return true;
        }

        private string ConvertToString(IIfcPropertySingleValue ifcProperty)
        {
            var ifcValue = ifcProperty.NominalValue;
            if (ifcValue == null) return null;
            if (ifcValue is IfcTimeStamp)
            {
                var timeStamp = (IfcTimeStamp)ifcValue;
                return WriteDateTime(timeStamp.ToDateTime());
            }
            var expressVal = (IExpressValueType)ifcValue ;
            if (expressVal.UnderlyingSystemType == typeof(bool) || expressVal.UnderlyingSystemType == typeof(bool?))
            {
                if (expressVal.Value != null && (bool)expressVal.Value)
                    return "yes";
                return "no";
            }
            // all other cases will convert to a string
            if (expressVal.Value != null)
            {
                return expressVal.Value.ToString();
            }
            return null;
        }

        private string WriteDateTime(DateTime dateTime)
        {
            if (dateTime.TimeOfDay == new TimeSpan(0)) //it is a date only
                return string.Format("{0:0000}-{1:00}-{2:00}", dateTime.Year, dateTime.Month, dateTime.Day);
            return string.Format("{0:0000}-{1:00}-{2:00}T{3:00}:{4:00}:{5:00}{6}", dateTime.Year, dateTime.Month, dateTime.Day,
                dateTime.Hour, dateTime.Minute, dateTime.Second, dateTime.Kind == DateTimeKind.Utc ? "Z" : "");
        }

        private string ConvertToString(IIfcProperty ifcProperty)
        {

            if (ifcProperty is IIfcPropertySingleValue ifcPropertySingleValue)
            {
                return ConvertToString(ifcPropertySingleValue);
            }
            if (ifcProperty is IIfcPropertyEnumeratedValue ifcPropertyEnumeratedValue)
            {

                if (ifcPropertyEnumeratedValue.EnumerationValues.Count() == 1)
                {
                    var value = ifcPropertyEnumeratedValue.EnumerationValues.FirstOrDefault();
                    if (value != null)
                        return value.ToString();
                }
                var result = ifcPropertyEnumeratedValue
                    .EnumerationValues
                    .Aggregate("", (current, enumValue) => current + (enumValue + ";"));
                return result.TrimEnd(';', ' ');
            }

            _log.LogWarning("Conversion Error: #{0}={1} [{2}] cannot be converted to s simple value type", ifcProperty.EntityLabel, ifcProperty.Name, ifcProperty.GetType().Name);
            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="propertyName"></param>
        /// <param name="val"></param>
        /// <typeparam name="TSimpleType"></typeparam>
        /// <returns></returns>
        public bool TryGetAttributeValue<TSimpleType>(string propertyName, out TSimpleType val)
        {
            IIfcProperty ifcProperty;
            if (_properties.TryGetValue(propertyName, out ifcProperty))
            {
                return TryGetAttributeValue(ifcProperty, out val);
            }

            IIfcPhysicalQuantity ifcQuantity;
            val = default(TSimpleType);
            return _quantities.TryGetValue(propertyName, out ifcQuantity) && 
                TryGetAttributeValue(ifcQuantity, out val);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ifcProperty"></param>
        /// <param name="result"></param>
        /// <typeparam name="TSimpleType"></typeparam>
        /// <returns></returns>
        private bool TryGetAttributeValue<TSimpleType>(IIfcProperty ifcProperty, out TSimpleType result)
        {
            var ifcPropertyTableValue = ifcProperty as IIfcPropertyTableValue;
            var ifcPropertyReferenceValue = ifcProperty as IIfcPropertyReferenceValue;
            var ifcPropertyListValue = ifcProperty as IIfcPropertyListValue;

            if (ifcProperty is IIfcPropertySingleValue ifcPropertySingleValue)
                return TryGetSimpleValue(ifcPropertySingleValue.NominalValue, out result);

            if (ifcProperty is IIfcPropertyEnumeratedValue ifcPropertyEnumeratedValue)
            {
                if (ifcPropertyEnumeratedValue.EnumerationValues.Count() == 1)
                    return TryGetSimpleValue(ifcPropertyEnumeratedValue.EnumerationValues.First(), out result);

                if (typeof(TSimpleType) == typeof(string)) //if it is a string we can add all the  values in a list
                {
                    result = (TSimpleType)(object)string.Join(";", ifcPropertyEnumeratedValue.EnumerationValues);
                    return true;
                }
                _log.LogWarning("IfcPropertyEnumeratedValue Conversion: Multiple Enumerated values can only be stored in a string type");
                result = default(TSimpleType);
                return false;
            }

            if (ifcProperty is IIfcPropertyBoundedValue ifcPropertyBoundedValue)
            {
                if (typeof(TSimpleType) == typeof(string)) //if it is a string we can add  the bounded values in a statement
                {
                    result = (TSimpleType)(object)(ifcPropertyBoundedValue.LowerBoundValue + " to " +
                                                     ifcPropertyBoundedValue.UpperBoundValue);
                    return true;
                }
                _log.LogWarning("IfcPropertyBoundedValue Conversion: Bounded values can only be stored in a string type");
            }
            else if (ifcPropertyTableValue != null)
            {
                //Logger.WarnFormat("Table values are not supported in COBie");
            }
            else if (ifcPropertyReferenceValue != null)
            {
                //Logger.WarnFormat("Reference property values are not supported in COBie");
            }
            else if (ifcPropertyListValue != null)
            {
                if (ifcPropertyListValue.ListValues.Count() == 1)
                    return TryGetSimpleValue(ifcPropertyListValue.ListValues.First(), out result);

                if (typeof(TSimpleType) == typeof(string)) //if it is a string we can add all the  values in a list
                {
                    result = (TSimpleType)(object)string.Join(";", ifcPropertyListValue.ListValues);
                    return true;
                }
                _log.LogWarning("IfcPropertyList Conversion: ValueMultiple List values can only be stored in a string type");
            }

            result = default(TSimpleType);
            return false;
        }

        //private static void SetCoBieAttributeValue<TCoBieValueBaseType>(TCoBieValueBaseType result, IIfcValue ifcValue) where TCoBieValueBaseType : AttributeValue
        //{
        //    SetCoBieAttributeValue(result, (IExpressValueType)ifcValue);
        //}


        private bool TryGetSimpleValue<TSimpleType>(IExpressValueType ifcValue, out TSimpleType result)
        {
            var targetType = typeof (TSimpleType);

            //handle null value if is it acceptable
            if (ifcValue == null || ifcValue.Value == null)
            {
                result = default(TSimpleType);
                //return true if null is acceptable value
                return targetType.IsClass ||
                       (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof (Nullable<>));
            }

            if (typeof(TSimpleType) == typeof(string))
            {
                result = (TSimpleType)(object)ifcValue.ToString();
                return true;
            }

            if (targetType == typeof(float) ||
                targetType == typeof(float?) ||
                targetType == typeof(double) ||
                targetType == typeof(double?))
            {
                try
                {
                    result = (TSimpleType)(object)Convert.ToDouble(ifcValue.Value, CultureInfo.InvariantCulture);
                    return true;
                }
                catch(NullReferenceException)
                {
                    if (typeof (TSimpleType) == typeof (float?) ||
                        typeof (TSimpleType) == typeof (double?))
                    {
                        result = default(TSimpleType);
                        return true;
                    }
                    //CoBieLiteUkHelper.Logger.WarnFormat("ifcValue is null.");
                }
                catch (ArgumentNullException)
                {
                    if (typeof(TSimpleType) == typeof(float?) ||
                        typeof(TSimpleType) == typeof(double?))
                    {
                        result = default(TSimpleType);
                        return true;
                    }
                    //CoBieLiteUkHelper.Logger.WarnFormat("Decimal Conversion: String is null.");
                }
                catch (FormatException)
                {
                    if (typeof(TSimpleType) == typeof(float?) ||
                        typeof(TSimpleType) == typeof(double?))
                    {
                        result = default(TSimpleType);
                        return true;
                    }
                    //CoBieLiteUkHelper.Logger.WarnFormat("Decimal Conversion: String does not consist of an " + "optional sign followed by a series of digits.");
                }
                catch (OverflowException)
                {
                    if (typeof(TSimpleType) == typeof(float?) ||
                        typeof(TSimpleType) == typeof(double?))
                    {
                        result = default(TSimpleType);
                        return true;
                    }
                    //CoBieLiteUkHelper.Logger.WarnFormat("Decimal Conversion: Overflow in string to int conversion.");
                }
                result = default(TSimpleType);
                return false;

            }
            if (targetType == typeof(bool) || targetType == typeof(bool?))
            {
                try
                {
                    result = (TSimpleType)(object)Convert.ToBoolean(ifcValue.Value);
                    return true;
                }
                catch (NullReferenceException)
                {
                    if (typeof (TSimpleType) == typeof (bool?))
                    {
                        result = default(TSimpleType);
                        return true;
                    }
                    //CoBieLiteUkHelper.Logger.WarnFormat("ifcValue is null.");
                }
                catch (ArgumentNullException)
                {
                    if (typeof(TSimpleType) == typeof(bool?))
                    {
                        result = default(TSimpleType);
                        return true;
                    }
                    //CoBieLiteUkHelper.Logger.WarnFormat("Boolean Conversion: String is null.");
                }
                catch (FormatException)
                {
                    //CoBieLiteUkHelper.Logger.WarnFormat("Boolean Conversion: String does not consist of an " + "optional sign followed by a series of digits.");
                }
                catch (OverflowException)
                {
                    //CoBieLiteUkHelper.Logger.WarnFormat("Boolean Conversion: Overflow in string to int conversion.");
                }
                result = default(TSimpleType);
                return false;
            }

            if (
                targetType == typeof(int) ||
                targetType == typeof(int?) ||
                targetType == typeof(long) ||
                targetType == typeof(long?)
                )
            {
                try
                {
                    //this looks like an error in COBieLite, suggest should be same as Decimal and Boolean
                    result = (TSimpleType)(object)Convert.ToInt32(ifcValue.Value);
                    return true;
                }
                catch (NullReferenceException)
                {
                    if (
                        targetType == typeof (int?) ||
                        targetType == typeof (long?)
                        )
                    {
                        result = default(TSimpleType);
                        return true;
                    }
                    //CoBieLiteUkHelper.Logger.WarnFormat("ifcValue is null.");
                }
                catch (ArgumentNullException)
                {
                    if (
                        targetType == typeof(int?) ||
                        targetType == typeof(long?)
                        )
                    {
                        result = default(TSimpleType);
                        return true;
                    }
                    //CoBieLiteUkHelper.Logger.WarnFormat("Integer Conversion: String is null.");
                }
                catch (FormatException)
                {
                    //CoBieLiteUkHelper.Logger.WarnFormat("Integer Conversion: String does not consist of an " +
                    //                                  "optional sign followed by a series of digits.");
                }
                catch (OverflowException)
                {
                    //CoBieLiteUkHelper.Logger.WarnFormat("Integer Conversion: Overflow in string to int conversion.");
                }
                result = default(TSimpleType);
                return false;
            }
            if (targetType == typeof (DateTime) || targetType == typeof (DateTime?))
            {
                try
                {
                    result = (TSimpleType)(object)Convert.ToDateTime(ifcValue.Value);
                    return true;
                }
                catch (Exception)
                {
                    result = default(TSimpleType);
                    return targetType == typeof (DateTime?);
                }
            }

            _log.LogWarning("Unexpected type " + targetType.Name);
            result = default(TSimpleType);
            return false;
        }
        private bool TryGetAttributeValue<TBaseType>(IIfcPhysicalQuantity ifcQuantity, out TBaseType result)
        {

            if (ifcQuantity is IIfcQuantityLength ifcQuantityLength)
                return TryGetSimpleValue(ifcQuantityLength.LengthValue, out result);

            if (ifcQuantity is IIfcQuantityArea ifcQuantityArea)
                return TryGetSimpleValue(ifcQuantityArea.AreaValue, out result);

            if (ifcQuantity is IIfcQuantityVolume ifcQuantityVolume)
                return TryGetSimpleValue(ifcQuantityVolume.VolumeValue, out result);

            if (ifcQuantity is IIfcQuantityCount ifcQuantityCount)
                return TryGetSimpleValue(ifcQuantityCount.CountValue, out result);

            if (ifcQuantity is IIfcQuantityWeight ifcQuantityWeight)
                return TryGetSimpleValue(ifcQuantityWeight.WeightValue, out result);

            if (ifcQuantity is IIfcQuantityTime ifcQuantityTime)
                return TryGetSimpleValue(ifcQuantityTime.TimeValue, out result);

            if (ifcQuantity is IIfcPhysicalComplexQuantity ifcPhysicalComplexQuantity)
            {
                //Logger.WarnFormat("Ifc Physical Complex Quantities  values are not supported in COBie");
            }
            result = default(TBaseType);
            return false;
        }

        /// <summary>
        /// Converts the property to a simple type, date, string
        /// </summary>
        /// <param name="ifcProperty"></param>
        /// <returns></returns>
        public TValue ConvertToSimpleType<TValue>(IIfcProperty ifcProperty) where TValue:struct
        {
            var ifcPropertyTableValue = ifcProperty as IIfcPropertyTableValue;
            var ifcPropertyReferenceValue = ifcProperty as IIfcPropertyReferenceValue;
            var ifcPropertyListValue = ifcProperty as IIfcPropertyListValue;

            if (ifcProperty is IIfcPropertySingleValue ifcPropertySingleValue)
            {
                return ConvertToSimpleType<TValue>(ifcPropertySingleValue);
            }
            if (ifcProperty is IIfcPropertyEnumeratedValue ifcPropertyEnumeratedValue)
            {

                if (ifcPropertyEnumeratedValue.EnumerationValues.Count() == 1)
                {
                    var value = ifcPropertyEnumeratedValue.EnumerationValues.FirstOrDefault();
                    if (value != null)
                        return (TValue)Convert.ChangeType(value.ToString(), typeof(TValue));
                }
                var result = string.Join(";", ifcPropertyEnumeratedValue.EnumerationValues);
                return (TValue)Convert.ChangeType(result, typeof(TValue));
            }
            if (ifcProperty is IIfcPropertyBoundedValue ifcPropertyBoundedValue)
            {
                _log.LogWarning("Conversion Error: PropertyBoundedValue #{0}={1} cannot be converted to s simple value type", ifcProperty.EntityLabel, ifcProperty.Name);
            }
            else if (ifcPropertyTableValue != null)
            {
                _log.LogWarning("Conversion Error: PropertyTableValue #{0}={1} cannot be converted to s simple value type", ifcProperty.EntityLabel, ifcProperty.Name);
            }
            else if (ifcPropertyReferenceValue != null)
            {
                _log.LogWarning("Conversion Error: PropertyReferenceValue #{0}={1} cannot be converted to s simple value type", ifcProperty.EntityLabel, ifcProperty.Name);
            }
            else if (ifcPropertyListValue != null)
            {
                _log.LogWarning("Conversion Error: PropertyListValue #{0}={1} cannot be converted to s simple value type", ifcProperty.EntityLabel, ifcProperty.Name);
            }
            return default(TValue);
        }


        /// <summary>
        /// Converts an IfcProperty to a COBie Attribute
        /// </summary>
        /// <param name="ifcProperty"></param>
        /// <returns></returns>
        public CobieAttribute ConvertToAttributeType(IIfcProperty ifcProperty)
        {

            var attribute = _targetModel.Instances.New<CobieAttribute>(a =>
            {
                a.Name = ifcProperty.Name;
                a.Description = ifcProperty.Description;

                //srl we need to define categories, the schema proposes "As Built|Submitted|Approved|Exact Requirement|Maximum Requirement|Minimum Requirement|Requirement", should DPoW set requirements?
            });
            var ifcPropertyReferenceValue = ifcProperty as IIfcPropertyReferenceValue;
            var ifcPropertyListValue = ifcProperty as IIfcPropertyListValue;

            if (ifcProperty is IIfcPropertySingleValue ifcPropertySingleValue)
            {
                SetAttributeValueType(ifcPropertySingleValue, attribute);
                return attribute;
            }

            if (ifcProperty is IIfcPropertyEnumeratedValue enumeratedValue)
            {
                if (enumeratedValue.EnumerationReference != null && enumeratedValue.EnumerationReference.Unit != null)
                    attribute.Unit = enumeratedValue.EnumerationReference.Unit.FullName;

                StringValue value = enumeratedValue.EnumerationValues.Count() == 1 ? enumeratedValue.EnumerationValues.First().ToString() : string.Join(";", enumeratedValue.EnumerationValues);
                attribute.Value = value;


                //add in the allowed values
                if (enumeratedValue.EnumerationReference == null ||
                    !enumeratedValue.EnumerationReference.EnumerationValues.Any())
                    return attribute;

                var allowedValues = enumeratedValue.EnumerationReference
                    .EnumerationValues
                    .Select(enumValue => enumValue.ToString()).ToList();
                attribute.AllowedValues.AddRange(allowedValues);
                return attribute;
            }

            if (ifcProperty is IIfcPropertyBoundedValue ifcPropertyBoundedValue)
            {
                SetAttributeValue(ifcPropertyBoundedValue, attribute);
                return attribute;
            }

            if (ifcProperty is IIfcPropertyTableValue ifcPropertyTableValue)
            {
                _log.LogWarning("Table values are not supported in COBie");
            }
            else if (ifcPropertyReferenceValue != null)
            {
                _log.LogWarning("Reference property values are not supported in COBie");
            }
            else if (ifcPropertyListValue != null)
            {
                _log.LogWarning("Multiple List values are not supported in COBie");
            }
            return attribute;
        }

        private static void SetAttributeValue(IIfcPropertyBoundedValue boundedValue, CobieAttribute attribute)
        {    
            var values = new List<IIfcValue>();
            if (boundedValue.LowerBoundValue != null) values.Add(boundedValue.LowerBoundValue);
            if (boundedValue.SetPointValue != null) values.Add(boundedValue.SetPointValue);
            if (boundedValue.UpperBoundValue != null) values.Add(boundedValue.UpperBoundValue);

            if (values.Any())
            {
                StringValue value = string.Join(",", values);
                attribute.Value = value;
            }

            if (boundedValue.Unit != null)
                attribute.Unit = boundedValue.Unit.FullName;

            //var ifcValue = (IExpressValueType) ifcPropertyBoundedValue.LowerBoundValue ;
            ////only upper and lowwer bounds are supported by COBie on integer and decimal values
            //if (ifcValue.UnderlyingSystemType == typeof(int) || ifcValue.UnderlyingSystemType == typeof(long) || ifcValue.UnderlyingSystemType == typeof(short) || ifcValue.UnderlyingSystemType == typeof(byte)
            //    || ifcValue.UnderlyingSystemType == typeof(int?) || ifcValue.UnderlyingSystemType == typeof(long?) || ifcValue.UnderlyingSystemType == typeof(short?) || ifcValue.UnderlyingSystemType == typeof(byte?))
            //{
            //    IntegerValue integerValue;
            //    if (ifcPropertyBoundedValue.UpperBoundValue != null)
            //    {
            //        attribute.MaximalValue = Convert.ToInt32(ifcPropertyBoundedValue.UpperBoundValue);
            //    }
            //    if (ifcPropertyBoundedValue.LowerBoundValue != null)
            //    {
            //        attribute.MinimalValue = Convert.ToInt32(ifcPropertyBoundedValue.LowerBoundValue);
            //    }
            //    return integerValue;
            //}
            //if (ifcValue.UnderlyingSystemType == typeof(double) || ifcValue.UnderlyingSystemType == typeof(float) 
            //    || ifcValue.UnderlyingSystemType == typeof(double?) || ifcValue.UnderlyingSystemType == typeof(float?))
            //{
            //    var decimalValue = new DecimalAttributeValue();
            //    if (ifcPropertyBoundedValue.UpperBoundValue != null)
            //        decimalValue.MaximalValue = (double) ifcPropertyBoundedValue.UpperBoundValue.Value;
            //    if (ifcPropertyBoundedValue.LowerBoundValue != null)
            //        decimalValue.MinimalValue = (double) ifcPropertyBoundedValue.LowerBoundValue.Value;
            //    return decimalValue;
            //}
            //return null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ifcProperty"></param>
        /// <typeparam name="TValue"></typeparam>
        /// <returns></returns>
        public TValue ConvertToSimpleType<TValue>(IIfcPropertySingleValue ifcProperty) where TValue : struct
        {
            var ifcValue = (IExpressValueType)ifcProperty.NominalValue;
            var value = new TValue();
            if (ifcValue is IfcMonetaryMeasure)
            {
                 value = (TValue)Convert.ChangeType(ifcValue.Value, typeof(TValue));
            }
            else if (ifcValue is IfcTimeStamp)
            {    
                var timeStamp = (IfcTimeStamp)ifcValue;
                value = (TValue)Convert.ChangeType(timeStamp.ToDateTime(), typeof(TValue)); 
            }
            else if (value is DateTime) //sometimes these are written as strings in the ifc file
            {
                value = (TValue)Convert.ChangeType(ReadDateTime(ifcValue.Value.ToString()), typeof(TValue));
            }
            else if (ifcValue.UnderlyingSystemType == typeof(int) || ifcValue.UnderlyingSystemType == typeof(long) || ifcValue.UnderlyingSystemType == typeof(short) || ifcValue.UnderlyingSystemType == typeof(byte))
            {
                value = (TValue)Convert.ChangeType(ifcValue.Value, typeof(TValue));             
            }
            else if (ifcValue.UnderlyingSystemType == typeof(double) || ifcValue.UnderlyingSystemType == typeof(float))
            {
                value = (TValue)Convert.ChangeType(ifcValue.Value, typeof(TValue)); 
            }
            else if (ifcValue.UnderlyingSystemType == typeof(string))
            {
                value = (TValue)Convert.ChangeType(ifcValue.Value, typeof(TValue)); 
            }
            else if (ifcValue.UnderlyingSystemType == typeof(bool) || ifcValue.UnderlyingSystemType == typeof(bool?))
            {
                value = (TValue)Convert.ChangeType(ifcValue.Value, typeof(TValue));
            }
            return value;
        }

        private DateTime ReadDateTime(string str)
        {
            try
            {
                var parts = str.Split(new[] { ':', '-', 'T', 'Z' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 6) //it is a date time
                {
                    var year = Convert.ToInt32(parts[0]);
                    var month = Convert.ToInt32(parts[1]);
                    var day = Convert.ToInt32(parts[2]);
                    var hours = Convert.ToInt32(parts[3]);
                    var minutes = Convert.ToInt32(parts[4]);
                    var seconds = Convert.ToInt32(parts[5]);
                    return new DateTime(year, month, day, hours, minutes, seconds, str.Last() == 'Z' ? DateTimeKind.Utc : DateTimeKind.Unspecified);
                }
                if (parts.Length == 3) //it is a date
                {
                    var year = Convert.ToInt32(parts[0]);
                    var month = Convert.ToInt32(parts[1]);
                    var day = Convert.ToInt32(parts[2]);
                    return new DateTime(year, month, day);
                }
            }
            catch (Exception) //eat and log exception so default is returned
            {
                _log.LogWarning("Date Time Conversion: An illegal date time string has been found [{0}]", str);
            }
            return default(DateTime);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="ifcProperty"></param>
        /// <param name="attribute"></param>
        /// <returns></returns>
        public static void SetAttributeValueType(IIfcPropertySingleValue ifcProperty, CobieAttribute attribute)
        {
            var ifcValue = ifcProperty.NominalValue;
            
            if (ifcValue == null) 
                return;

            if (ifcProperty.Unit != null)
                attribute.Unit = ifcProperty.Unit.FullName;

            if (ifcValue is IfcMonetaryMeasure)
            {
                attribute.Value = (FloatValue)ifcValue.Value;
                if (!(ifcProperty.Unit is IIfcMonetaryUnit monUnit)) return;

                attribute.Unit = monUnit.ToString();
                return;
            }

            if (ifcValue is IfcTimeStamp)
            {
                var timeStamp = (IfcTimeStamp)ifcValue;
                var timeValue = (DateTimeValue)timeStamp.ToDateTime();
                attribute.Value = timeValue;
                return;
            }
            if (ifcValue.UnderlyingSystemType == typeof(int) || ifcValue.UnderlyingSystemType == typeof(long) || ifcValue.UnderlyingSystemType == typeof(short) || ifcValue.UnderlyingSystemType == typeof(byte))
            {
                attribute.Value = (IntegerValue)Convert.ToInt32(ifcValue.Value);
                return;
            }
            if (ifcValue.UnderlyingSystemType == typeof(double) || ifcValue.UnderlyingSystemType == typeof(float) )
            {            
                attribute.Value = (FloatValue)(double)ifcValue.Value;
                return;
            }
            if (ifcValue.UnderlyingSystemType == typeof(string))
            {
                attribute.Value = (StringValue) ifcValue.ToString();
                return;
            }
            if (ifcValue.UnderlyingSystemType == typeof(bool) || ifcValue.UnderlyingSystemType == typeof(bool?))
            {
                if (ifcValue.Value == null || !(bool) ifcValue.Value) return;
                attribute.Value = (BooleanValue) (bool) ifcValue.Value;
            }
        }
    }

    
}
