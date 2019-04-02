using System;

namespace GraphQL.Annotations.TSql
{
	public struct WhereItem
	{
		public bool IsSecurity;
		public string DefaultAlias;
		public string Text;

		public static implicit operator WhereItem(string s)
		{
			return new WhereItem {Text = s, DefaultAlias = "", IsSecurity = false};
		}

		public override string ToString() => this.ToString(this.DefaultAlias);

		public string ToString(string alias) => String.Format(this.Text, alias);
	}
}