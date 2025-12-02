using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Summary.Common.Model
{
    public struct ConditionalValue<T>
    {
        public bool HasValue { get; }
        public T Value { get; }

        public ConditionalValue(bool hasValue, T value)
        {
            HasValue = hasValue;
            Value = value;
        }

        public static ConditionalValue<T> NoValue => new ConditionalValue<T>(false, default!);
        public static ConditionalValue<T> FromValue(T value) => new ConditionalValue<T>(true, value);
    }
}
