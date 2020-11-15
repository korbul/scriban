// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Scriban.Helpers;
using Scriban.Parsing;
using Scriban.Syntax;

namespace Scriban.Runtime
{
    /// <summary>
    /// A range of value, generated by 1..10 syntax.
    /// </summary>
    public class ScriptRange : IList<object>, IList, IEnumerable<object>, IScriptTransformable, IScriptCustomBinaryOperation
    {
        private IEnumerable _values;

        public ScriptRange()
        {
            _values = Enumerable.Empty<object>();
        }

        public ScriptRange(IEnumerable values)
        {
            _values = values ?? throw new ArgumentNullException(nameof(values));
        }

        public IEnumerable Values => _values;

        public IEnumerator<object> GetEnumerator()
        {
            var enumerator = _values.GetEnumerator();
            while (enumerator.MoveNext()) {
                yield return enumerator.Current;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable) _values).GetEnumerator();
        }

        public Type ElementType => typeof(object);

        public bool CanTransform(Type transformType)
        {
            return true;
        }

        public virtual bool Visit(TemplateContext context, SourceSpan span, Func<object, bool> visit)
        {
            foreach (var item in this)
            {
                if (!visit(item))
                {
                    return false;
                }
            }

            return true;
        }

        public virtual object Transform(TemplateContext context, SourceSpan span, Func<object, object> apply, Type destType)
        {
            return new ScriptRange(TransformImpl(apply));
        }

        private IEnumerable TransformImpl(Func<object, object> apply)
        {
            foreach (var value in this)
            {
                yield return apply(value);
            }
        }

        public static ScriptRange Offset(IEnumerable list, int index)
        {
            return list == null ? null : new ScriptRange(OffsetImpl(list, index));
        }

        private static IEnumerable OffsetImpl(IEnumerable list, int index)
        {
            foreach (var item in list)
            {
                if (index <= 0)
                {
                    yield return item;
                }
                else
                {
                    index--;
                }
            }
        }


        public static ScriptRange Limit(IEnumerable list, int count)
        {
            return list == null ? null : new ScriptRange(LimitImpl(list, count));
        }

        private static IEnumerable LimitImpl(IEnumerable list, int count)
        {
            foreach (var item in list)
            {
                if (count <= 0)
                {
                    break;
                }
                count--;
                yield return item;
            }
        }

        public static ScriptRange Compact(IEnumerable list)
        {
            return list == null ? null : new ScriptRange(CompactImpl(list));
        }

        public static ScriptRange Uniq(IEnumerable list)
        {
            return list == null ? null : new ScriptRange(list.Cast<object>().Distinct());
        }

        public static ScriptRange Reverse(IEnumerable list)
        {
            if (list == null)
            {
                return new ScriptRange(Enumerable.Empty<object>());
            }

            return new ScriptRange(list.Cast<object>().Reverse());
        }


        private static IEnumerable CompactImpl(IEnumerable list)
        {
            if (list == null) yield break;

            foreach (var item in list)
            {
                if (item != null)
                {
                    yield return item;
                }
            }
        }


        public static ScriptRange BinaryOr(IEnumerable<object> left, IEnumerable<object> right)
        {
            return new ScriptRange(left.Union(right));
        }

        public static ScriptRange BinaryAnd(IEnumerable<object> left, IEnumerable<object> right)
        {
            return new ScriptRange(left.Intersect(right));
        }

        public static ScriptRange ShiftLeft(IEnumerable left, object value)
        {
            return new ScriptRange(ShiftLeftImpl(left, value));
        }

        private static IEnumerable ShiftLeftImpl(IEnumerable left, object value)
        {
            foreach (var o in left) yield return o;
            yield return value;
        }

        public static ScriptRange ShiftRight(object value, IEnumerable right)
        {
            return new ScriptRange(ShiftRightImpl(value, right));
        }

        private static IEnumerable ShiftRightImpl(object value, IEnumerable right)
        {
            yield return value;
            foreach (var o in right) yield return o;
        }

        public static ScriptRange Multiply(IEnumerable left, int count)
        {
            return new ScriptRange(MultiplyImpl(left, count));
        }

        private static IEnumerable MultiplyImpl(IEnumerable left, int count)
        {
            for (int i = 0; i < count; i++)
            {
                foreach (var value in left) yield return value;
            }
        }

        public static ScriptRange Divide(IEnumerable left, int count)
        {
            return new ScriptRange(DivideImpl(left, count));
        }

        public static ScriptRange Modulus(IEnumerable left, int count)
        {
            return new ScriptRange(ModulusImpl(left, count));
        }

        private static IEnumerable DivideImpl(IEnumerable left, int count)
        {
            foreach (var value in left)
            {
                if (count < 0) break;
                yield return value;
                count--;
            }
        }

        private static IEnumerable ModulusImpl(IEnumerable left, int modulus)
        {
            int index = 0;
            foreach (var value in left)
            {
                if ((index % modulus) == 0) yield return value;
                index++;
            }
        }

