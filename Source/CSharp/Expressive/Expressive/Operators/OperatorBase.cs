﻿using Expressive.Expressions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Expressive.Operators
{
    internal abstract class OperatorBase : IOperator
    {
        #region IOperator Members

        public abstract string[] Tags { get; }

        public abstract IExpression BuildExpression(string previousToken, IExpression[] expressions);

        public bool CanGetCaptiveTokens(string previousToken, string token, Queue<string> remainingTokens)
        {
            return true;
        }

        public string[] GetCaptiveTokens(string previousToken, string token, Queue<string> remainingTokens)
        {
            return new[] { token };
        }

        public string[] GetInnerCaptiveTokens(string[] allCaptiveTokens)
        {
            return new string[0];
        }

        public abstract OperatorPrecedence GetPrecedence(string previousToken);

        #endregion
    }
}
