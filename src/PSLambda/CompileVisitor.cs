using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Reflection;
using PSLambda.Commands;

using static PSLambda.ExpressionUtils;
using static System.Linq.Expressions.Expression;

namespace PSLambda
{
    /// <summary>
    /// Provides the ability to compile a PowerShell <see cref="Ast" /> object into a
    /// Linq <see cref="Expression" /> tree.
    /// </summary>
    internal class CompileVisitor : ICustomAstVisitor, ICustomAstVisitor2
    {
        private static readonly CommandService s_commands = new CommandService();

        private static readonly Dictionary<PSVariable, Expression> s_wrapperCache = new Dictionary<PSVariable, Expression>();

        private static readonly ParameterExpression[] s_emptyParameters = new ParameterExpression[0];

        private static readonly PSVariable[] s_emptyVariables = new PSVariable[0];

        private static readonly ParameterModifier[] s_emptyModifiers = new ParameterModifier[0];

        private readonly ConstantExpression _engine;

        private readonly LabelScopeStack _returns = new LabelScopeStack();

        private readonly LoopScopeStack _loops = new LoopScopeStack();

        private readonly VariableScopeStack _scopeStack = new VariableScopeStack();

        private readonly List<ParseError> _parseErrors = new List<ParseError>();

        private Dictionary<string, PSVariable> _locals = new Dictionary<string, PSVariable>();

        private Dictionary<string, PSVariable> _allScopes;

        private CompileVisitor(EngineIntrinsics engine)
        {
            _engine = Constant(engine, typeof(EngineIntrinsics));
        }

        #pragma warning disable SA1600
        public object VisitArrayExpression(ArrayExpressionAst arrayExpressionAst)
        {
            var elements = arrayExpressionAst.SubExpression.Statements.CompileAll(this);
            if (elements.Length > 1)
            {
                if (elements.GroupBy(e => e.Type).Count() == 1)
                {
                    return NewArrayInit(elements[0].Type, elements);
                }

                return NewArrayInit(
                    typeof(object),
                    elements);
            }

            if (elements.Length == 1 && elements[0].Type.IsArray)
            {
                return elements[0];
            }

            if (elements.Length == 0)
            {
                return NewArrayInit(
                    typeof(object),
                    elements);
            }

            return NewArrayInit(
                elements[0].Type,
                elements);
        }

        public object VisitArrayLiteral(ArrayLiteralAst arrayLiteralAst)
        {
            var elements = arrayLiteralAst.Elements.CompileAll(this);
            if (elements.GroupBy(e => e.Type).Count() == 1)
            {
                return NewArrayInit(elements[0].Type, elements);
            }

            return NewArrayInit(typeof(object), elements);
        }

        public object VisitAssignmentStatement(AssignmentStatementAst assignmentStatementAst)
        {
            try
            {
                if (assignmentStatementAst.Right is CommandExpressionAst expression &&
                    expression.Expression is VariableExpressionAst rhsVariable &&
                    rhsVariable.VariablePath.UserPath.Equals(
                        Strings.NullVariableName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    var lhs = assignmentStatementAst.Left.Compile(this);
                    return Assign(lhs, Constant(null, lhs.Type));
                }

                VariableExpressionAst variable = assignmentStatementAst.Left as VariableExpressionAst;
                Type targetType = null;
                if (variable == null && assignmentStatementAst.Left is ConvertExpressionAst convert)
                {
                    variable = convert.Child as VariableExpressionAst;
                    if (!TryResolveType(convert.Attribute, out targetType))
                    {
                        return Empty();
                    }
                }

                if (variable == null)
                {
                    return MakeAssignment(
                        assignmentStatementAst.Left.Compile(this),
                        assignmentStatementAst.Right.Compile(this),
                        assignmentStatementAst.Operator,
                        assignmentStatementAst.ErrorPosition);
                }

                if (_locals.ContainsKey(variable.VariablePath.UserPath) ||
                    SpecialVariables.AllScope.ContainsKey(variable.VariablePath.UserPath) ||
                    SpecialVariables.Constants.ContainsKey(variable.VariablePath.UserPath))
                {
                    return MakeAssignment(
                        assignmentStatementAst.Left.Compile(this),
                        assignmentStatementAst.Right.Compile(this),
                        assignmentStatementAst.Operator,
                        assignmentStatementAst.ErrorPosition);
                }

                if (targetType != null)
                {
                    return MakeAssignment(
                        _scopeStack.GetVariable(variable.VariablePath.UserPath, targetType),
                        assignmentStatementAst.Right.Compile(this),
                        assignmentStatementAst.Operator,
                        assignmentStatementAst.ErrorPosition);
                }

                var rhs = assignmentStatementAst.Right.Compile(this);
                return MakeAssignment(
                    _scopeStack.GetVariable(variable.VariablePath.UserPath, rhs.Type),
                    rhs,
                    assignmentStatementAst.Operator,
                    assignmentStatementAst.ErrorPosition);
            }
            catch (ArgumentException e)
            {
                ReportParseError(assignmentStatementAst.Extent, e);
                return Empty();
            }
        }

        public object VisitBinaryExpression(BinaryExpressionAst binaryExpressionAst)
        {
            var lhs = binaryExpressionAst.Left.Compile(this);
            var rhs = binaryExpressionAst.Right.Compile(this);

