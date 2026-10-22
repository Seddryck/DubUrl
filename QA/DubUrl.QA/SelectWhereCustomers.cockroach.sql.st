select 
	$fields:{field | $field; format="lower"$ AS "$field$"}; separator="
, "$
from 
	Customer
where 
	$clauses:{clause | $clause.Field$ $clause.Operator$ '$clause.Value$'}; separator="
and "$