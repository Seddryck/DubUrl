.open <path>

CREATE SEQUENCE CustomerIdSeq 
	INCREMENT BY 1 
	START 1 
	NO CYCLE
;

CREATE TABLE Customer (
    CustomerId INT NOT NULL PRIMARY KEY,
    FullName VARCHAR(50),
    BirthDate DATE
);

INSERT INTO Customer
SELECT NextVal('CustomerIdSeq') as CustomerId, 'Nikola Tesla' as FullName, '1856-07-10' as BirthDate UNION ALL
SELECT NextVal('CustomerIdSeq'), 'Albert Einstein',    '1879-03-14' UNION ALL
SELECT NextVal('CustomerIdSeq'), 'John von Neumann',   '1903-12-28' UNION ALL
SELECT NextVal('CustomerIdSeq'), 'Alan Turing',        '1912-06-23' UNION ALL
SELECT NextVal('CustomerIdSeq'), 'Linus Torvalds',     '1969-12-28'
RETURNING *
;

