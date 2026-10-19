select
	CustomerId, FullName, BirthDate
from
	Customer
order by
	BirthDate desc
limit
	@count