﻿using DubUrl.Querying;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
using DubUrl.Querying.Reading;
using DubUrl.Querying.Templating;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static DubUrl.QA.MicroOrmCustomerRepository;

namespace DubUrl.QA;

internal class CustomerRepository
{
    private IDatabaseUrl DatabaseUrl { get; }

    public CustomerRepository(IDatabaseUrlFactory factory, string url)
        => DatabaseUrl = factory.Instantiate(url);

    public string SelectFirstCustomer()
        => DatabaseUrl.ReadScalarNonNull<string>(new SelectFirstCustomerQuery());

    private class SelectFirstCustomerQuery : EmbeddedResourceCommand
    {
        public SelectFirstCustomerQuery()
            : base($"{typeof(CustomerRepository).Assembly.GetName().Name}.{nameof(SelectFirstCustomer)}", NullQueryLogger.Instance)
        { }
    }

    public string SelectCustomerById(int id)
        => DatabaseUrl.ReadScalarNonNull<string>(new SelectCustomerByIdQuery(id));

    private class SelectCustomerByIdQuery : ParametrizedEmbeddedResourceCommand
    {
        public SelectCustomerByIdQuery(int id)
            : base(
                  new EmbeddedResourceManager(Assembly.GetExecutingAssembly())
                  , $"{typeof(CustomerRepository).Assembly.GetName().Name}.{nameof(SelectCustomerById)}"
                  , new DubUrlParameterCollection()
                        .Add("Id", id)
                  , NullQueryLogger.Instance
            )
        { }
    }
}

internal class MicroOrmCustomerRepository
{
    private MicroOrm.DatabaseUrl DatabaseUrl { get; }

    public MicroOrmCustomerRepository(DatabaseUrlFactory factory, string url)
        => DatabaseUrl = (MicroOrm.DatabaseUrl)factory.Instantiate(url);

    public List<Customer> SelectYoungestCustomers(int count)
        => DatabaseUrl.ReadMultiple<Customer>(new SelectYoungestCustomersQuery(count)).ToList();

    private class SelectYoungestCustomersQuery : ParametrizedEmbeddedResourceCommand
    {
        public SelectYoungestCustomersQuery(int count)
            : base(
                  new EmbeddedResourceManager(Assembly.GetExecutingAssembly())
                  , $"{typeof(CustomerRepository).Assembly.GetName().Name}.{nameof(SelectYoungestCustomers)}"
                  , new DubUrlParameterCollection()
                        .Add("count", count)
                  , NullQueryLogger.Instance
            )
        { }
    }

    public List<Customer> SelectWhereCustomers(IWhereClause[] clauses)
        => DatabaseUrl.ReadMultiple<Customer>(new SelectWhereCustomersQuery(clauses)).ToList();

    public interface IWhereClause
    {
        public string FieldName { get; }
        public string @Operator { get; }
        public object Value { get; }
    }

    public class BasicComparisonWhereClause<T> : IWhereClause
    {

        protected Expression<Func<Customer, T>> Member { get; }
        public Func<Expression, Expression, BinaryExpression> BinaryExpression { get; }
        public T Constant { get; }

        public string FieldName
        {
            get => (Member.Body as MemberExpression)?.Member.Name ?? throw new ArgumentException();
        }

        public string @Operator
        {
            get
            {
                return BinaryExpression.GetMethodInfo().Name switch
                {
                    "GreaterThan" => ">",
                    "LessThan" => "<",
                    "GreaterThanOrEqual" => ">=",
                    "LessThanOrEqual" => "<=",
                    "Equal" => "=",
                    _ => throw new NotSupportedException(),
                };
            }
        }

        public object Value => (object)(Constant ?? throw new ArgumentNullException());

        public BasicComparisonWhereClause(Expression<Func<Customer, T>> member, Func<Expression, Expression, BinaryExpression> binaryExpression, T constant)
            => (Member, BinaryExpression, Constant) = (member, binaryExpression, constant);
    }

    private class SelectWhereCustomersQuery : EmbeddedTemplateCommand
    {
        public SelectWhereCustomersQuery(IWhereClause[] clauses)
            : base(
                  $"{typeof(CustomerRepository).Assembly.GetName().Name}.{nameof(SelectWhereCustomers)}"
                  , new Dictionary<string, object?>() {
                      { "fields", new[] { "BirthDate", "CustomerId", "FullName" } },
                      { "table", "Customer" },
                      { "clauses", clauses.Select<IWhereClause, object>(x => new {
                                        Field = x.FieldName
                                        , Operator = x.Operator.ToString()
                                        , x.Value
                                    }).ToArray()
                      }
                  }
                  , NullQueryLogger.Instance
            )
        { }
    }

    public class Customer
    {
        public int CustomerId { get; set; }
        public string FullName { get; set; } = "";
        public DateTime BirthDate { get; set; }
    }
}
