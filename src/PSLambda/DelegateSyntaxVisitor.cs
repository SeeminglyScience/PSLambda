using System;
using System.Collections.Generic;
using System.Management.Automation.Language;

namespace PSLambda
{
    /// <summary>
    /// Provides the ability to reshape a <see cref="ScriptBlockAst" /> when it fits
    /// the custom syntax for anonymous method expressions.
    /// </summary>
    internal class DelegateSyntaxVisitor : ICustomAstVisitor
    {
        private readonly IParseErrorWriter _errorWriter;

        private readonly List<Tuple<ITypeName, VariableExpressionAst>> _variables = new List<Tuple<ITypeName, VariableExpressionAst>>();

        private IScriptExtent _paramBlockExtent;

        private bool _failed;

        /// <summary>
        /// Initializes a new instance of the <see cref="DelegateSyntaxVisitor" /> class.
        /// </summary>
        /// <param name="errorWriter">The <see cref="IParseErrorWriter" /> to report errors to.</param>
        internal DelegateSyntaxVisitor(IParseErrorWriter errorWriter)
        {
            _errorWriter = errorWriter;
        }

        #pragma warning disable SA1600
        public object VisitScriptBlockExpression(ScriptBlockExpressionAst scriptBlockExpressionAst)
        {
            return scriptBlockExpressionAst.ScriptBlock.Visit(this);
        }

        public object VisitScriptBlock(ScriptBlockAst scriptBlockAst)
        {
            if (scriptBlockAst.ParamBlock != null)
            {
                return scriptBlockAst;
            }

            if (scriptBlockAst.BeginBlock != null)
            {
                _errorWriter.ReportNotSupported(
                    scriptBlockAst.BeginBlock.Extent,
                    nameof(CompilerStrings.BeginBlock),
                    CompilerStrings.BeginBlock);
                return null;
            }

            if (scriptBlockAst.ProcessBlock != null)
            {
                _errorWriter.ReportNotSupported(
                    scriptBlockAst.ProcessBlock.Extent,
                    nameof(CompilerStrings.ProcessBlock),
                    CompilerStrings.ProcessBlock);
                return null;
            }

            if (scriptBlockAst.EndBlock == null)
            {
                _errorWriter.ReportMissing(
                    scriptBlockAst.Extent,
                    nameof(CompilerStrings.EndBlock),
                    CompilerStrings.EndBlock);
                return null;
            }

            var body = scriptBlockAst.EndBlock.Visit(this);
            if (_failed)
            {
                return scriptBlockAst;
            }

            _errorWriter.ThrowIfAnyErrors();
            var parameters = new ParameterAst[_variables.Count];
            for (var i = 0; i < parameters.Length; i++)
            {
                parameters[i] = new ParameterAst(
                    _variables[i].Item2.Extent,
                    (VariableExpressionAst)_variables[i].Item2.Copy(),
                    new[] { new TypeConstraintAst(_variables[i].Item1.Extent, _variables[i].Item1) },
                    null);
            }

            var paramBlock =
                new ParamBlockAst(
                    _paramBlockExtent,
                    Array.Empty<AttributeAst>(),
                    parameters);

            return new ScriptBlockAst(
                scriptBlockAst.Extent,
                paramBlock,
                null,
                null,
                (NamedBlockAst)((Ast)body).Copy(),
                null);
        }

        public object VisitNamedBlock(NamedBlockAst namedBlockAst)
        {
            if (namedBlockAst.Statements == null ||
                namedBlockAst.Statements.Count == 0)
            {
                _errorWriter.ReportMissing(
                    namedBlockAst.Extent,
                    nameof(CompilerStrings.EndBlock),
                    CompilerStrings.EndBlock);
                return null;
            }

            if (namedBlockAst.Statements.Count > 1)
            {
                _failed = true;
                return null;
            }

            var body = namedBlockAst.Statements[0].Visit(this);
            if (body == null)
            {
                _failed = true;
                return null;
            }

            return body;
        }

        public object VisitPipeline(PipelineAst pipelineAst)
        {
            return pipelineAst.PipelineElements[0].Visit(this);
        }

        public object VisitAssignmentStatement(AssignmentStatementAst assignmentStatementAst)
        {
            _paramBlockExtent = assignmentStatementAst.Left.Extent;
            DelegateParameterVisitor.AddVariables(
                assignmentStatementAst.Left,
                _variables);

            if (!(assignmentStatementAst.Right is PipelineAst pipeline))
            {
                return null;
            }

            if (!(pipeline.PipelineElements[0] is CommandAst commandAst) ||
                commandAst.GetCommandName() != Strings.DelegateSyntaxCommandName ||
                commandAst.CommandElements.Count != 2)
            {
                return null;
            }

            if (commandAst.CommandElements[1] is ScriptBlockExpressionAst sbAst)
            {
                return sbAst.ScriptBlock.EndBlock;
            }

            var expression = commandAst.CommandElements[1] as ExpressionAst;

            var statements =
                new StatementAst[]
                {
                    new CommandExpressionAst(
                        expression.Extent,
                        (ExpressionAst)expression.Copy(),
                        Array.Empty<RedirectionAst>()),
                };

            var statementBlockAst = new StatementBlockAst(
                commandAst.CommandElements[1].Extent,
                statements,
                Array.Empty<TrapStatementAst>());

            return new NamedBlockAst(
                commandAst.CommandElements[1].Extent,
                TokenKind.End,
                statementBlockAst,
                unnamed: true);
        }
        #pragma warning restore SA1600

        private class DelegateParameterVisitor : AstVisitor
        {
            private static readonly ITypeName s_objectTypeName = new TypeName(
                Empty.Extent,
                typeof(object).FullName);