            Type rhsTypeConstant = (rhs as ConstantExpression)?.Value as Type;
            switch (binaryExpressionAst.Operator)
            {
                case TokenKind.DotDot:
                    return PSDotDot(lhs, rhs);
                case TokenKind.Format:
                    return PSFormat(lhs, rhs);
                case TokenKind.Join:
                    return PSJoin(lhs, rhs);
                case TokenKind.Ireplace:
                    return PSReplace(lhs, rhs, false);
                case TokenKind.Creplace:
                    return PSReplace(lhs, rhs, true);
                case TokenKind.Isplit:
                    return PSSplit(lhs, rhs, false);
                case TokenKind.Csplit:
                    return PSSplit(lhs, rhs, true);
                case TokenKind.Iin:
                    return PSIsIn(lhs, rhs, false);
                case TokenKind.Cin:
                    return PSIsIn(lhs, rhs, true);
                case TokenKind.Inotin:
                    return Not(PSIsIn(lhs, rhs, false));
                case TokenKind.Cnotin:
                    return Not(PSIsIn(lhs, rhs, true));
                case TokenKind.Icontains:
                    return PSIsIn(rhs, lhs, false);
                case TokenKind.Ccontains:
                    return PSIsIn(rhs, lhs, true);
                case TokenKind.Inotcontains:
                    return Not(PSIsIn(rhs, lhs, false));
                case TokenKind.Cnotcontains:
                    return Not(PSIsIn(rhs, lhs, true));
                case TokenKind.Ige:
                    return Or(PSGreaterThan(lhs, rhs, false), PSEquals(lhs, rhs, false));
                case TokenKind.Cge:
                    return Or(PSGreaterThan(lhs, rhs, true), PSEquals(lhs, rhs, true));
                case TokenKind.Ile:
                    return Or(PSLessThan(lhs, rhs, false), PSEquals(lhs, rhs, false));
                case TokenKind.Cle:
                    return Or(PSLessThan(lhs, rhs, true), PSEquals(lhs, rhs, true));
                case TokenKind.Igt:
                    return PSGreaterThan(lhs, rhs, false);
                case TokenKind.Cgt:
                    return PSGreaterThan(lhs, rhs, true);
                case TokenKind.Ilt:
                    return PSLessThan(lhs, rhs, false);
                case TokenKind.Clt:
                    return PSLessThan(lhs, rhs, true);
                case TokenKind.Ilike:
                    return PSLike(lhs, rhs, isCaseSensitive: false);
                case TokenKind.Clike:
                    return PSLike(lhs, rhs, isCaseSensitive: true);
                case TokenKind.Inotlike:
                    return Not(PSLike(lhs, rhs, isCaseSensitive: false));
                case TokenKind.Cnotlike:
                    return Not(PSLike(lhs, rhs, isCaseSensitive: true));
                case TokenKind.Imatch:
                    return PSMatch(lhs, rhs, isCaseSensitive: false);
                case TokenKind.Cmatch:
                    return PSMatch(lhs, rhs, isCaseSensitive: true);
                case TokenKind.Inotmatch:
                    return Not(PSMatch(lhs, rhs, isCaseSensitive: false));
                case TokenKind.Cnotmatch:
                    return Not(PSMatch(lhs, rhs, isCaseSensitive: true));
                case TokenKind.Ieq:
                    return PSEquals(lhs, rhs, isCaseSensitive: false);
                case TokenKind.Ceq:
                    return PSEquals(lhs, rhs, isCaseSensitive: true);
                case TokenKind.Ine:
                    return Not(PSEquals(lhs, rhs, isCaseSensitive: false));
                case TokenKind.Cne:
                    return Not(PSEquals(lhs, rhs, isCaseSensitive: true));
                case TokenKind.And:
                    return AndAlso(PSIsTrue(lhs), PSIsTrue(rhs));
                case TokenKind.Or:
                    return OrElse(PSIsTrue(lhs), PSIsTrue(rhs));
                case TokenKind.Band:
                    return PSBitwiseOperation(ExpressionType.And, lhs, rhs);
                case TokenKind.Bor:
                    return PSBitwiseOperation(ExpressionType.Or, lhs, rhs);
                case TokenKind.Is:
                    if (rhsTypeConstant == null)
                    {
                        ReportNonConstantTypeAs(binaryExpressionAst.Right.Extent);
                        return Empty();
                    }

                    return TypeIs(lhs, rhsTypeConstant);
                case TokenKind.IsNot:
                    if (rhsTypeConstant == null)
                    {
                        ReportNonConstantTypeAs(binaryExpressionAst.Right.Extent);
                        return Empty();
                    }

                    return Not(TypeIs(lhs, rhsTypeConstant));
                case TokenKind.As:
                    if (rhsTypeConstant == null)
                    {
                        ReportNonConstantTypeAs(binaryExpressionAst.Right.Extent);
                        return Empty();
                    }

                    return PSTypeAs(lhs, rhsTypeConstant);
                case TokenKind.Plus:
                    return MakeBinary(ExpressionType.Add, lhs, rhs);
                case TokenKind.Minus:
                    return MakeBinary(ExpressionType.Subtract, lhs, rhs);
                case TokenKind.Divide:
                    return MakeBinary(ExpressionType.Divide, lhs, rhs);
                case TokenKind.Multiply:
                    return MakeBinary(ExpressionType.Multiply, lhs, rhs);
                case TokenKind.Rem:
                    return MakeBinary(ExpressionType.Modulo, lhs, rhs);
                default:
                    ReportNotSupported(
                        binaryExpressionAst.ErrorPosition,
                        binaryExpressionAst.Operator.ToString(),
                        binaryExpressionAst.Operator.ToString());
                    return Empty();
            }
        }

        public object VisitBlockStatement(BlockStatementAst blockStatementAst)
        {
            return blockStatementAst.Body.Visit(this);
        }

        public object VisitBreakStatement(BreakStatementAst breakStatementAst)
        {
            return _loops.Break != null
                ? Break(_loops.Break)
                : Return(_returns.GetOrCreateReturnLabel(typeof(void)));
        }

        public object VisitCatchClause(CatchClauseAst catchClauseAst)
        {
            return Catch(
                catchClauseAst.IsCatchAll
                    ? typeof(Exception)
                    : catchClauseAst.CatchTypes.Count() > 1
                        ? typeof(Exception)
                        : catchClauseAst.CatchTypes.First().TypeName.GetReflectionType()
                        ?? typeof(Exception),
                NewBlock(() => catchClauseAst.Body.Compile(this), typeof(void)));
        }

        public object VisitCommandExpression(CommandExpressionAst commandExpressionAst)
        {
            if (commandExpressionAst.Redirections.Count > 0)
            {
                commandExpressionAst.Redirections.CompileAll(this);
                return Empty();
            }

            return commandExpressionAst.Expression.Visit(this);
        }

        public object VisitConstantExpression(ConstantExpressionAst constantExpressionAst)
        {
            return Constant(
                constantExpressionAst.SafeGetValue(),
                constantExpressionAst.StaticType);
        }

        public object VisitContinueStatement(ContinueStatementAst continueStatementAst)
        {
            return Continue(_loops?.Continue);
        }

        public object VisitConvertExpression(ConvertExpressionAst convertExpressionAst)
        {
            Type resolvedType;
            if (!TryResolveType(convertExpressionAst.Attribute, out resolvedType))
            {
                return Empty();
            }

            if (convertExpressionAst.Child is VariableExpressionAst variableExpression &&
                convertExpressionAst.Parent is AssignmentStatementAst assignment &&
                assignment.Left == convertExpressionAst)
            {
                return _scopeStack.GetVariable(
                    variableExpression.VariablePath.UserPath,
                    resolvedType);
            }

            return PSConvertTo(
                convertExpressionAst.Child.Compile(this),
                resolvedType);
        }

        public object VisitDoUntilStatement(DoUntilStatementAst doUntilStatementAst)
        {
            return NewBlock(() =>
            {
                using (_loops.NewScope())
                {
                    var body = doUntilStatementAst.Body.Compile(this);
                    return new[]
                    {
                        body,
                        Loop(
                            IfThenElse(
                                Not(PSIsTrue(doUntilStatementAst.Condition.Compile(this))),
                                body,
                                Break(_loops.Break)),
                            _loops.Break,
                            _loops.Continue)
                    };
                }
            });
        }

        public object VisitDoWhileStatement(DoWhileStatementAst doWhileStatementAst)
        {
            return NewBlock(() =>
            {
                using (_loops.NewScope())
                {
                    var body = doWhileStatementAst.Body.Compile(this);
                    return new[]
                    {
                        body,
                        Loop(
                            IfThenElse(
                                PSIsTrue(doWhileStatementAst.Condition.Compile(this)),
                                body,
                                Break(_loops.Break)),
                            _loops.Break,
                            _loops.Continue)
                    };
                }
            });
        }

        public object VisitExitStatement(ExitStatementAst exitStatementAst)
        {
            if (exitStatementAst.Pipeline == null)
            {
                return Throw(New(ReflectionCache.ExitException_Ctor));
            }

            return Throw(
                New(
                    ReflectionCache.ExitException_Ctor_Object,
                    Convert(exitStatementAst.Pipeline.Compile(this), typeof(object))));
        }

        public object VisitForEachStatement(ForEachStatementAst forEachStatementAst)
        {
            using (_loops.NewScope())
            {
                var enumerator = Call(
                    ReflectionCache.LanguagePrimitives_GetEnumerator,
                    forEachStatementAst.Condition.Compile(this));

                using (_scopeStack.NewScope())
                {
                    var enumeratorRef = _scopeStack.GetVariable(
                        Strings.ForEachVariableName,
                        typeof(IEnumerator));
                    try
                    {
                        return Block(
                            _scopeStack.GetVariables(),
                            Assign(enumeratorRef, enumerator),
                            Loop(
                                IfThenElse(
                                    test: Call(enumeratorRef, ReflectionCache.IEnumerator_MoveNext),
                                    ifTrue: NewBlock(
                                        () => new[]
                                        {
                                            Assign(
                                                _scopeStack.GetVariable(
                                                    forEachStatementAst.Variable.VariablePath.UserPath,
                                                    typeof(object)),
                                                Property(enumeratorRef, ReflectionCache.IEnumerator_Current)),
                                            forEachStatementAst.Body.Compile(this)
                                        }),
                                    ifFalse: Break(_loops.Break)),
                                _loops.Break,
                                _loops.Continue));
                    }
                    catch (ArgumentException e)
                    {
                        ReportParseError(forEachStatementAst.Extent, e);
                        return Empty();
                    }
                }
            }
        }

