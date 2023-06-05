select 
	$fields:{field | $field$}; separator="
, "$
from 
	Customer
where 
	$clauses:{clause | $clause.Field$ $clause.Operator$ '$clause.Value$'}; separator="
and "$