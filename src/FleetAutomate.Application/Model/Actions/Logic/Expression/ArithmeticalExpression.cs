using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Canvas.TestRunner.Model.Actions.Logic.Expression
{
    public enum ArithmeticOperator
    {
        Add,
        Subtract,
        Multiply,
        Divide,
        Modulus
    }
    public class ArithmeticalExpression<TResult> : ExpressionBase<TResult> where TResult : INumber<TResult>
    {
        public object OperandLeft { get; set; }

        public object OperandRight { get; set; }

        public ArithmeticOperator Operator { get; set; }

        public override void Evaluate()
        {
            if (OperandLeft is ArithmeticalExpression<TResult> expLeft)
            {
                expLeft.Evaluate();
                OperandLeft = expLeft.Result;
            }
            else if (OperandLeft is not TResult)
            {
                throw new InvalidOperationException($"OperandLeft must be of type INumber or ArithmeticalExpression<INumber>, but is {OperandLeft.GetType().Name}");
            }
            if (OperandRight is ArithmeticalExpression<TResult> expRight)
            {
                expRight.Evaluate();
                OperandRight = expRight.Result;
            }
            else if (OperandRight is not TResult)
            {
                throw new InvalidOperationException($"OperandRight must be of type INumber or ArithmeticalExpression<INumber>, but is {OperandRight.GetType().Name}");
            }
            switch (Operator)
            {
                case ArithmeticOperator.Add:
                    if (OperandLeft is TResult left && OperandRight is TResult right)
                    {
                        Result = left + right;
                    }
                    break;
                case ArithmeticOperator.Subtract:
                    if (OperandLeft is TResult left1 && OperandRight is TResult right1)
                    {
                        Result = left1 - right1;
                    }
                    break;
                case ArithmeticOperator.Multiply:
                    if (OperandLeft is TResult left3 && OperandRight is TResult right3)
                    {
                        Result = left3 * right3;
                    }
                    break;
                case ArithmeticOperator.Divide:
                    if (OperandLeft is TResult left4 && OperandRight is TResult right4)
                    {
                        Result = left4 / right4;
                    }
                    break;
                case ArithmeticOperator.Modulus:
                    if (OperandLeft is TResult left5 && OperandRight is TResult right5)
                    {
                        Result = left5 % right5;
                    }
                    break;
            }
        }
    }
}
