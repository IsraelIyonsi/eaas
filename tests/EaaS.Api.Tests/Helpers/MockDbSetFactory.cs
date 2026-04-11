using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using NSubstitute;

namespace EaaS.Api.Tests.Helpers;

/// <summary>
/// Creates mock DbSet instances backed by in-memory lists.
/// Supports async LINQ operations for testing handlers that use EF.Functions.ILike
/// (which cannot be used with InMemory or SQLite providers).
/// </summary>
public static class MockDbSetFactory
{
    public static DbSet<T> Create<T>(List<T> data) where T : class
    {
        var queryable = data.AsQueryable();
        var mockSet = Substitute.For<DbSet<T>, IQueryable<T>, IAsyncEnumerable<T>>();

        // IQueryable setup
        ((IQueryable<T>)mockSet).Provider.Returns(new TestAsyncQueryProvider<T>(queryable.Provider));
        ((IQueryable<T>)mockSet).Expression.Returns(queryable.Expression);
        ((IQueryable<T>)mockSet).ElementType.Returns(queryable.ElementType);
        ((IQueryable<T>)mockSet).GetEnumerator().Returns(queryable.GetEnumerator());

        // IAsyncEnumerable setup
        ((IAsyncEnumerable<T>)mockSet).GetAsyncEnumerator(Arg.Any<CancellationToken>())
            .Returns(_ => new TestAsyncEnumerator<T>(data.GetEnumerator()));

        // Add tracking
        mockSet.When(x => x.Add(Arg.Any<T>())).Do(ci => data.Add(ci.Arg<T>()));

        // Remove tracking
        mockSet.When(x => x.Remove(Arg.Any<T>())).Do(ci => data.Remove(ci.Arg<T>()));

        return mockSet;
    }
}

/// <summary>
/// Replaces EF.Functions.ILike calls with case-insensitive string comparison
/// so expressions can be evaluated client-side in tests.
/// </summary>
internal sealed class ILikeReplacingVisitor : ExpressionVisitor
{
    public static Expression Replace(Expression expression) => new ILikeReplacingVisitor().Visit(expression);

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        // Replace EF.Functions.ILike(_, matchExpression, pattern) with
        // a case-insensitive comparison that handles SQL wildcards
        if (node.Method.Name == "ILike" && node.Method.DeclaringType?.Name == "NpgsqlDbFunctionsExtensions")
        {
            // Arguments: [DbFunctions, matchExpression, pattern]
            var matchExpression = Visit(node.Arguments[1]);
            var pattern = Visit(node.Arguments[2]);

            // Use a custom method that handles SQL LIKE patterns
            var iLikeMethod = typeof(ILikeReplacingVisitor)
                .GetMethod(nameof(ClientSideILike), BindingFlags.Static | BindingFlags.NonPublic)!;

            return Expression.Call(iLikeMethod, matchExpression, pattern);
        }

        return base.VisitMethodCall(node);
    }

    /// <summary>
    /// Client-side implementation of ILike that handles SQL LIKE wildcards (% and _).
    /// </summary>
    private static bool ClientSideILike(string matchExpression, string pattern)
    {
        if (matchExpression is null || pattern is null) return false;

        // Convert SQL LIKE pattern to regex: % = .*, _ = .
        var regexPattern = "^" + Regex.Escape(pattern)
            .Replace("%", ".*")
            .Replace("_", ".") + "$";

        return Regex.IsMatch(matchExpression, regexPattern, RegexOptions.IgnoreCase);
    }
}

internal sealed class TestAsyncQueryProvider<T> : IAsyncQueryProvider
{
    private readonly IQueryProvider _inner;

    internal TestAsyncQueryProvider(IQueryProvider inner) => _inner = inner;

    public IQueryable CreateQuery(Expression expression)
        => new TestAsyncEnumerable<T>(ILikeReplacingVisitor.Replace(expression), this);

    public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
        => new TestAsyncEnumerable<TElement>(ILikeReplacingVisitor.Replace(expression), this);

    public object? Execute(Expression expression)
        => _inner.Execute(ILikeReplacingVisitor.Replace(expression));

    public TResult Execute<TResult>(Expression expression)
        => _inner.Execute<TResult>(ILikeReplacingVisitor.Replace(expression));

    public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default)
    {
        var rewritten = ILikeReplacingVisitor.Replace(expression);
        var resultType = typeof(TResult).GetGenericArguments()[0];
        var executeMethod = typeof(IQueryProvider).GetMethod(nameof(IQueryProvider.Execute), 1, [typeof(Expression)])!;
        var result = executeMethod.MakeGenericMethod(resultType).Invoke(_inner, [rewritten]);
        return (TResult)typeof(Task).GetMethod(nameof(Task.FromResult))!
            .MakeGenericMethod(resultType)
            .Invoke(null, [result])!;
    }
}

internal sealed class TestAsyncEnumerable<T> : EnumerableQuery<T>, IAsyncEnumerable<T>, IQueryable<T>
{
    private readonly IAsyncQueryProvider _provider;

    internal TestAsyncEnumerable(Expression expression, IAsyncQueryProvider provider) : base(expression)
    {
        _provider = provider;
    }

    IQueryProvider IQueryable.Provider => _provider;

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        => new TestAsyncEnumerator<T>(this.AsEnumerable().GetEnumerator());
}

internal sealed class TestAsyncEnumerator<T> : IAsyncEnumerator<T>
{
    private readonly IEnumerator<T> _inner;

    internal TestAsyncEnumerator(IEnumerator<T> inner) => _inner = inner;

    public T Current => _inner.Current;
    public ValueTask DisposeAsync() { _inner.Dispose(); return ValueTask.CompletedTask; }
    public ValueTask<bool> MoveNextAsync() => ValueTask.FromResult(_inner.MoveNext());
}
