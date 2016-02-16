﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Expressive.Expressions
{
    internal class ConstantValueExpression : IExpression
    {
        private readonly ConstantValueExpressionType _expressionType;
        private readonly object _value;

        internal ConstantValueExpression(ConstantValueExpressionType type, object value)
        {
            _expressionType = type;
            _value = value;
        }

        #region IExpression Members

        public object Evaluate(IDictionary<string, object> arguments)
        {
            return _value;
        }

        #endregion
    }

    internal enum ConstantValueExpressionType
    {
        Unknown,
        Integer,
        String,
        DateTime,
        Float,
        Boolean,
        Null,
        Double,
        Decimal,
        Long,
    }
}