        public static ScriptRange Concat(IEnumerable left, IEnumerable right)
        {
            if (right == null && left == null)
            {
                return null;
            }

            if (right == null)
            {
                return new ScriptRange(left);
            }

            if (left == null)
            {
                return new ScriptRange(right);
            }

            return new ScriptRange(ConcatImpl(left, right));
        }

        private static IEnumerable ConcatImpl(IEnumerable left, IEnumerable right)
        {
            foreach (var value in left) yield return value;
            foreach (var value in right) yield return value;
        }

        public bool TryEvaluate(TemplateContext context, SourceSpan span, ScriptBinaryOperator op, SourceSpan leftSpan, object leftValue, SourceSpan rightSpan, object rightValue, out object result)
        {
            result = null;
            var leftArray = TryGetRange(leftValue);
            var rightArray = TryGetRange(rightValue);
            int intModifier = 0;
            var intSpan = leftSpan;

            var errorSpan = span;
            string reason = null;
            switch (op)
            {
                case ScriptBinaryOperator.BinaryOr:
                case ScriptBinaryOperator.BinaryAnd:
                case ScriptBinaryOperator.CompareEqual:
                case ScriptBinaryOperator.CompareNotEqual:
                case ScriptBinaryOperator.CompareLessOrEqual:
                case ScriptBinaryOperator.CompareGreaterOrEqual:
                case ScriptBinaryOperator.CompareLess:
                case ScriptBinaryOperator.CompareGreater:
                case ScriptBinaryOperator.Add:
                    if (leftArray == null)
                    {
                        errorSpan = leftSpan;
                        reason = " Expecting an array for the left argument.";
                    }
                    if (rightArray == null)
                    {
                        errorSpan = rightSpan;
                        reason = " Expecting an array for the right argument.";
                    }
                    break;
                case ScriptBinaryOperator.Multiply:
                    if (leftArray == null && rightArray == null || leftArray != null && rightArray != null)
                    {
                        reason = " Expecting only one array for the left or right argument.";
                    }
                    else
                    {
                        intModifier = context.ToInt(span, leftArray == null ? leftValue : rightValue);
                        if (rightArray == null) intSpan = rightSpan;
                    }
                    break;
                case ScriptBinaryOperator.Divide:
                case ScriptBinaryOperator.DivideRound:
                case ScriptBinaryOperator.Modulus:
                    if (leftArray == null)
                    {
                        errorSpan = leftSpan;
                        reason = " Expecting an array for the left argument.";
                    }
                    else
                    {
                        intModifier = context.ToInt(span, rightValue);
                        intSpan = rightSpan;
                    }
                    break;
                case ScriptBinaryOperator.ShiftLeft:
                    if (leftArray == null)
                    {
                        errorSpan = leftSpan;
                        reason = " Expecting an array for the left argument.";
                    }
                    break;
                case ScriptBinaryOperator.ShiftRight:
                    if (rightArray == null)
                    {
                        errorSpan = rightSpan;
                        reason = " Expecting an array for the right argument.";
                    }
                    break;
                default:
                    reason = string.Empty;
                    break;
            }

            if (intModifier < 0)
            {
                errorSpan = intSpan;
                reason = $" Integer {intModifier} cannot be negative when multiplying";
            }

            if (reason != null)
            {
                throw new ScriptRuntimeException(errorSpan, $"The operator `{op.ToText()}` is not supported between {context.GetTypeName(leftValue)} and {context.GetTypeName(rightValue)}.{reason}");
            }

            switch (op)
            {
                case ScriptBinaryOperator.BinaryOr:
                    result = BinaryOr(leftArray, rightArray);
                    return true;

                case ScriptBinaryOperator.BinaryAnd:
                    result = BinaryAnd(leftArray, rightArray);
                    return true;

                case ScriptBinaryOperator.Add:
                    result = Concat(leftArray, rightArray);
                    return true;

                case ScriptBinaryOperator.CompareEqual:
                case ScriptBinaryOperator.CompareNotEqual:
                case ScriptBinaryOperator.CompareLessOrEqual:
                case ScriptBinaryOperator.CompareGreaterOrEqual:
                case ScriptBinaryOperator.CompareLess:
                case ScriptBinaryOperator.CompareGreater:
                    result = CompareTo(context, span, op, leftArray, rightArray);
                    return true;

                case ScriptBinaryOperator.Multiply:
                {
                    // array with integer
                    var array = leftArray ?? rightArray;
                    if (intModifier == 0)
                    {
                        result = new ScriptRange();
                        return true;
                    }

                    result = Multiply(array, intModifier);
                    return true;
                }

                case ScriptBinaryOperator.Divide:
                case ScriptBinaryOperator.DivideRound:
                {
                    // array with integer
                    var array = leftArray ?? rightArray;
                    if (intModifier == 0) throw new ScriptRuntimeException(intSpan, "Cannot divide by 0");

                    result = Divide(array, intModifier);
                    return true;
                }

                case ScriptBinaryOperator.Modulus:
                {
                    // array with integer
                    var array = leftArray ?? rightArray;
                    if (intModifier == 0) throw new ScriptRuntimeException(intSpan, "Cannot divide by 0");

                    result = Modulus(array, intModifier);
                    return true;
                }

                case ScriptBinaryOperator.ShiftLeft:
                    result = ShiftLeft(leftArray, rightValue);
                    return true;

                case ScriptBinaryOperator.ShiftRight:
                    result = ShiftRight(leftValue, rightArray);
                    return true;
            }

            return false;
        }