        public object VisitForStatement(ForStatementAst forStatementAst)
        {
            return NewBlock(
                () =>
                {
                    using (_loops.NewScope())
                    {
                        return new[]
                        {
                            forStatementAst.Initializer.Compile(this),
                            Loop(
                                IfThenElse(
                                    forStatementAst.Condition.Compile(this),
                                    NewBlock(() => new[]
                                    {
                                        forStatementAst.Body.Compile(this),
                                        forStatementAst.Iterator?.Compile(this) ?? Empty()
                                    }),
                                    Break(_loops.Break)),
                                _loops.Break,
                                _loops.Continue)
                        };
                    }
                });
        }

        public object VisitHashtable(HashtableAst hashtableAst)
        {
            if (hashtableAst.KeyValuePairs.Count == 0)
            {
                return New(
                    ReflectionCache.Hashtable_Ctor,
                    Constant(0),
                    Property(null, ReflectionCache.StringComparer_CurrentCultureIgnoreCase));
            }

            var elements = new ElementInit[hashtableAst.KeyValuePairs.Count];
            for (var i = 0; i < elements.Length; i++)
            {
                elements[i] = ElementInit(
                    ReflectionCache.Hashtable_Add,
                    Convert(hashtableAst.KeyValuePairs[i].Item1.Compile(this), typeof(object)),
                    Convert(hashtableAst.KeyValuePairs[i].Item2.Compile(this), typeof(object)));
            }

            return ListInit(
                New(
                    ReflectionCache.Hashtable_Ctor,
                    Constant(hashtableAst.KeyValuePairs.Count, typeof(int)),
                    Property(null, ReflectionCache.StringComparer_CurrentCultureIgnoreCase)),
                elements);
        }

        public object VisitIfStatement(IfStatementAst ifStmtAst)
        {
            return HandleRemainingClauses(ifStmtAst);
        }

        public object VisitIndexExpression(IndexExpressionAst indexExpressionAst)
        {
            var source = indexExpressionAst.Target.Compile(this);
            if (source.Type.IsArray)
            {
                return ArrayIndex(
                    source,
                    indexExpressionAst.Index.Compile(this));
            }

            if (TryFindGenericInterface(source.Type, typeof(IList<>), out Type genericList))
            {
                return MakeIndex(
                    source,
                    genericList.GetProperty(Strings.DefaultIndexerPropertyName),
                    new[] { indexExpressionAst.Index.Compile(this) });
            }

            if (TryFindGenericInterface(source.Type, typeof(IDictionary<,>), out Type genericDictionary))
            {
                return MakeIndex(
                    source,
                    genericDictionary.GetProperty(Strings.DefaultIndexerPropertyName),
                    new[] { indexExpressionAst.Index.Compile(this) });
            }

            if (TryFindGenericInterface(source.Type, typeof(IEnumerable<>), out Type genericEnumerable) ||
                (source.Type.IsGenericType &&
                source.Type.GetGenericTypeDefinition() == typeof(IEnumerable<>)))
            {
                if (genericEnumerable == null)
                {
                    genericEnumerable = source.Type;
                }

                Expression index;
                try
                {
                    index = Convert(indexExpressionAst.Index.Compile(this), typeof(int));
                }
                catch (InvalidOperationException e)
                {
                    ReportParseError(indexExpressionAst.Index.Extent, e);
                    return Empty();
                }

                return Call(
                    typeof(Enumerable),
                    Strings.ElementAtOrDefaultMethodName,
                    genericEnumerable.GetGenericArguments(),
                    source,
                    index);
            }

            if (typeof(System.Collections.IList).IsAssignableFrom(source.Type))
            {
                return MakeIndex(
                    source,
                    ReflectionCache.IList_Item,
                    new[] { indexExpressionAst.Index.Compile(this) });
            }

            if (typeof(System.Collections.IDictionary).IsAssignableFrom(source.Type))
            {
                return MakeIndex(
                    source,
                    ReflectionCache.IDictionary_Item,
                    new[] { indexExpressionAst.Index.Compile(this) });
            }

            ReportParseError(
                indexExpressionAst.Extent,
                nameof(ErrorStrings.UnknownIndexer),
                string.Format(
                    CultureInfo.CurrentCulture,
                    ErrorStrings.UnknownIndexer,
                    source.Type.FullName));
            return Empty();
        }

        public object VisitInvokeMemberExpression(InvokeMemberExpressionAst invokeMemberExpressionAst)
        {
            return CompileInvokeMemberExpression(invokeMemberExpressionAst);
        }

        public object VisitMemberExpression(MemberExpressionAst memberExpressionAst)
        {
            string memberName;
            if (!TryResolveConstant(memberExpressionAst.Member, out memberName))
            {
                return Empty();
            }

            if (memberExpressionAst.Static)
            {
                Type resolvedType;
                if (!TryResolveType(memberExpressionAst.Expression, out resolvedType))
                {
                    return Empty();
                }

                var resolvedMember = resolvedType
                    .FindMembers(
                        MemberTypes.Property | MemberTypes.Field,
                        BindingFlags.Public | BindingFlags.Static,
                        Type.FilterNameIgnoreCase,
                        memberName)
                    .OrderBy(m => m.MemberType == MemberTypes.Property)
                    .FirstOrDefault();

                if (resolvedMember == null)
                {
                    ReportParseError(
                        memberExpressionAst.Extent,
                        nameof(ErrorStrings.MissingMember),
                        string.Format(
                            CultureInfo.CurrentCulture,
                            ErrorStrings.MissingMember,
                            memberName,
                            resolvedType.FullName));
                    return Empty();
                }

                if (resolvedMember is FieldInfo field)
                {
                    return Field(null, field);
                }

                return Property(null, (PropertyInfo)resolvedMember);
            }

            try
            {
                return PropertyOrField(
                    memberExpressionAst.Expression.Compile(this),
                    memberName);
            }
            catch (ArgumentException e)
            {
                ReportParseError(memberExpressionAst.Extent, e, nameof(ErrorStrings.MissingMember));
                return Empty();
            }
        }

        public object VisitNamedBlock(NamedBlockAst namedBlockAst)
        {
            if (namedBlockAst.Statements.Count < 1)
            {
                return Empty();
            }

            return NewBlock(() => namedBlockAst.Statements.CompileAll(this));
        }

        public object VisitParamBlock(ParamBlockAst paramBlockAst)
        {
            foreach (var parameter in paramBlockAst.Parameters)
            {
                parameter.Visit(this);
            }

            return Empty();
        }

        public object VisitParameter(ParameterAst parameterAst)
        {
            return Parameter(
                GetParameterType(parameterAst),
                parameterAst.Name.VariablePath.UserPath);
        }

        public object VisitParenExpression(ParenExpressionAst parenExpressionAst)
        {
            return parenExpressionAst.Pipeline.Visit(this);
        }

        public object VisitPipeline(PipelineAst pipelineAst)
        {
            if (pipelineAst.PipelineElements.Count > 1)
            {
                ReportNotSupported(
                    pipelineAst.Extent,
                    nameof(ErrorStrings.Pipeline),
                    ErrorStrings.Pipeline);
                return Empty();
            }

