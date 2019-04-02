namespace GraphQL.Annotations.TSql
{
	public static class CommonTransforms
	{
		public const string YesNoTransform =
			"CAST(CASE WHEN [{0}].[{1}] = 'Y' THEN 1 WHEN [{0}].[{1}] = 'N' THEN 0 ELSE NULL END AS BIT)";

		public const string YesNoReveseTransform =
			"CASE WHEN {0} = 1 THEN 'Y' WHEN {0} = 0 THEN 'N' ELSE NULL END";
	}
}
