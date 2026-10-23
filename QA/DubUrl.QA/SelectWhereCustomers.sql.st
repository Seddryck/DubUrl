select 
	$fields:{field | $field; format="identity"$}; separator="
, "$
from 
	$table; format="identity"$
where 
	$clauses:{clause | $clause.Field$ $clause.Operator$ $clause.Value; format="value"$}; separator="
and "$