            if (pipelineAst.PipelineElements.Count == 0)
            {
                return Empty();
            }

            return pipelineAst.PipelineElements[0].Visit(this);
        }

        public object VisitReturnStatement(ReturnStatementAst returnStatementAst)
        {
            if (returnStatementAst.Pipeline == null)
            {
                try
                {
                    return Return(_returns.GetOrCreateReturnLabel(typeof(void)));
                }
                catch (ArgumentException e)
                {
                    ReportParseError(returnStatementAst.Extent, e);
                    return Empty();
                }
            }

            Expression pipeline = returnStatementAst.Pipeline.Compile(this);
            try
            {
                return Return(
                    _returns.GetOrCreateReturnLabel(pipeline.Type),
                    pipeline);
            }
            catch (ArgumentException e)
            {
                ReportParseError(returnStatementAst.Extent, e);
                return Empty();
            }
        }

        public object VisitScriptBlock(ScriptBlockAst scriptBlockAst)
        {
            if (scriptBlockAst.Parent is ScriptBlockExpressionAst sbExpression &&
                sbExpression.Parent is ConvertExpressionAst convert &&
                TryResolveType(convert.Attribute, out Type resolvedType))
            {
                return CompileAstImpl(scriptBlockAst, s_emptyVariables, resolvedType);
            }

            return CompileAstImpl(scriptBlockAst, s_emptyVariables);
        }

        public object VisitScriptBlockExpression(ScriptBlockExpressionAst scriptBlockExpressionAst)
        {
            return scriptBlockExpressionAst.ScriptBlock.Visit(this);
        }

        public object VisitStatementBlock(StatementBlockAst statementBlockAst)
        {
            if (statementBlockAst.Statements.Count == 0)
            {
                return Empty();
            }

            using (_scopeStack.NewScope())
            {
                var stmts = new Expression[statementBlockAst.Statements.Count];
                for (var i = 0; i < stmts.Length; i++)
                {
                    stmts[i] = statementBlockAst.Statements[i].Compile(this);
                }

                try
                {
                    return Block(_scopeStack.GetVariables(), stmts);
                }
                catch (ArgumentException e)
                {
                    ReportParseError(statementBlockAst.Extent, e);
                    return Empty();
                }
            }
        }

        public object VisitStringConstantExpression(StringConstantExpressionAst stringConstantExpressionAst)
        {
            return Constant(stringConstantExpressionAst.SafeGetValue(), typeof(string));
        }

        public object VisitSubExpression(SubExpressionAst subExpressionAst)
        {
            return subExpressionAst.SubExpression.Visit(this);
        }

        public object VisitSwitchStatement(SwitchStatementAst switchStatementAst)
        {
            return NewBlock(() =>
            {
                using (_loops.NewScope())
                {
                    if (switchStatementAst.Clauses.Count == 0)
                    {
                        return new[]
                        {
                            switchStatementAst.Default.Compile(this),
                            Label(_loops.Break)
                        };
                    }

                    var clauses = new SwitchCase[switchStatementAst.Clauses.Count];
                    for (var i = 0; i < clauses.Length; i++)
                    {
                        clauses[i] = SwitchCase(
                            NewBlock(
                                () => switchStatementAst.Clauses[i].Item2.Compile(this),
                                typeof(void)),
                            switchStatementAst.Clauses[i].Item1.Compile(this));
                    }

                    Expression defaultBlock = null;
                    if (switchStatementAst.Default != null)
                    {
                        defaultBlock = NewBlock(
                            () => switchStatementAst.Default.Compile(this),
                            typeof(void));
                    }

                    return new Expression[]
                    {
                        Switch(
                            switchStatementAst.Condition.Compile(this),
                            defaultBlock,
                            ReflectionCache.ExpressionUtils_PSEqualsIgnoreCase,
                            clauses),
                        Label(_loops.Break)
                    };
                }
            });
        }

        public object VisitThrowStatement(ThrowStatementAst throwStatementAst)
        {
            if (throwStatementAst.IsRethrow)
            {
                return Rethrow();
            }

            if (throwStatementAst.Pipeline == null)
            {
                return Throw(
                    New(
                        ReflectionCache.RuntimeException_Ctor,
                        Constant(CompilerStrings.DefaultThrowMessage)));
            }

            var pipeline = throwStatementAst.Pipeline.Compile(this);
            if (typeof(Exception).IsAssignableFrom(pipeline.Type))
            {
                return Throw(pipeline);
            }

            return Throw(
                New(
                    ReflectionCache.RuntimeException_Ctor,
                    PSConvertTo<string>(pipeline)));
        }

        public object VisitTryStatement(TryStatementAst tryStatementAst)
        {
            if (tryStatementAst.Finally != null && tryStatementAst.CatchClauses.Any())
            {
                return TryCatchFinally(
                    NewBlock(() => tryStatementAst.Body.Compile(this), typeof(void)),
                    NewBlock(() => tryStatementAst.Finally.Compile(this), typeof(void)),
                    tryStatementAst.CatchClauses
                        .Select(ctch => ctch.Visit(this))
                        .Cast<CatchBlock>()
                        .ToArray());
            }

            if (tryStatementAst.Finally == null)
            {
                return TryCatch(
                    NewBlock(() => tryStatementAst.Body.Compile(this), typeof(void)),
                    tryStatementAst.CatchClauses
                        .Select(ctch => ctch.Visit(this))
                        .Cast<CatchBlock>()
                        .ToArray());
            }

            return TryFinally(
                NewBlock(() => tryStatementAst.Body.Compile(this), typeof(void)),
                NewBlock(() => tryStatementAst.Finally.Compile(this), typeof(void)));
        }

        public object VisitTypeExpression(TypeExpressionAst typeExpressionAst)
        {
            if (TryResolveType(typeExpressionAst, out Type type))
            {
                return Constant(type, typeof(Type));
            }

            return Empty();
        }

        public object VisitUnaryExpression(UnaryExpressionAst unaryExpressionAst)
        {
            var child = unaryExpressionAst.Child.Compile(this);
            switch (unaryExpressionAst.TokenKind)
            {
                case TokenKind.PostfixPlusPlus:
                    return Assign(child, Increment(child));
                case TokenKind.PostfixMinusMinus:
                    return Assign(child, Decrement(child));
                case TokenKind.Not:
                    return Not(PSIsTrue(child));
                default:
                    ReportNotSupported(
                        unaryExpressionAst.Extent,
                        unaryExpressionAst.TokenKind.ToString(),
                        unaryExpressionAst.TokenKind.ToString());
                    return Empty();
            }
        }

        public object VisitWhileStatement(WhileStatementAst whileStatementAst)
        {
            using (_loops.NewScope())
            {
                return Loop(
                    IfThenElse(
                        PSIsTrue(whileStatementAst.Condition.Compile(this)),
                        whileStatementAst.Body.Compile(this),
                        Break(_loops.Break)),
                    _loops.Break,
                    _loops.Continue);
            }
        }

        public object VisitVariableExpression(VariableExpressionAst variableExpressionAst)
        {
            if (SpecialVariables.Constants.TryGetValue(variableExpressionAst.VariablePath.UserPath, out Expression expression))
            {
                return expression;
            }

            if (_locals.TryGetValue(variableExpressionAst.VariablePath.UserPath, out PSVariable local))
            {
                return GetExpressionForLocal(local);
            }

