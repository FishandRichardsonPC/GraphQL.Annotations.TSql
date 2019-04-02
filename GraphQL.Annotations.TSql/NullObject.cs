using System;
using System.ComponentModel;

namespace GraphQL.Annotations.TSql
{
    public struct NullObject<T>
    {
	    [DefaultValue(true)]
	    public readonly bool IsNull;

        private NullObject(T item, bool isNull) : this()
        {
            this.IsNull = isNull;
            this.Item = item;
        }

        public NullObject(T item) : this(item, item == null)
        {
        }

        public static NullObject<T> Null()
        {
            return new NullObject<T>();
        }

        public T Item { get; }

        public static implicit operator T(NullObject<T> nullObject)
        {
            return nullObject.Item;
        }

        public static implicit operator NullObject<T>(T item)
        {
            return new NullObject<T>(item);
        }

        public override string ToString()
        {
            return (this.Item != null) ? this.Item.ToString() : "NULL";
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
            {
	            return this.IsNull;
            }

	        if (!(obj is NullObject<T>))
	        {
		        return false;
	        }

	        var no = (NullObject<T>)obj;

            if (this.IsNull)
            {
	            return no.IsNull;
            }

	        if (no.IsNull)
	        {
		        return false;
	        }

	        return this.Item.Equals(no.Item);
        }

        public override int GetHashCode()
        {
            return this.IsNull ? Int32.MinValue : this.Item.GetHashCode();
        }
    }
}