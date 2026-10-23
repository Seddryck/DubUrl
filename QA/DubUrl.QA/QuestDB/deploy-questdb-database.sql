CREATE TABLE "Customer" (
    "CustomerId" INT NOT NULL,
    "FullName" STRING,
    "BirthDate" DATE
);

INSERT INTO "Customer" ("CustomerId", "FullName", "BirthDate") VALUES
     (1, 'Nikola Tesla',       to_date('1856-07-10', 'yyyy-MM-dd'))
    ,(2, 'Albert Einstein',    to_date('1879-03-14', 'yyyy-MM-dd'))
    ,(3, 'John von Neumann',   to_date('1903-12-28', 'yyyy-MM-dd'))
    ,(4, 'Alan Turing',        to_date('1912-06-23', 'yyyy-MM-dd'))
    ,(5, 'Linus Torvalds',     to_date('1969-12-28', 'yyyy-MM-dd'))
;