            if (!SpecialVariables.AllScope.ContainsKey(variableExpressionAst.VariablePath.UserPath))
            {
                bool alreadyDefined = false;
                try
                {
                    return _scopeStack.GetVariable(variableExpressionAst, out alreadyDefined);
                }
                finally
                {
                    if (!alreadyDefined &&
                        !(variableExpressionAst.Parent is AssignmentStatementAst ||
                        (variableExpressionAst.Parent is ConvertExpressionAst &&
                        variableExpressionAst.Parent.Parent is AssignmentStatementAst)))
                    {
                        ReportParseError(
                            variableExpressionAst.Extent,
                            nameof(ErrorStrings.InvalidVariableReference),
                            string.Format(
                                CultureInfo.CurrentCulture,
                                ErrorStrings.InvalidVariableReference,
                                variableExpressionAst.VariablePath.UserPath));
                    }
                }
            }

            return GetExpressionForAllScope(variableExpressionAst.VariablePath.UserPath);
        }

        public object VisitCommand(CommandAst commandAst)
        {
            if (commandAst.Redirections.Count > 0)
            {
                commandAst.Redirections.CompileAll(this);
                return Empty();
            }

            if (s_commands.TryProcessAst(commandAst, this, out Expression expression))
            {
                return expression;
            }

            ReportNotSupportedAstType(commandAst);
            return Empty();
        }

        public object VisitExpandableStringExpression(ExpandableStringExpressionAst expandableStringExpressionAst)
        {
            var sb = new System.Text.StringBuilder(expandableStringExpressionAst.Value);
            var formatExpressions = new Expression[expandableStringExpressionAst.NestedExpressions.Count];
            var reversed = expandableStringExpressionAst.NestedExpressions.Reverse().ToArray();
            for (var i = 0; i < reversed.Length; i++)
            {
                var startOffset = reversed[i].Extent.StartOffset - expandableStringExpressionAst.Extent.StartOffset - 1;
                sb.Remove(
                    startOffset,
                    reversed[i].Extent.Text.Length)
                    .Insert(
                        startOffset,
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "{{{0}}}",
                            i));

                formatExpressions[i] = PSConvertTo<string>(reversed[i].Compile(this));
            }

            return PSFormat(
                Constant(sb.ToString()),
                NewArrayInit(typeof(object), formatExpressions));
        }

        public object VisitTypeDefinition(TypeDefinitionAst typeDefinitionAst)
        {
            ReportNotSupportedAstType(typeDefinitionAst);
            return Empty();
        }

        public object VisitPropertyMember(PropertyMemberAst propertyMemberAst)
        {
            ReportNotSupportedAstType(propertyMemberAst);
            return Empty();
        }

        public object VisitFunctionMember(FunctionMemberAst functionMemberAst)
        {
            ReportNotSupportedAstType(functionMemberAst);
            return Empty();
        }

        public object VisitBaseCtorInvokeMemberExpression(BaseCtorInvokeMemberExpressionAst baseCtorInvokeMemberExpressionAst)
        {
            ReportNotSupportedAstType(baseCtorInvokeMemberExpressionAst);
            return Empty();
        }

        public object VisitUsingStatement(UsingStatementAst usingStatement)
        {
            ReportNotSupportedAstType(usingStatement);
            return Empty();
        }

        public object VisitConfigurationDefinition(ConfigurationDefinitionAst configurationDefinitionAst)
        {
            ReportNotSupportedAstType(configurationDefinitionAst);
            return Empty();
        }

        public object VisitDynamicKeywordStatement(DynamicKeywordStatementAst dynamicKeywordAst)
        {
            ReportNotSupportedAstType(dynamicKeywordAst);
            return Empty();
        }

        public object VisitCommandParameter(CommandParameterAst commandParameterAst)
        {
            ReportNotSupportedAstType(commandParameterAst);
            return Empty();
        }

        public object VisitDataStatement(DataStatementAst dataStatementAst)
        {
            ReportNotSupportedAstType(dataStatementAst);
            return Empty();
        }

        public object VisitErrorExpression(ErrorExpressionAst errorExpressionAst)
        {
            ReportNotSupportedAstType(errorExpressionAst);
            return Empty();
        }

        public object VisitErrorStatement(ErrorStatementAst errorStatementAst)
        {
            ReportNotSupportedAstType(errorStatementAst);
            return Empty();
        }

        public object VisitFileRedirection(FileRedirectionAst fileRedirectionAst)
        {
            ReportNotSupportedAstType(fileRedirectionAst);
            return Empty();
        }

        public object VisitFunctionDefinition(FunctionDefinitionAst functionDefinitionAst)
        {
            ReportNotSupportedAstType(functionDefinitionAst);
            return Empty();
        }

        public object VisitMergingRedirection(MergingRedirectionAst mergingRedirectionAst)
        {
            ReportNotSupportedAstType(mergingRedirectionAst);
            return Empty();
        }

        public object VisitNamedAttributeArgument(NamedAttributeArgumentAst namedAttributeArgumentAst)
        {
            ReportNotSupportedAstType(namedAttributeArgumentAst);
            return Empty();
        }

        public object VisitTrap(TrapStatementAst trapStatementAst)
        {
            ReportNotSupportedAstType(trapStatementAst);
            return Empty();
        }

        public object VisitTypeConstraint(TypeConstraintAst typeConstraintAst)
        {
            ReportNotSupportedAstType(typeConstraintAst);
            return Empty();
        }

        public object VisitUsingExpression(UsingExpressionAst usingExpressionAst)
        {
            ReportNotSupportedAstType(usingExpressionAst);
            return Empty();
        }

        public object VisitAttribute(AttributeAst attributeAst)
        {
            ReportNotSupportedAstType(attributeAst);
            return Empty();
        }

        public object VisitAttributedExpression(AttributedExpressionAst attributedExpressionAst)
        {
            ReportNotSupportedAstType(attributedExpressionAst);
            return Empty();
        }
        #pragma warning restore SA1600

        /// <summary>
        /// Compiles a <see cref="Ast" /> into a <see cref="Delegate" />.
        /// </summary>
        /// <param name="engine">The <see cref="EngineIntrinsics" /> for the current runspace.</param>
        /// <param name="sbAst">The <see cref="ScriptBlockAst" /> to compile.</param>
        /// <param name="localVariables">
        /// Any <see cref="PSVariable" /> objects that should be accessible from the delegate and
        /// are not AllScope or Constant.
        /// </param>
        /// <param name="delegateType">A type that inherits <see cref="Delegate" />.</param>
        /// <returns>The compiled <see cref="Delegate" />.</returns>
        internal static Delegate CompileAst(
            EngineIntrinsics engine,
            ScriptBlockAst sbAst,
            PSVariable[] localVariables,
            Type delegateType)
        {
            var visitor = new CompileVisitor(engine);
            return visitor.CompileAstImpl(sbAst, localVariables, delegateType).Compile();
        }

        /// <summary>
        /// Compiles a <see cref="Ast" /> into a <see cref="Delegate" />.
        /// </summary>
        /// <param name="engine">The <see cref="EngineIntrinsics" /> for the current runspace.</param>
        /// <param name="sbAst">The <see cref="ScriptBlockAst" /> to compile.</param>
        /// <param name="localVariables">
        /// Any <see cref="PSVariable" /> objects that should be accessible from the delegate and
        /// are not AllScope or Constant.
        /// </param>
        /// <returns>The compiled <see cref="Delegate" />.</returns>
        internal static Delegate CompileAst(
            EngineIntrinsics engine,
            ScriptBlockAst sbAst,
            PSVariable[] localVariables)
        {
            var visitor = new CompileVisitor(engine);
            return visitor.CompileAstImpl(sbAst, localVariables).Compile();
        }

        /// <summary>
        /// Compile an <see cref="InvokeMemberExpressionAst" />.
        /// </summary>
        /// <param name="invokeMemberExpressionAst">The AST to interpret.</param>
        /// <returns>The generated <see cref="Expression" />.</returns>
        internal Expression CompileInvokeMemberExpression(InvokeMemberExpressionAst invokeMemberExpressionAst)
        {
            return CompileInvokeMemberExpression(invokeMemberExpressionAst, Type.EmptyTypes);
        }

