select
	CustomerId
	, FullName
	, cast(BirthDate as DateTime) as "BirthDate"
from
	Customer
order by
	BirthDate desc
limit
	?