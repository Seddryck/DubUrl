DROP DATABASE IF EXISTS DubUrl;
CREATE DATABASE DubUrl;

USE DubUrl;

CREATE TABLE Customer (
    CustomerId INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    FullName VARCHAR(50),
    BirthDate DATE
);

INSERT INTO Customer (FullName, BirthDate) VALUES
     ('Nikola Tesla',        '1856-07-10')
    ,('Albert Einstein',    '1879-03-14')
    ,('John von Neumann',   '1903-12-28')
    ,('Alan Turing',        '1912-06-23')
    ,('Linus Torvalds',     '1969-12-28')
;