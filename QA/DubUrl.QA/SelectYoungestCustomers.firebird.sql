select first 2
	CustomerId as "CustomerId"
    , FullName as "FullName"
    , BirthDate as "BirthDate"
from
	Customer
order by
	BirthDate desc
