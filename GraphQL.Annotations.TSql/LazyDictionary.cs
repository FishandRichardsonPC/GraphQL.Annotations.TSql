using System;
using System.Collections.Concurrent;

namespace GraphQL.Annotations.TSql
{
	public class LazyDictionary<TKey, TValue>: ConcurrentDictionary<TKey, TValue>
	{
		private readonly Func<TKey, TValue> _creator;

		public new TValue this[TKey key]
		{
			get
			{
				if (!this.ContainsKey(key))
				{
					base[key] = this._creator(key);
				}

				return base[key];
			}
			set { base[key] = value; }
		}

		public LazyDictionary(Func<TKey, TValue> creator)
		{
			this._creator = creator;
		}
	}
}
