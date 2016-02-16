﻿using Expressive.Expressions;
using Expressive.Functions;
using Expressive.Operators;
using Expressive.Operators.Additive;
using Expressive.Operators.Bitwise;
using Expressive.Operators.Grouping;
using Expressive.Operators.Logic;
using Expressive.Operators.Multiplicative;
using Expressive.Operators.Relational;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Expressive
{
    internal sealed class ExpressionParser
    {
        #region Constants

        private const char DateSeparator = '#';
        //private const char DecimalSeparator = '.';
        private const char ParameterSeparator = ',';

        #endregion

        #region Fields

        private readonly char _decimalSeparator;
        private IDictionary<string, Func<object[], object>> _registeredFunctions;
        private IDictionary<string, IOperator> _registeredOperators;

        #endregion

        #region Properties

        //internal static ExpressionParser Instance
        //{
        //    get
        //    {
        //        if (_instance == null)
        //        {
        //            lock (_instanceLock)
        //            {
        //                if (_instance == null)
        //                {
        //                    _instance = new ExpressionParser();
        //                }
        //            }
        //        }

        //        return _instance;
        //    }
        //}

        #endregion

        #region Constructors

        internal ExpressionParser()
        {
            _decimalSeparator = Convert.ToChar(CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator);
            _registeredFunctions = new Dictionary<string, Func<object[], object>>(StringComparer.OrdinalIgnoreCase);
            _registeredOperators = new Dictionary<string, IOperator>(StringComparer.OrdinalIgnoreCase);

            #region Operators
            // TODO: Do we allow for turning off operators?
            // Additive
            RegisterOperator(new PlusOperator());
            RegisterOperator(new SubtractOperator());
            // Bitwise
            RegisterOperator(new BitwiseAndOperator());
            RegisterOperator(new BitwiseOrOperator());
            RegisterOperator(new BitwiseXOrOperator());
            RegisterOperator(new LeftShiftOperator());
            RegisterOperator(new RightShiftOperator());
            // Grouping
            RegisterOperator(new ParenthesisCloseOperator());
            RegisterOperator(new ParenthesisOpenOperator());
            // Logic
            RegisterOperator(new AndOperator());
            RegisterOperator(new NotOperator());
            RegisterOperator(new OrOperator());
            // Multiplicative
            RegisterOperator(new DivideOperator());
            RegisterOperator(new ModulusOperator());
            RegisterOperator(new MultiplyOperator());
            // Relational
            RegisterOperator(new EqualOperator());
            RegisterOperator(new GreaterThanOperator());
            RegisterOperator(new GreaterThanOrEqualOperator());
            RegisterOperator(new LessThanOperator());
            RegisterOperator(new LessThanOrEqualOperator());
            RegisterOperator(new NotEqualOperator());
            #endregion

            #region Functions
            RegisterFunction(new AbsFunction());
            RegisterFunction(new AcosFunction());
            RegisterFunction(new AsinFunction());
            RegisterFunction(new AtanFunction());
            RegisterFunction(new AverageFunction());
            RegisterFunction(new CeilingFunction());
            RegisterFunction(new CosFunction());
            RegisterFunction(new CountFunction());
            RegisterFunction(new SumFunction());
            #endregion
        }

        #endregion

        #region Public Methods

        public void RegisterFunction(Func<object[], object> function)
        {
            RegisterFunction(function);
        }

        public void RegisterFunction(IFunction function)
        {
            _registeredFunctions.Add(function.Name, (p) =>
            {
                return function.Evaluate(p);
            });
        }

        public void UnregisterFunction(string name)
        {
            if (_registeredFunctions.ContainsKey(name))
            {
                _registeredFunctions.Remove(name);
            }
        }

        #endregion

        #region Internal Methods

        internal IExpression CompileExpression(string expression)
        {
            if (expression == null)
            {
                throw new ArgumentNullException("expression");
            }

            var tokens = Tokenise(expression);

            var openCount = tokens.Count(t => string.Equals(t, "(", StringComparison.Ordinal));
            var closeCount = tokens.Count(t => string.Equals(t, ")", StringComparison.Ordinal));

            // Bail out early if there isn't a matching set of ( and ) characters.
            if (openCount > closeCount)
            {
                throw new ArgumentException("There aren't enough ')' symbols. Expected " + openCount + " but there is only " + closeCount);
            }
            else if (openCount < closeCount)
            {
                throw new ArgumentException("There are too many ')' symbols. Expected " + openCount + " but there is " + closeCount);
            }

            return CompileExpression(new Queue<string>(tokens), OperatorPrecedence.Minimum);
        }

        internal void RegisterOperator(IOperator op)
        {
            foreach (var tag in op.Tags)
            {
                _registeredOperators.Add(tag, op);
            }
        }

        #endregion

        #region Private Methods

        private IExpression CompileExpression(Queue<string> tokens, OperatorPrecedence minimumPrecedence)
        {
            if (tokens == null)
            {
                throw new ArgumentNullException("tokens", "You must call Tokenise before compiling");
            }

            IExpression leftHandSide = null;
            var currentToken = tokens.FirstOrDefault();
            string previousToken = null;

            while (currentToken != null)
            {
                Func<object[], object> function = null;
                IOperator op = null;

                if (_registeredOperators.TryGetValue(currentToken, out op)) // Are we an IOperator?
                {
                    var precedence = op.GetPrecedence(previousToken);

                    if (precedence > minimumPrecedence)
                    {
                        tokens.Dequeue();

                        if (!op.CanGetCaptiveTokens(previousToken, currentToken, tokens))
                        {
                            // Do it anyway to update the list of tokens
                            op.GetCaptiveTokens(previousToken, currentToken, tokens);
                            break;
                        }
                        else
                        {
                            IExpression rightHandSide = null;

                            var captiveTokens = op.GetCaptiveTokens(previousToken, currentToken, tokens);

                            if (captiveTokens.Length > 1)
                            {
                                var innerTokens = op.GetInnerCaptiveTokens(captiveTokens);
                                rightHandSide = CompileExpression(new Queue<string>(innerTokens), precedence);

                                currentToken = captiveTokens[captiveTokens.Length - 1];
                            }
                            else
                            {
                                rightHandSide = CompileExpression(tokens, precedence);
                                // We are at the end of an expression so fake it up.
                                currentToken = ")";
                            }

                            leftHandSide = op.BuildExpression(previousToken, new[] { leftHandSide, rightHandSide });
                        }
                    }
                    else
                    {
                        break;
                    }
                }
                else if (_registeredFunctions.TryGetValue(currentToken, out function)) // or an IFunction?
                {
                    var expressions = new List<IExpression>();
                    var captiveTokens = new Queue<string>();
                    var parenCount = 0;
                    tokens.Dequeue();

                    // Loop through the list of tokens and split by ParameterSeparator character
                    while (tokens.Count > 0)
                    {
                        var nextToken = tokens.Dequeue();

                        if (string.Equals(nextToken, "(", StringComparison.Ordinal))
                        {
                            parenCount++;
                        }
                        else if (string.Equals(nextToken, ")", StringComparison.Ordinal))
                        {
                            parenCount--;
                        }

                        if (!(parenCount == 1 && nextToken == "(") &&
                                !(parenCount == 0 && nextToken == ")"))
                        {
                            captiveTokens.Enqueue(nextToken);
                        }

                        if (parenCount == 0 &&
                            captiveTokens.Any())
                        {
                            expressions.Add(CompileExpression(captiveTokens, minimumPrecedence: OperatorPrecedence.Minimum));
                            captiveTokens.Clear();
                        }
                        else if (string.Equals(nextToken, ParameterSeparator.ToString(), StringComparison.Ordinal) && parenCount == 1)
                        {
                            // TODO: Should we expect expressions to be null???
                            expressions.Add(CompileExpression(captiveTokens, minimumPrecedence: 0));
                            captiveTokens.Clear();
                        }

                        if (parenCount <= 0)
                        {
                            break;
                        }
                    }

                    leftHandSide = new FunctionExpression(currentToken, function, expressions.ToArray());
                }
                else if (currentToken.IsNumeric()) // Or a number
                {
                    tokens.Dequeue();
                    int intValue = 0;
                    decimal decimalValue = 0.0M;
                    double doubleValue = 0.0;
                    float floatValue = 0.0f;
                    long longValue = 0;

                    if (int.TryParse(currentToken, out intValue))
                    {
                        leftHandSide = new ConstantValueExpression(ConstantValueExpressionType.Integer, intValue);
                    }
                    else if (decimal.TryParse(currentToken, out decimalValue))
                    {
                        leftHandSide = new ConstantValueExpression(ConstantValueExpressionType.Decimal, decimalValue);
                    }
                    else if (double.TryParse(currentToken, out doubleValue))
                    {
                        leftHandSide = new ConstantValueExpression(ConstantValueExpressionType.Double, doubleValue);
                    }
                    else if (float.TryParse(currentToken, out floatValue))
                    {
                        leftHandSide = new ConstantValueExpression(ConstantValueExpressionType.Float, floatValue);
                    }
                    else if (long.TryParse(currentToken, out longValue))
                    {
                        leftHandSide = new ConstantValueExpression(ConstantValueExpressionType.Long, longValue);
                    }
                }
                else if (currentToken.StartsWith("[") && currentToken.EndsWith("]")) // or a variable?
                {
                    tokens.Dequeue();
                    leftHandSide = new VariableExpression(currentToken.Replace("[", "").Replace("]", ""));
                }
                else if (string.Equals(currentToken, "true", StringComparison.OrdinalIgnoreCase)) // or a boolean?
                {
                    tokens.Dequeue();
                    leftHandSide = new ConstantValueExpression(ConstantValueExpressionType.Boolean, true);
                }
                else if (string.Equals(currentToken, "false", StringComparison.OrdinalIgnoreCase))
                {
                    tokens.Dequeue();
                    leftHandSide = new ConstantValueExpression(ConstantValueExpressionType.Boolean, false);
                }
                else if (string.Equals(currentToken, "null", StringComparison.OrdinalIgnoreCase)) // or a null?
                {
                    tokens.Dequeue();
                    leftHandSide = new ConstantValueExpression(ConstantValueExpressionType.Null, null);
                }
                else if (currentToken.StartsWith(DateSeparator.ToString()) && currentToken.EndsWith(DateSeparator.ToString())) // or a date?
                {
                    tokens.Dequeue();
                    leftHandSide = new ConstantValueExpression(ConstantValueExpressionType.DateTime, DateTime.Parse(currentToken.Replace(DateSeparator.ToString(), "")));
                }
                else if ((currentToken.StartsWith("'") && currentToken.EndsWith("'")) ||
                    (currentToken.StartsWith("\"") && currentToken.EndsWith("\"")))
                {
                    tokens.Dequeue();
                    leftHandSide = new ConstantValueExpression(ConstantValueExpressionType.String, CleanString(currentToken.Substring(1, currentToken.Length - 2)));
                }
                else if (string.Equals(currentToken, ParameterSeparator.ToString(), StringComparison.Ordinal)) // Make sure we ignore the parameter separator
                {
                    tokens.Dequeue();

                    //throw new InvalidOperationException("Unrecognised token '" + currentToken + "'");

                    //if (!string.Equals(currentToken, ParameterSeparator.ToString(), StringComparison.Ordinal)) // Make sure we ignore the parameter separator
                    //{
                    //    currentToken = CleanString(currentToken);

                    //    leftHandSide = new ConstantValueExpression(ConstantValueExpressionType.Unknown, currentToken);
                    //}
                }
                else
                {
                    tokens.Dequeue();

                    throw new InvalidOperationException("Unrecognised token '" + currentToken + "'");
                }

                previousToken = currentToken;
                currentToken = tokens.PeekOrDefault();
            }

            return leftHandSide;
        }

        private string CleanString(string input)
        {
            if (input.Length <= 1) return input;

            // the input string can only get shorter
            // so init the buffer so we won't have to reallocate later
            char[] buffer = new char[input.Length];
            int outIdx = 0;
            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];
                if (c == '\\')
                {
                    if (i < input.Length - 1)
                    {
                        switch (input[i + 1])
                        {
                            case 'n':
                                buffer[outIdx++] = '\n';
                                i++;
                                continue;
                            case 'r':
                                buffer[outIdx++] = '\r';
                                i++;
                                continue;
                            case 't':
                                buffer[outIdx++] = '\t';
                                i++;
                                continue;
                            case '\'':
                                buffer[outIdx++] = '\'';
                                i++;
                                continue;
                        }
                    }
                }

                buffer[outIdx++] = c;
            }

            return new string(buffer, 0, outIdx);
        }

        private static string ExtractValue(string expression, int expressionLength, int index, string value)
        {
            string result = null;
            int valueLength = value.Length;

            if (expressionLength >= index + valueLength)
            {
                var trueString = expression.Substring(index, valueLength);
                bool isValidBoolean = true;
                if (expressionLength > index + valueLength)
                {
                    isValidBoolean = !char.IsLetterOrDigit(expression[index + valueLength]);
                }

                if (string.Equals(trueString, value, StringComparison.OrdinalIgnoreCase) &&
                    isValidBoolean)
                {
                    result = trueString;
                }
            }

            return result;
        }

        private string GetNumber(string expression, int startIndex)
        {
            bool hasDecimalPoint = false;
            var index = startIndex;
            var character = expression[index];

            while ((index < expression.Length) &&
                (char.IsDigit(character) || (!hasDecimalPoint && character == _decimalSeparator)))
            {
                if (!hasDecimalPoint && character == _decimalSeparator)
                {
                    hasDecimalPoint = true;
                }

                index++;

                if (index == expression.Length)
                {
                    break;
                }

                character = expression[index];
            }

            return expression.Substring(startIndex, index - startIndex);
        }

        private string GetString(string expression, int startIndex, char quoteCharacter)
        {
            int index = startIndex;
            bool foundEndingQuote = false;
            var character = expression[index];
            var previousCharacter = char.MinValue;

            while (index < expression.Length && !foundEndingQuote)
            {
                if (index != startIndex &&
                    character == quoteCharacter &&
                    previousCharacter != '\\') // Make sure the escape character wasn't previously used.
                {
                    foundEndingQuote = true;
                }

                previousCharacter = character;
                index++;

                if (index == expression.Length)
                {
                    break;
                }

                character = expression[index];
            }

            return expression.Substring(startIndex, index - startIndex);
        }

        private bool IsQuote(char character)
        {
            return character == '\'' || character == '\"';
        }

        private IList<string> Tokenise(string expression)
        {
            if (string.IsNullOrWhiteSpace(expression))
            {
                return null;
            }

            var expressionLength = expression.Length;
            var operators = _registeredOperators.OrderByDescending(op => op.Key.Length);
            var tokens = new List<string>();
            IList<char> unrecognised = null;

            var index = 0;

            while (index < expressionLength)
            {
                var lengthProcessed = 0;
                bool foundUnrecognisedCharacter = false;

                // Loop through and find any matching operators.
                foreach (var op in operators)
                {
                    var lookAhead = expression.Substring(index, Math.Min(op.Key.Length, expressionLength - index));

                    if (string.Equals(lookAhead, op.Key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.CheckForUnrecognised(unrecognised, tokens);
                        lengthProcessed = op.Key.Length;
                        tokens.Add(lookAhead);
                        break;
                    }
                }

                if (lengthProcessed == 0)
                {
                    foreach (var kvp in _registeredFunctions)
                    {
                        var lookAhead = expression.Substring(index, Math.Min(kvp.Key.Length, expressionLength - index));

                        if (string.Equals(lookAhead, kvp.Key, StringComparison.OrdinalIgnoreCase))
                        {
                            this.CheckForUnrecognised(unrecognised, tokens);
                            lengthProcessed = kvp.Key.Length;
                            tokens.Add(lookAhead);
                            break;
                        }
                    }
                }

                // If an operator wasn't found then process the current character.
                if (lengthProcessed == 0)
                {
                    var character = expression[index];

                    if (character == '[') // Apparently NCalc only uses the [ ] characters optionally but Xenplate forces them to be used.
                    {
                        var variable = expression.SubstringUpTo(index, ']');

                        this.CheckForUnrecognised(unrecognised, tokens);
                        tokens.Add(variable);
                        lengthProcessed = variable.Length;
                    }
                    else if (char.IsDigit(character))// || character == _decimalSeparator)
                    {
                        var number = GetNumber(expression, index);

                        this.CheckForUnrecognised(unrecognised, tokens);
                        tokens.Add(number);
                        lengthProcessed = number.Length;
                    }
                    else if (IsQuote(character))
                    {
                        var text = GetString(expression, index, character);

                        this.CheckForUnrecognised(unrecognised, tokens);
                        tokens.Add(text);
                        lengthProcessed = text.Length;
                    }
                    else if (character == DateSeparator)
                    {
                        // Ignore the first # when checking to allow us to find the second.
                        var dateString = "#" + expression.SubstringUpTo(index + 1, DateSeparator);

                        this.CheckForUnrecognised(unrecognised, tokens);
                        tokens.Add(dateString);
                        lengthProcessed = dateString.Length;
                    }
                    else if (character == ParameterSeparator)
                    {
                        this.CheckForUnrecognised(unrecognised, tokens);
                        tokens.Add(character.ToString());
                        lengthProcessed = 1;
                    }
                    else if (character == 't' || character == 'T')
                    {
                        this.CheckForUnrecognised(unrecognised, tokens);
                        var trueString = ExtractValue(expression, expressionLength, index, "true");

                        if (!string.IsNullOrWhiteSpace(trueString))
                        {
                            tokens.Add(trueString);
                            lengthProcessed = 4;
                        }
                    }
                    else if (character == 'f' || character == 'F')
                    {
                        this.CheckForUnrecognised(unrecognised, tokens);
                        var falseString = ExtractValue(expression, expressionLength, index, "false");

                        if (!string.IsNullOrWhiteSpace(falseString))
                        {
                            tokens.Add(falseString);
                            lengthProcessed = 5;
                        }
                    }
                    else if (character == 'n') // Check for null
                    {
                        this.CheckForUnrecognised(unrecognised, tokens);
                        var nullString = ExtractValue(expression, expressionLength, index, "null");

                        if (!string.IsNullOrWhiteSpace(nullString))
                        {
                            tokens.Add(nullString);
                            lengthProcessed = 4;
                        }
                    }
                    else if (!char.IsWhiteSpace(character))
                    {
                        // If we don't recognise this item then we had better keep going until we find something we know about.
                        if (unrecognised == null)
                        {
                            unrecognised = new List<char>();
                        }

                        foundUnrecognisedCharacter = true;
                        unrecognised.Add(character);
                    }
                }

                // Clear down the unrecognised buffer;
                if (!foundUnrecognisedCharacter)
                {
                    this.CheckForUnrecognised(unrecognised, tokens);
                    unrecognised = null;
                }
                index += (lengthProcessed == 0) ? 1 : lengthProcessed;
            }

            // Double check whether the last part is unrecognised.
            this.CheckForUnrecognised(unrecognised, tokens);

            return tokens;
        }

        private void CheckForUnrecognised(IList<char> unrecognised, IList<string> tokens)
        {
            if (unrecognised != null)
            {
                tokens.Add(new string(unrecognised.ToArray()));
            }
        }

        #endregion
    }
}