            private List<Tuple<ITypeName, VariableExpressionAst>> _variables = new List<Tuple<ITypeName, VariableExpressionAst>>();

            private DelegateParameterVisitor()
            {
            }

            public static void AddVariables(
                ExpressionAst expression,
                List<Tuple<ITypeName, VariableExpressionAst>> variables)
            {
                var visitor = new DelegateParameterVisitor
                {
                    _variables = variables,
                };

                expression.Visit(visitor);
            }

            public override AstVisitAction VisitConvertExpression(ConvertExpressionAst convertExpressionAst)
            {
                if (convertExpressionAst.Child is VariableExpressionAst variable)
                {
                    _variables.Add(
                        Tuple.Create(
                            convertExpressionAst.Attribute.TypeName,
                            variable));

                    return AstVisitAction.SkipChildren;
                }

                return AstVisitAction.StopVisit;
            }

            public override AstVisitAction VisitVariableExpression(VariableExpressionAst variableExpressionAst)
            {
                _variables.Add(
                    Tuple.Create(
                        s_objectTypeName,
                        variableExpressionAst));

                return AstVisitAction.SkipChildren;
            }
        }

        #pragma warning disable SA1600, SA1201, SA1516
        public object VisitArrayExpression(ArrayExpressionAst arrayExpressionAst) => null;
        public object VisitArrayLiteral(ArrayLiteralAst arrayLiteralAst) => null;
        public object VisitAttribute(AttributeAst attributeAst) => null;
        public object VisitAttributedExpression(AttributedExpressionAst attributedExpressionAst) => null;
        public object VisitBinaryExpression(BinaryExpressionAst binaryExpressionAst) => null;
        public object VisitBlockStatement(BlockStatementAst blockStatementAst) => null;
        public object VisitBreakStatement(BreakStatementAst breakStatementAst) => null;
        public object VisitCatchClause(CatchClauseAst catchClauseAst) => null;
        public object VisitCommand(CommandAst commandAst) => null;
        public object VisitCommandExpression(CommandExpressionAst commandExpressionAst) => null;
        public object VisitCommandParameter(CommandParameterAst commandParameterAst) => null;
        public object VisitConstantExpression(ConstantExpressionAst constantExpressionAst) => null;
        public object VisitContinueStatement(ContinueStatementAst continueStatementAst) => null;
        public object VisitConvertExpression(ConvertExpressionAst convertExpressionAst) => null;
        public object VisitDataStatement(DataStatementAst dataStatementAst) => null;
        public object VisitDoUntilStatement(DoUntilStatementAst doUntilStatementAst) => null;
        public object VisitDoWhileStatement(DoWhileStatementAst doWhileStatementAst) => null;
        public object VisitErrorExpression(ErrorExpressionAst errorExpressionAst) => null;
        public object VisitErrorStatement(ErrorStatementAst errorStatementAst) => null;
        public object VisitExitStatement(ExitStatementAst exitStatementAst) => null;
        public object VisitExpandableStringExpression(ExpandableStringExpressionAst expandableStringExpressionAst) => null;
        public object VisitFileRedirection(FileRedirectionAst fileRedirectionAst) => null;
        public object VisitForEachStatement(ForEachStatementAst forEachStatementAst) => null;
        public object VisitForStatement(ForStatementAst forStatementAst) => null;
        public object VisitFunctionDefinition(FunctionDefinitionAst functionDefinitionAst) => null;
        public object VisitHashtable(HashtableAst hashtableAst) => null;
        public object VisitIfStatement(IfStatementAst ifStmtAst) => null;
        public object VisitIndexExpression(IndexExpressionAst indexExpressionAst) => null;
        public object VisitInvokeMemberExpression(InvokeMemberExpressionAst invokeMemberExpressionAst) => null;
        public object VisitMemberExpression(MemberExpressionAst memberExpressionAst) => null;
        public object VisitMergingRedirection(MergingRedirectionAst mergingRedirectionAst) => null;
        public object VisitNamedAttributeArgument(NamedAttributeArgumentAst namedAttributeArgumentAst) => null;
        public object VisitParamBlock(ParamBlockAst paramBlockAst) => null;
        public object VisitParameter(ParameterAst parameterAst) => null;
        public object VisitParenExpression(ParenExpressionAst parenExpressionAst) => null;
        public object VisitReturnStatement(ReturnStatementAst returnStatementAst) => null;
        public object VisitStatementBlock(StatementBlockAst statementBlockAst) => null;
        public object VisitStringConstantExpression(StringConstantExpressionAst stringConstantExpressionAst) => null;
        public object VisitSubExpression(SubExpressionAst subExpressionAst) => null;
        public object VisitSwitchStatement(SwitchStatementAst switchStatementAst) => null;
        public object VisitThrowStatement(ThrowStatementAst throwStatementAst) => null;
        public object VisitTrap(TrapStatementAst trapStatementAst) => null;
        public object VisitTryStatement(TryStatementAst tryStatementAst) => null;
        public object VisitTypeConstraint(TypeConstraintAst typeConstraintAst) => null;
        public object VisitTypeExpression(TypeExpressionAst typeExpressionAst) => null;
        public object VisitUnaryExpression(UnaryExpressionAst unaryExpressionAst) => null;
        public object VisitUsingExpression(UsingExpressionAst usingExpressionAst) => null;
        public object VisitVariableExpression(VariableExpressionAst variableExpressionAst) => null;
        public object VisitWhileStatement(WhileStatementAst whileStatementAst) => null;
        #pragma warning restore SA1600, SA1201, SA1516
    }
}