        /// <summary>
        /// Compile an <see cref="InvokeMemberExpressionAst" />.
        /// </summary>
        /// <param name="invokeMemberExpressionAst">The AST to interpret.</param>
        /// <param name="genericArguments">
        /// The generic type arguments to use while resolving the method.
        /// </param>
        /// <returns>The generated <see cref="Expression" />.</returns>
        internal Expression CompileInvokeMemberExpression(
            InvokeMemberExpressionAst invokeMemberExpressionAst,
            Type[] genericArguments)
        {
            string memberName;
            if (!TryResolveConstant<string>(invokeMemberExpressionAst.Member, out memberName))
            {
                return Empty();
            }

            if (invokeMemberExpressionAst.Static)
            {
                Type resolvedType;
                if (!TryResolveType(invokeMemberExpressionAst.Expression, out resolvedType))
                {
                    return Empty();
                }

                if (memberName.Equals(Strings.ConstructorMemberName, StringComparison.Ordinal))
                {
                    var expressions = invokeMemberExpressionAst.Arguments.CompileAll(this);
                    return New(GetBestConstructor(resolvedType, expressions), expressions);
                }

                try
                {
                    return Call(
                        resolvedType,
                        memberName,
                        genericArguments,
                        invokeMemberExpressionAst.Arguments.CompileAll(this));
                }
                catch (InvalidOperationException e)
                {
                    ReportParseError(invokeMemberExpressionAst.Extent, e, nameof(ErrorStrings.MissingMember));
                    return Empty();
                }
            }

            try
            {
                return Call(
                    invokeMemberExpressionAst.Expression.Compile(this),
                    memberName,
                    genericArguments,
                    invokeMemberExpressionAst.Arguments.CompileAll(this));
            }
            catch (InvalidOperationException e)
            {
                ReportParseError(invokeMemberExpressionAst.Extent, e, nameof(ErrorStrings.MissingMember));
                return Empty();
            }
        }

        /// <summary>
        /// Create a new block under a new variable scope.
        /// </summary>
        /// <param name="expressionFactory">A function that generates the body of the block.</param>
        /// <returns>An <see cref="Expression" /> representing the block.</returns>
        internal Expression NewBlock(Func<Expression> expressionFactory)
        {
            using (_scopeStack.NewScope())
            {
                var expressions = expressionFactory();
                return Block(_scopeStack.GetVariables(), expressions);
            }
        }

        /// <summary>
        /// Create a new block under a new variable scope.
        /// </summary>
        /// <param name="expressionFactory">A function that generates the body of the block.</param>
        /// <returns>An <see cref="Expression" /> representing the block.</returns>
        internal Expression NewBlock(Func<IEnumerable<Expression>> expressionFactory)
        {
            using (_scopeStack.NewScope())
            {
                var expressions = expressionFactory();
                return Block(_scopeStack.GetVariables(), expressions);
            }
        }

        /// <summary>
        /// Create a new block under a new variable scope.
        /// </summary>
        /// <param name="expressionFactory">A function that generates the body of the block.</param>
        /// <param name="blockReturnType">The expected return type for the block.</param>
        /// <returns>An <see cref="Expression" /> representing the block.</returns>
        internal Expression NewBlock(Func<Expression> expressionFactory, Type blockReturnType)
        {
            using (_scopeStack.NewScope())
            {
                Expression[] expressions;
                if (blockReturnType == typeof(void))
                {
                    expressions = new[] { expressionFactory(), Empty() };
                }
                else
                {
                    expressions = new[] { expressionFactory() };
                }

                return Block(blockReturnType, _scopeStack.GetVariables(), expressions);
            }
        }

        /// <summary>
        /// Generate a <see cref="ParseError" /> citing an unsupported <see cref="Ast" /> type.
        /// </summary>
        /// <param name="ast">The <see cref="Ast" /> that is not supported.</param>
        internal void ReportNotSupportedAstType(Ast ast)
        {
            ReportNotSupported(
                ast.Extent,
                ast.GetType().Name,
                string.Format(
                    CultureInfo.CurrentCulture,
                    ErrorStrings.AstNotSupported,
                    ast.GetType().Name));
        }

        /// <summary>
        /// Generate a <see cref="ParseError" /> citing an unsupported element.
        /// </summary>
        /// <param name="extent">The <see cref="IScriptExtent" /> of the unsupported element.</param>
        /// <param name="id">The ID to be shown in the <see cref="ParseError" />.</param>
        /// <param name="message">The message to be shown in the <see cref="ParseError" />.</param>
        internal void ReportNotSupported(
            IScriptExtent extent,
            string id,
            string message)
        {
            ReportParseError(
                extent,
                nameof(ErrorStrings.ElementNotSupported),
                string.Format(
                    CultureInfo.CurrentCulture,
                    ErrorStrings.ElementNotSupported,
                    message));
        }

        /// <summary>
        /// Generate a <see cref="ParseError" /> citing a missing element.
        /// </summary>
        /// <param name="extent">The <see cref="IScriptExtent" /> of the missing element.</param>
        /// <param name="id">The ID to be shown in the <see cref="ParseError" />.</param>
        /// <param name="message">The message to be shown in the <see cref="ParseError" />.</param>
        internal void ReportMissing(IScriptExtent extent, string id, string message)
        {
            ReportParseError(
                extent,
                nameof(ErrorStrings.ElementMissing),
                string.Format(
                    CultureInfo.CurrentCulture,
                    ErrorStrings.ElementMissing,
                    message));
        }

        /// <summary>
        /// Generate a <see cref="ParseError" />.
        /// </summary>
        /// <param name="extent">The <see cref="IScriptExtent" /> of element in error.</param>
        /// <param name="id">The ID to be shown in the <see cref="ParseError" />.</param>
        /// <param name="message">The message to be shown in the <see cref="ParseError" />.</param>
        internal void ReportParseError(
            IScriptExtent extent,
            string id = nameof(ErrorStrings.CompileTimeParseError),
            string message = "")
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                message = ErrorStrings.CompileTimeParseError;
            }

