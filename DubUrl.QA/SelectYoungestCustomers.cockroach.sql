select
	customerid as "CustomerId",
	fullname as "FullName",
	birthdate as "BirthDate"
from
	Customer
order by
	BirthDate desc
limit
	($1)