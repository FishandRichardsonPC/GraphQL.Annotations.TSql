using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;

namespace GraphQL.Annotations.TSql.AspNetCore
{
    public class HttpContextUser: IServiceProvider
    {
        private readonly Dictionary<Type, object> _data = new Dictionary<Type, object>();

        private readonly HttpContext _context;

        public HttpContextUser(HttpContext context)
        {
	        this._context = context;
        }

        public object GetService(Type serviceType)
        {
            return this._context.RequestServices.GetService(serviceType);
        }

        public HttpContext GetHttpContext()
        {
            return this._context;
        }

        public T Get<T>()
        {
            var type = typeof(T);
            if (!this._data.ContainsKey(type))
            {
                var contextConstructor = type.GetConstructor(new[] {typeof(HttpContext)});
                if (contextConstructor != null)
                {
                    this._data[type] = contextConstructor.Invoke(new object[] {this._context});
                }
                else
                {
                    contextConstructor = type.GetConstructor(new Type[] { });
                    if (contextConstructor != null)
                    {
                        this._data[type] = contextConstructor.Invoke(new object[]{});
                    }
                    else
                    {
                        throw new ArgumentException("The provided type must either have a constructor which accepts HttpContext or a parameterless constructor");
                    }
                }
            }

            return (T) this._data[type];
        }
    }
}