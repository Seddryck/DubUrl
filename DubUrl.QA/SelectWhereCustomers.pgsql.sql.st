select 
	$fields:{field | "$field$"}; separator="
, "$
from 
	"Customer"
where 
	$clauses:{clause | "$clause.Field; format="s"$" $clause.Operator$ '$clause.Value$'}; separator="
and "$