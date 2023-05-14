DROP DATABASE IF EXISTS DubUrl;

CREATE DATABASE DubUrl;

DROP TABLE IF EXISTS Customer;

CREATE TABLE Customer (
    CustomerId INT NOT NULL GENERATED ALWAYS AS IDENTITY (START WITH 1 INCREMENT BY 1) PRIMARY KEY,
    FullName STRING,
    BirthDate DATE
);

INSERT INTO Customer (FullName, BirthDate) VALUES
     ('Nikola Tesla',        '1856-07-10')
    ,('Albert Einstein',    '1879-03-14')
    ,('John von Neumann',   '1903-12-28')
    ,('Alan Turing',        '1912-06-23')
    ,('Linus Torvalds',     '1969-12-28')
;