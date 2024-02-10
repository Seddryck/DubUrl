CREATE TABLE "Customer" (
    "CustomerId" INT NOT NULL PRIMARY KEY,
    "FullName" VARCHAR(50),
    "BirthDate" VARCHAR(10)
);

INSERT INTO "Customer" ("CustomerId", "FullName", "BirthDate") VALUES
     (1, 'Nikola Tesla',      '1856-07-10')
    ,(2, 'Albert Einstein',   '1879-03-14')
    ,(3, 'John von Neumann',  '1903-12-28')
    ,(4, 'Alan Turing',       '1912-06-23')
    ,(5, 'Linus Torvalds',    '1969-12-28')
;
