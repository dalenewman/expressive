﻿using Expressive.Expressions;
using Expressive.Expressions.Binary.Relational;

namespace Expressive.Operators.Relational
{
    internal class GreaterThanOrEqualOperator : OperatorBase
    {
        #region OperatorBase Members

        public override string[] Tags => new[] { ">=" };

        public override IExpression BuildExpression(Token previousToken, IExpression[] expressions, ExpressiveOptions options)
        {
            return new GreaterThanOrEqualExpression(expressions[0], expressions[1], options);
        }

        public override OperatorPrecedence GetPrecedence(Token previousToken)
        {
            return OperatorPrecedence.GreaterThanOrEqual;
        }

        #endregion
    }
}
