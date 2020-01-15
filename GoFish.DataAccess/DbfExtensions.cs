using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace GoFish.DataAccess
{
    public static class DbfExtensions
    {
        public static Func<object[], T> CreateMapper<T>(this Dbf dbf) where T : class, new()
        {
            var fields = dbf.GetHeader().Fields;
            var type = typeof(T);
            var props = type.GetProperties();

            var methodBody = new List<Expression>();

            var rowParam = Expression.Parameter(typeof(object[]), "row");

            var hoistedLocalVariable = Expression.Property(Expression.Constant(new ValueBag<T>()), "Value");
            methodBody.Add(Expression.Assign(hoistedLocalVariable, Expression.New(type)));

            for (int i = 0; i < fields.Count; i++)
            {
                var field = fields[i];
                var prop = props.FirstOrDefault(x => x.Name.Equals(field.Name, StringComparison.OrdinalIgnoreCase));
                if (prop is null) continue;
                var value = Expression.ArrayIndex(rowParam, Expression.Constant(field.Index));

                var access = Expression.MakeMemberAccess(hoistedLocalVariable, prop);
                var setter = Expression.Assign(access, Expression.Convert(value, prop.PropertyType));
                methodBody.Add(setter);
            }
            var returnTarget = Expression.Label(type);
            methodBody.Add(Expression.Return(returnTarget, hoistedLocalVariable, type));

            methodBody.Add(Expression.Label(returnTarget, hoistedLocalVariable));

            var lambda = Expression.Lambda<Func<object[], T>>(Expression.Block(methodBody), rowParam);
            return lambda.Compile();
        }
    }
}