            _parseErrors.Add(new ParseError(extent, id, message));
            if (_parseErrors.Count > 2)
            {
                throw new ParseException(_parseErrors.ToArray());
            }
        }

        /// <summary>
        /// Generate a <see cref="ParseError" />.
        /// </summary>
        /// <param name="extent">The <see cref="IScriptExtent" /> of element in error.</param>
        /// <param name="exception">
        /// The exception housing the message to be shown in the <see cref="ParseError" />.
        /// </param>
        /// <param name="id">The ID to be shown in the <see cref="ParseError" />.</param>
        internal void ReportParseError(
            IScriptExtent extent,
            Exception exception,
            string id = "")
        {
            if (string.IsNullOrEmpty(id))
            {
                id = exception.GetType().Name;
            }

            _parseErrors.Add(
                new ParseError(
                    extent,
                    id,
                    exception.Message));

            if (_parseErrors.Count > 2)
            {
                throw new ParseException(_parseErrors.ToArray());
            }
        }

        /// <summary>
        /// Attempt to resolve a type constant from an <see cref="Ast" />. If unable, then generate
        /// a <see cref="ParseError" />.
        /// </summary>
        /// <param name="ast">
        /// The <see cref="Ast" /> that is expected to hold a type literal expression.
        /// </param>
        /// <param name="resolvedType">The <see cref="Type" /> if resolution was successful.</param>
        /// <returns><c>true</c> if resolution was successful, otherwise <c>false</c></returns>
        internal bool TryResolveType(Ast ast, out Type resolvedType)
        {
            if (ast == null)
            {
                resolvedType = null;
                ReportParseError(
                    ast.Extent,
                    nameof(ErrorStrings.MissingType),
                    ErrorStrings.MissingType);
                return false;
            }

            if (ast is TypeExpressionAst typeExpression)
            {
                return TryResolveType(typeExpression.TypeName, out resolvedType);
            }

            if (ast is TypeConstraintAst typeConstraint)
            {
                return TryResolveType(typeConstraint.TypeName, out resolvedType);
            }

            resolvedType = null;
            ReportParseError(
                ast.Extent,
                nameof(ErrorStrings.MissingType),
                ErrorStrings.MissingType);
            return false;
        }

        private static bool FilterGenericInterfaceDefinition(Type m, object filterCriteria)
        {
            return m.IsGenericType && m.GetGenericTypeDefinition() == (Type)filterCriteria;
        }

        private LambdaExpression CompileAstImpl(
            ScriptBlockAst sbAst,
            PSVariable[] localVariables,
            Type delegateType = null)
        {
            foreach (var local in localVariables)
            {
                _locals.Add(local.Name, local);
            }

            sbAst = ConvertToDelegateAst(sbAst);
            var lambda = CreateLambda(
                sbAst,
                () =>
                {
                    using (_scopeStack.NewScope())
                    {
                        var returnsHandle = delegateType == null
                            ? _returns.NewScope()
                            : _returns.NewScope(delegateType);
                        using (returnsHandle)
                        {
                            var requireExplicitReturn =
                                sbAst.EndBlock.Statements.Count != 1 ||
                                    !(sbAst.EndBlock.Statements[0] is CommandBaseAst ||
                                    sbAst.EndBlock.Statements[0] is PipelineAst);

                            var body = sbAst.EndBlock.Compile(this);
                            return Block(
                                _scopeStack.GetVariables(),
                                _returns.WithReturn(new[] { body }, requireExplicitReturn));
                        }
                    }
                },
                delegateType);

            if (_parseErrors.Count != 0)
            {
                throw new ParseException(_parseErrors.ToArray());
            }

            return lambda;
        }

        private Expression MakeAssignment(
            Expression lhs,
            Expression rhs,
            TokenKind operatorKind,
            IScriptExtent operationExtent)
        {
            try
            {
                switch (operatorKind)
                {
                    case TokenKind.Equals:
                        return Assign(lhs, rhs);
                    case TokenKind.PlusEquals:
                        return MakeBinary(ExpressionType.AddAssign, lhs, rhs);
                    case TokenKind.MinusEquals:
                        return MakeBinary(ExpressionType.SubtractAssign, lhs, rhs);
                    case TokenKind.MultiplyEquals:
                        return MakeBinary(ExpressionType.MultiplyAssign, lhs, rhs);
                    case TokenKind.DivideEquals:
                        return MakeBinary(ExpressionType.DivideAssign, lhs, rhs);
                    case TokenKind.RemainderEquals:
                        return MakeBinary(ExpressionType.ModuloAssign, lhs, rhs);
                }
            }
            catch (InvalidOperationException e)
            {
                ReportParseError(operationExtent, e);
                return Empty();
            }

            ReportNotSupported(
                operationExtent,
                Strings.OperatorNotSupportedId,
                operatorKind.ToString());
            return Empty();
        }

        private Expression HandleRemainingClauses(IfStatementAst ifStmtAst, int clauseIndex = 0)
        {
            if (clauseIndex >= ifStmtAst.Clauses.Count)
            {
                if (ifStmtAst.ElseClause != null)
                {
                    return ifStmtAst.ElseClause.Compile(this);
                }

                return Empty();
            }

            return IfThenElse(
                PSIsTrue(ifStmtAst.Clauses[clauseIndex].Item1.Compile(this)),
                ifStmtAst.Clauses[clauseIndex].Item2.Compile(this),
                HandleRemainingClauses(ifStmtAst, clauseIndex + 1));
        }

        private ScriptBlockAst ConvertToDelegateAst(ScriptBlockAst scriptBlockAst)
        {
            if (scriptBlockAst.BeginBlock != null)
            {
                ReportNotSupported(
                    scriptBlockAst.BeginBlock.Extent,
                    nameof(CompilerStrings.BeginBlock),
                    CompilerStrings.BeginBlock);
            }

            if (scriptBlockAst.ProcessBlock != null)
            {
                ReportNotSupported(
                    scriptBlockAst.ProcessBlock.Extent,
                    nameof(CompilerStrings.ProcessBlock),
                    CompilerStrings.ProcessBlock);
            }

            if (scriptBlockAst.EndBlock == null ||
                scriptBlockAst.EndBlock.Statements == null ||
                scriptBlockAst.EndBlock.Statements.Count == 0)
            {
                ReportMissing(scriptBlockAst.Extent, nameof(CompilerStrings.EndBlock), CompilerStrings.EndBlock);
                throw new ParseException(_parseErrors.ToArray());
            }

            if (scriptBlockAst.EndBlock.Statements.Count == 1 &&
                scriptBlockAst.EndBlock.Statements[0] is AssignmentStatementAst assignment &&
                assignment.Right is PipelineAst pipeline &&
                pipeline.PipelineElements.Count == 1 &&
                pipeline.PipelineElements[0] is CommandAst command &&
                command.CommandElements.Count == 2 &&
                !string.IsNullOrEmpty(command.GetCommandName()) &&
                command.GetCommandName().Equals(Strings.DelegateSyntaxCommandName, StringComparison.Ordinal) &&
                command.CommandElements[1] is ScriptBlockExpressionAst body)
            {
                var paramBlock = new ParamBlockAst(
                    assignment.Left.Extent,
                    Enumerable.Empty<AttributeAst>(),
                    GetParametersFromDelegateExpression(assignment.Left));

                return new ScriptBlockAst(
                    scriptBlockAst.Extent,
                    paramBlock,
                    null,
                    null,
                    (NamedBlockAst)body.ScriptBlock.EndBlock.Copy(),
                    null);
            }

            return scriptBlockAst;
        }

        private IEnumerable<ParameterAst> GetParametersFromDelegateExpression(ExpressionAst expressionAst)
        {
            var parenExpression = expressionAst as ParenExpressionAst;
            if (parenExpression == null)
            {
                return Enumerable.Empty<ParameterAst>();
            }

            var pipelineAst = parenExpression.Pipeline as PipelineAst;
            if (pipelineAst == null)
            {
                return Enumerable.Empty<ParameterAst>();
            }

            if (pipelineAst.PipelineElements.Count != 1)
            {
                return Enumerable.Empty<ParameterAst>();
            }

            var commandExpression = pipelineAst.PipelineElements[0] as CommandExpressionAst;
            if (commandExpression == null)
            {
                return Enumerable.Empty<ParameterAst>();
            }

            var arrayLiteral = commandExpression.Expression as ArrayLiteralAst;
            if (arrayLiteral != null)
            {
                return arrayLiteral.Elements.Select(GetParameterFromExpression);
            }

            return new[] { GetParameterFromExpression(commandExpression.Expression) };
        }

        private ParameterAst GetParameterFromExpression(ExpressionAst expressionAst)
        {
            var convertExpression = expressionAst as ConvertExpressionAst;
            if (convertExpression != null)
            {
                return new ParameterAst(
                    convertExpression.Child.Extent,
                    (VariableExpressionAst)convertExpression.Child.Copy(),
                    new[] { new TypeConstraintAst(convertExpression.Attribute.Extent, convertExpression.Attribute.TypeName) },
                    null);
            }

            return new ParameterAst(
                expressionAst.Extent,
                (VariableExpressionAst)expressionAst.Copy(),
                Enumerable.Empty<AttributeAst>(),
                null);
        }

        private LambdaExpression CreateLambda(ScriptBlockAst scriptBlockAst, Func<Expression> blockFactory, Type delegateType = null)
        {
            if (delegateType == null)
            {
                using (_scopeStack.NewScope(GetParameters(scriptBlockAst?.ParamBlock)))
                {
                    return Lambda(
                        blockFactory(),
                        _scopeStack.GetParameters());
                }
            }

            if (!typeof(Delegate).IsAssignableFrom(delegateType))
            {
                throw new NotSupportedException();
            }

            var scope =
                _scopeStack.NewScope(
                    GetParameters(
                        scriptBlockAst?.ParamBlock,
                        delegateType
                            .GetMethod(Strings.DelegateInvokeMethodName)
                            .GetParameters()
                            .Select(p => p.ParameterType)
                            .ToArray()));

            using (scope)
            {
                return Lambda(
                    delegateType,
                    NewBlock(
                        () => blockFactory(),
                        delegateType.GetMethod(Strings.DelegateInvokeMethodName).ReturnType),
                    _scopeStack.GetParameters());
            }
        }

        private ParameterExpression GetParameter(ParameterAst parameterAst, Type expectedType)
        {
            return Parameter(
                expectedType == null ? GetParameterType(parameterAst) : expectedType,
                parameterAst.Name.VariablePath.UserPath);
        }

        private Type GetParameterType(ParameterAst parameter)
        {
            return parameter.Attributes
                .OfType<TypeConstraintAst>()
                .FirstOrDefault()
                ?.TypeName
                .GetReflectionType()
                ?? typeof(object);
        }

        private ConstructorInfo GetBestConstructor(
            Type type,
            Expression[] arguments)
        {
            return (ConstructorInfo)Type.DefaultBinder.SelectMethod(
                BindingFlags.Default,
                type.GetConstructors(),
                arguments.Select(a => a.Type).ToArray(),
                arguments.Length == 0 ? s_emptyModifiers : new[] { new ParameterModifier(arguments.Length) });
        }

        private MethodInfo GetBestMethod(
            Type type,
            string name,
            Expression[] arguments)
        {
            return (MethodInfo)Type.DefaultBinder.SelectMethod(
                BindingFlags.Default,
                Array.ConvertAll(
                    type.FindMembers(MemberTypes.Method, BindingFlags.Default, Type.FilterName, name),
                    member => (MethodInfo)member),
                arguments.Select(a => a.Type).ToArray(),
                new[] { new ParameterModifier(arguments.Length) });
        }

        private ParameterExpression[] GetParameters(ParamBlockAst ast)
        {
            if (ast?.Parameters == null || ast.Parameters.Count == 0)
            {
                return s_emptyParameters;
            }

            return ast.Parameters.Select(p => (ParameterExpression)p.Visit(this)).ToArray();
        }

        private ParameterExpression[] GetParameters(ParamBlockAst ast, Type[] expectedParameterTypes)
        {
            if (ast == null || ast.Parameters.Count == 0)
            {
                return s_emptyParameters;
            }

            var parameters = new ParameterExpression[ast.Parameters.Count];
            for (var i = 0; i < ast.Parameters.Count; i++)
            {
                parameters[i] = GetParameter(ast.Parameters[i], expectedParameterTypes[i]);
            }

            return parameters;
        }

        private Expression CreateWrappedVariableExpression(PSVariable variable, Type variableType)
        {
            return Property(
                Constant(
                    typeof(PSVariableWrapper<>).MakeGenericType(variableType)
                        .GetConstructor(new[] { typeof(PSVariable) })
                        .Invoke(new[] { variable })),
                Strings.PSVariableWrapperValuePropertyName);
        }

        private Expression GetExpressionForLocal(PSVariable variable)
        {
            if (s_wrapperCache.TryGetValue(variable, out Expression cachedExpression))
            {
                return cachedExpression;
            }

            var expression = CreateWrappedVariableExpression(variable, GetTypeForVariable(variable));
            s_wrapperCache.Add(variable, expression);
            return expression;
        }

        private Expression GetExpressionForAllScope(string name)
        {
            if (_allScopes == null)
            {
                using (var pwsh = PowerShell.Create(RunspaceMode.CurrentRunspace))
                {
                    _allScopes =
                        pwsh.AddCommand("Microsoft.PowerShell.Utility\\Get-Variable")
                            .AddParameter("Scope", "Global")
                            .Invoke<PSVariable>()
                            .Where(v => v.Options.HasFlag(ScopedItemOptions.AllScope))
                            .ToDictionary(v => v.Name, StringComparer.OrdinalIgnoreCase);
                }
            }

            if (s_wrapperCache.TryGetValue(_allScopes[name], out Expression cachedExpression))
            {
                return cachedExpression;
            }

            var expression = CreateWrappedVariableExpression(
                _allScopes[name],
                SpecialVariables.AllScope[name]);
            s_wrapperCache.Add(_allScopes[name], expression);
            return expression;
        }

        private Type GetTypeForVariable(PSVariable variable)
        {
            Type variableType;
            if (SpecialVariables.AllScope.TryGetValue(variable.Name, out variableType))
            {
                return variableType;
            }

            var transAttribute = variable.Attributes.OfType<ArgumentTransformationAttribute>().FirstOrDefault();
            if (transAttribute == null)
            {
                return GetTypeForVariableByValue(variable);
            }

            if (ReflectionCache.ArgumentTypeConverterAttribute == null ||
                ReflectionCache.ArgumentTypeConverterAttribute_TargetType == null)
            {
                return GetTypeForVariableByValue(variable);
            }

            if (!ReflectionCache.ArgumentTypeConverterAttribute.IsAssignableFrom(transAttribute.GetType()))
            {
                return GetTypeForVariableByValue(variable);
            }

            return (Type)ReflectionCache.ArgumentTypeConverterAttribute_TargetType.GetValue(transAttribute);
        }

        private Type GetTypeForVariableByValue(PSVariable variable)
        {
            if (variable.Value == null)
            {
                return typeof(object);
            }

            return variable.Value.GetType();
        }

        private void ReportNonConstantTypeAs(IScriptExtent extent)
        {
            ReportNotSupported(extent, nameof(ErrorStrings.NonConstantTypeAs), ErrorStrings.NonConstantTypeAs);
        }

        private bool TryResolveType(ITypeName typeName, out Type resolvedType)
        {
            resolvedType = typeName.GetReflectionType();
            if (resolvedType != null)
            {
                return true;
            }

            ReportParseError(
                typeName.Extent,
                nameof(ErrorStrings.TypeNotFound),
                string.Format(
                    CultureInfo.CurrentCulture,
                    ErrorStrings.TypeNotFound,
                    typeName.FullName));
            return false;
        }

        private bool TryResolveConstant<TResult>(Ast ast, out TResult resolvedValue)
        {
            object rawValue = null;
            try
            {
                rawValue = ast.SafeGetValue();
            }
            catch (InvalidOperationException)
            {
                ReportParseError(
                    ast.Extent,
                    nameof(ErrorStrings.UnexpectedExpression),
                    ErrorStrings.UnexpectedExpression);
                resolvedValue = default(TResult);
                return false;
            }

            resolvedValue = (TResult)rawValue;
            return true;
        }

        private bool TryFindGenericInterface(
            Type implementation,
            Type genericDefinition,
            out Type resolvedInterface)
        {
            resolvedInterface = implementation
                .FindInterfaces(FilterGenericInterfaceDefinition, genericDefinition)
                .FirstOrDefault();

            return resolvedInterface != null;
        }
    }
}
