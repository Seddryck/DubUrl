select
	*
from
	doc."Customer"
order by
	"BirthDate" desc
limit
	($1)
