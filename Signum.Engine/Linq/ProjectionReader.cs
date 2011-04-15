﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using Signum.Engine.Maps;
using Signum.Entities;
using Signum.Utilities;
using Signum.Utilities.Reflection;
using Signum.Engine.Properties;
using System.Data.SqlClient;
using Signum.Utilities.ExpressionTrees;

namespace Signum.Engine.Linq
{
    internal interface IProjectionRow
    {
        FieldReader Reader { get; }

        Retriever Retriever { get; }

        IEnumerable<S> Lookup<K, S>(ProjectionToken token, K key); 

        MList<S> GetList<S>(RelationalTable tr, int id);

        S GetIdentifiable<S>(int? id) where S : IdentifiableEntity;
        S GetImplementedBy<S>(Type[] types, params int?[] ids) where S :  IIdentifiable;
        S GetImplementedByAll<S>(int? id, int? typeId) where S :  IIdentifiable;

        Lite<S> GetLiteIdentifiable<S>(int? id, int? typeId, string str) where S : class, IIdentifiable;
        Lite<S> GetLiteImplementedByAll<S>(int? id, int? typeId) where S :class,  IIdentifiable;
    }

    internal class ProjectionRowEnumerator<T> : IProjectionRow, IEnumerator<T>
    {
        public FieldReader Reader { get; private set;}

        public IProjectionRow Parent { get; private set; }

        SqlDataReader dataReader;

        T current;
        Func<IProjectionRow, T> projector; 
        Expression<Func<IProjectionRow, T>> projectorExpression;

        Retriever retriever;
        Dictionary<ProjectionToken, IEnumerable> lookups;

        internal ProjectionRowEnumerator(SqlDataReader dataReader, Expression<Func<IProjectionRow, T>> projectorExpression, Retriever retriever, Dictionary<ProjectionToken, IEnumerable> lookups)
        {
            this.dataReader = dataReader;
            this.Reader = new FieldReader(dataReader);

            this.projectorExpression = ExpressionCompilableAsserter.Assert(projectorExpression);
            this.projector = projectorExpression.Compile();
            this.retriever = retriever;
            this.lookups = lookups;
        }

        public T Current
        {
            get { return this.current; }
        }

        object IEnumerator.Current
        {
            get { return this.current; }
        }

        public bool MoveNext()
        {
            if (dataReader.Read())
            {
                this.current = this.projector(this);
                return true;
            }
            return false;
        }

        public void Reset()
        {
        }

        public void Dispose()
        {
            dataReader.Dispose();
        }

        public Retriever Retriever
        {
            get { return retriever; }
        }
  
        public MList<S> GetList<S>(RelationalTable tr, int id)
        {
            return (MList<S>)Retriever.GetList(tr, id);
        }

        public S GetIdentifiable<S>(int? id) where S : IdentifiableEntity
        {
            if (id == null) return null; 
            return (S)Retriever.GetIdentifiable(ConnectionScope.Current.Schema.Table(typeof(S)), id.Value, true);
        }

        public S GetImplementedBy<S>(Type[] types, params int?[] ids)
            where S :  IIdentifiable
        {
            int pos = ids.IndexOf(id => id.HasValue);
            if (pos == -1) return default(S);
            return (S)(object)Retriever.GetIdentifiable(ConnectionScope.Current.Schema.Table(types[pos]), ids[pos].Value, true);
        }

        public S GetImplementedByAll<S>(int? id, int? idType)
            where S :  IIdentifiable
        {
            if (id == null) return default(S); 
            Table table = Schema.Current.IdToTable[idType.Value];
            return (S)(object)Retriever.GetIdentifiable(table, id.Value, true);
        }

        public Lite<S> GetLiteIdentifiable<S>( int? id, int? idType, string str) where S : class, IIdentifiable
        {
            if (id == null) return null;

            Type runtimeType = Schema.Current.IdToTable[idType.Value].Type;
            return new Lite<S>(runtimeType, id.Value) { ToStr = str }; 
        }

        public Lite<S> GetLiteImplementedByAll<S>(int? id, int? idType) where S : class, IIdentifiable
        {
            if (id == null) return null; 
            Table table = Schema.Current.IdToTable[idType.Value];
            return (Lite<S>)Retriever.GetLite(table, typeof(S), id.Value);
        }


        public IEnumerable<S> Lookup<K, S>(ProjectionToken token, K key)
        {
            Lookup<K, S> lookup = (Lookup<K, S>)lookups[token];

            if (!lookup.Contains(key))
                return Enumerable.Empty<S>();
            else
                return lookup[key];
        }
    }

    internal class ExpressionCompilableAsserter : SimpleExpressionVisitor
    {
        protected override Expression Visit(Expression exp)
        {
            try
            {
                return base.Visit(exp);
            }
            catch (InvalidOperationException e)
            {
                if (e.Message.StartsWith("Unhandled Expression"))
                    throw new NotSupportedException("The expression can not be compiled:\r\n{0}".Formato(exp.NiceToString()));
                throw;
            }
        }

        internal static Expression<Func<IProjectionRow, T>> Assert<T>(Expression<Func<IProjectionRow, T>> projectorExpression)
        {
            return (Expression<Func<IProjectionRow, T>>)new ExpressionCompilableAsserter().Visit(projectorExpression);
        }
    }

    internal class ProjectionRowEnumerable<T> : IEnumerable<T>, IEnumerable
    {
        ProjectionRowEnumerator<T> enumerator;

        internal ProjectionRowEnumerable(ProjectionRowEnumerator<T> enumerator)
        {
            this.enumerator = enumerator;
        }

        public IEnumerator<T> GetEnumerator()
        {
            return Interlocked.Exchange(ref enumerator, null)
                .ThrowIfNullC("Cannot enumerate more than once");
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }
}
