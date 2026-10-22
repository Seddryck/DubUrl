select 
	$fields:{field | $field$ AS "$field$"}; separator="
, "$
from 
	Customer
where 
	$clauses:{clause | $clause.Field$ $clause.Operator$ $clause.Value;format="value"$}; separator="
and "$
