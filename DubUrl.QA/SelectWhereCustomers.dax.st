EVALUATE
	SELECTCOLUMNS(
		FILTER (
			$table; format="identity"$
			, $clauses:{clause | $table; format="identity"$[$clause.Field$] $clause.Operator$ $clause.Value; format="value"$}; separator="
				&& "$ 
		)
		, $fields:{field | $field; format="value"$, $table; format="identity"$[$field$]}; separator="
		, "$
	)