        private static IEnumerable<object> TryGetRange(object rightValue)
        {
            return rightValue as IEnumerable<object>;
        }

        private static bool CompareTo(TemplateContext context, SourceSpan span, ScriptBinaryOperator op, IEnumerable<object> left, IEnumerable<object> right)
        {
            // Compare the length first
            var leftCount = left.Count();
            var rightCount = right.Count();
            var compare = leftCount.CompareTo(rightCount);
            switch (op)
            {
                case ScriptBinaryOperator.CompareEqual:
                    if (compare != 0) return false;
                    break;
                case ScriptBinaryOperator.CompareNotEqual:
                    if (compare != 0) return true;
                    if (leftCount == 0) return false;
                    break;
                case ScriptBinaryOperator.CompareLessOrEqual:
                case ScriptBinaryOperator.CompareLess:
                    if (compare < 0) return true;
                    if (compare > 0) return false;
                    if (leftCount == 0 && op == ScriptBinaryOperator.CompareLess) return false;
                    break;
                case ScriptBinaryOperator.CompareGreaterOrEqual:
                case ScriptBinaryOperator.CompareGreater:
                    if (compare < 0) return false;
                    if (compare > 0) return true;
                    if (leftCount == 0 && op == ScriptBinaryOperator.CompareGreater) return false;
                    break;
                default:
                    throw new ScriptRuntimeException(span, $"The operator `{op.ToText()}` is not supported between {context.GetTypeName(left)} and {context.GetTypeName(right)}.");
            }

            // Otherwise we need to compare each element

            var leftIterator = left.GetEnumerator();
            var rightIterator = right.GetEnumerator();

            while (leftIterator.MoveNext() && rightIterator.MoveNext())
            {
                var leftValue = leftIterator.Current;
                var rightValue = rightIterator.Current;
                var result = (bool) ScriptBinaryExpression.Evaluate(context, span, op, leftValue, rightValue);
                if (!result)
                {
                    return false;
                }
            }

            return true;
        }

        public void Add(object item)
        {
            AddImpl(item);
        }

        private int AddImpl(object item)
        {
            var list = _values as IList;
            if (list == null)
            {
                _values = list = new ScriptArray(_values);
            }
            return list.Add(item);
        }

        int IList.Add(object value)
        {
            return AddImpl(value);
        }

        public void Clear()
        {
            _values = Enumerable.Empty<object>();
        }

        public bool Contains(object item)
        {
            if (_values is IList list) return list.Contains(item);
            return _values.Cast<object>().Any(value => value == item);
        }

        public void CopyTo(object[] array, int arrayIndex)
        {
            if (_values is IList list) list.CopyTo(array, arrayIndex);

            foreach (var value in _values)
            {
                array[arrayIndex++] = value;
            }
        }

        public bool Remove(object item)
        {
            var list = _values as IList;
            if (list == null)
            {
                _values = list = new ScriptArray(_values);
            }

            var contains = list.Contains(item);
            list.Remove(item);
            return contains;
        }

        void ICollection.CopyTo(Array array, int index)
        {
            if (_values is ICollection list) list.CopyTo(array, index);

            foreach (var value in _values)
            {
                array.SetValue(value, index++);
            }
        }

        public int Count
        {
            get
            {
                if (_values is IList list) return list.Count;
                return _values.Cast<object>().Count();
            }
        }

        bool ICollection.IsSynchronized => false;

        object ICollection.SyncRoot => null;

        public bool IsReadOnly => false;

        public int IndexOf(object item)
        {
            if (_values is IList list) return list.IndexOf(item);
            int index = 0;
            foreach (var value in _values)
            {
                if (value == item) return index;
                index++;
            }

            return -1;
        }

        public void Insert(int index, object item)
        {
            var list = _values as IList;
            if (list == null)
            {
                _values = list = new ScriptArray(_values);
            }

            list.Insert(index, item);
        }

        void IList.Remove(object value)
        {
            throw new NotImplementedException();
        }

        public void RemoveAt(int index)
        {
            var list = _values as IList;
            if (list == null)
            {
                _values = list = new ScriptArray(_values);
            }
            list.RemoveAt(index);
        }

        bool IList.IsFixedSize => false;

        public object this[int index]
        {
            get
            {
                if (_values is IList list) return list[index];
                return _values.Cast<object>().ElementAtOrDefault(index);
            }

            set
            {
                var list = _values as IList;
                if (list == null)
                {
                    _values = list = new ScriptArray(_values);
                }

                list[index] = value;
            }
        }
    }
}